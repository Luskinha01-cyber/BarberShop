using System.Text;
using System.Text.Json;
using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services.Notifications;

public class EvolutionApiService : IWhatsAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EvolutionApiService> _logger;

    public EvolutionApiService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<EvolutionApiService> logger)
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
        if (string.IsNullOrWhiteSpace(settings.EvolutionApiUrl) || string.IsNullOrWhiteSpace(settings.EvolutionInstanceName))
        {
            notification.AttemptedAt = DateTime.UtcNow;
            notification.Status = NotificationStatus.Failed;
            notification.ErrorDescription = "Parâmetros da Evolution API (URL ou Instância) não estão configurados.";
            await _context.SaveChangesAsync();
            return notification;
        }

        return await SendPayloadToEvolutionAsync(notification, settings, notification.RecipientPhone, notification.Payload ?? string.Empty);
    }

    private async Task<WhatsAppNotification?> SendNotificationAsync(Appointment appointment, NotificationType type, string textMessage)
    {
        var client = appointment.Client ?? await _context.Users.FindAsync(appointment.ClientId);
        var recipientPhone = client?.PhoneNumber ?? "Sem Telefone";

        // Verifica consentimento LGPD
        if (client != null && !client.WhatsAppConsent)
        {
            _logger.LogWarning("[EVOLUTION API] Notificação não enviada para {Phone}: cliente não concedeu consentimento.", recipientPhone);
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

        if (string.IsNullOrWhiteSpace(settings.EvolutionApiUrl) || string.IsNullOrWhiteSpace(settings.EvolutionInstanceName))
        {
            notification.Status = NotificationStatus.Failed;
            notification.ErrorDescription = "URL da Evolution API ou Nome da Instância não foram configurados.";
            await _context.SaveChangesAsync();
            _logger.LogWarning("[EVOLUTION API] Configurações ausentes. Notificação marcada como falha.");
            return notification;
        }

        return await SendPayloadToEvolutionAsync(notification, settings, cleanPhone, textMessage);
    }

    private async Task<WhatsAppNotification> SendPayloadToEvolutionAsync(
        WhatsAppNotification notification,
        BusinessSettings settings,
        string cleanPhone,
        string textMessage)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("EvolutionApi");
            var baseUrl = (settings.EvolutionApiUrl ?? "http://localhost:8080").TrimEnd('/');
            var instance = settings.EvolutionInstanceName ?? "barbershop";
            var url = $"{baseUrl}/message/sendText/{instance}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrWhiteSpace(settings.EvolutionApiKey))
            {
                request.Headers.Add("apikey", settings.EvolutionApiKey);
            }

            var requestBody = new
            {
                number = cleanPhone,
                text = textMessage,
                delay = 1200
            };

            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            notification.AttemptedAt = DateTime.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                notification.Status = NotificationStatus.AcceptedByApi;
                try
                {
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("key", out var keyObj) && keyObj.TryGetProperty("id", out var idProp))
                    {
                        notification.MetaMessageId = idProp.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("messageId", out var msgIdProp))
                    {
                        notification.MetaMessageId = msgIdProp.GetString();
                    }
                }
                catch
                {
                    // Ignora falha de parse do ID
                }

                _logger.LogInformation("[EVOLUTION API] Mensagem enviada com sucesso para {Phone}.", cleanPhone);
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
                notification.ErrorDescription = $"HTTP {(int)response.StatusCode}: {responseString}";
                _logger.LogError("[EVOLUTION API] Erro ao enviar mensagem para {Phone}: {Error}", cleanPhone, notification.ErrorDescription);
            }
        }
        catch (Exception ex)
        {
            notification.Status = NotificationStatus.Failed;
            notification.ErrorDescription = $"Exceção: {ex.Message}";
            _logger.LogError(ex, "[EVOLUTION API] Exceção ao tentar enviar mensagem para {Phone}", cleanPhone);
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

        return $"💈 *BarberShop - Agendamento Confirmado!*\n\n" +
               $"Olá, *{clientName}*! Seu horário foi reservado com sucesso.\n\n" +
               $"✂️ *Serviço:* {serviceName}\n" +
               $"👤 *Profissional:* {barberName}\n" +
               $"📅 *Data:* {dateFormatted}\n" +
               $"⏰ *Horário:* {timeFormatted}\n" +
               $"💰 *Valor:* {priceFormatted}\n\n" +
               $"Caso precise reagendar ou cancelar, entre em contato ou acesse nosso site.\n" +
               $"Agradecemos a preferência! ✨";
    }

    private static string BuildCancellationMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var serviceName = appointment.Service?.Name ?? "Serviço";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");

        return $"💈 *BarberShop - Cancelamento*\n\n" +
               $"Olá, *{clientName}*! Seu agendamento para *{serviceName}* no dia *{dateFormatted}* às *{timeFormatted}* foi cancelado.\n\n" +
               $"Esperamos você em uma próxima oportunidade!";
    }

    private static string BuildRescheduleMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var serviceName = appointment.Service?.Name ?? "Serviço";
        var barberName = appointment.Barber?.Name ?? "Profissional";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");

        return $"💈 *BarberShop - Agendamento Reagendado!*\n\n" +
               $"Olá, *{clientName}*! Seu agendamento foi reagendado com sucesso:\n\n" +
               $"✂️ *Serviço:* {serviceName}\n" +
               $"👤 *Profissional:* {barberName}\n" +
               $"📅 *Nova Data:* {dateFormatted}\n" +
               $"⏰ *Novo Horário:* {timeFormatted}\n\n" +
               $"Te aguardamos! ✨";
    }

    private static string BuildReminderMessage(Appointment appointment)
    {
        var clientName = appointment.Client?.FullName ?? "Cliente";
        var serviceName = appointment.Service?.Name ?? "Serviço";
        var dateFormatted = appointment.StartTime.ToString("dd/MM/yyyy");
        var timeFormatted = appointment.StartTime.ToString("HH:mm");

        return $"⏰ *Lembrete BarberShop*\n\n" +
               $"Olá, *{clientName}*! Lembramos do seu horário hoje para *{serviceName}* às *{timeFormatted}*.\n\n" +
               $"Até breve!";
    }
}
