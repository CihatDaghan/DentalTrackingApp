using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Kiracı. ITenantOwned DEĞİLDİR (kendisi kiracının ta kendisidir) ama I aşamasından beri
/// <see cref="ISoftDelete"/> uygular: süper admin kiracıyı kapattığında kayıt saklama
/// yükümlülüğü nedeniyle veri silinmez, yalnız erişim kapanır.
/// </summary>
public class Tenant : BaseEntity, ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedByUserId { get; set; }

    public required string Name { get; set; }
    public string? TaxOffice { get; set; }
    /// <summary>VKN (10 hane) veya şahıs hekimde TCKN (11 hane).</summary>
    public string? TaxNumber { get; set; }
    public TenantLegalType LegalType { get; set; } = TenantLegalType.Company;
    /// <summary>KDVK 13/l (334) istisnası ancak bu bayrak açıkken uygulanabilir.</summary>
    public bool HasHealthTourismAuthorization { get; set; }
    public string DefaultLocale { get; set; } = "tr";
    public TenantStatus Status { get; set; } = TenantStatus.Trial;
    public DateTime? TrialEndsAtUtc { get; set; }
    public string? PlanCode { get; set; }

    public ICollection<Clinic> Clinics { get; set; } = [];
}
