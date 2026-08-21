using Dental.Application.Common;

namespace Dental.Application.Finance;

/// <summary>Kurum/sigorta carisi CRUD + ekstre (LedgerService.GetStatement).</summary>
public interface ICompanyService
{
    Task<CompanyDto> CreateAsync(CompanyUpsertRequest request, CancellationToken ct = default);
    Task<CompanyDto> UpdateAsync(long id, CompanyUpsertRequest request, CancellationToken ct = default);
    Task<CompanyDto> GetAsync(long id, CancellationToken ct = default);
    Task<PagedResult<CompanyDto>> ListAsync(string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default);
    /// <summary>Cari hareketi veya bağlı hastası olan kurum silinemez.</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<LedgerStatementDto> GetStatementAsync(long id, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
}
