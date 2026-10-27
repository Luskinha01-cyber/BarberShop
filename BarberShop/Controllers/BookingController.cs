using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services;
using BarberShop.ViewModels.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly IBookingService _bookingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BookingController> _logger;

    public BookingController(
        ApplicationDbContext context,
        IAvailabilityService availabilityService,
        IBookingService bookingService,
        UserManager<ApplicationUser> userManager,
        ILogger<BookingController> logger)
    {
        _context = context;
        _availabilityService = availabilityService;
        _bookingService = bookingService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? serviceId = null)
    {
        var services = await _context.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Id)
            .ToListAsync();

        var barbers = await _context.Barbers
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();

        var viewModel = new BookingWizardViewModel
        {
            Services = services,
            Barbers = barbers,
            Settings = settings,
            PreSelectedServiceId = serviceId
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableSlots(DateTime date, int serviceId, int? barberId)
    {
        if (serviceId <= 0)
        {
            return BadRequest(new { message = "Serviço inválido." });
        }

        var slots = await _availabilityService.GetAvailableSlotsAsync(date, serviceId, barberId);
        return Json(slots);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return BadRequest(new { success = false, message = errors });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { success = false, message = "Sessão expirada. Por favor, faça login novamente." });
        }

        var result = await _bookingService.CreateBookingAsync(user.Id, request);

        if (!result.Success)
        {
            return StatusCode(409, new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            appointmentId = result.Appointment!.Id,
            redirectUrl = Url.Action("Confirmation", "Booking", new { id = result.Appointment.Id })
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Confirmation(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var appointment = await _context.Appointments
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.WhatsAppNotifications)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound("Agendamento não encontrado.");
        }

        // Garante que clientes só visualizam os próprios agendamentos
        bool isAdmin = User.IsInRole(SeedData.AdminRole);
        if (!isAdmin && appointment.ClientId != user.Id)
        {
            return Forbid();
        }

        var latestNotification = appointment.WhatsAppNotifications
            .OrderByDescending(n => n.AttemptedAt)
            .FirstOrDefault();

        var viewModel = new BookingConfirmationViewModel
        {
            AppointmentId = appointment.Id,
            ServiceName = appointment.Service.Name,
            Price = appointment.Price,
            DurationMinutes = appointment.DurationMinutes,
            BarberName = appointment.Barber.Name,
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            ClientName = user.FullName,
            ClientPhone = user.PhoneNumber ?? "",
            PaymentMethod = "Presencial",
            WhatsAppConsent = user.WhatsAppConsent,
            WhatsAppNotificationSimulated = latestNotification?.IsSimulated ?? true,
            WhatsAppStatusDescription = latestNotification != null 
                ? $"Status: {latestNotification.Status} ({latestNotification.ErrorDescription ?? "Processada"})"
                : "Notificação pendente ou não solicitada"
        };

        return View(viewModel);
    }
}
