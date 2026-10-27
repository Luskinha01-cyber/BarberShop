using System.ComponentModel.DataAnnotations;
using BarberShop.Models.Enums;

namespace BarberShop.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public int TodayAppointmentsCount { get; set; }
    public int UpcomingAppointmentsCount { get; set; }
    public int ConfirmedAppointmentsCount { get; set; }
    public int CompletedAppointmentsCount { get; set; }
    public int CancelledAppointmentsCount { get; set; }
    public int TotalClientsCount { get; set; }
    public decimal EstimatedRevenue { get; set; }
    public decimal ReceivedRevenue { get; set; }
    public string PeriodFilter { get; set; } = "today"; // today, week, month, year

    public List<TopServiceStat> TopServices { get; set; } = new();
    public List<BarberStat> AppointmentsByBarber { get; set; } = new();
    public List<AdminAppointmentItemViewModel> RecentAppointments { get; set; } = new();
}

public class TopServiceStat
{
    public string ServiceName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public class BarberStat
{
    public string BarberName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public class AdminAppointmentItemViewModel
{
    public int Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string BarberName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal Price { get; set; }
    public AppointmentStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal? AmountPaid { get; set; }
    public string? Notes { get; set; }
    public string? WhatsAppStatus { get; set; }
}

public class ManualAppointmentViewModel
{
    [Required(ErrorMessage = "Informe o cliente.")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o profissional.")]
    public int BarberId { get; set; }

    [Required(ErrorMessage = "Selecione o serviço.")]
    public int ServiceId { get; set; }

    [Required(ErrorMessage = "Informe a data.")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Informe o horário inicial.")]
    public string StartTime { get; set; } = "14:00";

    public string? Notes { get; set; }
}

public class ServiceFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O nome do serviço é obrigatório.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "O preço é obrigatório.")]
    [Range(0.01, 10000.00, ErrorMessage = "Informe um preço válido maior que zero.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "A duração em minutos é obrigatória.")]
    [Range(5, 480, ErrorMessage = "A duração deve estar entre 5 e 480 minutos.")]
    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}

public class BarberFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<BarberWorkingHourDto> WorkingHours { get; set; } = new();
}

public class BarberWorkingHourDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "19:00";
    public string? LunchBreakStart { get; set; } = "12:00";
    public string? LunchBreakEnd { get; set; } = "13:00";
}

public class AdminClientViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool WhatsAppConsent { get; set; }
    public int TotalAppointments { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastVisit { get; set; }
}
