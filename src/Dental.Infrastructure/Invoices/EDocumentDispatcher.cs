using System.IO.Compression;
using System.Text;
using Dental.Application.Abstractions;
using Dental.Application.Invoices;
using Dental.Application.Media;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Invoices;

/// <summary>
/// Gönderim/durum yoklama motoru.
///
/// Yeniden deneme politikası (design-2 §1.0-b): geçici hatada AttemptCount artar ve
/// NextAttemptAtUtc 1dk → 5dk → 30dk → 2sa → 12sa şeklinde uzar; 6. denemeden sonra belge
/// <see cref="InvoiceStatus.ManualReview"/> kuyruğuna düşer. İş reddi (entegratör "kabul etmedi")
/// yeniden denenmez — doğrudan GibRejected'a gider.
/// </summary>
public sealed class EDocumentDispatcher(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IMediaService media,
    IIntegrationProviderFactory providerFactory,
    Application.Notifications.INotificationService notifications,
    ILogger<EDocumentDispatcher> logger) : IEDocumentDispatcher
{
    /// <summary>Artan yeniden deneme aralıkları; son eleman aşıldığında ManualReview'a düşülür.</summary>
    internal static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    ];

    public const int MaxAttempts = 6;

    public async Task<InvoiceStatus> DispatchAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new KeyNotFoundException("Fatura bulunamadı.");

        if (invoice.Status is not (InvoiceStatus.Queued or InvoiceStatus.Error))
        {
            logger.LogDebug("Gönderim atlandı, durum uygun değil. Id={Id} Durum={Status}", invoice.Id, invoice.Status);
            return invoice.Status;
        }

        if (invoice.UblFileId is not { } ublFileId)
        {
            Fail(invoice, "Belgenin UBL dosyası yok; önce UBL üretilmelidir.", permanent: true);
            await db.SaveChangesAsync(ct);
            await NotifyErrorAsync(invoice, ct);
            return invoice.Status;
        }

        var tenantId = RequireTenantId();
        var resolved = await providerFactory.ResolveAsync<IEInvoiceProvider>(tenantId, ct);
        invoice.IntegratorProvider = MapProvider(resolved.ProviderKey);

        string ublXml;
        var download = await media.OpenDownloadAsync(ublFileId, ct);
        await using (download.Content)
        using (var reader = new StreamReader(download.Content, Encoding.UTF8))
        {
            ublXml = await reader.ReadToEndAsync(ct);
        }

        var envelope = new EDocumentEnvelope(
            InvoiceService.MapDocType(invoice.DocumentKind),
            ublXml,
            invoice.Ettn?.ToString("D") ?? throw new InvalidOperationException("Belgenin ETTN'si yok."),
            invoice.DocumentKind == InvoiceDocumentKind.EFatura ? invoice.BuyerAlias : null,
            EDocSendMode.Elektronik);

        EDocumentSendResult result;
        try
        {
            result = await resolved.Instance.SendDocumentAsync(envelope, ct);
        }
        catch (Exception ex) when (ex is EInvoiceProviderException or HttpRequestException or TaskCanceledException)
        {
            // Taşıma/altyapı hatası → geçici kabul edilir, artan aralıkla yeniden denenir.
            logger.LogWarning(ex, "e-belge gönderimi başarısız (geçici). Id={Id} Sürücü={Provider}",
                invoice.Id, resolved.ProviderKey);
            Fail(invoice, ex.Message, permanent: false);
            await db.SaveChangesAsync(ct);
            await NotifyErrorAsync(invoice, ct);
            return invoice.Status;
        }

        if (!result.Success)
        {
            // Entegratör iş kuralıyla reddetti (UBL hatası, mükellef bulunamadı...): yeniden denemek anlamsız.
            logger.LogWarning("e-belge entegratörce reddedildi. Id={Id} Hata={Error}", invoice.Id, result.Error);
            invoice.ErrorMessage = Truncate(result.Error);
            Transition(invoice, InvoiceStatus.GibRejected, result.Error);
            await db.SaveChangesAsync(ct);
            await NotifyErrorAsync(invoice, ct);
            return invoice.Status;
        }

        invoice.IntegratorRefId = result.ProviderRef;
        invoice.ErrorMessage = null;
        invoice.NextAttemptAtUtc = null;
        Transition(invoice, InvoiceStatus.SentToIntegrator, $"Entegratör referansı: {result.ProviderRef}");
        await db.SaveChangesAsync(ct);

        logger.LogInformation("e-belge gönderildi. Id={Id} No={No} Ref={Ref} Sürücü={Provider}",
            invoice.Id, invoice.InvoiceNumber, result.ProviderRef, resolved.ProviderKey);
        return invoice.Status;
    }

    public async Task<int> DispatchQueuedAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var ids = await db.Invoices.AsNoTracking()
            .Where(i => (i.Status == InvoiceStatus.Queued && (i.NextAttemptAtUtc == null || i.NextAttemptAtUtc <= now))
                     || (i.Status == InvoiceStatus.Error && i.NextAttemptAtUtc != null && i.NextAttemptAtUtc <= now))
            .OrderBy(i => i.Id)
            .Take(batchSize)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await DispatchAsync(id, ct);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tek belgenin hatası kuyruğun tamamını durdurmasın.
                logger.LogError(ex, "Kuyruk gönderiminde beklenmeyen hata. Id={Id}", id);
                db.ChangeTracker.Clear();
            }
        }

        return processed;
    }

    public async Task<int> PollStatusesAsync(int batchSize = 100, CancellationToken ct = default)
    {
        var pending = await db.Invoices
            .Where(i => (i.Status == InvoiceStatus.SentToIntegrator || i.Status == InvoiceStatus.GibProcessing)
                        && i.IntegratorRefId != null)
            .OrderBy(i => i.LastStatusCheckUtc ?? DateTime.MinValue)
            .Take(batchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        var tenantId = RequireTenantId();
        var resolved = await providerFactory.ResolveAsync<IEInvoiceProvider>(tenantId, ct);
        var updated = 0;

        foreach (var invoice in pending)
        {
            ct.ThrowIfCancellationRequested();
            EDocumentStatusResult status;
            try
            {
                status = await resolved.Instance.GetStatusAsync(
                    invoice.IntegratorRefId!, InvoiceService.MapDocType(invoice.DocumentKind), ct);
            }
            catch (Exception ex) when (ex is EInvoiceProviderException or HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "Durum sorgusu başarısız. Id={Id}", invoice.Id);
                invoice.LastStatusCheckUtc = clock.UtcNow;
                continue;
            }

            invoice.LastStatusCheckUtc = clock.UtcNow;
            var next = MapStatus(status.Status, invoice.Status);
            if (next == invoice.Status) continue;

            if (next is InvoiceStatus.GibRejected or InvoiceStatus.BuyerRejected)
                invoice.ErrorMessage = Truncate(status.StatusDetail);

            Transition(invoice, next, status.StatusDetail ?? status.RawResponse);
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return updated;
    }

    public async Task<int> SyncGibTaxpayersAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var resolved = await providerFactory.ResolveAsync<IEInvoiceProvider>(tenantId, ct);

        Stream stream;
        try
        {
            stream = await resolved.Instance.DownloadGibUserListAsync(ct);
        }
        catch (Exception ex) when (ex is EInvoiceProviderException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "GİB mükellef listesi indirilemedi. Sürücü={Provider}", resolved.ProviderKey);
            return 0;
        }

        await using (stream)
        {
            var rows = ReadTaxpayerRows(stream);
            if (rows.Count == 0) return 0;

            var now = clock.UtcNow;
            var identifiers = rows.Select(r => r.Vkn).ToList();
            var existing = await db.GibTaxpayers
                .Where(g => identifiers.Contains(g.Vkn))
                .ToDictionaryAsync(g => g.Vkn, ct);

            foreach (var row in rows)
            {
                if (existing.TryGetValue(row.Vkn, out var taxpayer))
                {
                    taxpayer.Title = row.Title ?? taxpayer.Title;
                    taxpayer.Alias = row.Alias ?? taxpayer.Alias;
                    taxpayer.AccountType = row.AccountType ?? taxpayer.AccountType;
                    taxpayer.LastSyncUtc = now;
                }
                else
                {
                    db.GibTaxpayers.Add(new GibTaxpayer
                    {
                        Vkn = row.Vkn,
                        Title = row.Title,
                        Alias = row.Alias,
                        AccountType = row.AccountType,
                        FirstSeenUtc = now,
                        LastSyncUtc = now,
                    });
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("GİB mükellef aynası güncellendi. Kayıt={Count} Sürücü={Provider}",
                rows.Count, resolved.ProviderKey);
            return rows.Count;
        }
    }

    // ---- Ayrıştırma ----

    /// <summary>
    /// Entegratörler listeyi ya zip içinde CSV ya da düz CSV olarak verir; ikisi de desteklenir.
    /// Ayraç ';' veya '\t'; ilk kolon VKN/TCKN, ikinci unvan, üçüncü etiket, dördüncü etiket tipi.
    /// </summary>
    internal static List<TaxpayerRow> ReadTaxpayerRows(Stream stream)
    {
        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var rows = new List<TaxpayerRow>();
        if (IsZip(buffer))
        {
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
                ReadCsv(entryStream, rows);
            }
        }
        else
        {
            buffer.Position = 0;
            ReadCsv(buffer, rows);
        }

        return [.. rows.GroupBy(r => r.Vkn).Select(g => g.First())];
    }

    private static bool IsZip(MemoryStream stream)
    {
        if (stream.Length < 4) return false;
        var header = stream.GetBuffer();
        var isZip = header[0] == 0x50 && header[1] == 0x4B;
        stream.Position = 0;
        return isZip;
    }

    private static void ReadCsv(Stream stream, List<TaxpayerRow> rows)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split([';', '\t'], StringSplitOptions.TrimEntries);
            var identifier = parts[0];
            // Başlık satırını ve bozuk satırları atla: kimlik 10 veya 11 haneli rakam olmalı.
            if (identifier.Length is not (10 or 11) || !identifier.All(char.IsAsciiDigit)) continue;

            rows.Add(new TaxpayerRow(
                identifier,
                parts.Length > 1 ? NullIfEmpty(parts[1]) : null,
                parts.Length > 2 ? NullIfEmpty(parts[2]) : null,
                parts.Length > 3 ? NullIfEmpty(parts[3]) : null));
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    internal sealed record TaxpayerRow(string Vkn, string? Title, string? Alias, string? AccountType);

    // ---- Durum makinesi ----

    private void Fail(Invoice invoice, string? message, bool permanent)
    {
        invoice.ErrorMessage = Truncate(message);
        invoice.AttemptCount++;

        if (permanent || invoice.AttemptCount >= MaxAttempts)
        {
            invoice.NextAttemptAtUtc = null;
            Transition(invoice, InvoiceStatus.ManualReview,
                $"{invoice.AttemptCount}. denemeden sonra elle incelemeye alındı: {message}");
            return;
        }

        var backoff = RetryBackoff[Math.Min(invoice.AttemptCount - 1, RetryBackoff.Length - 1)];
        invoice.NextAttemptAtUtc = clock.UtcNow.Add(backoff);
        Transition(invoice, InvoiceStatus.Error,
            $"{invoice.AttemptCount}. deneme başarısız; sonraki deneme " +
            $"{invoice.NextAttemptAtUtc:yyyy-MM-dd HH:mm} UTC: {message}");
    }

    /// <summary>Sağlayıcı durumunu belge durum makinesine çevirir; bilinmeyen durum mevcut durumu korur.</summary>
    internal static InvoiceStatus MapStatus(EDocProviderStatus status, InvoiceStatus current) => status switch
    {
        EDocProviderStatus.Queued => InvoiceStatus.SentToIntegrator,
        EDocProviderStatus.Processing => InvoiceStatus.GibProcessing,
        EDocProviderStatus.Succeeded => InvoiceStatus.Succeeded,
        EDocProviderStatus.GibRejected => InvoiceStatus.GibRejected,
        EDocProviderStatus.BuyerRejected => InvoiceStatus.BuyerRejected,
        EDocProviderStatus.Cancelled => InvoiceStatus.Cancelled,
        // NotFound/Unknown: entegratör henüz işlememiş olabilir; durum korunur, bir sonraki turda tekrar bakılır.
        _ => current,
    };

    internal static IntegratorProvider MapProvider(string providerKey) => providerKey.ToLowerInvariant() switch
    {
        "uyumsoft" => IntegratorProvider.Uyumsoft,
        "nes" => IntegratorProvider.Nes,
        "turkcell" => IntegratorProvider.TurkcellEsirket,
        "izibiz" => IntegratorProvider.Izibiz,
        _ => IntegratorProvider.Fake,
    };

    private void Transition(Invoice invoice, InvoiceStatus to, string? detail)
    {
        db.InvoiceStatusLogs.Add(new InvoiceStatusLog
        {
            TenantId = invoice.TenantId,
            InvoiceId = invoice.Id,
            FromStatus = invoice.Status,
            ToStatus = to,
            AtUtc = clock.UtcNow,
            ActorUserId = tenant.UserId,
            IntegratorRawResponse = detail,
        });
        invoice.Status = to;
    }

    /// <summary>
    /// Hata kuyruğuna düşen belge için uygulama içi bildirim üretir (topbar zili).
    /// Bildirim yan etkidir: <c>PublishAsync</c> hiçbir koşulda gönderim akışını patlatmaz.
    /// </summary>
    private Task NotifyErrorAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.Status is not (InvoiceStatus.Error or InvoiceStatus.ManualReview
            or InvoiceStatus.GibRejected or InvoiceStatus.BuyerRejected))
            return Task.CompletedTask;

        return notifications.PublishAsync(new Application.Notifications.NotificationCreateRequest(
            Application.Notifications.NotificationEvents.EInvoiceError,
            "e-Belge hatası",
            $"{invoice.InvoiceNumber ?? "Taslak belge"} — {invoice.ErrorMessage ?? invoice.Status.ToString()}",
            LinkPath: $"/invoices/{invoice.Id}",
            TenantId: invoice.TenantId), ct);
    }

    private long RequireTenantId() => tenant.TenantId
        ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan e-belge gönderimi yapılamaz.");

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= 2000 ? value : value[..2000];
}
