using Dental.Domain.Enums;

namespace Dental.Application.Labs;

// ---- Laboratuvar firmaları ----

public sealed record LaboratoryUpsertRequest(
    string Name,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? ContactPerson = null);

public sealed record LaboratoryDto(
    long Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson);

// ---- Vakalar ----

public sealed record LabCaseUpsertRequest(
    long PatientId,
    long DoctorUserId,
    long LaboratoryId,
    string WorkType,
    string? TeethCsv = null,
    string? Shade = null,
    string? Material = null,
    DateOnly? SentDate = null,
    DateOnly? DueDate = null,
    decimal Price = 0,
    string? Note = null,
    long? ClinicId = null);

public sealed record LabCaseStatusChangeRequest(LabCaseStatus Status, string? Note = null);

public sealed record LabCaseDto(
    long Id,
    string CaseNo,
    long ClinicId,
    long PatientId,
    string PatientName,
    long DoctorUserId,
    string DoctorName,
    long LaboratoryId,
    string LaboratoryName,
    string WorkType,
    string? TeethCsv,
    string? Shade,
    string? Material,
    LabCaseStatus Status,
    DateOnly? SentDate,
    DateOnly? DueDate,
    DateOnly? ReceivedDate,
    decimal Price,
    string? Note,
    bool IsOverdue,
    DateTime CreatedAtUtc);

public sealed record LabCaseHistoryDto(
    long Id,
    LabCaseStatus Status,
    DateTime ChangedAtUtc,
    long? ChangedByUserId,
    string? ChangedByName,
    string? Note);

public sealed record LabCaseListQuery(
    LabCaseStatus? Status = null,
    long? LaboratoryId = null,
    long? DoctorUserId = null,
    long? PatientId = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    bool OverdueOnly = false,
    int Page = 1,
    int PageSize = 25);
