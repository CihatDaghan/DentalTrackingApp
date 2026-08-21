using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Finance;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Finance;

/// <summary>Gider kategorileri + giderler. Kasa günlük özetine ExpenseDate üzerinden girer.</summary>
public sealed class ExpenseService(
    AppDbContext db,
    ITenantContext tenant,
    IValidator<ExpenseCategoryUpsertRequest> categoryValidator,
    IValidator<ExpenseUpsertRequest> expenseValidator) : IExpenseService
{
    // ---- Kategoriler ----

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        await db.ExpenseCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(
        ExpenseCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        var name = request.Name.Trim();
        if (await db.ExpenseCategories.AnyAsync(c => c.Name == name, ct))
            throw new InvalidOperationException($"'{name}' adında bir gider kategorisi zaten var.");

        var category = new ExpenseCategory { Name = name };
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return new ExpenseCategoryDto(category.Id, category.Name);
    }

    public async Task<ExpenseCategoryDto> UpdateCategoryAsync(
        long id, ExpenseCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Gider kategorisi bulunamadı.");
        var name = request.Name.Trim();
        if (await db.ExpenseCategories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            throw new InvalidOperationException($"'{name}' adında bir gider kategorisi zaten var.");

        category.Name = name;
        await db.SaveChangesAsync(ct);
        return new ExpenseCategoryDto(category.Id, category.Name);
    }

    public async Task DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Gider kategorisi bulunamadı.");
        if (await db.Expenses.AnyAsync(e => e.CategoryId == id, ct))
            throw new InvalidOperationException("Gideri olan kategori silinemez.");

        db.ExpenseCategories.Remove(category); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    // ---- Giderler ----

    public async Task<ExpenseDto> CreateAsync(ExpenseUpsertRequest request, CancellationToken ct = default)
    {
        await expenseValidator.ValidateAndThrowAsync(request, ct);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        var expense = new Expense
        {
            ClinicId = await ResolveClinicIdAsync(request.ClinicId, ct),
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            Method = request.Method,
            Description = request.Description,
            PaidByUserId = request.PaidByUserId ?? tenant.UserId,
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return await GetAsync(expense.Id, ct);
    }

    public async Task<ExpenseDto> UpdateAsync(long id, ExpenseUpsertRequest request, CancellationToken ct = default)
    {
        await expenseValidator.ValidateAndThrowAsync(request, ct);
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Gider bulunamadı.");
        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        expense.CategoryId = request.CategoryId;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.Method = request.Method;
        expense.Description = request.Description;
        if (request.ClinicId is { } clinicId) expense.ClinicId = clinicId;
        if (request.PaidByUserId is { } paidBy) expense.PaidByUserId = paidBy;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ExpenseDto> GetAsync(long id, CancellationToken ct = default) =>
        await Project(db.Expenses.AsNoTracking().Where(e => e.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Gider bulunamadı.");

    public async Task<PagedResult<ExpenseDto>> ListAsync(ExpenseListQuery query, CancellationToken ct = default)
    {
        var q = db.Expenses.AsNoTracking().AsQueryable();
        if (query.From is { } from) q = q.Where(e => e.ExpenseDate >= from);
        if (query.To is { } to) q = q.Where(e => e.ExpenseDate <= to);
        if (query.CategoryId is { } categoryId) q = q.Where(e => e.CategoryId == categoryId);
        if (query.ClinicId is { } clinicId) q = q.Where(e => e.ClinicId == clinicId);

        var page = new PageRequest(query.Page, query.PageSize);
        var totalCount = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id)
            .Skip(page.Skip).Take(page.EffectivePageSize)).ToListAsync(ct);
        return new PagedResult<ExpenseDto>(items, page.Page, page.EffectivePageSize, totalCount);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Gider bulunamadı.");
        db.Expenses.Remove(expense); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    // ---- Yardımcılar ----

    private async Task EnsureCategoryExistsAsync(long categoryId, CancellationToken ct)
    {
        if (!await db.ExpenseCategories.AnyAsync(c => c.Id == categoryId, ct))
            throw new KeyNotFoundException("Gider kategorisi bulunamadı.");
    }

    private async Task<long> ResolveClinicIdAsync(long? requestClinicId, CancellationToken ct)
    {
        if (requestClinicId is { } explicitClinic) return explicitClinic;
        if (tenant.ClinicId is { } contextClinic) return contextClinic;
        return await db.Clinics.OrderBy(c => c.Id).Select(c => (long?)c.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Kiracının kliniği yok; gider için klinik gereklidir.");
    }

    private IQueryable<ExpenseDto> Project(IQueryable<Expense> source) =>
        from e in source
        join u in db.Users on e.PaidByUserId equals u.Id into uj
        from u in uj.DefaultIfEmpty()
        select new ExpenseDto(
            e.Id, e.ClinicId, e.CategoryId, e.Category!.Name, e.Amount, e.ExpenseDate,
            e.Method, e.Description, e.PaidByUserId,
            u != null ? u.FirstName + " " + u.LastName : null);
}
