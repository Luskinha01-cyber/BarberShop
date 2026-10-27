using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BarberShop.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public bool WhatsAppConsent { get; set; }

    public DateTime? WhatsAppConsentDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
