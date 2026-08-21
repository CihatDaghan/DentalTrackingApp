using Dental.Domain.Enums;

namespace Dental.Application.Treatments;

/// <summary>Diş şeması render verisi: diş başına kalıcı durum + aktif tedavi işaretleri.</summary>
public sealed record PatientTeethDto(
    long PatientId,
    IReadOnlyList<ToothDto> Teeth,
    /// <summary>Ağız geneli (ToothNumber=NULL) aktif tedavi işaretleri (detertraj, beyazlatma...).</summary>
    IReadOnlyList<ToothMarkDto> MouthWideMarks);

public sealed record ToothDto(
    string ToothNumber,
    ToothCondition Condition,
    IReadOnlyList<ToothMarkDto> Marks);

/// <summary>Diş üzerindeki tedavi işareti katmanı: durum + kategori rengi + yüzeyler + kanal.</summary>
public sealed record ToothMarkDto(
    long TreatmentRecordId,
    long TreatmentDefinitionId,
    string TreatmentName,
    TreatmentRecordStatus Status,
    string? CategoryColorHex,
    ToothSurfaces Surfaces,
    byte? RootCanalCount);

public sealed record TreatmentAddItem(
    long TreatmentDefinitionId,
    string? ToothNumber = null,
    ToothSurfaces Surfaces = ToothSurfaces.None,
    TreatmentRecordStatus Status = TreatmentRecordStatus.Planned,
    long? DoctorUserId = null,
    /// <summary>NULL ise tarifeden çözümlenen fiyat kullanılır.</summary>
    decimal? Price = null,
    decimal DiscountAmount = 0m,
    string? Description = null,
    string? DiagnosisIcdCode = null,
    byte? RootCanalCount = null,
    int? PlanGroupNo = null,
    long? VisitId = null);

/// <summary>Toplu tedavi ekleme (diş şemasından çoklu seçim).</summary>
public sealed record TreatmentAddRequest(
    IReadOnlyList<TreatmentAddItem> Items,
    long? PriceListId = null,
    long? ClinicId = null);

public sealed record TreatmentRecordDto(
    long Id,
    long ClinicId,
    long PatientId,
    long DoctorUserId,
    string DoctorName,
    long TreatmentDefinitionId,
    string TreatmentName,
    string TreatmentCode,
    string? CategoryColorHex,
    string? ToothNumber,
    ToothSurfaces Surfaces,
    byte? RootCanalCount,
    TreatmentRecordStatus Status,
    string? DiagnosisIcdCode,
    int? PlanGroupNo,
    decimal Price,
    decimal DiscountAmount,
    decimal VatRate,
    string CurrencyCode,
    DateTime? PerformedAtUtc,
    string? Description,
    EnabizStatus EnabizStatus,
    long? VisitId,
    DateTime CreatedAtUtc);

/// <summary>Fiyat/indirim/açıklama/yüzey düzeltmesi; NULL alanlar değiştirilmez.</summary>
public sealed record TreatmentUpdateRequest(
    decimal? Price = null,
    decimal? DiscountAmount = null,
    string? Description = null,
    ToothSurfaces? Surfaces = null,
    byte? RootCanalCount = null,
    string? DiagnosisIcdCode = null,
    int? PlanGroupNo = null,
    long? DoctorUserId = null);

public sealed record TreatmentStatusRequest(
    TreatmentRecordStatus Status,
    /// <summary>Done geçişinde işlem zamanı; NULL ise şimdi.</summary>
    DateTime? PerformedAtUtc = null);

public sealed record TreatmentPriceItem(long Id, decimal Price, decimal DiscountAmount = 0m);

/// <summary>"Edit Price" toplu fiyat/indirim güncellemesi.</summary>
public sealed record TreatmentBulkPriceRequest(IReadOnlyList<TreatmentPriceItem> Items);
