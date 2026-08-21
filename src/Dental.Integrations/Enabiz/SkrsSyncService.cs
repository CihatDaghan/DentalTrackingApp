using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Enabiz;

/// <summary>Tek bir SKRS kod satırı (sürücüden bağımsız biçim).</summary>
public sealed record SkrsCodeRow(string Code, string Name, string? ParentCode, bool IsActive);

/// <summary>SKRS kod sistemi başlığı.</summary>
public sealed record SkrsCodeSystemRow(Guid CodeSystemGuid, string Name, string? Description);

/// <summary>
/// SKRS (Sağlık Kodlama Referans Sunucusu) istemcisi.
///
/// <para>Sözleşme resmi dokümandan alınmıştır (<c>skrs.saglik.gov.tr/doc/index.html</c>):
/// <list type="bullet">
///   <item><c>GET /SkrsService/GetSkrsList?baslangicTarihi=yyyyMMdd</c> → aktif kod sistemi listesi.</item>
///   <item><c>GET /SkrsService/GetSkrsObject?skrsCodeSystemGuid={guid}&amp;page={n}</c> → kod listesi;
///         sayfa başına 1000 kayıt, başlangıç sayfası 1, yanıttaki <c>sonrakiSayfa</c> ile ilerlenir.</item>
///   <item>Kimlik HTTP header'ıyla: <c>KullaniciAdi</c>, <c>Sifre</c>, <c>UygulamaKodu</c>.</item>
///   <item>Yanıt zarfı: <c>{"durum": 1|2, "sonuc": ..., "mesaj": ...}</c> — 1 başarı, 2 hata.</item>
/// </list></para>
///
/// <para><b>Ölçüm:</b> uç kimliksiz de erişilebilirdir ve
/// <c>{"durum":2,"sonuc":"","mesaj":"Kullanıcı adı veya şifre hatalı!"}</c> döner — yani servis
/// ayakta, yalnız kimlik eksiktir. Kimlik yokken üst katman tohum listelere düşer.</para>
/// </summary>
public sealed class SkrsSyncService(
    HttpClient http,
    EnabizSettings settings,
    ILogger<SkrsSyncService> logger)
{
    /// <summary>Servis sayfa başına 1000 kayıt döner.</summary>
    public const int PageSize = 1000;

    /// <summary>Sonsuz döngüye karşı üst sınır (ICD-10 ~ 20 sayfa; 500 fazlasıyla yeter).</summary>
    private const int MaxPages = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public bool HasCredentials => settings.HasCredentials;

    /// <summary>Aktif kod sistemlerini listeler.</summary>
    public async Task<IReadOnlyList<SkrsCodeSystemRow>> GetCodeSystemsAsync(
        DateOnly? since = null, CancellationToken ct = default)
    {
        var start = (since ?? new DateOnly(2000, 1, 1)).ToString("yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture);

        var envelope = await GetAsync<List<CodeSystemDto>>(
            $"GetSkrsList?baslangicTarihi={start}", ct).ConfigureAwait(false);

        if (envelope is null) return [];

        return [.. envelope
            .Where(s => Guid.TryParse(s.Kodu, out _))
            .Select(s => new SkrsCodeSystemRow(Guid.Parse(s.Kodu!), s.Adi ?? s.Kodu!, s.Aciklama))];
    }

    /// <summary>Bir kod sisteminin tüm kodlarını sayfalayarak çeker.</summary>
    public async Task<IReadOnlyList<SkrsCodeRow>> GetCodesAsync(
        Guid codeSystemGuid, CancellationToken ct = default)
    {
        var rows = new List<SkrsCodeRow>();
        var page = 1;

        for (var guard = 0; guard < MaxPages; guard++)
        {
            ct.ThrowIfCancellationRequested();

            var envelope = await GetAsync<CodePageDto>(
                $"GetSkrsObject?skrsCodeSystemGuid={codeSystemGuid:D}&page={page}", ct).ConfigureAwait(false);

            // 'sonuc' bazı kod sistemlerinde 'sonuc', bazılarında 'kayit' dizisiyle gelir.
            var items = envelope?.Sonuc ?? envelope?.Kayit;
            if (items is not { Count: > 0 }) break;

            foreach (var item in items)
            {
                var code = item.Kodu ?? item.Kodu2 ?? item.Numarasi;
                if (string.IsNullOrWhiteSpace(code)) continue;

                rows.Add(new SkrsCodeRow(
                    code.Trim(),
                    (item.Adi ?? item.Adi2 ?? item.TurkceKarsiligi ?? code).Trim(),
                    string.IsNullOrWhiteSpace(item.UstKodu) ? null : item.UstKodu.Trim(),
                    item.Aktif ?? item.Aktif2 ?? true));
            }

            // sonrakiSayfa <= mevcut sayfa ise son sayfaya ulaşılmıştır.
            var next = envelope?.SonrakiSayfa ?? 0;
            if (next <= page) break;
            page = next;
        }

        logger.LogInformation("SKRS kod sistemi çekildi. Guid={Guid} Kayıt={Count}", codeSystemGuid, rows.Count);
        return rows;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{settings.SkrsBaseUrl.TrimEnd('/')}/{path}");

        request.Headers.TryAddWithoutValidation("KullaniciAdi", settings.UssUsername ?? "");
        request.Headers.TryAddWithoutValidation("Sifre", settings.UssPassword ?? "");
        request.Headers.TryAddWithoutValidation("UygulamaKodu", settings.ApplicationCode ?? "");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new Application.Abstractions.EnabizClientException(
                $"SKRS çağrısı başarısız ({path}): {ex.Message}", ex);
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new Application.Abstractions.EnabizClientException(
                    $"SKRS HTTP {(int)response.StatusCode} döndürdü ({path}).");
            }

            SkrsEnvelope<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<SkrsEnvelope<T>>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new Application.Abstractions.EnabizClientException(
                    $"SKRS yanıtı çözümlenemedi ({path}): {ex.Message}", ex);
            }

            // durum: 1 başarı, 2 hata (kimlik hatası dahil).
            if (envelope is null || envelope.Durum != 1)
            {
                throw new Application.Abstractions.EnabizClientException(
                    $"SKRS isteği reddedildi ({path}): {envelope?.Mesaj ?? "bilinmeyen hata"}");
            }

            return envelope.Sonuc;
        }
    }

    // ---- Yanıt sözleşmesi ----

    private sealed class SkrsEnvelope<T>
    {
        [JsonPropertyName("durum")] public int Durum { get; set; }
        [JsonPropertyName("sonuc")] public T? Sonuc { get; set; }
        [JsonPropertyName("mesaj")] public string? Mesaj { get; set; }
    }

    private sealed class CodeSystemDto
    {
        [JsonPropertyName("kodu")] public string? Kodu { get; set; }
        [JsonPropertyName("adi")] public string? Adi { get; set; }
        [JsonPropertyName("aciklama")] public string? Aciklama { get; set; }
    }

    private sealed class CodePageDto
    {
        [JsonPropertyName("sonuc")] public List<CodeDto>? Sonuc { get; set; }
        [JsonPropertyName("kayit")] public List<CodeDto>? Kayit { get; set; }
        [JsonPropertyName("sonrakiSayfa")] public int? SonrakiSayfa { get; set; }
    }

    /// <summary>Kod satırı; SKRS aynı alanı kod sistemine göre farklı adla döndürebiliyor.</summary>
    private sealed class CodeDto
    {
        [JsonPropertyName("kodu")] public string? Kodu { get; set; }
        [JsonPropertyName("KODU")] public string? Kodu2 { get; set; }
        [JsonPropertyName("NUMARASI")] public string? Numarasi { get; set; }
        [JsonPropertyName("adi")] public string? Adi { get; set; }
        [JsonPropertyName("ADI")] public string? Adi2 { get; set; }
        [JsonPropertyName("TURKCEKARSILIGI")] public string? TurkceKarsiligi { get; set; }
        [JsonPropertyName("USTKODU")] public string? UstKodu { get; set; }
        [JsonPropertyName("aktif")] public bool? Aktif { get; set; }
        [JsonPropertyName("AKTIF")] public bool? Aktif2 { get; set; }
    }
}
