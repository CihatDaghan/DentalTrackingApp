using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// İlaç referansı. TenantId NULL = merkezi (seed) liste; dolu = kiracının kendi eklediği satır.
/// Bilerek ITenantOwned DEĞİL: global query filter'a takılmaz, görünürlük sorgularda elle
/// filtrelenir (TenantId == null || TenantId == current). IsControlled → UI'da Renkli Reçete uyarısı.
/// </summary>
public class Drug : BaseEntity
{
    public long? TenantId { get; set; }
    public string? Barcode { get; set; }
    public required string Name { get; set; }
    public string? AtcCode { get; set; }
    /// <summary>Tablet / şurup / gargara / jel...</summary>
    public string? Form { get; set; }
    public string? DefaultDose { get; set; }
    /// <summary>'2x1' vb.</summary>
    public string? DefaultUsage { get; set; }
    /// <summary>Kontrole tabi ilaç (kırmızı/yeşil reçete) — entegrasyon yok, yalnız uyarı.</summary>
    public bool IsControlled { get; set; }
}

/// <summary>Reçete şablonu ('Çekim Sonrası' gibi hazır ilaç setleri).</summary>
public class PrescriptionTemplate : TenantEntity
{
    public required string Name { get; set; }

    public ICollection<PrescriptionTemplateItem> Items { get; set; } = [];
}

public class PrescriptionTemplateItem : TenantEntity
{
    public long TemplateId { get; set; }
    public long DrugId { get; set; }
    public int BoxCount { get; set; } = 1;
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? UsageNote { get; set; }

    public PrescriptionTemplate? Template { get; set; }
    public Drug? Drug { get; set; }
}

/// <summary>
/// Reçete. PrescriptionNo tenant içi sıra ('RX-2026-000001'). PDF ilk istekte üretilip
/// MediaFile'a yazılır ve Status=Printed olur. SubmittedToUss+ durumları H aşaması rezervi.
/// </summary>
public class Prescription : TenantEntity
{
    public long ClinicId { get; set; }
    public long PatientId { get; set; }
    /// <summary>Reçeteyi yazan hekim — UserType.Dentist doğrulaması serviste.</summary>
    public long DoctorUserId { get; set; }
    public long? VisitId { get; set; }
    public required string PrescriptionNo { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Draft;
    /// <summary>Reçetem sisteminin hastaya verdiği alfanümerik kod (H aşaması).</summary>
    public string? RecetemCode { get; set; }
    public long? PdfFileId { get; set; }

    public ICollection<PrescriptionItem> Items { get; set; } = [];
}

public class PrescriptionItem : TenantEntity
{
    public long PrescriptionId { get; set; }
    public long DrugId { get; set; }
    public int BoxCount { get; set; } = 1;
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? UsageNote { get; set; }

    public Prescription? Prescription { get; set; }
    public Drug? Drug { get; set; }
}
