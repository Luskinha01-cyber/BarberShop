using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Models.Enums;
using BarberShop.ViewModels.Booking;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(ApplicationDbContext context, ILogger<AvailabilityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public DateTime GetCurrentBrasiliaTime()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(-3);
            }
        }
    }

    public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(DateTime date, int serviceId, int? barberId)
    {
        var result = new List<AvailableSlotDto>();

        var service = await _context.Services.FindAsync(serviceId);
        if (service == null || !service.IsActive)
        {
            return result;
        }

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
        var nowBrasilia = GetCurrentBrasiliaTime();
        var targetDate = date.Date;

        // Não permite datas passadas
        if (targetDate < nowBrasilia.Date)
        {
            return result;
        }

        int durationMinutes = service.DurationMinutes;
        int slotInterval = settings.SlotIntervalMinutes > 0 ? settings.SlotIntervalMinutes : 30;
        int bufferMinutes = settings.BufferMinutes;
        var minNoticeTime = nowBrasilia.AddHours(settings.MinimumNoticeHours);

        // Se for um barbeiro específico
        if (barberId.HasValue)
        {
            var barber = await _context.Barbers
                .Include(b => b.WorkingHours)
                .Include(b => b.TimeOffs)
                .FirstOrDefaultAsync(b => b.Id == barberId.Value && b.IsActive);

            if (barber == null) return result;

            var workingHour = barber.WorkingHours.FirstOrDefault(w => w.DayOfWeek == targetDate.DayOfWeek);
            if (workingHour == null || !workingHour.IsWorkingDay)
            {
                return result; // Folga do barbeiro
            }

            var slots = await GenerateSlotsForBarberAsync(barber, workingHour, targetDate, durationMinutes, slotInterval, bufferMinutes, minNoticeTime);
            return slots;
        }

        // Caso "Qualquer Profissional Disponível"
        var activeBarbers = await _context.Barbers
            .Include(b => b.WorkingHours)
            .Include(b => b.TimeOffs)
            .Where(b => b.IsActive)
            .ToListAsync();

        if (!activeBarbers.Any()) return result;

        // Gera os horários possíveis com base no horário de funcionamento geral
        var opening = settings.OpeningTime;
        var closing = settings.ClosingTime;

        var currentTime = opening;
        while (currentTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= closing)
        {
            var slotStart = targetDate.Add(currentTime);
            var slotEnd = slotStart.AddMinutes(durationMinutes);

            if (slotStart >= minNoticeTime)
            {
                // Descobre quais barbeiros estão livres nesse slot
                var availableBarbersForSlot = new List<Barber>();
                foreach (var b in activeBarbers)
                {
                    var wh = b.WorkingHours.FirstOrDefault(w => w.DayOfWeek == targetDate.DayOfWeek);
                    if (wh == null || !wh.IsWorkingDay) continue;

                    // O horário precisa estar dentro do expediente do barbeiro
                    if (currentTime < wh.StartTime || currentTime.Add(TimeSpan.FromMinutes(durationMinutes)) > wh.EndTime) continue;

                    // Intervalo de almoço do barbeiro
                    if (wh.LunchBreakStart.HasValue && wh.LunchBreakEnd.HasValue)
                    {
                        var lunchStart = targetDate.Add(wh.LunchBreakStart.Value);
                        var lunchEnd = targetDate.Add(wh.LunchBreakEnd.Value);
                        if (slotStart < lunchEnd && slotEnd > lunchStart) continue;
                    }

                    // Folgas / Férias
                    bool hasTimeOff = b.TimeOffs.Any(t => t.StartDate <= slotEnd && t.EndDate >= slotStart);
                    if (hasTimeOff) continue;

                    // Bloqueios administrativos gerais
                    bool hasAdminBlock = await _context.BarberTimeOffs
                        .AnyAsync(t => t.BarberId == null && t.StartDate <= slotEnd && t.EndDate >= slotStart);
                    if (hasAdminBlock) continue;

                    // Verifica sobreposição de agendamento do barbeiro
                    bool hasOverlap = await _context.Appointments.AnyAsync(a =>
                        a.BarberId == b.Id &&
                        a.Status != AppointmentStatus.CancelledByClient &&
                        a.Status != AppointmentStatus.CancelledByAdmin &&
                        a.StartTime < slotEnd.AddMinutes(bufferMinutes) &&
                        a.EndTime > slotStart.AddMinutes(-bufferMinutes));

                    if (!hasOverlap)
                    {
                        availableBarbersForSlot.Add(b);
                    }
                }

                if (availableBarbersForSlot.Any())
                {
                    // Aplica balanceamento de carga para sugerir o melhor
                    var bestBarber = await SelectBarberWithLeastLoadAsync(availableBarbersForSlot, targetDate);

                    result.Add(new AvailableSlotDto
                    {
                        Time = currentTime.ToString(@"hh\:mm"),
                        FormattedTime = currentTime.ToString(@"hh\:mm"),
                        EndTime = currentTime.Add(TimeSpan.FromMinutes(durationMinutes)).ToString(@"hh\:mm"),
                        IsAvailable = true,
                        AssignedBarberId = bestBarber?.Id,
                        AssignedBarberName = bestBarber?.Name
                    });
                }
            }

            currentTime = currentTime.Add(TimeSpan.FromMinutes(slotInterval));
        }

        return result;
    }

    public async Task<int?> FindBestAvailableBarberAsync(DateTime startTime, DateTime endTime)
    {
        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
        var bufferMinutes = settings.BufferMinutes;
        var targetDate = startTime.Date;
        var dayOfWeek = targetDate.DayOfWeek;

        var activeBarbers = await _context.Barbers
            .Include(b => b.WorkingHours)
            .Include(b => b.TimeOffs)
            .Where(b => b.IsActive)
            .ToListAsync();

        var freeBarbers = new List<Barber>();

        foreach (var barber in activeBarbers)
        {
            var wh = barber.WorkingHours.FirstOrDefault(w => w.DayOfWeek == dayOfWeek);
            if (wh == null || !wh.IsWorkingDay) continue;

            if (startTime.TimeOfDay < wh.StartTime || endTime.TimeOfDay > wh.EndTime) continue;

            if (wh.LunchBreakStart.HasValue && wh.LunchBreakEnd.HasValue)
            {
                var lunchStart = targetDate.Add(wh.LunchBreakStart.Value);
                var lunchEnd = targetDate.Add(wh.LunchBreakEnd.Value);
                if (startTime < lunchEnd && endTime > lunchStart) continue;
            }

            bool hasTimeOff = barber.TimeOffs.Any(t => t.StartDate <= endTime && t.EndDate >= startTime);
            if (hasTimeOff) continue;

            bool hasAdminBlock = await _context.BarberTimeOffs
                .AnyAsync(t => t.BarberId == null && t.StartDate <= endTime && t.EndDate >= startTime);
            if (hasAdminBlock) continue;

            bool hasOverlap = await _context.Appointments.AnyAsync(a =>
                a.BarberId == barber.Id &&
                a.Status != AppointmentStatus.CancelledByClient &&
                a.Status != AppointmentStatus.CancelledByAdmin &&
                a.StartTime < endTime.AddMinutes(bufferMinutes) &&
                a.EndTime > startTime.AddMinutes(-bufferMinutes));

            if (!hasOverlap)
            {
                freeBarbers.Add(barber);
            }
        }

        if (!freeBarbers.Any()) return null;

        var selected = await SelectBarberWithLeastLoadAsync(freeBarbers, targetDate);
        return selected?.Id;
    }

    public async Task<bool> IsSlotAvailableAsync(int barberId, DateTime startTime, DateTime endTime, int? excludeAppointmentId = null)
    {
        var barber = await _context.Barbers
            .Include(b => b.WorkingHours)
            .Include(b => b.TimeOffs)
            .FirstOrDefaultAsync(b => b.Id == barberId && b.IsActive);

        if (barber == null) return false;

        var targetDate = startTime.Date;
        var dayOfWeek = targetDate.DayOfWeek;

        var wh = barber.WorkingHours.FirstOrDefault(w => w.DayOfWeek == dayOfWeek);
        if (wh == null || !wh.IsWorkingDay) return false;

        if (startTime.TimeOfDay < wh.StartTime || endTime.TimeOfDay > wh.EndTime) return false;

        if (wh.LunchBreakStart.HasValue && wh.LunchBreakEnd.HasValue)
        {
            var lunchStart = targetDate.Add(wh.LunchBreakStart.Value);
            var lunchEnd = targetDate.Add(wh.LunchBreakEnd.Value);
            if (startTime < lunchEnd && endTime > lunchStart) return false;
        }

        // Folgas ou férias do barbeiro
        if (barber.TimeOffs.Any(t => t.StartDate <= endTime && t.EndDate >= startTime))
            return false;

        // Bloqueio geral administrativo
        bool hasAdminBlock = await _context.BarberTimeOffs
            .AnyAsync(t => t.BarberId == null && t.StartDate <= endTime && t.EndDate >= startTime);
        if (hasAdminBlock) return false;

        var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
        var bufferMinutes = settings.BufferMinutes;

        // Sobreposição de agendamentos no banco
        var query = _context.Appointments.Where(a =>
            a.BarberId == barberId &&
            a.Status != AppointmentStatus.CancelledByClient &&
            a.Status != AppointmentStatus.CancelledByAdmin &&
            a.StartTime < endTime.AddMinutes(bufferMinutes) &&
            a.EndTime > startTime.AddMinutes(-bufferMinutes));

        if (excludeAppointmentId.HasValue)
        {
            query = query.Where(a => a.Id != excludeAppointmentId.Value);
        }

        return !await query.AnyAsync();
    }

    private async Task<List<AvailableSlotDto>> GenerateSlotsForBarberAsync(
        Barber barber,
        BarberWorkingHour workingHour,
        DateTime targetDate,
        int durationMinutes,
        int slotInterval,
        int bufferMinutes,
        DateTime minNoticeTime)
    {
        var slots = new List<AvailableSlotDto>();
        var currentTime = workingHour.StartTime;
        var endTime = workingHour.EndTime;

        while (currentTime.Add(TimeSpan.FromMinutes(durationMinutes)) <= endTime)
        {
            var slotStart = targetDate.Add(currentTime);
            var slotEnd = slotStart.AddMinutes(durationMinutes);

            bool available = slotStart >= minNoticeTime;

            if (available)
            {
                // Almoço
                if (workingHour.LunchBreakStart.HasValue && workingHour.LunchBreakEnd.HasValue)
                {
                    var lunchStart = targetDate.Add(workingHour.LunchBreakStart.Value);
                    var lunchEnd = targetDate.Add(workingHour.LunchBreakEnd.Value);
                    if (slotStart < lunchEnd && slotEnd > lunchStart)
                    {
                        available = false;
                    }
                }
            }

            if (available)
            {
                // Folgas / Férias
                bool hasTimeOff = barber.TimeOffs.Any(t => t.StartDate <= slotEnd && t.EndDate >= slotStart);
                if (hasTimeOff) available = false;
            }

            if (available)
            {
                // Bloqueios administrativos
                bool hasAdminBlock = await _context.BarberTimeOffs
                    .AnyAsync(t => t.BarberId == null && t.StartDate <= slotEnd && t.EndDate >= slotStart);
                if (hasAdminBlock) available = false;
            }

            if (available)
            {
                // Sobreposição de agendamento
                bool hasOverlap = await _context.Appointments.AnyAsync(a =>
                    a.BarberId == barber.Id &&
                    a.Status != AppointmentStatus.CancelledByClient &&
                    a.Status != AppointmentStatus.CancelledByAdmin &&
                    a.StartTime < slotEnd.AddMinutes(bufferMinutes) &&
                    a.EndTime > slotStart.AddMinutes(-bufferMinutes));

                if (hasOverlap) available = false;
            }

            if (available)
            {
                slots.Add(new AvailableSlotDto
                {
                    Time = currentTime.ToString(@"hh\:mm"),
                    FormattedTime = currentTime.ToString(@"hh\:mm"),
                    EndTime = currentTime.Add(TimeSpan.FromMinutes(durationMinutes)).ToString(@"hh\:mm"),
                    IsAvailable = true,
                    AssignedBarberId = barber.Id,
                    AssignedBarberName = barber.Name
                });
            }

            currentTime = currentTime.Add(TimeSpan.FromMinutes(slotInterval));
        }

        return slots;
    }

    private async Task<Barber?> SelectBarberWithLeastLoadAsync(List<Barber> candidates, DateTime targetDate)
    {
        if (candidates.Count == 1) return candidates[0];

        var candidateIds = candidates.Select(c => c.Id).ToList();

        // Conta quantos agendamentos ativos cada barbeiro possui na data
        var appointmentCounts = await _context.Appointments
            .Where(a => candidateIds.Contains(a.BarberId) &&
                        a.StartTime.Date == targetDate.Date &&
                        a.Status != AppointmentStatus.CancelledByClient &&
                        a.Status != AppointmentStatus.CancelledByAdmin)
            .GroupBy(a => a.BarberId)
            .Select(g => new { BarberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BarberId, x => x.Count);

        var sorted = candidates
            .OrderBy(c => appointmentCounts.TryGetValue(c.Id, out var count) ? count : 0)
            .ThenBy(c => c.Name)
            .ToList();

        return sorted.FirstOrDefault();
    }
}
