using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services;
using BarberShop.Services.Notifications;
using BarberShop.ViewModels.Booking;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BarberShop.Tests;

public class BookingAndAvailabilityTests
{
    private static (ApplicationDbContext context, AvailabilityService availability, BookingService booking, Mock<IWhatsAppNotificationService> mockWa) SetupServices()
    {
        var context = TestHelper.CreateInMemoryDbContext();
        var mockLoggerAvail = new Mock<ILogger<AvailabilityService>>();
        var mockLoggerBooking = new Mock<ILogger<BookingService>>();
        var mockWa = new Mock<IWhatsAppNotificationService>();

        var availability = new AvailabilityService(context, mockLoggerAvail.Object);
        var booking = new BookingService(context, availability, mockWa.Object, mockLoggerBooking.Object);

        return (context, availability, booking, mockWa);
    }

    [Fact]
    public async Task CreateBooking_ValidData_CreatesAppointmentWithCorrectDurationAndEndTime()
    {
        // Arrange
        var (context, availability, booking, mockWa) = SetupServices();

        var barber = new Barber { Name = "Barbeiro Teste", IsActive = true };
        context.Barbers.Add(barber);

        var service = new Service
        {
            Name = "Cabelo + barba",
            Description = "Completo",
            Price = 85.00m,
            DurationMinutes = 60,
            IsActive = true
        };
        context.Services.Add(service);

        // Horário de trabalho na terça-feira
        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barber,
            DayOfWeek = DayOfWeek.Tuesday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            LunchBreakStart = new TimeSpan(12, 0, 0),
            LunchBreakEnd = new TimeSpan(13, 0, 0),
            IsWorkingDay = true
        });

        var client = new ApplicationUser
        {
            Id = "client-1",
            FullName = "Cliente Um",
            PhoneNumber = "5511999991111",
            WhatsAppConsent = true
        };
        context.Users.Add(client);
        await context.SaveChangesAsync();

        // Encontra a próxima terça-feira futura
        var nextTuesday = DateTime.Today.AddDays(1);
        while (nextTuesday.DayOfWeek != DayOfWeek.Tuesday)
        {
            nextTuesday = nextTuesday.AddDays(1);
        }

        var request = new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber.Id,
            Date = nextTuesday,
            TimeSlot = "14:00"
        };

        // Act
        var result = await booking.CreateBookingAsync(client.Id, request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Appointment);
        Assert.Equal(service.Price, result.Appointment.Price);
        Assert.Equal(60, result.Appointment.DurationMinutes);
        Assert.Equal(new TimeSpan(14, 0, 0), result.Appointment.StartTime.TimeOfDay);
        Assert.Equal(new TimeSpan(15, 0, 0), result.Appointment.EndTime.TimeOfDay); // 14:00 + 60m = 15:00!
        Assert.Equal(AppointmentStatus.Confirmed, result.Appointment.Status);

        // Verifica disparo do WhatsApp
        mockWa.Verify(w => w.SendBookingConfirmationAsync(It.IsAny<Appointment>()), Times.Once);
    }

    [Fact]
    public async Task CreateBooking_OverlappingInterval_RejectsConflict()
    {
        // Arrange
        var (context, availability, booking, mockWa) = SetupServices();

        var barber = new Barber { Name = "Barbeiro Teste", IsActive = true };
        context.Barbers.Add(barber);

        var service = new Service { Name = "Corte", Price = 50.00m, DurationMinutes = 30, IsActive = true };
        context.Services.Add(service);

        var nextWednesday = DateTime.Today.AddDays(1);
        while (nextWednesday.DayOfWeek != DayOfWeek.Wednesday)
        {
            nextWednesday = nextWednesday.AddDays(1);
        }

        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barber,
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsWorkingDay = true
        });

        var client1 = new ApplicationUser { Id = "c1", FullName = "C1", PhoneNumber = "5511999991111" };
        var client2 = new ApplicationUser { Id = "c2", FullName = "C2", PhoneNumber = "5511999992222" };
        context.Users.AddRange(client1, client2);
        await context.SaveChangesAsync();

        // 1ª Reserva: 14:00 às 14:30
        var req1 = new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber.Id,
            Date = nextWednesday,
            TimeSlot = "14:00"
        };
        var res1 = await booking.CreateBookingAsync(client1.Id, req1);
        Assert.True(res1.Success);

        // 2ª Reserva no mesmo horário para o mesmo barbeiro
        var req2 = new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber.Id,
            Date = nextWednesday,
            TimeSlot = "14:00"
        };
        var res2 = await booking.CreateBookingAsync(client2.Id, req2);

        // Assert: Deve falhar e alertar sobre o horário reservado
        Assert.False(res2.Success);
        Assert.Contains("Este horário acabou de ser reservado", res2.Message);
    }

    [Fact]
    public async Task CreateBooking_TwoBarbersSimultaneously_BothAreConfirmed()
    {
        // Arrange
        var (context, availability, booking, _) = SetupServices();

        var barber1 = new Barber { Name = "Carlos", IsActive = true };
        var barber2 = new Barber { Name = "Rafael", IsActive = true };
        context.Barbers.AddRange(barber1, barber2);

        var service = new Service { Name = "Cabelo", Price = 50.00m, DurationMinutes = 30, IsActive = true };
        context.Services.Add(service);

        var nextThursday = DateTime.Today.AddDays(1);
        while (nextThursday.DayOfWeek != DayOfWeek.Thursday)
        {
            nextThursday = nextThursday.AddDays(1);
        }

        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barber1,
            DayOfWeek = DayOfWeek.Thursday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsWorkingDay = true
        });
        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barber2,
            DayOfWeek = DayOfWeek.Thursday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsWorkingDay = true
        });

        var c1 = new ApplicationUser { Id = "c1", FullName = "C1", PhoneNumber = "111" };
        var c2 = new ApplicationUser { Id = "c2", FullName = "C2", PhoneNumber = "222" };
        context.Users.AddRange(c1, c2);
        await context.SaveChangesAsync();

        // Act: Barbeiro 1 e Barbeiro 2 atendem simultaneamente às 15:00
        var res1 = await booking.CreateBookingAsync(c1.Id, new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber1.Id,
            Date = nextThursday,
            TimeSlot = "15:00"
        });

        var res2 = await booking.CreateBookingAsync(c2.Id, new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber2.Id,
            Date = nextThursday,
            TimeSlot = "15:00"
        });

        // Assert: Ambos devem ser aceitos com sucesso!
        Assert.True(res1.Success);
        Assert.True(res2.Success);
    }

    [Fact]
    public async Task CreateBooking_PastDate_IsRejected()
    {
        // Arrange
        var (context, availability, booking, _) = SetupServices();
        var service = new Service { Name = "Cabelo", Price = 50m, DurationMinutes = 30, IsActive = true };
        context.Services.Add(service);
        var barber = new Barber { Name = "Carlos", IsActive = true };
        context.Barbers.Add(barber);
        var user = new ApplicationUser { Id = "u1", FullName = "User 1", PhoneNumber = "111" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var pastDate = DateTime.Today.AddDays(-1);

        // Act
        var result = await booking.CreateBookingAsync(user.Id, new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber.Id,
            Date = pastDate,
            TimeSlot = "14:00"
        });

        // Assert
        Assert.False(result.Success);
        Assert.Contains("passados", result.Message);
    }

    [Fact]
    public async Task CancelBooking_FreesUpSlotForNewReservation()
    {
        // Arrange
        var (context, availability, booking, mockWa) = SetupServices();

        var barber = new Barber { Name = "Barbeiro", IsActive = true };
        context.Barbers.Add(barber);
        var service = new Service { Name = "Corte", Price = 50m, DurationMinutes = 30, IsActive = true };
        context.Services.Add(service);

        var futureDate = DateTime.Today.AddDays(5);
        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barber,
            DayOfWeek = futureDate.DayOfWeek,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsWorkingDay = true
        });

        var user1 = new ApplicationUser { Id = "u1", FullName = "User 1", PhoneNumber = "111" };
        var user2 = new ApplicationUser { Id = "u2", FullName = "User 2", PhoneNumber = "222" };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        // Cria reserva 1
        var bookResult = await booking.CreateBookingAsync(user1.Id, new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber.Id,
            Date = futureDate,
            TimeSlot = "10:00"
        });
        Assert.True(bookResult.Success);
        int appointmentId = bookResult.Appointment!.Id;

        // Cancela a reserva
        var cancelResult = await booking.CancelBookingAsync(appointmentId, user1.Id, isAdmin: false);
        Assert.True(cancelResult.Success);

        // Act: Outro cliente tenta reservar o mesmo horário liberado
        var bookResult2 = await booking.CreateBookingAsync(user2.Id, new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = barber.Id,
            Date = futureDate,
            TimeSlot = "10:00"
        });

        // Assert: Horário liberado e nova reserva aceita!
        Assert.True(bookResult2.Success);
        Assert.Equal(user2.Id, bookResult2.Appointment!.ClientId);
    }

    [Fact]
    public async Task AnyAvailableBarber_BalancesLoadByChoosingBarberWithFewestAppointments()
    {
        // Arrange
        var (context, availability, booking, _) = SetupServices();

        var barberBusy = new Barber { Name = "Barbeiro Ocupado", IsActive = true };
        var barberFree = new Barber { Name = "Barbeiro Livre", IsActive = true };
        context.Barbers.AddRange(barberBusy, barberFree);

        var service = new Service { Name = "Corte", Price = 50m, DurationMinutes = 30, IsActive = true };
        context.Services.Add(service);

        var targetDate = DateTime.Today.AddDays(3);
        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barberBusy,
            DayOfWeek = targetDate.DayOfWeek,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsWorkingDay = true
        });
        context.BarberWorkingHours.Add(new BarberWorkingHour
        {
            Barber = barberFree,
            DayOfWeek = targetDate.DayOfWeek,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsWorkingDay = true
        });

        var dummyClient = new ApplicationUser { Id = "c0", FullName = "C0", PhoneNumber = "000" };
        var targetClient = new ApplicationUser { Id = "c-target", FullName = "Target", PhoneNumber = "999" };
        context.Users.AddRange(dummyClient, targetClient);

        // Barbeiro Ocupado já tem 2 agendamentos no dia em horários diferentes
        context.Appointments.Add(new Appointment
        {
            ClientId = dummyClient.Id,
            BarberId = barberBusy.Id,
            ServiceId = service.Id,
            StartTime = targetDate.AddHours(9),
            EndTime = targetDate.AddHours(9).AddMinutes(30),
            Price = 50m,
            DurationMinutes = 30,
            Status = AppointmentStatus.Confirmed
        });
        context.Appointments.Add(new Appointment
        {
            ClientId = dummyClient.Id,
            BarberId = barberBusy.Id,
            ServiceId = service.Id,
            StartTime = targetDate.AddHours(10),
            EndTime = targetDate.AddHours(10).AddMinutes(30),
            Price = 50m,
            DurationMinutes = 30,
            Status = AppointmentStatus.Confirmed
        });

        await context.SaveChangesAsync();

        // Act: Cliente agenda às 14:00 escolhendo "Qualquer profissional disponível" (BarberId = null)
        var result = await booking.CreateBookingAsync(targetClient.Id, new CreateBookingRequest
        {
            ServiceId = service.Id,
            BarberId = null, // Qualquer profissional
            Date = targetDate,
            TimeSlot = "14:00"
        });

        // Assert: Deve ter sido alocado para o Barbeiro Livre (com 0 atendimentos vs 2 do Ocupado)!
        Assert.True(result.Success);
        Assert.Equal(barberFree.Id, result.Appointment!.BarberId);
    }
}
