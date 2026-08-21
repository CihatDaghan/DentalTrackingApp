using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>Tenant bazlı entegrasyon ayarları — kimlik bilgileri Data Protection ile şifreli JSON olarak saklanır.</summary>
public class TenantIntegrationSetting : TenantEntity
{
    /// <summary>'EInvoice' | 'Sms' | 'WhatsApp' | 'Payment' | 'Enabiz'</summary>
    public required string IntegrationKey { get; set; }
    /// <summary>'uyumsoft' | 'nes' | 'netgsm' | 'meta' | 'iyzico' | 'fake'</summary>
    public required string ProviderKey { get; set; }
    /// <summary>'Test' | 'Live'</summary>
    public string Environment { get; set; } = "Test";
    public string? SettingsJsonEncrypted { get; set; }
    public bool IsEnabled { get; set; }
    public long? UpdatedByUserId { get; set; }
}

/// <summary>Dış servis çağrılarının PII-maskeli özet logu (tam yükler kendi tablolarında).</summary>
public class IntegrationCallLog
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public required string IntegrationKey { get; set; }
    public required string ProviderKey { get; set; }
    public required string Operation { get; set; }
    public Guid CorrelationId { get; set; }
    public int? HttpStatus { get; set; }
    public int DurationMs { get; set; }
    public string? RequestSummary { get; set; }
    public string? ResponseSummary { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Uygulama içi bildirim (topbar zili). Mobil push bilinçli olarak kapsam dışıdır.</summary>
public class Notification : TenantEntity
{
    public long? UserId { get; set; }
    /// <summary>appointment_created / appointment_cancelled / payment_received / integration_error ...</summary>
    public required string EventType { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? LinkPath { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>
/// Abonelik planı — GLOBAL (kiracıya ait değil, bilerek <see cref="ITenantOwned"/> uygulamaz).
/// Kiracı <see cref="Tenant.PlanCode"/> ile buraya bağlanır; kota alanları raporlama/limit
/// göstergesidir, teknik zorlama süper admin panelinden yapılır.
/// </summary>
public class Plan : BaseEntity
{
    /// <summary>Benzersiz kod ('starter' | 'pro' | 'enterprise').</summary>
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int MaxUsers { get; set; }
    public int MaxPatients { get; set; }
    public int MonthlySmsQuota { get; set; }
    public int StorageGb { get; set; }
    public decimal PriceMonthly { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// Platform duyurusu — GLOBAL. <see cref="TargetTenantId"/> NULL ise tüm kiracılara,
/// dolu ise yalnız o kiracıya gösterilir. Aktiflik penceresi StartsAtUtc/EndsAtUtc ile sorgulanır.
/// </summary>
public class Announcement : BaseEntity
{
    public required string Title { get; set; }
    public required string Body { get; set; }
    public AnnouncementSeverity Severity { get; set; } = AnnouncementSeverity.Info;
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>NULL = tüm kiracılar.</summary>
    public long? TargetTenantId { get; set; }
}
