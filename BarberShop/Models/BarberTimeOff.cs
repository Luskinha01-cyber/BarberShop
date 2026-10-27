using System.ComponentModel.DataAnnotations;
using BarberShop.Models.Enums;

namespace BarberShop.Models;

public class BarberTimeOff
{
    public int Id { get; set; }

    public int? BarberId { get; set; }
    public virtual Barber? Barber { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;

    public TimeOffType Type { get; set; } = TimeOffType.TimeOff;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
