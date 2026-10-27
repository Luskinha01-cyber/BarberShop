using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BarberShop.Models.Enums;

namespace BarberShop.Models;

public class Appointment
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public virtual ApplicationUser Client { get; set; } = null!;

    public int BarberId { get; set; }
    public virtual Barber Barber { get; set; } = null!;

    public int ServiceId { get; set; }
    public virtual Service Service { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int DurationMinutes { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountPaid { get; set; }

    public DateTime? PaidAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    public virtual ICollection<WhatsAppNotification> WhatsAppNotifications { get; set; } = new List<WhatsAppNotification>();
}
