using Dental.Domain.Enums;

namespace Dental.Application.Invoices;

/// <summary>
/// Fatura taslağı isteği: alıcı (hasta VEYA kurum) + faturalanacak tedavi kayıtları + senaryo bayrakları.
/// Preview ve Create aynı isteği kullanır; preview yan etkisizdir.
/// </summary>
public sealed record InvoiceDraftRequest(
    long? PatientId,
    long? CompanyId,
    IReadOnlyList<long> TreatmentRecordIds,
    // Yabancı hasta (sağlık turizmi 334 senaryosu) — hasta kartındaki uyruk ile birlikte değerlendirilir.
    bool IsForeignPatient = false,
    bool IsRefund = false,
    long? SourceInvoiceId = null,
    // Kamu idaresi alıcı — KDV tevkifatı (616, 5/10) uygulanır.
    bool IsGovernmentBuyer = false,
    // Serbest kalemler (tedavi kaydı olmadan elle satır); tedavi kayıtlarına eklenir.
    IReadOnlyList<InvoiceManualLineRequest>? ManualLines = null);

public sealed record InvoiceManualLineRequest(
    string ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate = null,
    decimal DiscountAmount = 0m,
    bool IsAesthetic = false);

public sealed record InvoiceLineDto(
    long Id,
    int SeqNo,
    long? TreatmentRecordId,
    string ItemName,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal LineTotal,
    bool IsAesthetic);

public sealed record InvoiceTotalsDto(
    decimal SubTotal,
    decimal DiscountTotal,
    decimal VatTotal,
    decimal WithholdingTotal,
    decimal GvStopajTotal,
    decimal PayableAmount);

/// <summary>
/// Karar motoru çıktısı + hesaplanmış taslak. <see cref="CanCreate"/> false ise
/// <see cref="Errors"/> düzeltilmeden fatura oluşturulamaz.
/// </summary>
public sealed record InvoicePreviewDto(
    InvoiceDocumentKind DocumentKind,
    string? ProfileId,
    string TypeCode,
    // Kullanıcıya gösterilecek gerekçe metni ("Şirket kiracı + GİB kaydı olmayan bireysel hasta → e-Arşiv SATIS").
    string Rationale,
    string? ExemptionCode,
    string? ExemptionReason,
    string? WithholdingCode,
    decimal? WithholdingPercent,
    InvoiceCustomerType CustomerType,
    long? PatientId,
    long? CompanyId,
    string BuyerName,
    string? BuyerTcknVkn,
    string? BuyerPassportNo,
    string? BuyerNationality,
    DateOnly? BuyerLastEntryDate,
    string CurrencyCode,
    IReadOnlyList<InvoiceLineDto> Lines,
    InvoiceTotalsDto Totals,
    // Engelleyici olmayan eksikler (e-posta yok, adres yok...).
    IReadOnlyList<string> Warnings,
    // Engelleyici hatalar (yetki belgesi yok, pasaport yok, estetik + 334...).
    IReadOnlyList<string> Errors,
    bool CanCreate);

public sealed record InvoiceStatusLogDto(
    long Id,
    InvoiceStatus? FromStatus,
    InvoiceStatus ToStatus,
    DateTime AtUtc,
    long? ActorUserId,
    string? Detail);

/// <summary>Liste satırı: no, tip, alıcı, tutar, durum, hata mesajı.</summary>
public sealed record InvoiceListItemDto(
    long Id,
    string? InvoiceNumber,
    InvoiceDocumentKind DocumentKind,
    string TypeCode,
    string BuyerName,
    decimal PayableAmount,
    string CurrencyCode,
    InvoiceStatus Status,
    string? ErrorMessage,
    DateOnly IssueDate,
    Guid? Ettn);

public sealed record InvoiceDto(
    long Id,
    long ClinicId,
    InvoiceDocumentKind DocumentKind,
    string? ProfileId,
    string TypeCode,
    InvoiceStatus Status,
    string? InvoiceNumber,
    string? Serial,
    Guid? Ettn,
    DateOnly IssueDate,
    TimeOnly? IssueTime,
    InvoiceCustomerType CustomerType,
    long? PatientId,
    long? CompanyId,
    string BuyerName,
    string? BuyerTcknVkn,
    string? BuyerPassportNo,
    string? BuyerNationality,
    DateOnly? BuyerLastEntryDate,
    string? BuyerAddress,
    string? BuyerEmail,
    string CurrencyCode,
    decimal ExchangeRate,
    InvoiceTotalsDto Totals,
    string? ExemptionCode,
    string? ExemptionReason,
    string? WithholdingCode,
    IntegratorProvider? IntegratorProvider,
    string? IntegratorRefId,
    DateTime? LastStatusCheckUtc,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    string? ErrorMessage,
    long? UblFileId,
    long? PdfFileId,
    long? SourceInvoiceId,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<InvoiceStatusLogDto> StatusLogs,
    DateTime CreatedAtUtc);

public sealed record InvoiceCancelRequest(string Reason);

/// <summary>GİB mükellef aynası sorgusu (lokal cache).</summary>
public sealed record GibTaxpayerDto(
    string Vkn,
    string? Title,
    string? Alias,
    string? AccountType,
    bool IsEInvoiceUser,
    DateTime? LastSyncUtc);
