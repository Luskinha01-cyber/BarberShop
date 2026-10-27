using System.Diagnostics;
using BarberShop.Data;
using BarberShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var services = await _context.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Price)
            .ToListAsync();

        var barbers = await _context.Barbers
            .Where(b => b.IsActive)
            .ToListAsync();

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();

        ViewBag.Services = services;
        ViewBag.Barbers = barbers;
        ViewBag.Settings = settings;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
