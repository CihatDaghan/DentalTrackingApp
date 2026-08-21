using Dental.Application.Common;
using Dental.Domain.Enums;

namespace Dental.Application.Enabiz;

/// <summary>
/// Klinik iş akışının e-Nabız'a dokunduğu TEK nokta.
///
/// <para>TreatmentService/PrescriptionService bu soyutlamayı çağırır; paket üretimini, mod
/// kontrolünü ve kuyruğa almayı uygulaması üstlenir. Ayrı bir port olmasının sebebi, klinik
/// servislerin e-Nabız'a bağımlı hâle gelmemesi ve testlerde tetiklemenin kolayca
/// kapatılabilmesi/gözlenebilmesidir (<c>Integrations:Enabiz:Trigger</c> = off).</para>
/// </summary>
public interface IEnabizSubmissionQueue
{
    /// <summary>
    /// Tedavi <see cref="TreatmentRecordStatus.Done"/> olduğunda çağrılır. Gerekirse Visit oluşturur
    /// (ProtocolNo üretir) ve 101 → 103 → 203 paketlerini bağımlılık sırasıyla kuyruğa alır.
    /// Mod <see cref="EnabizMode.Disabled"/> ise hiçbir şey yapmaz.
    /// </summary>
    Task OnTreatmentDoneAsync(long treatmentRecordId, CancellationToken ct = default);

    /// <summary>Reçete USS'ye gönderilmek üzere kuyruğa alınır (Reçetem akışı).</summary>
    Task OnPrescriptionSubmittedAsync(long prescriptionId, CancellationToken ct = default);

    /// <summary>Bir ziyaretin paketlerini elle kuyruğa alır/yeniden üretir.</summary>
    Task<EnabizQueueResultDto> QueueVisitAsync(long visitId, CancellationToken ct = default);
}

/// <summary>Kuyruk işleyici: gönderim, durum makinesi, geri doldurma ve mutabakat.</summary>
public interface IEnabizDispatcher
{
    /// <summary>Tek paketi gönderir; sonucu durum makinesine işler.</summary>
    Task<EnabizSubmissionState> DispatchAsync(long submissionId, CancellationToken ct = default);

    /// <summary>Gönderime hazır paketleri (bağımlılığı karşılanmış, zamanı gelmiş) işler.</summary>
    Task<int> DispatchQueuedAsync(int batchSize = 50, CancellationToken ct = default);

    /// <summary>
    /// Mod Held'den TestOnly/Live'a geçtiğinde bekleyenleri ziyaret sırasına göre Queued'a çeker.
    /// </summary>
    Task<int> BackfillHeldAsync(int batchSize = 200, CancellationToken ct = default);

    /// <summary>405 günlük veri sorgusuyla mutabakat; USS'de görünmeyen paketleri Queued'a döndürür.</summary>
    Task<int> ReconcileAsync(DateOnly? date = null, CancellationToken ct = default);

    /// <summary>Elle yeniden deneme: ManualReview/Rejected paketi Queued'a alır.</summary>
    Task<EnabizSubmissionState> RetryAsync(long submissionId, CancellationToken ct = default);
}

/// <summary>API tarafı sorgular + ayar yönetimi.</summary>
public interface IEnabizService
{
    Task<PagedResult<EnabizSubmissionListItemDto>> ListAsync(
        EnabizSubmissionState? state = null,
        EnabizPacketType? packetType = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default);

    Task<EnabizSubmissionDto> GetAsync(long id, CancellationToken ct = default);
    Task<EnabizStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<EnabizSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<EnabizSettingsDto> UpdateSettingsAsync(EnabizSettingsRequest request, CancellationToken ct = default);
}

/// <summary>SKRS kod seti sorgusu + senkronu.</summary>
public interface ISkrsCodeService
{
    Task<IReadOnlyList<SkrsCodeDto>> SearchAsync(
        string? systemName = null, string? search = null, int limit = 50, CancellationToken ct = default);

    /// <summary>Kimlik bilgisi varsa canlı SKRS'den, yoksa tohum listelerden doldurur.</summary>
    Task<int> SyncAsync(CancellationToken ct = default);
}
