using Dental.Application.Media;

namespace Dental.Application.Prescriptions;

public interface IPrescriptionService
{
    // ---- İlaç listesi (merkezi + kiracı özel) ----
    /// <summary>Ad/barkod araması. Görünürlük: TenantId == null (merkezi) || TenantId == current.</summary>
    Task<IReadOnlyList<DrugDto>> SearchDrugsAsync(string? search, CancellationToken ct = default);
    /// <summary>Kiracıya özel ilaç satırı ekler (merkezi liste değişmez).</summary>
    Task<DrugDto> CreateDrugAsync(DrugCreateRequest request, CancellationToken ct = default);

    // ---- Şablonlar ----
    Task<IReadOnlyList<PrescriptionTemplateDto>> ListTemplatesAsync(CancellationToken ct = default);
    Task<PrescriptionTemplateDto> GetTemplateAsync(long id, CancellationToken ct = default);
    Task<PrescriptionTemplateDto> CreateTemplateAsync(PrescriptionTemplateUpsertRequest request, CancellationToken ct = default);
    Task<PrescriptionTemplateDto> UpdateTemplateAsync(long id, PrescriptionTemplateUpsertRequest request, CancellationToken ct = default);
    Task DeleteTemplateAsync(long id, CancellationToken ct = default);

    // ---- Reçeteler ----
    /// <summary>Oluşturur (hekim = UserType.Dentist zorunlu). TemplateId verilirse kalemler şablondan doldurulur.</summary>
    Task<PrescriptionDto> CreateAsync(long patientId, PrescriptionCreateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionDto>> ListForPatientAsync(long patientId, CancellationToken ct = default);
    Task<PrescriptionDto> GetAsync(long id, CancellationToken ct = default);
    /// <summary>Reçete kalemlerini yeni şablon olarak kaydeder.</summary>
    Task<PrescriptionTemplateDto> SaveAsTemplateAsync(long id, PrescriptionSaveAsTemplateRequest request, CancellationToken ct = default);
    /// <summary>A5 PDF: ilk istekte üretilip MediaFile'a yazılır (Status=Printed), sonraki isteklerde arşivden akar.</summary>
    Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default);
}
