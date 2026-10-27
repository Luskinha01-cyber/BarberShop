using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services;
using BarberShop.ViewModels.Admin;
using BarberShop.ViewModels.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers.Admin;

[Authorize(Roles = SeedData.AdminRole)]
[Route("Admin/[controller]")]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBookingService _bookingService;
    private readonly IAvailabilityService _availabilityService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppointmentsController(
        ApplicationDbContext context,
        IBookingService bookingService,
        IAvailabilityService availabilityService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _bookingService = bookingService;
        _availabilityService = availabilityService;
        _userManager = userManager;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        DateTime? date = null,
        int? barberId = null,
        int? serviceId = null,
        string? status = null,
        string? search = null)
    {
        var query = _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.WhatsAppNotifications)
            .AsQueryable();

        if (date.HasValue)
        {
            var target = date.Value.Date;
            query = query.Where(a => a.StartTime.Date == target);
        }

        if (barberId.HasValue && barberId > 0)
        {
            query = query.Where(a => a.BarberId == barberId.Value);
        }

        if (serviceId.HasValue && serviceId > 0)
        {
            query = query.Where(a => a.ServiceId == serviceId.Value);
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.Client.FullName.ToLower().Contains(term) ||
                                     a.Client.PhoneNumber!.Contains(term));
        }

        var appointments = await query
            .OrderByDescending(a => a.StartTime)
            .Select(a => new AdminAppointmentItemViewModel
            {
                Id = a.Id,
                ClientName = a.Client.FullName,
                ClientPhone = a.Client.PhoneNumber ?? "",
                BarberName = a.Barber.Name,
                ServiceName = a.Service.Name,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Price = a.Price,
                Status = a.Status,
                PaymentStatus = a.PaymentStatus,
                AmountPaid = a.AmountPaid,
                Notes = a.Notes,
                WhatsAppStatus = a.WhatsAppNotifications.OrderByDescending(n => n.AttemptedAt).FirstOrDefault() != null
                    ? (a.WhatsAppNotifications.OrderByDescending(n => n.AttemptedAt).First().IsSimulated ? "Simulada" : a.WhatsAppNotifications.OrderByDescending(n => n.AttemptedAt).First().Status.ToString())
                    : "Pendente"
            })
            .ToListAsync();

        ViewBag.Barbers = await _context.Barbers.OrderBy(b => b.Name).ToListAsync();
        ViewBag.Services = await _context.Services.OrderBy(s => s.Name).ToListAsync();
        ViewBag.CurrentDate = date?.ToString("yyyy-MM-dd");
        ViewBag.CurrentBarberId = barberId;
        ViewBag.CurrentServiceId = serviceId;
        ViewBag.CurrentStatus = status;
        ViewBag.SearchTerm = search;

        return View(appointments);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Clients = await _context.Users.OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Barbers = await _context.Barbers.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
        ViewBag.Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

        return View(new ManualAppointmentViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManualAppointmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Clients = await _context.Users.OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Barbers = await _context.Barbers.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            return View(model);
        }

        var request = new CreateBookingRequest
        {
            ServiceId = model.ServiceId,
            BarberId = model.BarberId,
            Date = model.Date,
            TimeSlot = model.StartTime,
            Notes = model.Notes
        };

        var (success, message, appointment) = await _bookingService.CreateBookingAsync(model.ClientId, request);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            ViewBag.Clients = await _context.Users.OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Barbers = await _context.Barbers.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = "Agendamento registrado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, AppointmentStatus status, string? notes)
    {
        var appointment = await _context.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        appointment.Status = status;
        if (!string.IsNullOrEmpty(notes))
        {
            appointment.Notes = notes;
        }

        if (status == AppointmentStatus.Completed && appointment.PaymentStatus == PaymentStatus.Pending)
        {
            // Opcional: pode manter pending até que o pagamento seja expressamente confirmado
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Status do agendamento #{id} atualizado para {status}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UpdatePayment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePayment(int id, PaymentStatus paymentStatus, decimal? amountPaid)
    {
        var appointment = await _context.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        appointment.PaymentStatus = paymentStatus;
        if (paymentStatus == PaymentStatus.Paid)
        {
            appointment.AmountPaid = amountPaid ?? appointment.Price;
            appointment.PaidAt = DateTime.UtcNow;
        }
        else
        {
            appointment.AmountPaid = null;
            appointment.PaidAt = null;
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Pagamento do agendamento #{id} registrado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var (success, message) = await _bookingService.CancelBookingAsync(id, user?.Id ?? "", isAdmin: true);

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
}
