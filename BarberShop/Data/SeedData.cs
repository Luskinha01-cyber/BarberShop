using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BarberShop.Models;

namespace BarberShop.Data;

public static class SeedData
{
    public const string AdminRole = "Admin";
    public const string ClientRole = "Client";

    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Aplica migrations pendentes se o banco for relacional
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }

        // 1. Roles
        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
        }
        if (!await roleManager.RoleExistsAsync(ClientRole))
        {
            await roleManager.CreateAsync(new IdentityRole(ClientRole));
        }

        // 2. Configurações da Barbearia
        if (!await context.BusinessSettings.AnyAsync())
        {
            context.BusinessSettings.Add(new BusinessSettings
            {
                ShopName = "BarberShop Premium",
                Phone = "+5511987654321",
                Address = "Av. Paulista, 1000 - Jardins, São Paulo - SP",
                OpeningTime = new TimeSpan(9, 0, 0),
                ClosingTime = new TimeSpan(19, 0, 0),
                SlotIntervalMinutes = 30,
                MinimumNoticeHours = 1,
                CancellationNoticeHours = 2,
                BufferMinutes = 0,
                EnableWhatsAppNotifications = true,
                EnableWhatsAppSimulation = true,
                MetaPhoneNumberId = configuration["WhatsApp:PhoneNumberId"],
                MetaAccessToken = configuration["WhatsApp:AccessToken"],
                MetaApiVersion = configuration["WhatsApp:ApiVersion"] ?? "v21.0",
                MetaBusinessAccountId = configuration["WhatsApp:BusinessAccountId"]
            });
            await context.SaveChangesAsync();
        }

        // 3. Usuário Administrador
        var adminEmail = configuration["Admin:Email"] ?? "admin@barbershop.com";
        var adminPhone = configuration["Admin:Phone"] ?? "+5511999990000";
        var adminPassword = configuration["Admin:Password"] ?? "Admin@BarberShop2026!";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                PhoneNumber = adminPhone,
                FullName = "Administrador Geral",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                WhatsAppConsent = true,
                WhatsAppConsentDate = DateTime.UtcNow,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AdminRole);
            }
        }

        // 4. Serviços Iniciais
        if (!await context.Services.AnyAsync())
        {
            var initialServices = new List<Service>
            {
                new()
                {
                    Name = "Cabelo",
                    Description = "Corte personalizado com visagismo, lavagem e finalização premium com produtos selecionados.",
                    Price = 50.00m,
                    DurationMinutes = 30,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Cabelo + barba",
                    Description = "Combo completo com corte moderno, toalha quente, alinhamento navalhado e hidratação de barba.",
                    Price = 85.00m,
                    DurationMinutes = 60,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Apenas barba",
                    Description = "Barboterapia tradicional com toalha quente, esfoliação facial, navalha afiada e óleo reconstrutor.",
                    Price = 40.00m,
                    DurationMinutes = 30,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Cabelo + sobrancelha",
                    Description = "Corte exclusivo com alinhamento e limpeza precisa de sobrancelha na navalha ou pinça.",
                    Price = 65.00m,
                    DurationMinutes = 40,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Services.AddRange(initialServices);
            await context.SaveChangesAsync();
        }

        // 5. Barbeiros Iniciais
        if (!await context.Barbers.AnyAsync())
        {
            var barber1 = new Barber
            {
                Name = "Carlos Silva",
                Phone = "+5511988881111",
                Description = "Barbeiro Master com mais de 10 anos de experiência em cortes clássicos, fades perfeitos e consultoria de estilo.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var barber2 = new Barber
            {
                Name = "Rafael Santos",
                Phone = "+5511988882222",
                Description = "Especialista em barboterapia, visagismo facial e tratamentos capilares masculinos de alta performance.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Barbers.AddRange(barber1, barber2);
            await context.SaveChangesAsync();

            // Adiciona horários padrão de trabalho para os barbeiros (Terça a Sábado: 09h às 19h, Almoço: 12h às 13h)
            var days = new[]
            {
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            };

            foreach (var barber in new[] { barber1, barber2 })
            {
                foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                {
                    bool isWorkDay = days.Contains(day);
                    context.BarberWorkingHours.Add(new BarberWorkingHour
                    {
                        BarberId = barber.Id,
                        DayOfWeek = day,
                        StartTime = new TimeSpan(9, 0, 0),
                        EndTime = new TimeSpan(19, 0, 0),
                        LunchBreakStart = isWorkDay ? new TimeSpan(12, 0, 0) : null,
                        LunchBreakEnd = isWorkDay ? new TimeSpan(13, 0, 0) : null,
                        IsWorkingDay = isWorkDay
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
