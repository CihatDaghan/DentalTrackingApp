using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Randevu. PatientId NULL = boş slot / blok. Çakışma kontrolü uygulama katmanında
/// (aynı DoctorUserId + zaman kesişimi, Cancelled/NoShow hariç) + RowVersion ile iyimser kilit.
/// </summary>
public class Appointment : TenantEntity
{
    public long ClinicId { get; set; }
    public long? PatientId { get; set; }
    public long DoctorUserId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public AppointmentType Type { get; set; } = AppointmentType.Normal;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    /// <summary>Boş slot/blok için serbest başlık.</summary>
    public string? Title { get; set; }
    public string? Note { get; set; }
    /// <summary>Takvim rengi (#rrggbb); boşsa hekim rengi kullanılır.</summary>
    public string? Color { get; set; }
    /// <summary>Kontrol randevusunun doğduğu tedavi — TreatmentRecord FK'sı sonraki aşamada.</summary>
    public long? SourceTreatmentRecordId { get; set; }
    public ReminderState ReminderState { get; set; }
    public string? CancelReason { get; set; }
    public long? CancelledByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Hekim çalışma saati — takvimde müsaitlik grid'i için.</summary>
public class DoctorWorkingHour : TenantEntity
{
    public long DoctorUserId { get; set; }
    public long ClinicId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
