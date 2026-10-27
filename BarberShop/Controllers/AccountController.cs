using BarberShop.Data;
using BarberShop.Models;
using BarberShop.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole(SeedData.AdminRole))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }
            return RedirectToAction("Index", "Client");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = model.Identifier.Trim();
        ApplicationUser? user = null;

        // Se for e-mail
        if (identifier.Contains('@'))
        {
            user = await _userManager.FindByEmailAsync(identifier);
        }
        else
        {
            // Se for celular: normaliza para dígitos
            var cleanPhone = NormalizePhoneNumber(identifier);
            user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone || u.UserName == cleanPhone);
        }

        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Celular/e-mail ou senha inválidos.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Usuário {User} autenticado com sucesso.", user.UserName);

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(SeedData.AdminRole))
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Client");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Esta conta foi temporariamente bloqueada devido a múltiplas tentativas inválidas. Tente novamente em 10 minutos.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Celular/e-mail ou senha inválidos.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Client");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var cleanPhone = NormalizePhoneNumber(model.PhoneNumber);
        if (cleanPhone.Length < 10)
        {
            ModelState.AddModelError("PhoneNumber", "Informe um número de celular válido com DDD.");
            return View(model);
        }

        // Verifica duplicidade de telefone
        bool phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == cleanPhone || u.UserName == cleanPhone);
        if (phoneExists)
        {
            ModelState.AddModelError("PhoneNumber", "Já existe uma conta cadastrada com este número de celular. Faça login ou utilize outro número.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = cleanPhone,
            PhoneNumber = cleanPhone,
            FullName = model.FullName.Trim(),
            PhoneNumberConfirmed = true,
            WhatsAppConsent = model.WhatsAppConsent,
            WhatsAppConsentDate = model.WhatsAppConsent ? DateTime.UtcNow : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, SeedData.ClientRole);
            await _signInManager.SignInAsync(user, isPersistent: true);

            _logger.LogInformation("Novo cliente cadastrado com sucesso: {Phone}", cleanPhone);
            return RedirectToAction("Index", "Booking");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private static string NormalizePhoneNumber(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        // Se já tiver 55 no início com 12 ou 13 dígitos
        if (digits.StartsWith("55") && (digits.Length == 12 || digits.Length == 13))
        {
            return digits;
        }
        // Se tiver 10 ou 11 dígitos (DDD + número), adiciona o DDI 55
        if (digits.Length is 10 or 11)
        {
            return "55" + digits;
        }
        return digits;
    }
}
