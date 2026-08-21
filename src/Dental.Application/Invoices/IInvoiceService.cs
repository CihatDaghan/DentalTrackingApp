using Dental.Application.Common;
using Dental.Application.Media;
using Dental.Domain.Enums;

namespace Dental.Application.Invoices;

/// <summary>
/// e-Belge iş akışı: taslak önizleme (karar motoru) → Draft kayıt → UBL üretimi (numara + ETTN)
/// → kuyruğa alma/gönderim → durum sorgulama → iptal.
/// Durum geçişleri yalnız bu servis üzerinden yapılır; her geçiş InvoiceStatusLog'a yazılır.
/// </summary>
public interface IInvoiceService
{
    /// <summary>
    /// Yan etkisiz taslak: belge tipini (Dental.EDocument.Ubl.EDocumentTypeResolver) çözer,
    /// satırları ve toplamları hesaplar, eksik alanları uyarı/hata olarak döner.
    /// </summary>
    Task<InvoicePreviewDto> PreviewAsync(InvoiceDraftRequest request, CancellationToken ct = default);

    /// <summary>Draft fatura oluşturur (alıcı snapshot + satırlar). Preview hatası varsa reddeder.</summary>
    Task<InvoiceDto> CreateAsync(InvoiceDraftRequest request, CancellationToken ct = default);

    /// <summary>
    /// Draft → UblGenerated: NumberSequence'ten atomik belge numarası + ETTN atar,
    /// UBL XML üretir ve MediaFile'a yazar.
    /// </summary>
    Task<InvoiceDto> GenerateUblAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// UblGenerated → Queued. <paramref name="sendNow"/> true ise (dev/senkron akış) sürücüye
    /// hemen gönderir ve sonucu durum makinesine işler; aksi hâlde EDocumentDispatchJob gönderir.
    /// </summary>
    Task<InvoiceDto> SendAsync(long id, bool sendNow = true, CancellationToken ct = default);

    /// <summary>e-Arşiv iptal bildirimi (e-Faturada iptal yoktur; IADE belgesi kesilir).</summary>
    Task<InvoiceDto> CancelAsync(long id, InvoiceCancelRequest request, CancellationToken ct = default);

    Task<PagedResult<InvoiceListItemDto>> ListAsync(
        InvoiceStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);

    Task<InvoiceDto> GetAsync(long id, CancellationToken ct = default);

    /// <summary>Üretilmiş UBL XML akışı.</summary>
    Task<MediaDownload> OpenUblAsync(long id, CancellationToken ct = default);

    /// <summary>Entegratörden PDF çeker (ilk çağrıda indirilir ve MediaFile'a yazılır).</summary>
    Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default);

    /// <summary>Lokal GİB mükellef aynasından sorgu (e-Fatura mı e-Arşiv mi kararı).</summary>
    Task<GibTaxpayerDto> GetTaxpayerAsync(string vkn, CancellationToken ct = default);
}
