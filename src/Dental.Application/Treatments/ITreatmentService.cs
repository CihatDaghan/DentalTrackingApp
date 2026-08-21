using Dental.Domain.Enums;

namespace Dental.Application.Treatments;

/// <summary>Diş şeması verisi + tedavi kayıtları (tanı/plan/yapıldı yaşam döngüsü).</summary>
public interface ITreatmentService
{
    /// <summary>Diş şeması render verisi: ToothStatus listesi + aktif (Cancelled olmayan) tedavi işaretleri.</summary>
    Task<PatientTeethDto> GetPatientTeethAsync(long patientId, CancellationToken ct = default);

    /// <summary>
    /// Toplu tedavi ekleme. FDI + RequiresSurface + ToothScope doğrulanır; fiyat verilmemişse
    /// tarifeden çözümlenir. Done ile eklenen kayıtlara ToothStatusEffect anında uygulanır.
    /// </summary>
    Task<IReadOnlyList<TreatmentRecordDto>> AddTreatmentsAsync(long patientId, TreatmentAddRequest request, CancellationToken ct = default);

    Task<TreatmentRecordDto> UpdateAsync(long id, TreatmentUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Durum geçişi. Done geçişinde PerformedAtUtc set edilir ve tedavi tanımının
    /// ToothStatusEffect'i ToothStatus'a işlenir. Cancelled her durumdan mümkündür.
    /// </summary>
    Task<TreatmentRecordDto> UpdateStatusAsync(long id, TreatmentStatusRequest request, CancellationToken ct = default);

    /// <summary>Tedavi geçmişi tablosu (tarih, hekim, diş, yüzey, tedavi adı, açıklama, tutar).</summary>
    Task<IReadOnlyList<TreatmentRecordDto>> ListTreatmentsAsync(long patientId, TreatmentRecordStatus? status = null, CancellationToken ct = default);

    Task<IReadOnlyList<TreatmentRecordDto>> BulkUpdatePricesAsync(TreatmentBulkPriceRequest request, CancellationToken ct = default);

    /// <summary>Done kayıtlar silinemez (yalnız Cancelled'a çevrilebilir); Diagnosis/Planned silinebilir.</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);
}
