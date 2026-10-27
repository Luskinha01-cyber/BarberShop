using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers.Admin;

[Authorize(Roles = SeedData.AdminRole)]
[Route("Admin/[controller]")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppNotificationService _whatsAppService;

    public SettingsController(ApplicationDbContext context, IWhatsAppNotificationService whatsAppService)
    {
        _context = context;
        _whatsAppService = whatsAppService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
        var notifications = await _context.WhatsAppNotifications
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.Service)
            .OrderByDescending(n => n.AttemptedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.Notifications = notifications;
        return View(settings);
    }

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(BusinessSettings model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Notifications = await _context.WhatsAppNotifications
                .OrderByDescending(n => n.AttemptedAt)
                .Take(50)
                .ToListAsync();
            return View("Index", model);
        }

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new BusinessSettings();
            _context.BusinessSettings.Add(settings);
        }

        settings.ShopName = model.ShopName;
        settings.Phone = model.Phone;
        settings.Address = model.Address;
        settings.OpeningTime = model.OpeningTime;
        settings.ClosingTime = model.ClosingTime;
        settings.SlotIntervalMinutes = model.SlotIntervalMinutes;
        settings.MinimumNoticeHours = model.MinimumNoticeHours;
        settings.CancellationNoticeHours = model.CancellationNoticeHours;
        settings.BufferMinutes = model.BufferMinutes;
        settings.EnableWhatsAppNotifications = model.EnableWhatsAppNotifications;
        settings.EnableWhatsAppSimulation = model.EnableWhatsAppSimulation;
        settings.MetaPhoneNumberId = model.MetaPhoneNumberId;
        settings.MetaAccessToken = model.MetaAccessToken;
        settings.MetaApiVersion = string.IsNullOrWhiteSpace(model.MetaApiVersion) ? "v21.0" : model.MetaApiVersion;
        settings.MetaBusinessAccountId = model.MetaBusinessAccountId;
        settings.WebhookVerifyToken = model.WebhookVerifyToken;
        settings.WhatsAppProvider = string.IsNullOrWhiteSpace(model.WhatsAppProvider) ? "EvolutionApi" : model.WhatsAppProvider;
        settings.EvolutionApiUrl = model.EvolutionApiUrl;
        settings.EvolutionApiKey = model.EvolutionApiKey;
        settings.EvolutionInstanceName = model.EvolutionInstanceName;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Configurações da barbearia atualizadas com sucesso!";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("RetryNotification/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryNotification(int id)
    {
        var result = await _whatsAppService.RetryNotificationAsync(id);
        if (result == null)
        {
            TempData["ErrorMessage"] = "Notificação não encontrada para retentativa.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Retentativa processada. Novo status: {result.Status}.";
        }

        return RedirectToAction(nameof(Index));
    }
}
