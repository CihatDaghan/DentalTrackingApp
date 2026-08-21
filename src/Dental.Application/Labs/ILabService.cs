using Dental.Application.Common;

namespace Dental.Application.Labs;

public interface ILabService
{
    // ---- Laboratuvar firmaları ----
    Task<IReadOnlyList<LaboratoryDto>> ListLaboratoriesAsync(CancellationToken ct = default);
    Task<LaboratoryDto> GetLaboratoryAsync(long id, CancellationToken ct = default);
    Task<LaboratoryDto> CreateLaboratoryAsync(LaboratoryUpsertRequest request, CancellationToken ct = default);
    Task<LaboratoryDto> UpdateLaboratoryAsync(long id, LaboratoryUpsertRequest request, CancellationToken ct = default);
    /// <summary>Açık vakası (Delivered olmayan) olan laboratuvar silinemez.</summary>
    Task DeleteLaboratoryAsync(long id, CancellationToken ct = default);

    // ---- Vakalar ----
    Task<LabCaseDto> CreateCaseAsync(LabCaseUpsertRequest request, CancellationToken ct = default);
    Task<LabCaseDto> UpdateCaseAsync(long id, LabCaseUpsertRequest request, CancellationToken ct = default);
    Task<LabCaseDto> GetCaseAsync(long id, CancellationToken ct = default);
    Task DeleteCaseAsync(long id, CancellationToken ct = default);
    /// <summary>Durum geçişi; her geçiş LabCaseStatusHistory'ye yazılır. Sent → SentDate, Received → ReceivedDate doldurur.</summary>
    Task<LabCaseDto> ChangeStatusAsync(long id, LabCaseStatusChangeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LabCaseHistoryDto>> GetHistoryAsync(long caseId, CancellationToken ct = default);
    /// <summary>Filtreli liste; IsOverdue = DueDate &lt; bugün &amp;&amp; Status &lt; Received (sorgu bazlı).</summary>
    Task<PagedResult<LabCaseDto>> ListCasesAsync(LabCaseListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<LabCaseDto>> ListForPatientAsync(long patientId, CancellationToken ct = default);
}
