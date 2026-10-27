using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services.Notifications;

public class FakeWhatsAppNotificationService : IWhatsAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FakeWhatsAppNotificationService> _logger;

    public FakeWhatsAppNotificationService(ApplicationDbContext context, ILogger<FakeWhatsAppNotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WhatsAppNotification?> SendBookingConfirmationAsync(Appointment appointment)
    {
        return await ProcessFakeNotificationAsync(appointment, NotificationType.BookingConfirmation, BuildConfirmationMessage(appointment));
    }

    public async Task<WhatsAppNotification?> SendBookingCancellationAsync(Appointment appointment)
    {
        return await ProcessFakeNotificationAsync(appointment, NotificationType.BookingCancellation, BuildCancellationMessage(appointment));
    }

    public async Task<WhatsAppNotification?> SendBookingRescheduleAsync(Appointment appointment)
    {
        return await ProcessFakeNotificationAsync(appointment, NotificationType.BookingReschedule, BuildRescheduleMessage(appointment));
    }

    public async Task<WhatsAppNotification?> SendReminderAsync(Appointment appointment)
    {
        return await ProcessFakeNotificationAsync(appointment, NotificationType.BookingReminder, BuildReminderMessage(appointment));
    }

    public async Task<WhatsAppNotification?> RetryNotificationAsync(int notificationId)
    {
        var notification = await _context.WhatsAppNotifications
            .Include(n => n.Appointment)
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification == null) return null;

        notification.AttemptedAt = DateTime.UtcNow;
        notification.Status = NotificationStatus.Simulated;
        notification.IsSimulated = true;
        notification.ErrorDescription = null;
        notification.MetaMessageId = $"sim_retry_{Guid.NewGuid():N}";

        await _context.SaveChangesAsync();
        _logger.LogInformation("[WHATSAPP SIMULAÇÃO] Retentativa simulada com sucesso para {Phone}", notification.RecipientPhone);

        return notification;
    }

    private async Task<WhatsAppNotification?> ProcessFakeNotificationAsync(Appointment appointment, NotificationType type, string message)
    {
        var client = appointment.Client ?? await _context.Users.FindAsync(appointment.ClientId);
        var recipientPhone = client?.PhoneNumber ?? "Sem Telefone";

        // Verifica consentimento LGPD
        if (client != null && !client.WhatsAppConsent)
        {
            _logger.LogWarning("[WHATSAPP SIMULAÇÃO] Notificação não enviada para {Phone}: cliente não concedeu consentimento.", recipientPhone);
            return null;
        }

        var notification = new WhatsAppNotification
        {
            AppointmentId = appointment.Id,
            RecipientPhone = recipientPhone,
            Type = type,
            AttemptedAt = DateTime.UtcNow,
            Status = NotificationStatus.Simulated,
            MetaMessageId = $"sim_{Guid.NewGuid():N}",
            ErrorDescription = "Mensagem simulada em ambiente local (nenhum SMS/WhatsApp real foi cobrado ou disparado).",
            Payload = message,
            IsSimulated = true
        };

        _context.WhatsAppNotifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("\n=========================================\n[WHATSAPP SIMULAÇÃO REGISTRADA]\nDestinatário: {Phone}\nTipo: {Type}\nMensagem:\n{Msg}\n=========================================", 
            recipientPhone, type, message);

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

        return $"Olá, {clientName}! Informamos que seu agendamento para {serviceName} no dia {dateFormatted} às {timeFormatted} foi cancelado.\n\n" +
               $"Caso queira agendar um novo horário, acesse o nosso site a qualquer momento.";
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

        return $"Lembrete BarberShop: Olá {clientName}, estamos te esperando hoje ({dateFormatted}) às {timeFormatted} para o seu atendimento! Até breve!";
    }
}
