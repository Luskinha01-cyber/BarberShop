using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services;
using BarberShop.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers.Admin;

[Authorize(Roles = SeedData.AdminRole)]
[Route("Admin/[controller]")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;

    public DashboardController(ApplicationDbContext context, IAvailabilityService availabilityService)
    {
        _context = context;
        _availabilityService = availabilityService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string period = "today")
    {
        var nowBrasilia = _availabilityService.GetCurrentBrasiliaTime();
        var today = nowBrasilia.Date;

        DateTime startDate = today;
        DateTime endDate = today.AddDays(1).AddTicks(-1);

        switch (period.ToLower())
        {
            case "week":
                startDate = today.AddDays(-(int)today.DayOfWeek);
                endDate = startDate.AddDays(7).AddTicks(-1);
                break;
            case "month":
                startDate = new DateTime(today.Year, today.Month, 1);
                endDate = startDate.AddMonths(1).AddTicks(-1);
                break;
            case "year":
                startDate = new DateTime(today.Year, 1, 1);
                endDate = startDate.AddYears(1).AddTicks(-1);
                break;
            default: // today
                startDate = today;
                endDate = today.AddDays(1).AddTicks(-1);
                break;
        }

        var periodAppointments = await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.WhatsAppNotifications)
            .Where(a => a.StartTime >= startDate && a.StartTime <= endDate)
            .ToListAsync();

        var todayCount = await _context.Appointments
            .CountAsync(a => a.StartTime >= today && a.StartTime < today.AddDays(1));

        var upcomingCount = await _context.Appointments
            .CountAsync(a => a.StartTime >= nowBrasilia && a.Status == AppointmentStatus.Confirmed);

        var totalClients = await _context.Users
            .CountAsync(u => u.UserName != "admin@barbershop.com");

        var confirmedCount = periodAppointments.Count(a => a.Status == AppointmentStatus.Confirmed);
        var completedCount = periodAppointments.Count(a => a.Status == AppointmentStatus.Completed);
        var cancelledCount = periodAppointments.Count(a => a.Status == AppointmentStatus.CancelledByClient || a.Status == AppointmentStatus.CancelledByAdmin);

        // Faturamento estimado (soma dos agendamentos confirmados ou concluídos no período)
        var estimatedRevenue = periodAppointments
            .Where(a => a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Completed)
            .Sum(a => a.Price);

        // Faturamento recebido presencialmente
        var receivedRevenue = periodAppointments
            .Where(a => a.PaymentStatus == PaymentStatus.Paid)
            .Sum(a => a.AmountPaid ?? a.Price);

        // Serviços mais solicitados no período
        var topServices = periodAppointments
            .GroupBy(a => a.Service.Name)
            .Select(g => new TopServiceStat
            {
                ServiceName = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(x => x.Price)
            })
            .OrderByDescending(s => s.Count)
            .Take(5)
            .ToList();

        // Agendamentos por Barbeiro
        var appointmentsByBarber = periodAppointments
            .GroupBy(a => a.Barber.Name)
            .Select(g => new BarberStat
            {
                BarberName = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(x => x.Price)
            })
            .OrderByDescending(b => b.Count)
            .ToList();

        // Agendamentos recentes
        var recentAppointments = await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .Include(a => a.WhatsAppNotifications)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
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

        var viewModel = new AdminDashboardViewModel
        {
            PeriodFilter = period,
            TodayAppointmentsCount = todayCount,
            UpcomingAppointmentsCount = upcomingCount,
            ConfirmedAppointmentsCount = confirmedCount,
            CompletedAppointmentsCount = completedCount,
            CancelledAppointmentsCount = cancelledCount,
            TotalClientsCount = totalClients,
            EstimatedRevenue = estimatedRevenue,
            ReceivedRevenue = receivedRevenue,
            TopServices = topServices,
            AppointmentsByBarber = appointmentsByBarber,
            RecentAppointments = recentAppointments
        };

        return View(viewModel);
    }
}
