using BarberShop.ViewModels.Booking;

namespace BarberShop.Services;

public interface IAvailabilityService
{
    Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(DateTime date, int serviceId, int? barberId);
    Task<int?> FindBestAvailableBarberAsync(DateTime startTime, DateTime endTime);
    Task<bool> IsSlotAvailableAsync(int barberId, DateTime startTime, DateTime endTime, int? excludeAppointmentId = null);
    DateTime GetCurrentBrasiliaTime();
}
