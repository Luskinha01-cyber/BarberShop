using System.Text.Json;
using BarberShop.Data;
using BarberShop.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(ApplicationDbContext context, ILogger<WebhookController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Validação inicial do Webhook pela Meta
    [HttpGet("whatsapp")]
    public async Task<IActionResult> VerifyWhatsAppWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var settings = await _context.BusinessSettings.FirstOrDefaultAsync();
        var configuredToken = settings?.WebhookVerifyToken ?? "BarberShopSecretToken2026";

        if (mode == "subscribe" && verifyToken == configuredToken)
        {
            _logger.LogInformation("[META WEBHOOK] Validação de handshake aceita com sucesso.");
            return Ok(challenge);
        }

        _logger.LogWarning("[META WEBHOOK] Falha na validação do token do webhook. Recebido: {Token}", verifyToken);
        return Forbid();
    }

    // Recebimento de eventos e status da Meta Cloud API
    [HttpPost("whatsapp")]
    public async Task<IActionResult> ReceiveWhatsAppEvent([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("[META WEBHOOK] Evento recebido: {Payload}", payload.ToString());

            if (payload.TryGetProperty("entry", out var entries) && entries.GetArrayLength() > 0)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (entry.TryGetProperty("changes", out var changes))
                    {
                        foreach (var change in changes.EnumerateArray())
                        {
                            if (change.TryGetProperty("value", out var val) && val.TryGetProperty("statuses", out var statuses))
                            {
                                foreach (var statusItem in statuses.EnumerateArray())
                                {
                                    if (statusItem.TryGetProperty("id", out var idProp) &&
                                        statusItem.TryGetProperty("status", out var statusProp))
                                    {
                                        var metaId = idProp.GetString();
                                        var statusStr = statusProp.GetString();

                                        var notification = await _context.WhatsAppNotifications
                                            .FirstOrDefaultAsync(n => n.MetaMessageId == metaId);

                                        if (notification != null)
                                        {
                                            if (statusStr == "delivered" || statusStr == "read")
                                            {
                                                notification.Status = NotificationStatus.Delivered;
                                            }
                                            else if (statusStr == "failed")
                                            {
                                                notification.Status = NotificationStatus.Failed;
                                                notification.ErrorDescription = "Entrega rejeitada ou com falha informada pela Meta.";
                                            }

                                            await _context.SaveChangesAsync();
                                            _logger.LogInformation("[META WEBHOOK] Status da mensagem {Id} atualizado para {Status}", metaId, notification.Status);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[META WEBHOOK] Erro ao processar payload do webhook.");
            return Ok(); // Sempre responde 200 à Meta para evitar reenvio em loop
        }
    }
}
