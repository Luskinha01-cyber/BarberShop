using BarberShop.Data;
using BarberShop.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BarberShop.Tests;

public static class TestHelper
{
    public static ApplicationDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Seed básico de configurações e serviços
        if (!context.BusinessSettings.Any())
        {
            context.BusinessSettings.Add(new BusinessSettings
            {
                Id = 1,
                ShopName = "BarberShop Test",
                Phone = "+5511987654321",
                Address = "Rua Teste, 100",
                OpeningTime = new TimeSpan(9, 0, 0),
                ClosingTime = new TimeSpan(19, 0, 0),
                SlotIntervalMinutes = 30,
                MinimumNoticeHours = 1,
                CancellationNoticeHours = 2,
                BufferMinutes = 0,
                EnableWhatsAppNotifications = true,
                EnableWhatsAppSimulation = true
            });
            context.SaveChanges();
        }

        return context;
    }

    public static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        return mgr;
    }

    public static void SetFakeTempData(this Microsoft.AspNetCore.Mvc.Controller controller)
    {
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var tempDataProvider = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>();
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, tempDataProvider.Object);
    }
}
