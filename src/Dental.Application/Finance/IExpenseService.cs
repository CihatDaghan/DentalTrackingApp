using Dental.Application.Common;

namespace Dental.Application.Finance;

/// <summary>Gider kategorileri + giderler (tarih/kategori filtreli liste).</summary>
public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryUpsertRequest request, CancellationToken ct = default);
    Task<ExpenseCategoryDto> UpdateCategoryAsync(long id, ExpenseCategoryUpsertRequest request, CancellationToken ct = default);
    /// <summary>Gideri olan kategori silinemez.</summary>
    Task DeleteCategoryAsync(long id, CancellationToken ct = default);

    Task<ExpenseDto> CreateAsync(ExpenseUpsertRequest request, CancellationToken ct = default);
    Task<ExpenseDto> UpdateAsync(long id, ExpenseUpsertRequest request, CancellationToken ct = default);
    Task<ExpenseDto> GetAsync(long id, CancellationToken ct = default);
    Task<PagedResult<ExpenseDto>> ListAsync(ExpenseListQuery query, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
