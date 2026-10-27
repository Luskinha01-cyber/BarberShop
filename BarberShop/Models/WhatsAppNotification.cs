using System.ComponentModel.DataAnnotations;
using BarberShop.Models.Enums;

namespace BarberShop.Models;

public class WhatsAppNotification
{
    public int Id { get; set; }

    public int? AppointmentId { get; set; }
    public virtual Appointment? Appointment { get; set; }

    [Required]
    [MaxLength(30)]
    public string RecipientPhone { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    public NotificationStatus Status { get; set; } = NotificationStatus.Simulated;

    [MaxLength(100)]
    public string? MetaMessageId { get; set; }

    [MaxLength(1000)]
    public string? ErrorDescription { get; set; }

    public string? Payload { get; set; }

    public bool IsSimulated { get; set; } = true;
}
