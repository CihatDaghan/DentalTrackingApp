using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>Anlaşmalı diş laboratuvarı firması.</summary>
public class Laboratory : TenantEntity
{
    public required string Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
}

/// <summary>
/// Laboratuvar vakası (protez/kron işi). CaseNo tenant içi sıra ('LAB-2026-0001').
/// Durum geçişleri LabCaseStatusHistory'ye yazılır; gecikmiş bayrağı sorgu bazlıdır
/// (DueDate &lt; bugün &amp;&amp; Status &lt; Received).
/// </summary>
public class LabCase : TenantEntity
{
    public long ClinicId { get; set; }
    public long PatientId { get; set; }
    public long DoctorUserId { get; set; }
    public long LaboratoryId { get; set; }
    public required string CaseNo { get; set; }
    /// <summary>Kron / Köprü / Total protez / Zirkonyum...</summary>
    public required string WorkType { get; set; }
    /// <summary>FDI diş numaraları CSV ('11,12,21').</summary>
    public string? TeethCsv { get; set; }
    /// <summary>Vita renk skalası (A1-D4).</summary>
    public string? Shade { get; set; }
    public string? Material { get; set; }
    public LabCaseStatus Status { get; set; } = LabCaseStatus.Draft;
    public DateOnly? SentDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    /// <summary>Laboratuvara ödenen maliyet.</summary>
    public decimal Price { get; set; }
    public string? Note { get; set; }

    public Laboratory? Laboratory { get; set; }
}

/// <summary>Vaka durum geçmişi — her durum değişikliğinde bir satır.</summary>
public class LabCaseStatusHistory : TenantEntity
{
    public long LabCaseId { get; set; }
    public LabCaseStatus Status { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public long? ChangedByUserId { get; set; }
    public string? Note { get; set; }
}
