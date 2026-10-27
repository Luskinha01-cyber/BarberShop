using BarberShop.Models;
using BarberShop.ViewModels.Booking;

namespace BarberShop.Services;

public interface IBookingService
{
    Task<(bool Success, string Message, Appointment? Appointment)> CreateBookingAsync(string clientId, CreateBookingRequest request);
    Task<(bool Success, string Message)> CancelBookingAsync(int appointmentId, string userId, bool isAdmin);
    Task<(bool Success, string Message)> RescheduleBookingAsync(int appointmentId, string userId, DateTime newDate, string newTimeSlot, bool isAdmin);
}
