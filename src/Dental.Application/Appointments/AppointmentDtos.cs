using Dental.Domain.Enums;

namespace Dental.Application.Appointments;

public sealed record AppointmentUpsertRequest(
    long ClinicId,
    long DoctorUserId,
    DateTime StartUtc,
    DateTime EndUtc,
    AppointmentType Type = AppointmentType.Normal,
    long? PatientId = null,
    string? Title = null,
    string? Note = null,
    string? Color = null,
    long? SourceTreatmentRecordId = null,
    string? RowVersion = null);

public sealed record AppointmentDto(
    long Id,
    long ClinicId,
    long? PatientId,
    string? PatientName,
    long DoctorUserId,
    string DoctorName,
    DateTime StartUtc,
    DateTime EndUtc,
    AppointmentType Type,
    AppointmentStatus Status,
    string? Title,
    string? Note,
    string? Color,
    long? SourceTreatmentRecordId,
    ReminderState ReminderState,
    string? CancelReason,
    string RowVersion);

public sealed record AppointmentListQuery(
    DateTime From,
    DateTime To,
    long? ClinicId = null,
    IReadOnlyList<long>? DoctorIds = null);

public sealed record AppointmentStatusRequest(AppointmentStatus Status, string? CancelReason = null);

public sealed record WorkingHourItemDto(long ClinicId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record WorkingHoursSaveRequest(long DoctorUserId, IReadOnlyList<WorkingHourItemDto> Items);

public sealed record DoctorWorkingHourDto(
    long Id, long DoctorUserId, long ClinicId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>Takvim için hekim listesi (UserType=Dentist).</summary>
public sealed record DoctorDto(long Id, string FirstName, string LastName, string? Color, string? Branch);
