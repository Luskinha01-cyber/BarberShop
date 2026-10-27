using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services.Notifications;

public class WhatsAppCloudApiService : IWhatsAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppCloudApiService> _logger;

    public WhatsAppCloudApiService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppCloudApiService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WhatsAppNotification?> SendBookingConfirmationAsync(Appointment appointment)
    {
        return await SendNotificationAsync(appointment, NotificationType.BookingConfirmation, BuildConfirmationMessage(appointment));
    }

    public async Task<WhatsAppNotification?> SendBookingCancellationAsync(Appointment appointment)
    {
        return await SendNotificationAsync(appointment, NotificationType.BookingCancellation, BuildCancellationMessage(appointment));
    }

    public async Task<WhatsAppNotification?> SendBookingRescheduleAsync(Appointment appointment)
    {
        return await SendNotificationAsync(appointment, NotificationType.BookingReschedule, BuildRescheduleMessage(appointment));
    }

    public async Task<WhatsAppNotification?> SendReminderAsync(Appointment appointment)
    {
        return await SendNotificationAsync(appointment, NotificationType.BookingReminder, BuildReminderMessage(appointment));
    }

    public async Task<WhatsAppNotification?> RetryNotificationAsync(int notificationId)
    {
        var notification = await _context.WhatsAppNotifications
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.Client)
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.Barber)
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.Service)
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification == null || notification.Appointment == null) return null;

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
        if (string.IsNullOrWhiteSpace(settings.MetaAccessToken) || string.IsNullOrWhiteSpace(settings.MetaPhoneNumberId))
        {
            notification.AttemptedAt = DateTime.UtcNow;
            notification.Status = NotificationStatus.Failed;
            notification.ErrorDescription = "Credenciais da Meta Cloud API não estão configuradas nas configurações da barbearia.";
            await _context.SaveChangesAsync();
            return notification;
        }

        return await SendPayloadToMetaAsync(notification, settings, notification.RecipientPhone, notification.Payload ?? string.Empty);
    }

    private async Task<WhatsAppNotification?> SendNotificationAsync(Appointment appointment, NotificationType type, string textMessage)
    {
        var client = appointment.Client ?? await _context.Users.FindAsync(appointment.ClientId);
        var recipientPhone = client?.PhoneNumber ?? "Sem Telefone";

        // Verifica consentimento LGPD
        if (client != null && !client.WhatsAppConsent)
        {
            _logger.LogWarning("[WHATSAPP CLOUD API] Notificação não enviada para {Phone}: cliente não concedeu consentimento.", recipientPhone);
            return null;
        }

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();

        // Limpa número de telefone (apenas dígitos)
        var cleanPhone = new string(recipientPhone.Where(char.IsDigit).ToArray());
        if (!cleanPhone.StartsWith("55") && cleanPhone.Length is 10 or 11)
        {
            cleanPhone = "55" + cleanPhone;
        }

        var notification = new WhatsAppNotification
        {
            AppointmentId = appointment.Id,
            RecipientPhone = recipientPhone,
            Type = type,
            AttemptedAt = DateTime.UtcNow,
            Status = NotificationStatus.Pending,
            Payload = textMessage,
            IsSimulated = false
        };

        _context.WhatsAppNotifications.Add(notification);
        await _context.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(settings.MetaAccessToken) || string.IsNullOrWhiteSpace(settings.MetaPhoneNumberId))
        {
            notification.Status = NotificationStatus.Failed;
            notification.ErrorDescription = "Token ou Phone Number ID da Meta Cloud API não configurados.";
            await _context.SaveChangesAsync();
            _logger.LogWarning("[WHATSAPP CLOUD API] Credenciais ausentes. Notificação marcada como falha.");
            return notification;
        }

        return await SendPayloadToMetaAsync(notification, settings, cleanPhone, textMessage);
    }

    private async Task<WhatsAppNotification> SendPayloadToMetaAsync(
        WhatsAppNotification notification,
        BusinessSettings settings,
        string cleanPhone,
        string textMessage)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WhatsAppCloudApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.MetaAccessToken);

            var url = $"https://graph.facebook.com/{settings.MetaApiVersion}/{settings.MetaPhoneNumberId}/messages";

            // Payload padrão do WhatsApp Cloud API para mensagem de texto ou template
            var requestBody = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = cleanPhone,
                type = "text",
                text = new
                {
                    preview_url = false,
                    body = textMessage
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, jsonContent);
            var responseString = await response.Content.ReadAsStringAsync();

            notification.AttemptedAt = DateTime.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                notification.Status = NotificationStatus.AcceptedByApi;
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                {
                    notification.MetaMessageId = messages[0].GetProperty("id").GetString();
                }
                _logger.LogInformation("[WHATSAPP CLOUD API] Mensagem enviada com sucesso para {Phone}. ID: {Id}", cleanPhone, notification.MetaMessageId);
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
                notification.ErrorDescription = $"HTTP {(int)response.StatusCode}: {responseString}";
                _logger.LogError("[WHATSAPP CLOUD API] Erro ao enviar mensagem para {Phone}: {Error}", cleanPhone, notification.ErrorDescription);
            }
        }
        catch (Exception ex)
        {
            notification.Status = NotificationStatus.Failed;
            notification.ErrorDescription = $"Exceção: {ex.Message}";
            _logger.LogError(ex, "[WHATSAPP CLOUD API] Exceção ao tentar enviar mensagem para {Phone}", cleanPhone);
        }

        await _context.SaveChangesAsync();
        return notification;
    }

    private static string BuildConfirmationMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var serviceName = appointment.Service?.Name ?? "Serviço";
        var barberName = appointment.Barber?.Name ?? "Profissional BarberShop";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");
        var priceFormatted = appointment.Price.ToString("C");

        return $"Olá, {clientName}! Seu agendamento na BarberShop foi confirmado.\n\n" +
               $"Serviço: {serviceName}\n" +
               $"Profissional: {barberName}\n" +
               $"Data: {dateFormatted}\n" +
               $"Horário: {timeFormatted}\n" +
               $"Valor: {priceFormatted}\n\n" +
               $"Forma de pagamento: presencial.\n\n" +
               $"Agradecemos pela preferência!";
    }

    private static string BuildCancellationMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var serviceName = appointment.Service?.Name ?? "Serviço";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");

        return $"Olá, {clientName}! Seu agendamento para {serviceName} no dia {dateFormatted} às {timeFormatted} foi cancelado.";
    }

    private static string BuildRescheduleMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var serviceName = appointment.Service?.Name ?? "Serviço";
        var barberName = appointment.Barber?.Name ?? "Profissional";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");

        return $"Olá, {clientName}! Seu agendamento foi reagendado com sucesso!\n\n" +
               $"Serviço: {serviceName}\n" +
               $"Profissional: {barberName}\n" +
               $"Nova Data: {dateFormatted}\n" +
               $"Novo Horário: {timeFormatted}\n\n" +
               $"Agradecemos pela preferência!";
    }

    private static string BuildReminderMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");

        return $"Lembrete BarberShop: Olá {clientName}, estamos te esperando hoje ({dateFormatted}) às {timeFormatted}! Até breve!";
    }
}
