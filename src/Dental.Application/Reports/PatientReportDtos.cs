using Dental.Domain.Enums;

namespace Dental.Application.Reports;

/// <summary>Tedavi dökümü satırı (hasta kartı "Rapor" sekmesi).</summary>
public sealed record PatientTreatmentRowDto(
    DateOnly? Date,
    string DoctorName,
    string? ToothNumber,
    string TreatmentName,
    decimal Price,
    decimal DiscountAmount,
    decimal NetAmount,
    TreatmentRecordStatus Status);

public sealed record PatientTreatmentReportDto(
    long PatientId,
    string PatientName,
    string FileNo,
    string ClinicName,
    DateOnly? From,
    DateOnly? To,
    DateOnly IssuedOn,
    IReadOnlyList<PatientTreatmentRowDto> Rows,
    decimal TotalGross,
    decimal TotalDiscount,
    decimal TotalNet,
    /// <summary>format=pdf ile üretildiyse arşive yazılan MediaFile kimliği.</summary>
    long? PdfFileId = null);

public sealed record PatientToothStatusRowDto(string ToothNumber, ToothCondition Condition, string ConditionText);

/// <summary>"Durum Bildirir Rapor": kimlik bloğu + mevcut diş durumu + yapılan tedaviler + imza alanı.</summary>
public sealed record PatientStatusReportDto(
    long PatientId,
    string PatientName,
    string FileNo,
    string ClinicName,
    DateOnly? BirthDate,
    int? Age,
    string? GenderText,
    string? IdentityMasked,
    string? Phone,
    DateOnly IssuedOn,
    string DoctorName,
    string? DiplomaNo,
    IReadOnlyList<PatientToothStatusRowDto> Teeth,
    IReadOnlyList<PatientTreatmentRowDto> Treatments,
    long? PdfFileId = null);

/// <summary>Proforma (fiyat teklifi) isteği: hastanın PLANLANMIŞ tedavi kayıtlarından seçim.</summary>
public sealed record ProformaRequest(
    IReadOnlyList<long> TreatmentRecordIds,
    DateOnly? ValidUntil = null,
    string? Note = null);

public sealed record ProformaLineDto(
    int SeqNo,
    string TreatmentName,
    string? ToothNumber,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal VatRate,
    decimal VatAmount,
    /// <summary>İndirim sonrası, KDV hariç satır tutarı.</summary>
    decimal LineTotal);

public sealed record ProformaDto(
    long PatientId,
    string PatientName,
    string FileNo,
    string ClinicName,
    DateOnly IssuedOn,
    DateOnly ValidUntil,
    IReadOnlyList<ProformaLineDto> Lines,
    decimal SubTotal,
    decimal DiscountTotal,
    decimal VatTotal,
    decimal GrandTotal,
    string? Note,
    /// <summary>Belgenin hukuki niteliği: fatura DEĞİLDİR.</summary>
    string Disclaimer,
    long? PdfFileId = null);

/// <summary>
/// Hasta kartı "Rapor" sekmesinin arka ucu. PDF üretilirse belge MediaFile arşivine
/// (<see cref="MediaCategory.PatientReportPdf"/>) yazılır ve hasta dosyasında görünür.
/// </summary>
public interface IPatientReportService
{
    Task<PatientTreatmentReportDto> GetTreatmentReportAsync(
        long patientId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<(PatientTreatmentReportDto Report, ReportFileDto File)> GetTreatmentReportPdfAsync(
        long patientId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<PatientStatusReportDto> GetStatusReportAsync(long patientId, CancellationToken ct = default);

    Task<(PatientStatusReportDto Report, ReportFileDto File)> GetStatusReportPdfAsync(
        long patientId, CancellationToken ct = default);

    Task<ProformaDto> CreateProformaAsync(long patientId, ProformaRequest request, CancellationToken ct = default);

    Task<(ProformaDto Report, ReportFileDto File)> CreateProformaPdfAsync(
        long patientId, ProformaRequest request, CancellationToken ct = default);
}
