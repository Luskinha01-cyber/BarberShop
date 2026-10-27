using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers.Admin;

[Authorize(Roles = SeedData.AdminRole)]
[Route("Admin/[controller]")]
public class BarbersController : Controller
{
    private readonly ApplicationDbContext _context;

    public BarbersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var barbers = await _context.Barbers
            .Include(b => b.WorkingHours)
            .Include(b => b.TimeOffs)
            .OrderBy(b => b.Name)
            .ToListAsync();

        return View(barbers);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        var model = new BarberFormViewModel();
        // Inicializa horários para a semana
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            bool isWork = day != DayOfWeek.Sunday && day != DayOfWeek.Monday;
            model.WorkingHours.Add(new BarberWorkingHourDto
            {
                DayOfWeek = day,
                IsWorkingDay = isWork,
                StartTime = "09:00",
                EndTime = "19:00",
                LunchBreakStart = "12:00",
                LunchBreakEnd = "13:00"
            });
        }
        return View(model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BarberFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var barber = new Barber
        {
            Name = model.Name.Trim(),
            Phone = model.Phone?.Trim(),
            Description = model.Description?.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Barbers.Add(barber);
        await _context.SaveChangesAsync();

        // Adiciona horários de trabalho
        if (model.WorkingHours != null)
        {
            foreach (var wh in model.WorkingHours)
            {
                TimeSpan.TryParse(wh.StartTime, out var start);
                TimeSpan.TryParse(wh.EndTime, out var end);
                TimeSpan? lunchStart = TimeSpan.TryParse(wh.LunchBreakStart, out var ls) ? ls : null;
                TimeSpan? lunchEnd = TimeSpan.TryParse(wh.LunchBreakEnd, out var le) ? le : null;

                _context.BarberWorkingHours.Add(new BarberWorkingHour
                {
                    BarberId = barber.Id,
                    DayOfWeek = wh.DayOfWeek,
                    IsWorkingDay = wh.IsWorkingDay,
                    StartTime = start,
                    EndTime = end,
                    LunchBreakStart = lunchStart,
                    LunchBreakEnd = lunchEnd
                });
            }
            await _context.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = $"Barbeiro '{barber.Name}' cadastrado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var barber = await _context.Barbers
            .Include(b => b.WorkingHours)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null) return NotFound();

        var model = new BarberFormViewModel
        {
            Id = barber.Id,
            Name = barber.Name,
            Phone = barber.Phone,
            Description = barber.Description,
            IsActive = barber.IsActive,
            WorkingHours = barber.WorkingHours.OrderBy(w => w.DayOfWeek).Select(w => new BarberWorkingHourDto
            {
                DayOfWeek = w.DayOfWeek,
                IsWorkingDay = w.IsWorkingDay,
                StartTime = w.StartTime.ToString(@"hh\:mm"),
                EndTime = w.EndTime.ToString(@"hh\:mm"),
                LunchBreakStart = w.LunchBreakStart?.ToString(@"hh\:mm"),
                LunchBreakEnd = w.LunchBreakEnd?.ToString(@"hh\:mm")
            }).ToList()
        };

        // Garante que todos os dias da semana estejam presentes
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            if (!model.WorkingHours.Any(w => w.DayOfWeek == day))
            {
                model.WorkingHours.Add(new BarberWorkingHourDto
                {
                    DayOfWeek = day,
                    IsWorkingDay = false,
                    StartTime = "09:00",
                    EndTime = "19:00"
                });
            }
        }

        model.WorkingHours = model.WorkingHours.OrderBy(w => w.DayOfWeek).ToList();

        return View(model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BarberFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var barber = await _context.Barbers
            .Include(b => b.WorkingHours)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barber == null) return NotFound();

        barber.Name = model.Name.Trim();
        barber.Phone = model.Phone?.Trim();
        barber.Description = model.Description?.Trim();
        barber.IsActive = model.IsActive;
        barber.UpdatedAt = DateTime.UtcNow;

        // Atualiza horários
        if (model.WorkingHours != null)
        {
            foreach (var whDto in model.WorkingHours)
            {
                var existingWh = barber.WorkingHours.FirstOrDefault(w => w.DayOfWeek == whDto.DayOfWeek);
                TimeSpan.TryParse(whDto.StartTime, out var start);
                TimeSpan.TryParse(whDto.EndTime, out var end);
                TimeSpan? lunchStart = TimeSpan.TryParse(whDto.LunchBreakStart, out var ls) ? ls : null;
                TimeSpan? lunchEnd = TimeSpan.TryParse(whDto.LunchBreakEnd, out var le) ? le : null;

                if (existingWh != null)
                {
                    existingWh.IsWorkingDay = whDto.IsWorkingDay;
                    existingWh.StartTime = start;
                    existingWh.EndTime = end;
                    existingWh.LunchBreakStart = lunchStart;
                    existingWh.LunchBreakEnd = lunchEnd;
                }
                else
                {
                    barber.WorkingHours.Add(new BarberWorkingHour
                    {
                        BarberId = barber.Id,
                        DayOfWeek = whDto.DayOfWeek,
                        IsWorkingDay = whDto.IsWorkingDay,
                        StartTime = start,
                        EndTime = end,
                        LunchBreakStart = lunchStart,
                        LunchBreakEnd = lunchEnd
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Barbeiro '{barber.Name}' atualizado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var barber = await _context.Barbers.FindAsync(id);
        if (barber == null) return NotFound();

        barber.IsActive = !barber.IsActive;
        barber.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Barbeiro '{barber.Name}' {(barber.IsActive ? "ativado" : "desativado")} com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AddTimeOff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeOff(int barberId, DateTime startDate, DateTime endDate, string reason, TimeOffType type)
    {
        var barber = await _context.Barbers.FindAsync(barberId);
        if (barber == null) return NotFound();

        if (endDate < startDate)
        {
            TempData["ErrorMessage"] = "A data final da folga/férias não pode ser anterior à data inicial.";
            return RedirectToAction(nameof(Index));
        }

        var timeOff = new BarberTimeOff
        {
            BarberId = barberId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = reason,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        _context.BarberTimeOffs.Add(timeOff);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Bloqueio/folga registrado para {barber.Name}.";
        return RedirectToAction(nameof(Index));
    }
}
