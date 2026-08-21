using Dental.Domain.Enums;

namespace Dental.Application.Tenants;

public sealed record CreateTenantRequest(
    string ClinicName,
    TenantLegalType LegalType,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string AdminPassword,
    string? TaxNumber = null,
    string? Phone = null);

public sealed record CreateTenantResult(long TenantId, long ClinicId, long AdminUserId);

/// <summary>Yeni kiracı açılışı: Tenant + ilk Clinic + Owner kullanıcı + şablon verilerin kopyalanması.</summary>
public interface ITenantProvisioningService
{
    Task<CreateTenantResult> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
}
