using BarberShop.Models.Enums;

namespace BarberShop.ViewModels.Client;

public class AppointmentItemViewModel
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? Notes { get; set; }
    public string? WhatsAppStatus { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReschedule { get; set; }
}

public class ClientDashboardViewModel
{
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public bool WhatsAppConsent { get; set; }
    public AppointmentItemViewModel? NextAppointment { get; set; }
    public List<AppointmentItemViewModel> History { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string? SelectedPeriod { get; set; }
}

public class RescheduleRequest
{
    public int AppointmentId { get; set; }
    public DateTime NewDate { get; set; }
    public string NewTimeSlot { get; set; } = string.Empty;
}
