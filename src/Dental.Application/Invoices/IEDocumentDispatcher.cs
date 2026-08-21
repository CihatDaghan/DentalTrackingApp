using Dental.Domain.Enums;

namespace Dental.Application.Invoices;

/// <summary>
/// Entegratöre gönderim ve durum yoklama — API'nin senkron gönderimi ile Hangfire job'larının
/// ORTAK yoludur; durum makinesi kuralları tek yerde kalır.
/// Çağıran her zaman tenant bağlamı kurulmuş bir scope'ta olmalıdır (job'larda ITenantScopeFactory).
/// </summary>
public interface IEDocumentDispatcher
{
    /// <summary>Tek belgeyi gönderir ve sonucu durum makinesine işler; yeni durumu döner.</summary>
    Task<InvoiceStatus> DispatchAsync(long invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Kuyruktaki (Queued) ve yeniden deneme zamanı gelmiş (Error) belgeleri gönderir.
    /// İşlenen belge sayısını döner.
    /// </summary>
    Task<int> DispatchQueuedAsync(int batchSize = 50, CancellationToken ct = default);

    /// <summary>SentToIntegrator/GibProcessing belgelerinin nihai durumunu sorgular.</summary>
    Task<int> PollStatusesAsync(int batchSize = 100, CancellationToken ct = default);

    /// <summary>GİB mükellef aynasını entegratörden tazeler; upsert edilen kayıt sayısını döner.</summary>
    Task<int> SyncGibTaxpayersAsync(CancellationToken ct = default);
}
