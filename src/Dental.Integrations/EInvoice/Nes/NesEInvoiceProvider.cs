using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dental.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.EInvoice.Nes;

/// <summary>
/// NES REST + JWT e-belge sürücüsü. İkinci sürücü olarak arayüzün SOAP'a yamulmasını engeller:
/// aynı <see cref="IEInvoiceProvider"/> portu multipart/JSON bir sağlayıcıyla da karşılanabiliyor.
///
/// KAPSAM NOTU: NES test hesabı başvuru formu gerektirdiğinden bu sürücü CANLI DOĞRULANMADI.
/// Uç adresleri ve alan adları NES geliştirici dokümanındaki (developertest.nes.com.tr/docs)
/// şekle göre yazıldı; hesap açıldığında sözleşme doğrulanmalı ve gerekirse eşleme düzeltilmelidir.
/// Kod yolu tamdır (NotImplementedException yoktur) — hesapsız çalıştırıldığında ağ/yetki hatası verir.
/// </summary>
public sealed class NesEInvoiceProvider(
    HttpClient http,
    NesSettings settings,
    ILogger<NesEInvoiceProvider> logger) : IEInvoiceProvider
{
    private string? _accessToken;
    private DateTime _accessTokenExpiresUtc = DateTime.MinValue;

    // ---- IEInvoiceProvider ----

    public async Task<EDocumentSendResult> SendDocumentAsync(
        EDocumentEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var content = new MultipartFormDataContent();
        var xml = new ByteArrayContent(Encoding.UTF8.GetBytes(envelope.UblXml));
        xml.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        content.Add(xml, "file", $"{envelope.Ettn}.xml");
        content.Add(new StringContent(MapDocumentType(envelope.DocType)), "documentType");
        content.Add(new StringContent(envelope.Ettn), "uuid");
        if (!string.IsNullOrWhiteSpace(envelope.TargetAlias))
            content.Add(new StringContent(envelope.TargetAlias), "receiverAlias");
        if (envelope.DocType == EDocType.EArchive)
            content.Add(new StringContent(envelope.SendMode == EDocSendMode.Kagit ? "PAPER" : "ELECTRONIC"), "sendType");

        using var request = new HttpRequestMessage(HttpMethod.Post, Url("v1/uploads/document")) { Content = content };
        using var response = await SendAuthorizedAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("NES belge reddetti. Ettn={Ettn} Http={Status} Gövde={Body}",
                envelope.Ettn, (int)response.StatusCode, Truncate(body));
            return new EDocumentSendResult(false, Error: ReadErrorMessage(body) ?? $"HTTP {(int)response.StatusCode}");
        }

        var uploadId = ReadString(body, "id") ?? ReadString(body, "uuid") ?? envelope.Ettn;
        logger.LogInformation("NES belge kabul etti. Ettn={Ettn} Ref={Ref}", envelope.Ettn, uploadId);
        return new EDocumentSendResult(true, uploadId);
    }

    public async Task<EDocumentStatusResult> GetStatusAsync(
        string documentId, EDocType type, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        using var request = new HttpRequestMessage(HttpMethod.Get, Url($"v1/documents/{documentId}/status"));
        using var response = await SendAuthorizedAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new EDocumentStatusResult(EDocProviderStatus.NotFound, "NES: belge bulunamadı.", Truncate(body));

        if (!response.IsSuccessStatusCode)
            return new EDocumentStatusResult(EDocProviderStatus.Unknown, ReadErrorMessage(body), Truncate(body));

        var status = ReadString(body, "status") ?? "";
        return new EDocumentStatusResult(MapStatus(status), ReadString(body, "description") ?? status, Truncate(body));
    }

    public async Task<byte[]> GetPdfAsync(string documentId, EDocType type, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        using var request = new HttpRequestMessage(HttpMethod.Get, Url($"v1/documents/{documentId}/pdf"));
        using var response = await SendAuthorizedAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new EInvoiceProviderException(ReadErrorMessage(body) ?? $"NES PDF hatası: HTTP {(int)response.StatusCode}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    public async Task<Stream> DownloadGibUserListAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("v1/definitions/gib-users"));
        using var response = await SendAuthorizedAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new EInvoiceProviderException(
                ReadErrorMessage(body) ?? $"NES mükellef listesi hatası: HTTP {(int)response.StatusCode}");
        }

        // Akış çağrıya devredilir; HttpResponseMessage dispose olduğunda içerik kaybolmasın diye kopyalanır.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }

    public async Task CancelEArchiveAsync(string documentId, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        using var request = new HttpRequestMessage(HttpMethod.Post, Url($"v1/documents/{documentId}/cancel"))
        {
            Content = JsonContent.Create(new { reason, cancelDate = DateTime.UtcNow }),
        };
        using var response = await SendAuthorizedAsync(request, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new EInvoiceProviderException(
            ReadErrorMessage(body) ?? $"NES iptal hatası: HTTP {(int)response.StatusCode}");
    }

    /// <summary>
    /// Taban adres tenant ayarından geldiği için HttpClient.BaseAddress kullanılmaz
    /// (typed client yapılandırması kök kapsamda çalışır, tenant ayarını göremez).
    /// </summary>
    private Uri Url(string path) => new(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), path);

    // ---- JWT ----

    /// <summary>Token süresi dolduysa yeniler; 401'de bir kez daha dener (token iptal edilmiş olabilir).</summary>
    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new EInvoiceProviderException($"NES isteği başarısız: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new EInvoiceProviderException("NES isteği zaman aşımına uğradı.", ex);
        }

        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        _accessToken = null;
        var refreshed = await GetAccessTokenAsync(ct).ConfigureAwait(false);
        using var retry = CloneRequest(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await http.SendAsync(retry, ct).ConfigureAwait(false);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTime.UtcNow < _accessTokenExpiresUtc)
            return _accessToken;

        using var response = await http.PostAsJsonAsync(Url("auth/v1/login"),
            new { userName = settings.Username, password = settings.Password }, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new EInvoiceProviderException(
                ReadErrorMessage(body) ?? $"NES oturum açma başarısız: HTTP {(int)response.StatusCode}");

        var token = ReadString(body, "token") ?? ReadString(body, "access_token")
            ?? throw new EInvoiceProviderException("NES oturum yanıtında token yok.");

        // Süre bildirilmezse temkinli bir varsayılan kullanılır; 401'de zaten yenilenir.
        var expiresIn = ReadInt(body, "expiresIn") ?? 3600;
        _accessToken = token;
        _accessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        return token;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri) { Content = source.Content };
        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }

    // ---- Eşlemeler ----

    private static string MapDocumentType(EDocType type) => type switch
    {
        EDocType.EInvoice => "EINVOICE",
        EDocType.EArchive => "EARCHIVE",
        EDocType.ESmm => "ESMM",
        _ => "EINVOICE",
    };

    internal static EDocProviderStatus MapStatus(string status) => status.ToUpperInvariant() switch
    {
        "QUEUED" or "WAITING" or "PENDING" => EDocProviderStatus.Queued,
        "PROCESSING" or "SENT" or "SENDING" => EDocProviderStatus.Processing,
        "SUCCEED" or "SUCCEEDED" or "APPROVED" or "COMPLETED" => EDocProviderStatus.Succeeded,
        "REJECTED" or "DECLINED" => EDocProviderStatus.BuyerRejected,
        "FAILED" or "ERROR" or "GIB_REJECTED" => EDocProviderStatus.GibRejected,
        "CANCELED" or "CANCELLED" => EDocProviderStatus.Cancelled,
        "NOTFOUND" => EDocProviderStatus.NotFound,
        _ => EDocProviderStatus.Unknown,
    };

    // ---- JSON yardımcıları (şema doğrulanana kadar esnek okuma) ----

    private static string? ReadString(string json, string property) =>
        TryGet(json, property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static int? ReadInt(string json, string property) =>
        TryGet(json, property, out var element) && element.TryGetInt32(out var value) ? value : null;

    private static string? ReadErrorMessage(string json) =>
        ReadString(json, "message") ?? ReadString(json, "error") ?? ReadString(json, "detail");

    private static bool TryGet(string json, string property, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(property, out var found)) return false;
            element = found.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
