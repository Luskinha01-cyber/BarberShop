using System.ComponentModel.DataAnnotations;

namespace BarberShop.ViewModels.Booking;

public class CreateBookingRequest
{
    [Required(ErrorMessage = "O serviço é obrigatório.")]
    public int ServiceId { get; set; }

    public int? BarberId { get; set; } // null = Qualquer profissional disponível

    [Required(ErrorMessage = "A data é obrigatória.")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "O horário é obrigatório.")]
    public string TimeSlot { get; set; } = string.Empty; // "14:30"

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class AvailableSlotDto
{
    public string Time { get; set; } = string.Empty; // "14:30"
    public string FormattedTime { get; set; } = string.Empty; // "14:30"
    public string EndTime { get; set; } = string.Empty; // "15:00"
    public bool IsAvailable { get; set; }
    public int? AssignedBarberId { get; set; }
    public string? AssignedBarberName { get; set; }
}

public class BookingWizardViewModel
{
    public IEnumerable<BarberShop.Models.Service> Services { get; set; } = new List<BarberShop.Models.Service>();
    public IEnumerable<BarberShop.Models.Barber> Barbers { get; set; } = new List<BarberShop.Models.Barber>();
    public BarberShop.Models.BusinessSettings Settings { get; set; } = new();
    public int? PreSelectedServiceId { get; set; }
}

public class BookingConfirmationViewModel
{
    public int AppointmentId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Presencial";
    public bool WhatsAppConsent { get; set; }
    public bool WhatsAppNotificationSimulated { get; set; }
    public string WhatsAppStatusDescription { get; set; } = string.Empty;
}
