using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Finance;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    // ---- Kategoriler ----

    [HttpGet("expense-categories")]
    [HasPermission("expense.read")]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategoryDto>>> ListCategories(CancellationToken ct)
        => Ok(await expenses.ListCategoriesAsync(ct));

    [HttpPost("expense-categories")]
    [HasPermission("expense.create")]
    public async Task<ActionResult<ExpenseCategoryDto>> CreateCategory(
        ExpenseCategoryUpsertRequest request, CancellationToken ct)
        => Ok(await expenses.CreateCategoryAsync(request, ct));

    [HttpPut("expense-categories/{id:long}")]
    [HasPermission("expense.update")]
    public async Task<ActionResult<ExpenseCategoryDto>> UpdateCategory(
        long id, ExpenseCategoryUpsertRequest request, CancellationToken ct)
        => Ok(await expenses.UpdateCategoryAsync(id, request, ct));

    /// <summary>Gideri olan kategori silinemez.</summary>
    [HttpDelete("expense-categories/{id:long}")]
    [HasPermission("expense.delete")]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken ct)
    {
        await expenses.DeleteCategoryAsync(id, ct);
        return NoContent();
    }

    // ---- Giderler ----

    [HttpGet("expenses")]
    [HasPermission("expense.read")]
    public async Task<ActionResult<PagedResult<ExpenseDto>>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? categoryId, [FromQuery] long? clinicId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await expenses.ListAsync(new ExpenseListQuery(from, to, categoryId, clinicId, page, pageSize), ct));

    [HttpGet("expenses/{id:long}")]
    [HasPermission("expense.read")]
    public async Task<ActionResult<ExpenseDto>> Get(long id, CancellationToken ct)
        => Ok(await expenses.GetAsync(id, ct));

    [HttpPost("expenses")]
    [HasPermission("expense.create")]
    public async Task<ActionResult<ExpenseDto>> Create(ExpenseUpsertRequest request, CancellationToken ct)
    {
        var dto = await expenses.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("expenses/{id:long}")]
    [HasPermission("expense.update")]
    public async Task<ActionResult<ExpenseDto>> Update(long id, ExpenseUpsertRequest request, CancellationToken ct)
        => Ok(await expenses.UpdateAsync(id, request, ct));

    [HttpDelete("expenses/{id:long}")]
    [HasPermission("expense.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await expenses.DeleteAsync(id, ct);
        return NoContent();
    }
}
