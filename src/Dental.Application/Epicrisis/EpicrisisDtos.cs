namespace Dental.Application.Epicrisis;

/// <summary>Seçilen ICD tanısı (arama mevcut /api/v1/icd-codes ucundan yapılır; snapshot burada saklanır).</summary>
public sealed record EpicrisisDiagnosis(string Code, string Name);

/// <summary>Dahil edilen tedavi kaydının oluşturma anındaki özeti (snapshot — kayıt sonradan değişse de sabit).</summary>
public sealed record EpicrisisTreatmentLine(
    long Id,
    DateOnly? Date,
    string? ToothNumber,
    string Name,
    string DoctorName);

public sealed record EpicrisisCreateRequest(
    long DoctorUserId,
    string Title,
    IReadOnlyList<EpicrisisDiagnosis> Diagnoses,
    IReadOnlyList<long> TreatmentRecordIds,
    string? BodyText = null);

public sealed record EpicrisisDto(
    long Id,
    long PatientId,
    string PatientName,
    long DoctorUserId,
    string DoctorName,
    string Title,
    IReadOnlyList<EpicrisisDiagnosis> Diagnoses,
    IReadOnlyList<EpicrisisTreatmentLine> Treatments,
    string? BodyText,
    long? PdfFileId,
    DateTime CreatedAtUtc);
