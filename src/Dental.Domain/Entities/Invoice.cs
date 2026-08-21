using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// e-Belge başlığı (e-Fatura / e-Arşiv / e-SMM). Alıcı bilgileri belge kesildiği andaki haliyle
/// snapshot olarak saklanır — hasta kartı sonradan değişse bile yasal belge içeriği sabit kalır.
/// (c)-1: InvoiceNumber ve Ettn Draft'ta NULL'dır; ikisi de Draft→UblGenerated geçişinde atanır.
/// </summary>
public class Invoice : TenantEntity
{
    public long ClinicId { get; set; }

    public InvoiceDocumentKind DocumentKind { get; set; }

    /// <summary>TEMELFATURA / TICARIFATURA / EARSIVFATURA / (e-SMM: EARSIVBELGE).</summary>
    public string? ProfileId { get; set; }

    /// <summary>SATIS / IADE / TEVKIFAT / ISTISNA. e-SMM'de belgede yazılmaz, kayıtta tutulur.</summary>
    public required string TypeCode { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>16 hane: 3 harf seri + 4 hane yıl + 9 hane sıra (DIS2026000000042).</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Seri kodu (DIS/SMM).</summary>
    public string? Serial { get; set; }

    /// <summary>Evrensel tekil belge numarası (UUID v4).</summary>
    public Guid? Ettn { get; set; }

    public DateOnly IssueDate { get; set; }

    /// <summary>e-Arşiv/e-SMM'de zorunlu (TR yerel saati).</summary>
    public TimeOnly? IssueTime { get; set; }

    public InvoiceCustomerType CustomerType { get; set; }
    public long? PatientId { get; set; }
    public long? CompanyId { get; set; }

    // ---- Alıcı snapshot ----
    public required string BuyerName { get; set; }
    /// <summary>TCKN (11) / VKN (10); yabancı hastada "2222222222".</summary>
    public string? BuyerTcknVkn { get; set; }
    public string? BuyerPassportNo { get; set; }
    /// <summary>Uyruk (alfa-3 SKRS kodu).</summary>
    public string? BuyerNationality { get; set; }
    public DateOnly? BuyerLastEntryDate { get; set; }
    public string? BuyerAddress { get; set; }
    /// <summary>UBL PostalAddress/CityName zorunlu olduğu için ayrı snapshot kolonu.</summary>
    public string? BuyerCity { get; set; }
    /// <summary>UBL PostalAddress/CitySubdivisionName (ilçe).</summary>
    public string? BuyerDistrict { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerTaxOffice { get; set; }
    /// <summary>e-Faturada alıcının GİB posta kutusu etiketi.</summary>
    public string? BuyerAlias { get; set; }

    public string CurrencyCode { get; set; } = "TRY";
    public decimal ExchangeRate { get; set; } = 1m;

    // ---- Tutarlar ----
    /// <summary>İskonto sonrası, KDV hariç satır toplamı (LineExtensionAmount).</summary>
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    /// <summary>Tevkif edilen KDV.</summary>
    public decimal WithholdingTotal { get; set; }
    /// <summary>e-SMM gelir vergisi stopajı (%20, yalnız mükellef alıcı).</summary>
    public decimal GvStopajTotal { get; set; }
    /// <summary>Ödenecek tutar = KDV dahil − tevkifat − stopaj.</summary>
    public decimal PayableAmount { get; set; }

    /// <summary>KDV istisna kodu (sağlık turizmi: 334).</summary>
    public string? ExemptionCode { get; set; }
    public string? ExemptionReason { get; set; }
    /// <summary>Tevkifat kodu (kamu diğer hizmetler: 616).</summary>
    public string? WithholdingCode { get; set; }

    // ---- Entegratör ----
    public IntegratorProvider? IntegratorProvider { get; set; }
    /// <summary>Entegratörün belge kimliği (Uyumsoft InvoiceIdentity.Id).</summary>
    public string? IntegratorRefId { get; set; }
    public DateTime? LastStatusCheckUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Üretilen UBL XML dosyası (MediaCategory.InvoiceUbl).</summary>
    public long? UblFileId { get; set; }
    /// <summary>Entegratörden çekilen PDF (MediaCategory.InvoicePdf).</summary>
    public long? PdfFileId { get; set; }

    /// <summary>IADE belgesinde kaynak fatura (BillingReference).</summary>
    public long? SourceInvoiceId { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = [];
}

/// <summary>Fatura satırı; tedavi kaydından üretildiğinde TreatmentRecordId doludur.</summary>
public class InvoiceLine : TenantEntity
{
    public long InvoiceId { get; set; }
    public int SeqNo { get; set; }
    public long? TreatmentRecordId { get; set; }
    public required string ItemName { get; set; }
    public decimal Quantity { get; set; } = 1m;
    /// <summary>UN/ECE birim kodu; hizmette C62 (adet).</summary>
    public string UnitCode { get; set; } = "C62";
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    /// <summary>İskonto sonrası, KDV hariç satır tutarı.</summary>
    public decimal LineTotal { get; set; }
    /// <summary>(c)-4: estetik işlem (KDV %20) — 334 istisnasıyla birleşemez.</summary>
    public bool IsAesthetic { get; set; }

    public Invoice? Invoice { get; set; }
}

/// <summary>Durum geçiş izi; ActorUserId NULL ise geçişi sistem/job yapmıştır.</summary>
public class InvoiceStatusLog : TenantEntity
{
    public long InvoiceId { get; set; }
    public InvoiceStatus? FromStatus { get; set; }
    public InvoiceStatus ToStatus { get; set; }
    public DateTime AtUtc { get; set; }
    public long? ActorUserId { get; set; }
    /// <summary>Entegratör ham yanıtı / hata metni (yasal iz).</summary>
    public string? IntegratorRawResponse { get; set; }
}

/// <summary>
/// Atomik numara sayacı. Artırım HER ZAMAN tek ifadelik
/// <c>UPDATE ... SET LastValue = LastValue + 1 OUTPUT INSERTED.LastValue</c> ile yapılır;
/// oku-artır-yaz deseni paralel istekte aynı numarayı üretebileceği için yasaktır.
/// </summary>
public class NumberSequence : TenantEntity
{
    public NumberSequenceType SequenceType { get; set; }
    /// <summary>3 harf seri kodu (DIS/SMM).</summary>
    public required string Serial { get; set; }
    public int Year { get; set; }
    public long LastValue { get; set; }
}

/// <summary>
/// GİB e-fatura mükellef aynası — kiracıdan bağımsız global cache.
/// Günlük job entegratörden zip listeyi indirip upsert eder; fatura anında lokalden bakılır.
/// </summary>
public class GibTaxpayer
{
    /// <summary>VKN (10) veya TCKN (11) — birincil anahtar.</summary>
    public required string Vkn { get; set; }
    public string? Title { get; set; }
    /// <summary>Posta kutusu etiketi (urn:mail:defaultpk@...).</summary>
    public string? Alias { get; set; }
    /// <summary>Etiket tipi: PK (gönderici) / GB (alıcı posta kutusu).</summary>
    public string? AccountType { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSyncUtc { get; set; }
}

/// <summary>
/// Vergi kod listeleri (KDV oranları, istisna/tevkifat kodları, birim kodları) — global,
/// hardcode edilmez. Süper admin ekranından güncellenebilir; ValidFrom/ValidTo ile versiyonlanır.
/// </summary>
public class TaxConfig : BaseEntity
{
    public TaxConfigType ConfigType { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
    /// <summary>Yüzde değeri (KDV %10 → 10; tevkifat 5/10 → 50). Kod listelerinde NULL.</summary>
    public decimal? Rate { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}
