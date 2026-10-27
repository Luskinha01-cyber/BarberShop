using System.Data;
using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.Services.Notifications;
using BarberShop.ViewModels.Booking;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly IWhatsAppNotificationService _whatsAppNotificationService;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        ApplicationDbContext context,
        IAvailabilityService availabilityService,
        IWhatsAppNotificationService whatsAppNotificationService,
        ILogger<BookingService> logger)
    {
        _context = context;
        _availabilityService = availabilityService;
        _whatsAppNotificationService = whatsAppNotificationService;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, Appointment? Appointment)> CreateBookingAsync(string clientId, CreateBookingRequest request)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return (false, "Cliente não autenticado ou inválido.", null);
        }

        var service = await _context.Services.FindAsync(request.ServiceId);
        if (service == null || !service.IsActive)
        {
            return (false, "O serviço selecionado é inválido ou está inativo.", null);
        }

        if (service.Price <= 0)
        {
            return (false, "Não é permitido agendar serviços sem preço configurado.", null);
        }

        if (!TimeSpan.TryParse(request.TimeSlot, out var timeOfDay))
        {
            return (false, "Horário informado é inválido.", null);
        }

        var startTime = request.Date.Date.Add(timeOfDay);
        var endTime = startTime.AddMinutes(service.DurationMinutes);
        var nowBrasilia = _availabilityService.GetCurrentBrasiliaTime();

        if (startTime < nowBrasilia)
        {
            return (false, "Não é permitido realizar agendamentos para datas ou horários passados.", null);
        }

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
        if (startTime < nowBrasilia.AddHours(settings.MinimumNoticeHours))
        {
            return (false, $"O agendamento requer antecedência mínima de {settings.MinimumNoticeHours} hora(s).", null);
        }

        int selectedBarberId = 0;

        // Suporte para transação Serializable no SQL Server
        var strategy = _context.Database.CreateExecutionStrategy();

        Appointment? createdAppointment = null;
        string? failureReason = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;

            try
            {
                if (request.BarberId.HasValue)
                {
                    var barber = await _context.Barbers.FindAsync(request.BarberId.Value);
                    if (barber == null || !barber.IsActive)
                    {
                        failureReason = "O profissional selecionado não está ativo ou não foi encontrado.";
                        if (transaction != null) await transaction.RollbackAsync();
                        return;
                    }
                    selectedBarberId = barber.Id;
                }
                else
                {
                    // "Qualquer profissional disponível" -> Escolhe o de menor carga
                    var bestBarberId = await _availabilityService.FindBestAvailableBarberAsync(startTime, endTime);
                    if (!bestBarberId.HasValue)
                    {
                        failureReason = "Este horário acabou de ser reservado. Atualize a disponibilidade e escolha outro horário.";
                        if (transaction != null) await transaction.RollbackAsync();
                        return;
                    }
                    selectedBarberId = bestBarberId.Value;
                }

                // Revalidação estrita imediatamente antes de confirmar no banco
                bool isAvailable = await _availabilityService.IsSlotAvailableAsync(selectedBarberId, startTime, endTime);
                if (!isAvailable)
                {
                    failureReason = "Este horário acabou de ser reservado. Atualize a disponibilidade e escolha outro horário.";
                    if (transaction != null) await transaction.RollbackAsync();
                    return;
                }

                createdAppointment = new Appointment
                {
                    ClientId = clientId,
                    BarberId = selectedBarberId,
                    ServiceId = service.Id,
                    StartTime = startTime,
                    EndTime = endTime,
                    Price = service.Price, // Snapshot histórico
                    DurationMinutes = service.DurationMinutes, // Snapshot histórico
                    Status = AppointmentStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Pending,
                    Notes = request.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(createdAppointment);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Erro de concorrência ou transação ao criar agendamento.");
                failureReason = "Este horário acabou de ser reservado. Atualize a disponibilidade e escolha outro horário.";
            }
        });

        if (failureReason != null || createdAppointment == null)
        {
            return (false, failureReason ?? "Não foi possível confirmar o agendamento.", null);
        }

        // Carrega as navegações para montar as notificações
        await _context.Entry(createdAppointment).Reference(a => a.Client).LoadAsync();
        await _context.Entry(createdAppointment).Reference(a => a.Barber).LoadAsync();
        await _context.Entry(createdAppointment).Reference(a => a.Service).LoadAsync();

        // Dispara WhatsApp de forma segura (falhas na API do WhatsApp nunca cancelam a reserva salva)
        try
        {
            await _whatsAppNotificationService.SendBookingConfirmationAsync(createdAppointment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha não impeditiva ao disparar notificação do WhatsApp.");
        }

        return (true, "Agendamento confirmado com sucesso!", createdAppointment);
    }

    public async Task<(bool Success, string Message)> CancelBookingAsync(int appointmentId, string userId, bool isAdmin)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
        {
            return (false, "Agendamento não encontrado.");
        }

        if (!isAdmin && appointment.ClientId != userId)
        {
            return (false, "Você não tem permissão para cancelar este agendamento.");
        }

        if (appointment.Status == AppointmentStatus.Completed)
        {
            return (false, "Não é permitido cancelar um atendimento que já foi concluído.");
        }

        if (appointment.Status == AppointmentStatus.CancelledByClient || appointment.Status == AppointmentStatus.CancelledByAdmin)
        {
            return (false, "Este agendamento já está cancelado.");
        }

        var nowBrasilia = _availabilityService.GetCurrentBrasiliaTime();
        if (appointment.StartTime < nowBrasilia)
        {
            return (false, "Não é possível cancelar agendamentos passados.");
        }

        if (!isAdmin)
        {
            var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
            if (appointment.StartTime < nowBrasilia.AddHours(settings.CancellationNoticeHours))
            {
                return (false, $"O cancelamento deve ser feito com pelo menos {settings.CancellationNoticeHours} hora(s) de antecedência. Entre em contato diretamente com a barbearia.");
            }
        }

        appointment.Status = isAdmin ? AppointmentStatus.CancelledByAdmin : AppointmentStatus.CancelledByClient;
        appointment.CancelledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Dispara notificação de cancelamento
        try
        {
            await _whatsAppNotificationService.SendBookingCancellationAsync(appointment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar mensagem de cancelamento pelo WhatsApp.");
        }

        return (true, "Agendamento cancelado com sucesso. O horário foi liberado.");
    }

    public async Task<(bool Success, string Message)> RescheduleBookingAsync(int appointmentId, string userId, DateTime newDate, string newTimeSlot, bool isAdmin)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
        {
            return (false, "Agendamento não encontrado.");
        }

        if (!isAdmin && appointment.ClientId != userId)
        {
            return (false, "Você não tem permissão para reagendar este agendamento.");
        }

        if (appointment.Status == AppointmentStatus.Completed)
        {
            return (false, "Não é permitido reagendar um atendimento concluído.");
        }

        if (!TimeSpan.TryParse(newTimeSlot, out var timeOfDay))
        {
            return (false, "Novo horário informado é inválido.");
        }

        var newStartTime = newDate.Date.Add(timeOfDay);
        var newEndTime = newStartTime.AddMinutes(appointment.DurationMinutes);
        var nowBrasilia = _availabilityService.GetCurrentBrasiliaTime();

        if (newStartTime < nowBrasilia)
        {
            return (false, "Não é permitido reagendar para uma data ou horário passado.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        bool success = false;
        string? failureReason = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;

            try
            {
                // Revalida se o barbeiro está livre no novo horário, ignorando o próprio agendamento atual
                bool isAvailable = await _availabilityService.IsSlotAvailableAsync(appointment.BarberId, newStartTime, newEndTime, appointment.Id);
                if (!isAvailable)
                {
                    failureReason = "O novo horário selecionado não está mais disponível. Por favor, escolha outro horário.";
                    if (transaction != null) await transaction.RollbackAsync();
                    return;
                }

                appointment.StartTime = newStartTime;
                appointment.EndTime = newEndTime;
                appointment.Status = AppointmentStatus.Confirmed; // Garante que fica confirmado

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                success = true;
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Erro ao reagendar agendamento.");
                failureReason = "Ocorreu um erro ao reagendar. O agendamento original foi mantido.";
            }
        });

        if (!success)
        {
            return (false, failureReason ?? "Não foi possível reagendar.");
        }

        try
        {
            await _whatsAppNotificationService.SendBookingRescheduleAsync(appointment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar notificação de reagendamento pelo WhatsApp.");
        }

        return (true, "Agendamento reagendado com sucesso!");
    }
}
