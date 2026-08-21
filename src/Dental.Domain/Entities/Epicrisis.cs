using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>
/// Epikriz belgesi. Tanılar ve dahil edilen tedavi satırları oluşturma anında JSON snapshot
/// olarak sabitlenir (tedavi kaydı sonradan değişse de belge kanıt olarak sabit kalır).
/// PDF ilk istekte üretilip MediaFile'a yazılır.
/// </summary>
public class EpicrisisDocument : TenantEntity
{
    public long ClinicId { get; set; }
    public long PatientId { get; set; }
    public long DoctorUserId { get; set; }
    public required string Title { get; set; }
    /// <summary>Seçilen ICD tanıları snapshot'ı: [{"code":"K04.7","name":"..."}].</summary>
    public string DiagnosisJson { get; set; } = "[]";
    /// <summary>Dahil edilen tedavi özet satırları snapshot'ı: [{"id","date","toothNumber","name","doctorName"}].</summary>
    public string TreatmentsJson { get; set; } = "[]";
    /// <summary>Sonuç / öneri serbest metni.</summary>
    public string? BodyText { get; set; }
    public long? PdfFileId { get; set; }
}
