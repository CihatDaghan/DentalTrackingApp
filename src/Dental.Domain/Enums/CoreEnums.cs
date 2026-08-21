namespace Dental.Domain.Enums;

public enum TenantLegalType : byte
{
    /// <summary>Şahıs diş hekimi (muayenehane) — e-SMM keser.</summary>
    SoleProprietor = 1,
    /// <summary>Şirket klinik/poliklinik — e-Fatura / e-Arşiv keser.</summary>
    Company = 2,
}

public enum TenantStatus : byte
{
    Trial = 1,
    Active = 2,
    Suspended = 3,
}

public enum UserType : byte
{
    Owner = 1,
    Manager = 2,
    Dentist = 3,
    Assistant = 4,
    Secretary = 5,
    Accountant = 6,
}

/// <summary>Platform duyurusunun görsel şiddeti (banner rengi).</summary>
public enum AnnouncementSeverity : byte
{
    Info = 1,
    Warning = 2,
}

public enum AuditActionType : byte
{
    Create = 1,
    Update = 2,
    SoftDelete = 3,
    Login = 4,
    LoginFailed = 5,
    Export = 6,
    PermissionChange = 7,
    Impersonation = 8,
}
