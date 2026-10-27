using System.ComponentModel.DataAnnotations;

namespace BarberShop.Models;

public class Barber
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<BarberWorkingHour> WorkingHours { get; set; } = new List<BarberWorkingHour>();
    public virtual ICollection<BarberTimeOff> TimeOffs { get; set; } = new List<BarberTimeOff>();
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
