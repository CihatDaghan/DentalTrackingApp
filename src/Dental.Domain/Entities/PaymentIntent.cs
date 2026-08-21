using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Sanal POS ödeme linki niyeti. Akış: kayıt → sağlayıcıda checkout → link SMS/WA ile hastaya →
/// hasta öder → callback → SUNUCUDAN yeniden doğrulama → tahsilat (Payment) kaydı.
///
/// İdempotanlık: <see cref="ProviderPaymentId"/> filtered UNIQUE'tir; aynı ödeme ikinci kez
/// callback ettiğinde ikinci tahsilat oluşmaz (callback'ler tekrarlanabilir).
/// </summary>
public class PaymentIntent : TenantEntity
{
    public long PatientId { get; set; }
    public long ClinicId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string? Description { get; set; }
    /// <summary>Sağlayıcıya gönderilen eşleşme anahtarı = Id'nin string hali (kayıt sonrası yazılır).</summary>
    public string? ConversationId { get; set; }
    /// <summary>
    /// Public ödeme sayfası ve callback eşleşmesi için bizim token'ımız; sağlayıcının
    /// token'ından ayrıdır ve hastaya giden linkte yalnız bu görünür.
    /// </summary>
    public Guid PublicToken { get; set; }
    public string? ProviderKey { get; set; }
    /// <summary>Sağlayıcının checkout token'ı; doğrulama (VerifyPaymentAsync) bununla yapılır.</summary>
    public string? ProviderToken { get; set; }
    /// <summary>Sağlayıcının hosted ödeme sayfası adresi.</summary>
    public string? LinkUrl { get; set; }
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Created;
    public decimal? PaidAmount { get; set; }
    /// <summary>Sağlayıcının ödeme kimliği — idempotanlık anahtarı (filtered UNIQUE).</summary>
    public string? ProviderPaymentId { get; set; }
    /// <summary>Doğrulama sonrası oluşan tahsilat kaydı.</summary>
    public long? PaymentId { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    /// <summary>Sağlayıcı doğrulama yanıtının ham JSON'u (mutabakat/uyuşmazlık kanıtı).</summary>
    public string? RawResponseJson { get; set; }
    /// <summary>
    /// Linki oluşturan kullanıcı. Callback anonim geldiği için tahsilatın "alan kullanıcısı"
    /// olarak bu kimlik kullanılır (yoksa kiracı sahibine düşülür).
    /// </summary>
    public long? CreatedByUserId { get; set; }
}
