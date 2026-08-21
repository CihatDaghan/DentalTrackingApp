using Dental.Domain.Enums;

namespace Dental.Application.Payments;

public sealed record PaymentLinkCreateRequest(
    long PatientId,
    decimal Amount,
    string? Description = null,
    /// <summary>Linkin hastaya hangi kanaldan gönderileceği; NULL ise kanal politikasından çözülür.</summary>
    MessageChannel? Channel = null,
    string CurrencyCode = "TRY",
    /// <summary>Linkin geçerlilik süresi (saat). Varsayılan 72 saat.</summary>
    int ExpiresInHours = 72);

public sealed record PaymentLinkDto(
    long Id,
    long PatientId,
    string PatientName,
    long ClinicId,
    decimal Amount,
    string CurrencyCode,
    string? Description,
    Guid PublicToken,
    string? ProviderKey,
    string? LinkUrl,
    PaymentIntentStatus Status,
    decimal? PaidAmount,
    string? ProviderPaymentId,
    long? PaymentId,
    DateTime? PaidAtUtc,
    DateTime? ExpiresAtUtc,
    long? MessageId,
    DateTime CreatedAtUtc);

/// <summary>Hastanın gördüğü public ödeme sayfası verisi — hasta adı dışında klinik verisi taşımaz.</summary>
public sealed record PublicPaymentViewDto(
    string ClinicName,
    string PatientName,
    decimal Amount,
    string CurrencyCode,
    string? Description,
    PaymentIntentStatus Status,
    string? PayUrl,
    DateTime? ExpiresAtUtc);

/// <summary>Public sayfanın kısa aralıklı yokladığı hafif durum yanıtı.</summary>
public sealed record PublicPaymentStatusDto(
    PaymentIntentStatus Status,
    decimal? PaidAmount,
    DateTime? PaidAtUtc);

/// <summary>Callback işleme sonucu; <paramref name="AlreadyProcessed"/> idempotent tekrar demektir.</summary>
public sealed record PaymentCallbackResult(
    long IntentId,
    Guid PublicToken,
    PaymentIntentStatus Status,
    long? PaymentId,
    bool AlreadyProcessed,
    string? Error = null);
