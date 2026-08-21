using Dental.Domain.Common;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Dental.Domain.Entities;

/// <summary>
/// Uygulama kullanıcısı. TenantId NULL ise platform (süper admin) kullanıcısıdır.
/// Hekim = UserType.Dentist olan kullanıcı; ayrı Doctor tablosu yoktur.
/// </summary>
public class AppUser : IdentityUser<long>
{
    public long? TenantId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public UserType UserType { get; set; } = UserType.Secretary;
    /// <summary>Deterministik değil — uygulama katmanında şifrelenmiş TCKN (Data Protection).</summary>
    public string? TcknEncrypted { get; set; }
    /// <summary>Eşitlik araması için anahtarlı HMAC-SHA256 özeti (hex).</summary>
    public string? TcknHash { get; set; }
    public string? DiplomaNo { get; set; }
    public string Locale { get; set; } = "tr";
    public bool IsActive { get; set; } = true;
    /// <summary>Takvimde hekim rengi (#rrggbb).</summary>
    public string? Color { get; set; }
    /// <summary>Hekim branşı (Ortodonti, Cerrahi...).</summary>
    public string? Branch { get; set; }
    /// <summary>Reçete/epikriz baskısında kullanılan imza görseli.</summary>
    public long? SignatureImageFileId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<UserClinic> Clinics { get; set; } = [];
    public ICollection<UserRole> Roles { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";
}

public class UserClinic
{
    public long UserId { get; set; }
    public long ClinicId { get; set; }
    public bool IsDefault { get; set; }

    public AppUser? User { get; set; }
    public Clinic? Clinic { get; set; }
}
