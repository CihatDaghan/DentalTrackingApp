using Dental.Domain.Enums;

namespace Dental.Application.Appointments;

public sealed record RecallCreateRequest(
    long PatientId,
    DateOnly SuggestedDate,
    string? Reason = null,
    long? DoctorUserId = null,
    long? SourceTreatmentRecordId = null);

public sealed record RecallDto(
    long Id,
    long PatientId,
    string PatientName,
    long? DoctorUserId,
    DateOnly SuggestedDate,
    string? Reason,
    RecallStatus Status,
    long? AppointmentId,
    DateTime? LastReminderAtUtc,
    DateTime CreatedAtUtc);

public sealed record RecallListQuery(
    long? PatientId = null,
    RecallStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 25);

public sealed record RecallStatusRequest(RecallStatus Status);

/// <summary>Recall'ı randevuya çevirme isteği; randevu Type=Control olarak açılır.</summary>
public sealed record RecallConvertRequest(
    long ClinicId,
    long DoctorUserId,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Note = null);
