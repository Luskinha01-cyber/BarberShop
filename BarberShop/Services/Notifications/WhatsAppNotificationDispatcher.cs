using BarberShop.Data;
using BarberShop.Models;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services.Notifications;

public class WhatsAppNotificationDispatcher : IWhatsAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly FakeWhatsAppNotificationService _fakeService;
    private readonly WhatsAppCloudApiService _realService;
    private readonly ILogger<WhatsAppNotificationDispatcher> _logger;

    public WhatsAppNotificationDispatcher(
        ApplicationDbContext context,
        FakeWhatsAppNotificationService fakeService,
        WhatsAppCloudApiService realService,
        ILogger<WhatsAppNotificationDispatcher> logger)
    {
        _context = context;
        _fakeService = fakeService;
        _realService = realService;
        _logger = logger;
    }

    private async Task<IWhatsAppNotificationService> GetActiveServiceAsync()
    {
        var settings = await _context.BusinessSettings.FirstOrDefaultAsync();
        bool useSimulation = settings?.EnableWhatsAppSimulation ?? true;

        if (useSimulation)
        {
            _logger.LogInformation("[WHATSAPP DISPATCHER] Roteando para FakeWhatsAppNotificationService (Modo Simulado ativo).");
            return _fakeService;
        }

        _logger.LogInformation("[WHATSAPP DISPATCHER] Roteando para WhatsAppCloudApiService (Meta Cloud API oficial).");
        return _realService;
    }

    public async Task<WhatsAppNotification?> SendBookingConfirmationAsync(Appointment appointment)
    {
        var service = await GetActiveServiceAsync();
        return await service.SendBookingConfirmationAsync(appointment);
    }

    public async Task<WhatsAppNotification?> SendBookingCancellationAsync(Appointment appointment)
    {
        var service = await GetActiveServiceAsync();
        return await service.SendBookingCancellationAsync(appointment);
    }

    public async Task<WhatsAppNotification?> SendBookingRescheduleAsync(Appointment appointment)
    {
        var service = await GetActiveServiceAsync();
        return await service.SendBookingRescheduleAsync(appointment);
    }

    public async Task<WhatsAppNotification?> SendReminderAsync(Appointment appointment)
    {
        var service = await GetActiveServiceAsync();
        return await service.SendReminderAsync(appointment);
    }

    public async Task<WhatsAppNotification?> RetryNotificationAsync(int notificationId)
    {
        var service = await GetActiveServiceAsync();
        return await service.RetryNotificationAsync(notificationId);
    }
}
