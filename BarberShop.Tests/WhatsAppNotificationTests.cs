using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services.Notifications;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BarberShop.Tests;

public class WhatsAppNotificationTests
{
    [Fact]
    public async Task FakeNotification_WithConsent_RecordsSimulatedNotificationInDatabase()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();
        var mockLogger = new Mock<ILogger<FakeWhatsAppNotificationService>>();
        var fakeService = new FakeWhatsAppNotificationService(context, mockLogger.Object);

        var client = new ApplicationUser
        {
            Id = "c1",
            FullName = "Lucas Silva",
            PhoneNumber = "5511988887777",
            WhatsAppConsent = true,
            WhatsAppConsentDate = DateTime.UtcNow
        };
        context.Users.Add(client);

        var barber = new Barber { Id = 1, Name = "Carlos", IsActive = true };
        var service = new Service { Id = 1, Name = "Cabelo", Price = 50m, DurationMinutes = 30, IsActive = true };
        context.Barbers.Add(barber);
        context.Services.Add(service);

        var appointment = new Appointment
        {
            Id = 10,
            ClientId = client.Id,
            Client = client,
            BarberId = barber.Id,
            Barber = barber,
            ServiceId = service.Id,
            Service = service,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddMinutes(30),
            Price = 50m,
            DurationMinutes = 30,
            Status = AppointmentStatus.Confirmed
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        // Act
        var notification = await fakeService.SendBookingConfirmationAsync(appointment);

        // Assert
        Assert.NotNull(notification);
        Assert.True(notification.IsSimulated);
        Assert.Equal(NotificationStatus.Simulated, notification.Status);
        Assert.Equal("5511988887777", notification.RecipientPhone);
        Assert.Contains("Olá, Lucas Silva!", notification.Payload);
        Assert.Contains("Cabelo", notification.Payload);
        Assert.Contains("Carlos", notification.Payload);

        // Verifica persistência no banco
        var savedInDb = await context.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(savedInDb);
        Assert.True(savedInDb.IsSimulated);
    }

    [Fact]
    public async Task FakeNotification_WithoutConsent_DoesNotSendOrRecord()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();
        var mockLogger = new Mock<ILogger<FakeWhatsAppNotificationService>>();
        var fakeService = new FakeWhatsAppNotificationService(context, mockLogger.Object);

        var client = new ApplicationUser
        {
            Id = "c-no-consent",
            FullName = "Cliente Sem Consentimento",
            PhoneNumber = "5511911112222",
            WhatsAppConsent = false // Não consentiu!
        };
        context.Users.Add(client);

        var appointment = new Appointment
        {
            Id = 11,
            ClientId = client.Id,
            Client = client,
            BarberId = 1,
            ServiceId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddMinutes(30),
            Price = 50m,
            DurationMinutes = 30
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        // Act
        var notification = await fakeService.SendBookingConfirmationAsync(appointment);

        // Assert: Retorna nulo e nada é gravado
        Assert.Null(notification);
        Assert.Empty(context.WhatsAppNotifications);
    }
}
