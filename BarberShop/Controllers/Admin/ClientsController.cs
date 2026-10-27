using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers.Admin;

[Authorize(Roles = SeedData.AdminRole)]
[Route("Admin/[controller]")]
public class ClientsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClientsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? search = null)
    {
        var usersQuery = _context.Users
            .Where(u => u.UserName != "admin@barbershop.com")
            .Include(u => u.Appointments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            usersQuery = usersQuery.Where(u => u.FullName.ToLower().Contains(term) ||
                                               u.PhoneNumber!.Contains(term));
        }

        var clients = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminClientViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber ?? "",
                CreatedAt = u.CreatedAt,
                WhatsAppConsent = u.WhatsAppConsent,
                TotalAppointments = u.Appointments.Count(a => a.Status != AppointmentStatus.CancelledByClient && a.Status != AppointmentStatus.CancelledByAdmin),
                TotalSpent = u.Appointments.Where(a => a.PaymentStatus == PaymentStatus.Paid).Sum(a => a.AmountPaid ?? a.Price),
                LastVisit = u.Appointments.Where(a => a.Status == AppointmentStatus.Completed).OrderByDescending(a => a.StartTime).Select(a => (DateTime?)a.StartTime).FirstOrDefault()
            })
            .ToListAsync();

        ViewBag.SearchTerm = search;
        return View(clients);
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var user = await _context.Users
            .Include(u => u.Appointments)
                .ThenInclude(a => a.Barber)
            .Include(u => u.Appointments)
                .ThenInclude(a => a.Service)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        return View(user);
    }
}
