using Dental.Api.Auth;
using Dental.Application.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>Kiracının mesaj şablonları (SMS/WhatsApp/e-posta gövdeleri, yer tutuculu).</summary>
[ApiController]
[Route("api/v1/message-templates")]
public sealed class MessageTemplatesController(IMessageTemplateService templates) : ControllerBase
{
    [HttpGet]
    [HasPermission("messaging.read")]
    public async Task<ActionResult<IReadOnlyList<MessageTemplateDto>>> List(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await templates.ListAsync(includeInactive, ct));

    [HttpGet("{id:long}")]
    [HasPermission("messaging.read")]
    public async Task<ActionResult<MessageTemplateDto>> Get(long id, CancellationToken ct)
        => Ok(await templates.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("messaging.templates")]
    public async Task<ActionResult<MessageTemplateDto>> Create(
        MessageTemplateUpsertRequest request, CancellationToken ct)
    {
        var dto = await templates.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("messaging.templates")]
    public async Task<ActionResult<MessageTemplateDto>> Update(
        long id, MessageTemplateUpsertRequest request, CancellationToken ct)
        => Ok(await templates.UpdateAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("messaging.templates")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await templates.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>
/// Meta'ya sunulan WhatsApp şablon kayıtları. Gönderim yalnız MetaStatus=Approved
/// şablonla yapılır; onaysızsa mesaj kanal politikasına göre SMS'e düşer.
/// </summary>
[ApiController]
[Route("api/v1/whatsapp-templates")]
public sealed class WhatsAppTemplatesController(IMessageTemplateService templates) : ControllerBase
{
    [HttpGet]
    [HasPermission("messaging.read")]
    public async Task<ActionResult<IReadOnlyList<WhatsAppTemplateDto>>> List(CancellationToken ct)
        => Ok(await templates.ListWhatsAppAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission("messaging.read")]
    public async Task<ActionResult<WhatsAppTemplateDto>> Get(long id, CancellationToken ct)
        => Ok(await templates.GetWhatsAppAsync(id, ct));

    [HttpPost]
    [HasPermission("messaging.templates")]
    public async Task<ActionResult<WhatsAppTemplateDto>> Create(
        WhatsAppTemplateUpsertRequest request, CancellationToken ct)
    {
        var dto = await templates.CreateWhatsAppAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("messaging.templates")]
    public async Task<ActionResult<WhatsAppTemplateDto>> Update(
        long id, WhatsAppTemplateUpsertRequest request, CancellationToken ct)
        => Ok(await templates.UpdateWhatsAppAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("messaging.templates")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await templates.DeleteWhatsAppAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Otomatik gönderim kuralları (randevu hatırlatma, doğum günü, ödeme, kontrol).</summary>
[ApiController]
[Route("api/v1/automation-rules")]
public sealed class AutomationRulesController(IAutomationRuleService rules) : ControllerBase
{
    [HttpGet]
    [HasPermission("settings.view")]
    public async Task<ActionResult<IReadOnlyList<AutomationRuleDto>>> List(CancellationToken ct)
        => Ok(await rules.ListAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission("settings.view")]
    public async Task<ActionResult<AutomationRuleDto>> Get(long id, CancellationToken ct)
        => Ok(await rules.GetAsync(id, ct));

    /// <summary>Kural türü kiracıda tekildir: aynı tür varsa günceller, yoksa oluşturur.</summary>
    [HttpPost]
    [HasPermission("settings.update")]
    public async Task<ActionResult<AutomationRuleDto>> Upsert(
        AutomationRuleUpsertRequest request, CancellationToken ct)
    {
        var dto = await rules.UpsertAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<AutomationRuleDto>> Update(
        long id, AutomationRuleUpsertRequest request, CancellationToken ct)
        => Ok(await rules.UpdateAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await rules.DeleteAsync(id, ct);
        return NoContent();
    }
}
