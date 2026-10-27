using System.ComponentModel.DataAnnotations;

namespace BarberShop.Models;

public class BusinessSettings
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ShopName { get; set; } = "BarberShop";

    [Required]
    [MaxLength(30)]
    public string Phone { get; set; } = "+55 (11) 98765-4321";

    [Required]
    [MaxLength(250)]
    public string Address { get; set; } = "Av. Paulista, 1000 - Bela Vista, São Paulo - SP";

    public TimeSpan OpeningTime { get; set; } = new TimeSpan(9, 0, 0);

    public TimeSpan ClosingTime { get; set; } = new TimeSpan(19, 0, 0);

    public int SlotIntervalMinutes { get; set; } = 30;

    public int MinimumNoticeHours { get; set; } = 1;

    public int CancellationNoticeHours { get; set; } = 2;

    public int BufferMinutes { get; set; } = 0;

    public bool EnableWhatsAppNotifications { get; set; } = true;

    public bool EnableWhatsAppSimulation { get; set; } = true;

    [MaxLength(100)]
    public string? MetaPhoneNumberId { get; set; }

    [MaxLength(500)]
    public string? MetaAccessToken { get; set; }

    [MaxLength(20)]
    public string MetaApiVersion { get; set; } = "v21.0";

    [MaxLength(100)]
    public string? MetaBusinessAccountId { get; set; }

    [MaxLength(100)]
    public string? WebhookVerifyToken { get; set; } = "BarberShopSecretToken2026";

    [MaxLength(30)]
    public string WhatsAppProvider { get; set; } = "EvolutionApi";

    [MaxLength(250)]
    public string? EvolutionApiUrl { get; set; } = "http://localhost:8080";

    [MaxLength(200)]
    public string? EvolutionApiKey { get; set; } = "BarberShopEvolutionKey2026";

    [MaxLength(100)]
    public string? EvolutionInstanceName { get; set; } = "barbershop";
}
