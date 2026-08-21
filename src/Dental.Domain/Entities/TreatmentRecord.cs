using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Diş başına kalıcı durum (şema render'ı için). Done tedavilerin ToothStatusEffect'i
/// bu tabloya işlenir; UpdatedFromTreatmentRecordId son etkiyi yapan kayıttır.
/// </summary>
public class ToothStatus : TenantEntity
{
    public long PatientId { get; set; }
    /// <summary>FDI diş numarası (char(2)); geçerlilik FdiTeeth ile doğrulanır.</summary>
    public required string ToothNumber { get; set; }
    public ToothCondition Condition { get; set; } = ToothCondition.Present;
    public long? UpdatedFromTreatmentRecordId { get; set; }
}

/// <summary>e-Nabız protokol birimi. 101/103 paketleri Visit'ten, 102/203 TreatmentRecord'lardan üretilir.</summary>
public class Visit : TenantEntity
{
    public long ClinicId { get; set; }
    public long PatientId { get; set; }
    public long DoctorUserId { get; set; }
    /// <summary>Protokol numarası; tenant içinde benzersiz (filtered unique index).</summary>
    public required string ProtocolNo { get; set; }
    public DateOnly VisitDate { get; set; }
    /// <summary>e-Nabız 101 paketi yanıtındaki sistem takip no.</summary>
    public string? SysTakipNo { get; set; }
}

/// <summary>
/// Çekirdek klinik kayıt: tanı → plan → yapıldı yaşam döngüsü tek satırda durum alanıyla.
/// Planned→Done geçişinde PerformedAtUtc set edilir ve tedavi tanımının ToothStatusEffect'i
/// ToothStatus'a işlenir. Done kayıtlar silinemez (yalnız Cancelled'a çevrilebilir).
/// </summary>
public class TreatmentRecord : TenantEntity
{
    public long ClinicId { get; set; }
    public long PatientId { get; set; }
    public long DoctorUserId { get; set; }
    public long? VisitId { get; set; }
    public long TreatmentDefinitionId { get; set; }
    /// <summary>FDI diş numarası; çene/ağız geneli işlemde NULL.</summary>
    public string? ToothNumber { get; set; }
    /// <summary>Yüzey bit bayrağı (M=1, O/I=2, D=4, B/V=8, L/P=16).</summary>
    public ToothSurfaces Surfaces { get; set; }
    /// <summary>Kanal tedavisinde kanal sayısı işareti.</summary>
    public byte? RootCanalCount { get; set; }
    public TreatmentRecordStatus Status { get; set; } = TreatmentRecordStatus.Planned;
    /// <summary>ICD-10 tanı kodu (103/203 paketi).</summary>
    public string? DiagnosisIcdCode { get; set; }
    /// <summary>Alternatif tedavi planı grubu (Plan A/B).</summary>
    public int? PlanGroupNo { get; set; }
    public decimal Price { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRate { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public DateTime? PerformedAtUtc { get; set; }
    public string? Description { get; set; }
    /// <summary>Done olunca oluşan borç kaydı — D aşamasında (LedgerEntry) bağlanacak; şimdilik düz kolon.</summary>
    public long? LedgerEntryId { get; set; }
    /// <summary>Faturalanan satır — fatura aşamasında bağlanacak; şimdilik düz kolon.</summary>
    public long? InvoiceLineId { get; set; }
    public EnabizStatus EnabizStatus { get; set; }

    public TreatmentDefinition? TreatmentDefinition { get; set; }
}
