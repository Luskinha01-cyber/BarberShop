using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services;
using BarberShop.ViewModels.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[Authorize]
public class ClientController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBookingService _bookingService;
    private readonly IAvailabilityService _availabilityService;

    public ClientController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IBookingService bookingService,
        IAvailabilityService availabilityService)
    {
        _context = context;
        _userManager = userManager;
        _bookingService = bookingService;
        _availabilityService = availabilityService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null, string? period = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var nowBrasilia = _availabilityService.GetCurrentBrasiliaTime();
        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();

        // Próximo agendamento ativo futuro
        var nextApp = await _context.Appointments
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.WhatsAppNotifications)
            .Where(a => a.ClientId == user.Id &&
                        a.StartTime >= nowBrasilia &&
                        a.Status == AppointmentStatus.Confirmed)
            .OrderBy(a => a.StartTime)
            .FirstOrDefaultAsync();

        // Consulta histórico
        var query = _context.Appointments
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.WhatsAppNotifications)
            .Where(a => a.ClientId == user.Id);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrEmpty(period))
        {
            switch (period.ToLower())
            {
                case "future":
                    query = query.Where(a => a.StartTime >= nowBrasilia);
                    break;
                case "past":
                    query = query.Where(a => a.StartTime < nowBrasilia);
                    break;
                case "last30":
                    var thirtyDaysAgo = nowBrasilia.AddDays(-30);
                    query = query.Where(a => a.StartTime >= thirtyDaysAgo);
                    break;
            }
        }

        var historyList = await query
            .OrderByDescending(a => a.StartTime)
            .ToListAsync();

        var viewModel = new ClientDashboardViewModel
        {
            ClientName = user.FullName,
            ClientPhone = user.PhoneNumber ?? "",
            WhatsAppConsent = user.WhatsAppConsent,
            SelectedStatus = status,
            SelectedPeriod = period,
            NextAppointment = nextApp != null ? MapToItem(nextApp, nowBrasilia, settings) : null,
            History = historyList.Select(a => MapToItem(a, nowBrasilia, settings)).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var (success, message) = await _bookingService.CancelBookingAsync(id, user.Id, isAdmin: false);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
        }
        else
        {
            TempData["SuccessMessage"] = message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule([FromBody] RescheduleRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized(new { success = false, message = "Sessão expirada." });

        var (success, message) = await _bookingService.RescheduleBookingAsync(
            request.AppointmentId,
            user.Id,
            request.NewDate,
            request.NewTimeSlot,
            isAdmin: false);

        if (!success)
        {
            return StatusCode(409, new { success = false, message });
        }

        return Ok(new { success = true, message });
    }

    private static AppointmentItemViewModel MapToItem(Appointment a, DateTime nowBrasilia, BusinessSettings settings)
    {
        var lastNotif = a.WhatsAppNotifications.OrderByDescending(n => n.AttemptedAt).FirstOrDefault();
        string notifStatus = lastNotif != null
            ? (lastNotif.IsSimulated ? "Simulada" : lastNotif.Status.ToString())
            : "Pendente";

        bool canCancel = a.Status == AppointmentStatus.Confirmed &&
                         a.StartTime >= nowBrasilia.AddHours(settings.CancellationNoticeHours);

        bool canReschedule = a.Status == AppointmentStatus.Confirmed &&
                            a.StartTime >= nowBrasilia;

        return new AppointmentItemViewModel
        {
            Id = a.Id,
            ServiceName = a.Service.Name,
            Price = a.Price,
            DurationMinutes = a.DurationMinutes,
            BarberName = a.Barber.Name,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = a.Status,
            PaymentStatus = a.PaymentStatus,
            Notes = a.Notes,
            WhatsAppStatus = notifStatus,
            CanCancel = canCancel,
            CanReschedule = canReschedule
        };
    }
}
