namespace Dental.Application.Clinical;

public interface IAnamnesisService
{
    // ---- Şablonlar ----
    Task<IReadOnlyList<AnamnesisTemplateListItemDto>> ListTemplatesAsync(CancellationToken ct = default);
    Task<AnamnesisTemplateDto> GetTemplateAsync(long id, CancellationToken ct = default);
    Task<AnamnesisTemplateDto> CreateTemplateAsync(AnamnesisTemplateUpsertRequest request, CancellationToken ct = default);
    Task<AnamnesisTemplateDto> UpdateTemplateAsync(long id, AnamnesisTemplateUpsertRequest request, CancellationToken ct = default);
    Task DeleteTemplateAsync(long id, CancellationToken ct = default);

    // ---- Hasta yanıtları ----
    /// <summary>Versiyonlu doldurma: her çağrı yeni AnamnesisResponse üretir, eskiler korunur.</summary>
    Task<AnamnesisResponseDto> FillAsync(long patientId, AnamnesisFillRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AnamnesisResponseDto>> ListResponsesAsync(long patientId, CancellationToken ct = default);
    /// <summary>En son doldurmadaki olumlu kritik yanıtlar (hasta başlığı kırmızı rozeti).</summary>
    Task<IReadOnlyList<CriticalFlagDto>> GetCriticalFlagsAsync(long patientId, CancellationToken ct = default);
}

public interface IPatientNoteService
{
    /// <summary>IsPinned üstte, sonra en yeni.</summary>
    Task<IReadOnlyList<PatientNoteDto>> ListAsync(long patientId, CancellationToken ct = default);
    Task<PatientNoteDto> CreateAsync(long patientId, PatientNoteUpsertRequest request, CancellationToken ct = default);
    /// <summary>Yalnız yazar veya Owner/Manager düzenleyebilir.</summary>
    Task<PatientNoteDto> UpdateAsync(long patientId, long noteId, PatientNoteUpsertRequest request, CancellationToken ct = default);
    /// <summary>Yalnız yazar veya Owner/Manager silebilir.</summary>
    Task DeleteAsync(long patientId, long noteId, CancellationToken ct = default);
}
