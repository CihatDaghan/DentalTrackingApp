namespace Dental.Domain.Enums;

public enum AppointmentType : byte
{
    Normal = 1,
    /// <summary>Kontrol randevusu — genellikle bir tedaviden (SourceTreatmentRecordId) doğar.</summary>
    Control = 2,
    /// <summary>Hasta atanmamış boş slot.</summary>
    EmptySlot = 3,
    /// <summary>Hekimin kapattığı zaman bloğu (izin, toplantı...).</summary>
    Blocked = 4,
}

public enum AppointmentStatus : byte
{
    Scheduled = 1,
    Confirmed = 2,
    Arrived = 3,
    InChair = 4,
    Completed = 5,
    Cancelled = 6,
    NoShow = 7,
}

public enum ReminderState : byte
{
    None = 0,
    Queued = 1,
    Sent = 2,
}

public enum RecallStatus : byte
{
    Planned = 1,
    ConvertedToAppointment = 2,
    Arrived = 3,
    Abandoned = 4,
}
