using Dental.Api.Auth;
using Dental.Application.Clinical;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AnamnesisController(IAnamnesisService anamnesis) : ControllerBase
{
    // ---- Şablonlar ----

    [HttpGet("anamnesis-templates")]
    [HasPermission("anamnesis.read")]
    public async Task<ActionResult<IReadOnlyList<AnamnesisTemplateListItemDto>>> ListTemplates(CancellationToken ct)
        => Ok(await anamnesis.ListTemplatesAsync(ct));

    [HttpGet("anamnesis-templates/{id:long}")]
    [HasPermission("anamnesis.read")]
    public async Task<ActionResult<AnamnesisTemplateDto>> GetTemplate(long id, CancellationToken ct)
        => Ok(await anamnesis.GetTemplateAsync(id, ct));

    [HttpPost("anamnesis-templates")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<AnamnesisTemplateDto>> CreateTemplate(
        AnamnesisTemplateUpsertRequest request, CancellationToken ct)
    {
        var dto = await anamnesis.CreateTemplateAsync(request, ct);
        return CreatedAtAction(nameof(GetTemplate), new { id = dto.Id }, dto);
    }

    [HttpPut("anamnesis-templates/{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<AnamnesisTemplateDto>> UpdateTemplate(
        long id, AnamnesisTemplateUpsertRequest request, CancellationToken ct)
        => Ok(await anamnesis.UpdateTemplateAsync(id, request, ct));

    /// <summary>Doldurulmuş yanıtı olan şablon silinemez.</summary>
    [HttpDelete("anamnesis-templates/{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> DeleteTemplate(long id, CancellationToken ct)
    {
        await anamnesis.DeleteTemplateAsync(id, ct);
        return NoContent();
    }

    // ---- Hasta yanıtları ----

    [HttpGet("patients/{id:long}/anamnesis")]
    [HasPermission("anamnesis.read")]
    public async Task<ActionResult<IReadOnlyList<AnamnesisResponseDto>>> ListResponses(long id, CancellationToken ct)
        => Ok(await anamnesis.ListResponsesAsync(id, ct));

    /// <summary>Versiyonlu doldurma: her çağrı yeni yanıt seti oluşturur, eskiler korunur.</summary>
    [HttpPost("patients/{id:long}/anamnesis")]
    [HasPermission("anamnesis.create")]
    public async Task<ActionResult<AnamnesisResponseDto>> Fill(
        long id, AnamnesisFillRequest request, CancellationToken ct)
        => Ok(await anamnesis.FillAsync(id, request, ct));

    /// <summary>Hasta başlığı kırmızı rozet verisi: son doldurmadaki olumlu kritik yanıtlar.</summary>
    [HttpGet("patients/{id:long}/critical-flags")]
    [HasPermission("patient.read")]
    public async Task<ActionResult<IReadOnlyList<CriticalFlagDto>>> CriticalFlags(long id, CancellationToken ct)
        => Ok(await anamnesis.GetCriticalFlagsAsync(id, ct));
}
