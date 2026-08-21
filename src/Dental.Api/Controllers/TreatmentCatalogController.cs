using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Treatments;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/treatment-categories")]
public sealed class TreatmentCategoriesController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<IReadOnlyList<TreatmentCategoryDto>>> List(CancellationToken ct)
        => Ok(await catalog.ListCategoriesAsync(ct));

    [HttpPost]
    [HasPermission("settings.update")]
    public async Task<ActionResult<TreatmentCategoryDto>> Create(TreatmentCategoryUpsertRequest request, CancellationToken ct)
        => Ok(await catalog.CreateCategoryAsync(request, ct));

    [HttpPut("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<TreatmentCategoryDto>> Update(long id, TreatmentCategoryUpsertRequest request, CancellationToken ct)
        => Ok(await catalog.UpdateCategoryAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await catalog.DeleteCategoryAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/treatment-catalog")]
public sealed class TreatmentCatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<PagedResult<TreatmentDefinitionDto>>> List(
        [FromQuery] string? search, [FromQuery] long? categoryId, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await catalog.ListDefinitionsAsync(new TreatmentCatalogQuery(search, categoryId, isActive, page, pageSize), ct));

    [HttpGet("{id:long}")]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<TreatmentDefinitionDto>> Get(long id, CancellationToken ct)
        => Ok(await catalog.GetDefinitionAsync(id, ct));

    [HttpPost]
    [HasPermission("settings.update")]
    public async Task<ActionResult<TreatmentDefinitionDto>> Create(TreatmentDefinitionUpsertRequest request, CancellationToken ct)
    {
        var dto = await catalog.CreateDefinitionAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<TreatmentDefinitionDto>> Update(long id, TreatmentDefinitionUpsertRequest request, CancellationToken ct)
        => Ok(await catalog.UpdateDefinitionAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await catalog.DeleteDefinitionAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/price-lists")]
public sealed class PriceListsController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<IReadOnlyList<PriceListDto>>> List(CancellationToken ct)
        => Ok(await catalog.ListPriceListsAsync(ct));

    [HttpPost]
    [HasPermission("settings.update")]
    public async Task<ActionResult<PriceListDto>> Create(PriceListUpsertRequest request, CancellationToken ct)
        => Ok(await catalog.CreatePriceListAsync(request, ct));

    [HttpPut("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<PriceListDto>> Update(long id, PriceListUpsertRequest request, CancellationToken ct)
        => Ok(await catalog.UpdatePriceListAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await catalog.DeletePriceListAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/items")]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<IReadOnlyList<PriceListItemDto>>> GetItems(long id, CancellationToken ct)
        => Ok(await catalog.GetPriceListItemsAsync(id, ct));

    /// <summary>Kalem toplu güncelleme (upsert; listede olmayan mevcut kalemler korunur).</summary>
    [HttpPut("{id:long}/items")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<IReadOnlyList<PriceListItemDto>>> SaveItems(
        long id, PriceListItemsSaveRequest request, CancellationToken ct)
        => Ok(await catalog.SavePriceListItemsAsync(id, request, ct));
}

[ApiController]
[Route("api/v1/icd-codes")]
public sealed class IcdCodesController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<IReadOnlyList<IcdCodeDto>>> Search([FromQuery] string? search, CancellationToken ct)
        => Ok(await catalog.SearchIcdCodesAsync(search, ct));
}
