using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Kontrol (recall) planı: henüz randevuya dönüşmemiş "şu tarihte görelim" kaydı.
/// Randevuya çevrilince AppointmentId bağlanır ve Status=ConvertedToAppointment olur.
/// </summary>
public class RecallPlan : TenantEntity
{
    public long PatientId { get; set; }
    /// <summary>Planın doğduğu tedavi — TreatmentRecord FK'sı sonraki aşamada.</summary>
    public long? SourceTreatmentRecordId { get; set; }
    public long? DoctorUserId { get; set; }
    public DateOnly SuggestedDate { get; set; }
    public string? Reason { get; set; }
    public RecallStatus Status { get; set; } = RecallStatus.Planned;
    public long? AppointmentId { get; set; }
    public DateTime? LastReminderAtUtc { get; set; }
}
