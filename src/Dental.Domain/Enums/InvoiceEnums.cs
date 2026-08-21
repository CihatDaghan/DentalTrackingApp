namespace Dental.Domain.Enums;

/// <summary>Üretilen e-belge türü (Dental.EDocument.Ubl.Models.DocumentKind karşılığı).</summary>
public enum InvoiceDocumentKind : byte
{
    /// <summary>e-Fatura — alıcı GİB mükellef aynasında kayıtlı; UBL Invoice.</summary>
    EFatura = 1,
    /// <summary>e-Arşiv fatura — kayıtsız/bireysel alıcı; UBL Invoice.</summary>
    EArsiv = 2,
    /// <summary>e-Serbest Meslek Makbuzu — şahıs hekim; UBL CreditNote.</summary>
    ESmm = 3,
}

/// <summary>
/// Fatura durum makinesi (design-0 §3.13 + F aşaması genişletmesi).
/// Draft→UblGenerated→Queued→SentToIntegrator→GibProcessing→Succeeded
/// | GibRejected | BuyerRejected | Error→ManualReview | Cancelled.
/// </summary>
public enum InvoiceStatus : byte
{
    Draft = 1,
    /// <summary>Numara + ETTN atandı, UBL üretildi ve MediaFile'a yazıldı.</summary>
    UblGenerated = 2,
    /// <summary>Gönderim kuyruğunda (EDocumentDispatchJob işleyecek).</summary>
    Queued = 3,
    /// <summary>Entegratör belgeyi kabul etti, referans numarası alındı.</summary>
    SentToIntegrator = 4,
    /// <summary>Entegratör GİB'e iletti; nihai yanıt bekleniyor.</summary>
    GibProcessing = 5,
    Succeeded = 6,
    GibRejected = 7,
    /// <summary>TİCARİ senaryoda alıcı reddi.</summary>
    BuyerRejected = 8,
    /// <summary>Geçici hata; NextAttemptAtUtc ile yeniden denenecek.</summary>
    Error = 9,
    /// <summary>6 denemeden sonra elle inceleme kuyruğu.</summary>
    ManualReview = 10,
    Cancelled = 11,
}

public enum InvoiceCustomerType : byte
{
    Patient = 1,
    Company = 2,
}

/// <summary>Belgeyi gönderen entegratör (TenantIntegrationSettings.ProviderKey ile eşleşir).</summary>
public enum IntegratorProvider : byte
{
    /// <summary>Entegratörsüz geliştirme/test sürücüsü.</summary>
    Fake = 1,
    Uyumsoft = 2,
    Nes = 3,
    TurkcellEsirket = 4,
    Izibiz = 5,
}

/// <summary>Atomik numara sayacının türü (NumberSequence.SequenceType).</summary>
public enum NumberSequenceType : byte
{
    InvoiceEFatura = 1,
    InvoiceEArsiv = 2,
    ESmm = 3,
    PatientFileNo = 4,
    ProtocolNo = 5,
}

/// <summary>Vergi kod listesi türü — oran/kodlar hardcode edilmez, TaxConfig'ten okunur.</summary>
public enum TaxConfigType : byte
{
    VatRate = 1,
    ExemptionCode = 2,
    WithholdingCode = 3,
    UnitCode = 4,
}
