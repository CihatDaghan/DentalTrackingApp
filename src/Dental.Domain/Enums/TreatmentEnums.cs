namespace Dental.Domain.Enums;

/// <summary>Tedavinin uygulandığı kapsam: tek diş / çene / ağız geneli.</summary>
public enum ToothScope : byte
{
    PerTooth = 1,
    PerJaw = 2,
    WholeMouth = 3,
}

/// <summary>Diş şeması render'ı için dişin kalıcı durumu (ToothStatus.Condition).</summary>
public enum ToothCondition : byte
{
    Present = 1,
    Missing = 2,
    Extracted = 3,
    Implant = 4,
    Crown = 5,
    Bridge = 6,
    RootCanalTreated = 7,
    Unerupted = 8,
}

/// <summary>Tedavi kaydının yaşam döngüsü: tanı → plan → yapıldı (tek satır, durum alanıyla).</summary>
public enum TreatmentRecordStatus : byte
{
    Diagnosis = 1,
    Planned = 2,
    Done = 3,
    Cancelled = 4,
}

/// <summary>Diş yüzeyleri bit bayrağı (dolgu işaretleri).</summary>
[Flags]
public enum ToothSurfaces : byte
{
    None = 0,
    /// <summary>Mesial.</summary>
    M = 1,
    /// <summary>Okluzal / İnsizal.</summary>
    O = 2,
    /// <summary>Distal.</summary>
    D = 4,
    /// <summary>Bukkal / Vestibül.</summary>
    B = 8,
    /// <summary>Lingual / Palatinal.</summary>
    L = 16,
}

/// <summary>
/// Tedavi tanımının Done'a geçişte ToothStatus'a etkisi
/// (çekim→Extracted, implant→Implant, kron→Crown, kanal→RootCanalTreated, köprü→Bridge).
/// </summary>
public enum ToothStatusEffect : byte
{
    None = 0,
    Extracted = 1,
    Implant = 2,
    Crown = 3,
    RootCanal = 4,
    Bridge = 5,
}

/// <summary>e-Nabız 102/203 paketi gönderim durumu (gönderim kuyruğu sonraki aşamada).</summary>
public enum EnabizStatus : byte
{
    NotRequired = 0,
    Pending = 1,
    Sent = 2,
    Accepted = 3,
    Rejected = 4,
}
