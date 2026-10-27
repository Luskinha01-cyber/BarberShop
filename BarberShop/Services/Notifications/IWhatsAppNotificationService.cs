using BarberShop.Models;

namespace BarberShop.Services.Notifications;

public interface IWhatsAppNotificationService
{
    Task<WhatsAppNotification?> SendBookingConfirmationAsync(Appointment appointment);
    Task<WhatsAppNotification?> SendBookingCancellationAsync(Appointment appointment);
    Task<WhatsAppNotification?> SendBookingRescheduleAsync(Appointment appointment);
    Task<WhatsAppNotification?> SendReminderAsync(Appointment appointment);
    Task<WhatsAppNotification?> RetryNotificationAsync(int notificationId);
}
