namespace Dental.Domain.Entities;

/// <summary>Rol. TenantId NULL = sistem rolü (seed: Owner, Dentist, Assistant, Secretary, Accountant, SuperAdmin).</summary>
public class Role
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public required string Name { get; set; }
    /// <summary>Sistem rolleri silinemez/yeniden adlandırılamaz.</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = [];
}

/// <summary>Tenant-bağımsız sabit izin listesi ("patient.read", "payment.delete"...). Seed ile yüklenir.</summary>
public class Permission
{
    public long Id { get; set; }
    public required string Code { get; set; }
    public required string Module { get; set; }
}

public class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}

public class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }

    public AppUser? User { get; set; }
    public Role? Role { get; set; }
}

public class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public long? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }

    public AppUser? User { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
