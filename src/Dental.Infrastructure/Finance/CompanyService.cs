using Dental.Application.Common;
using Dental.Application.Finance;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Finance;

/// <summary>Kurum/sigorta carisi CRUD + ekstre. Balance yalnız LedgerService'ten güncellenir.</summary>
public sealed class CompanyService(
    AppDbContext db,
    ILedgerService ledger,
    IValidator<CompanyUpsertRequest> upsertValidator) : ICompanyService
{
    public async Task<CompanyDto> CreateAsync(CompanyUpsertRequest request, CancellationToken ct = default)
    {
        await upsertValidator.ValidateAndThrowAsync(request, ct);
        await EnsurePriceListExistsAsync(request.PriceListId, ct);

        var company = new Company { Name = request.Name.Trim() };
        Apply(company, request);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<CompanyDto> UpdateAsync(long id, CompanyUpsertRequest request, CancellationToken ct = default)
    {
        await upsertValidator.ValidateAndThrowAsync(request, ct);
        var company = await FindAsync(id, ct);
        await EnsurePriceListExistsAsync(request.PriceListId, ct);

        company.Name = request.Name.Trim();
        Apply(company, request);
        await db.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<CompanyDto> GetAsync(long id, CancellationToken ct = default) =>
        ToDto(await FindAsync(id, ct));

    public async Task<PagedResult<CompanyDto>> ListAsync(
        string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var q = db.Companies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c => c.Name.Contains(term) || (c.Vkn != null && c.Vkn.Contains(term)));
        }

        var pageRequest = new PageRequest(page, pageSize);
        var totalCount = await q.CountAsync(ct);
        var items = await q.OrderBy(c => c.Name)
            .Skip(pageRequest.Skip).Take(pageRequest.EffectivePageSize)
            .ToListAsync(ct);
        return new PagedResult<CompanyDto>([.. items.Select(ToDto)],
            pageRequest.Page, pageRequest.EffectivePageSize, totalCount);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var company = await FindAsync(id, ct);
        if (await db.LedgerEntries.AnyAsync(e => e.CompanyId == id, ct))
            throw new InvalidOperationException("Cari hareketi olan kurum silinemez.");
        if (await db.Patients.AnyAsync(p => p.CompanyId == id, ct))
            throw new InvalidOperationException("Bağlı hastası olan kurum silinemez; önce hastaların kurum bağını kaldırın.");

        db.Companies.Remove(company); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    public async Task<LedgerStatementDto> GetStatementAsync(
        long id, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        await ledger.GetStatementAsync(new LedgerStatementQuery(LedgerAccountType.Company, id, from, to), ct);

    // ---- Yardımcılar ----

    private async Task<Company> FindAsync(long id, CancellationToken ct) =>
        await db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Kurum bulunamadı.");

    private async Task EnsurePriceListExistsAsync(long? priceListId, CancellationToken ct)
    {
        if (priceListId is { } id && !await db.PriceLists.AnyAsync(p => p.Id == id, ct))
            throw new KeyNotFoundException("Fiyat listesi bulunamadı.");
    }

    private static void Apply(Company company, CompanyUpsertRequest request)
    {
        company.TaxOffice = request.TaxOffice?.Trim();
        company.Vkn = request.Vkn?.Trim();
        company.Address = request.Address?.Trim();
        company.Email = request.Email?.Trim();
        company.Phone = request.Phone?.Trim();
        company.PriceListId = request.PriceListId;
        company.IsEInvoiceUser = request.IsEInvoiceUser;
        company.EInvoiceAlias = request.EInvoiceAlias?.Trim();
    }

    private static CompanyDto ToDto(Company c) =>
        new(c.Id, c.Name, c.TaxOffice, c.Vkn, c.Address, c.Email, c.Phone,
            c.PriceListId, c.IsEInvoiceUser, c.EInvoiceAlias, c.Balance, c.CreatedAtUtc);
}
