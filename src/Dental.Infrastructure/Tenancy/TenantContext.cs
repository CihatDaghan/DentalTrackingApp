using Dental.Application.Abstractions;

namespace Dental.Infrastructure.Tenancy;

/// <summary>Scoped yaşam süresi: her HTTP isteği ya da her ITenantScope için bir örnek.</summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    public long? TenantId { get; private set; }
    public long? ClinicId { get; private set; }
    public long? UserId { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public bool IsResolved { get; private set; }

    public void Set(long? tenantId, long? clinicId, long? userId, bool isSuperAdmin)
    {
        TenantId = tenantId;
        ClinicId = clinicId;
        UserId = userId;
        IsSuperAdmin = isSuperAdmin;
        IsResolved = true;
    }
}
