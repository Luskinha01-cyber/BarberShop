using BarberShop.Controllers.Admin;
using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BarberShop.Tests;

public class AdminManagementTests
{
    [Fact]
    public async Task ServicePriceUpdate_PreservesHistoricalPriceOnPastAppointments()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();

        var service = new Service
        {
            Name = "Cabelo",
            Description = "Corte",
            Price = 50.00m,
            DurationMinutes = 30,
            IsActive = true
        };
        context.Services.Add(service);

        var barber = new Barber { Name = "Barbeiro", IsActive = true };
        context.Barbers.Add(barber);

        var client = new ApplicationUser { Id = "c1", FullName = "Cliente", PhoneNumber = "111" };
        context.Users.Add(client);

        var pastAppointment = new Appointment
        {
            ClientId = client.Id,
            Barber = barber,
            Service = service,
            StartTime = DateTime.UtcNow.AddDays(-2),
            EndTime = DateTime.UtcNow.AddDays(-2).AddMinutes(30),
            Price = 50.00m, // Preço gravado na época
            DurationMinutes = 30,
            Status = AppointmentStatus.Completed
        };
        context.Appointments.Add(pastAppointment);
        await context.SaveChangesAsync();

        var controller = new ServicesController(context);
        controller.SetFakeTempData();

        // Act: Administrador altera o preço do serviço para R$ 75,00
        var editModel = new ServiceFormViewModel
        {
            Id = service.Id,
            Name = "Cabelo Premium",
            Description = "Corte Atualizado",
            Price = 75.00m,
            DurationMinutes = 35,
            IsActive = true
        };
        var result = await controller.Edit(service.Id, editModel);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        // O serviço foi atualizado para R$ 75
        var updatedService = await context.Services.FindAsync(service.Id);
        Assert.Equal(75.00m, updatedService!.Price);
        Assert.Equal(35, updatedService.DurationMinutes);

        // Mas o agendamento anterior PRESERVA os R$ 50,00 originais e os 30 min de duração!
        var savedAppointment = await context.Appointments.FindAsync(pastAppointment.Id);
        Assert.Equal(50.00m, savedAppointment!.Price);
        Assert.Equal(30, savedAppointment.DurationMinutes);
    }

    [Fact]
    public async Task Barber_ToggleStatus_DeactivatesWithoutDeletingHistory()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();
        var barber = new Barber { Name = "Mestre Carlos", IsActive = true };
        context.Barbers.Add(barber);

        var appointment = new Appointment
        {
            ClientId = "c1",
            Barber = barber,
            ServiceId = 1,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            Price = 50m,
            DurationMinutes = 30,
            Status = AppointmentStatus.Completed
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var controller = new BarbersController(context);
        controller.SetFakeTempData();

        // Act: Desativa o barbeiro
        await controller.ToggleStatus(barber.Id);

        // Assert
        var updatedBarber = await context.Barbers.FindAsync(barber.Id);
        Assert.False(updatedBarber!.IsActive);

        // O histórico permanece intacto
        var appointmentCount = await context.Appointments.CountAsync(a => a.BarberId == barber.Id);
        Assert.Equal(1, appointmentCount);
    }
}
