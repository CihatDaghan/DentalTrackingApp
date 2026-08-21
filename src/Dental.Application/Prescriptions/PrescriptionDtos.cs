using Dental.Domain.Enums;

namespace Dental.Application.Prescriptions;

// ---- İlaçlar ----

public sealed record DrugDto(
    long Id,
    long? TenantId,
    string? Barcode,
    string Name,
    string? AtcCode,
    string? Form,
    string? DefaultDose,
    string? DefaultUsage,
    bool IsControlled);

/// <summary>Kiracıya özel ilaç satırı ekleme (merkezi listede olmayan ilaçlar için).</summary>
public sealed record DrugCreateRequest(
    string Name,
    string? Barcode = null,
    string? AtcCode = null,
    string? Form = null,
    string? DefaultDose = null,
    string? DefaultUsage = null,
    bool IsControlled = false);

// ---- Şablonlar ----

public sealed record PrescriptionTemplateItemRequest(
    long DrugId,
    int BoxCount = 1,
    string? Dose = null,
    string? Frequency = null,
    string? Duration = null,
    string? UsageNote = null);

public sealed record PrescriptionTemplateUpsertRequest(
    string Name,
    IReadOnlyList<PrescriptionTemplateItemRequest> Items);

public sealed record PrescriptionTemplateItemDto(
    long Id,
    long DrugId,
    string DrugName,
    string? DrugForm,
    bool IsControlled,
    int BoxCount,
    string? Dose,
    string? Frequency,
    string? Duration,
    string? UsageNote);

public sealed record PrescriptionTemplateDto(
    long Id,
    string Name,
    IReadOnlyList<PrescriptionTemplateItemDto> Items);

// ---- Reçeteler ----

public sealed record PrescriptionItemRequest(
    long DrugId,
    int BoxCount = 1,
    string? Dose = null,
    string? Frequency = null,
    string? Duration = null,
    string? UsageNote = null);

/// <summary>
/// Reçete oluşturma. TemplateId verilir ve Items boşsa kalemler şablondan kopyalanır;
/// Items doluysa istekteki kalemler kullanılır.
/// </summary>
public sealed record PrescriptionCreateRequest(
    long DoctorUserId,
    long? VisitId = null,
    long? TemplateId = null,
    IReadOnlyList<PrescriptionItemRequest>? Items = null);

public sealed record PrescriptionItemDto(
    long Id,
    long DrugId,
    string DrugName,
    string? DrugForm,
    bool IsControlled,
    int BoxCount,
    string? Dose,
    string? Frequency,
    string? Duration,
    string? UsageNote);

public sealed record PrescriptionDto(
    long Id,
    long PatientId,
    string PatientName,
    long DoctorUserId,
    string DoctorName,
    long? VisitId,
    string PrescriptionNo,
    PrescriptionStatus Status,
    string? RecetemCode,
    long? PdfFileId,
    bool HasControlledDrug,
    string? ControlledWarning,
    IReadOnlyList<PrescriptionItemDto> Items,
    DateTime CreatedAtUtc);

public sealed record PrescriptionSaveAsTemplateRequest(string Name);
