using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Finance;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>Kurum/sigorta carisi. Okuma payment.read, yazma settings.update iznine bağlı.</summary>
[ApiController]
[Route("api/v1/companies")]
public sealed class CompaniesController(ICompanyService companies) : ControllerBase
{
    [HttpGet]
    [HasPermission("payment.read")]
    public async Task<ActionResult<PagedResult<CompanyDto>>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await companies.ListAsync(search, page, pageSize, ct));

    [HttpGet("{id:long}")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<CompanyDto>> Get(long id, CancellationToken ct)
        => Ok(await companies.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("settings.update")]
    public async Task<ActionResult<CompanyDto>> Create(CompanyUpsertRequest request, CancellationToken ct)
    {
        var dto = await companies.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<CompanyDto>> Update(long id, CompanyUpsertRequest request, CancellationToken ct)
        => Ok(await companies.UpdateAsync(id, request, ct));

    /// <summary>Cari hareketi veya bağlı hastası olan kurum silinemez.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await companies.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Kurum cari ekstresi (koşan bakiye + özet).</summary>
    [HttpGet("{id:long}/ledger")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<LedgerStatementDto>> GetLedger(
        long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await companies.GetStatementAsync(id, from, to, ct));
}
