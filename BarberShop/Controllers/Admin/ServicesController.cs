using BarberShop.Data;
using BarberShop.Models;
using BarberShop.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers.Admin;

[Authorize(Roles = SeedData.AdminRole)]
[Route("Admin/[controller]")]
public class ServicesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ServicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var services = await _context.Services
            .OrderBy(s => s.Id)
            .ToListAsync();
        return View(services);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View(new ServiceFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var service = new Service
        {
            Name = model.Name.Trim(),
            Description = model.Description.Trim(),
            Price = model.Price,
            DurationMinutes = model.DurationMinutes,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Serviço '{service.Name}' criado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service == null) return NotFound();

        var model = new ServiceFormViewModel
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsActive = service.IsActive
        };

        return View(model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var service = await _context.Services.FindAsync(id);
        if (service == null) return NotFound();

        service.Name = model.Name.Trim();
        service.Description = model.Description.Trim();
        service.Price = model.Price;
        service.DurationMinutes = model.DurationMinutes;
        service.IsActive = model.IsActive;
        service.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Serviço '{service.Name}' atualizado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service == null) return NotFound();

        service.IsActive = !service.IsActive;
        service.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Serviço '{service.Name}' {(service.IsActive ? "ativado" : "desativado")} com sucesso.";

        return RedirectToAction(nameof(Index));
    }
}
