using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Dental.Infrastructure.Enabiz;

/// <summary>
/// Kiracının e-Nabız modunu ve sistem düzeyi KTS tescil bayrağını çözer.
///
/// <para><b>Live moda geçiş iki anahtarlıdır:</b> kiracı ayarında <see cref="EnabizMode.Live"/>
/// seçilmiş olması TEK BAŞINA yetmez; sistem düzeyi <c>Integrations:Enabiz:KtsRegistered</c>
/// bayrağı da açık olmalıdır. Bayrak kapalıyken Live isteği <see cref="EnabizMode.TestOnly"/>'ye
/// düşürülür — çünkü KTS'de tescili olmayan yazılımın canlıya veri göndermesi mevzuata aykırıdır
/// (SBYS Yönetmeliği, RG 25.08.2022) ve tesisi de yaptırıma açar.</para>
/// </summary>
public sealed class EnabizModeResolver(IIntegrationSettingsStore store, IConfiguration configuration)
{
    public const string IntegrationKey = "Enabiz";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Ürünün KTS/DHBS tescili tamamlandı mı (süper admin bayrağı).</summary>
    public bool KtsRegistered => configuration.GetValue("Integrations:Enabiz:KtsRegistered", false);

    /// <summary>
    /// Klinik iş akışından paket tetiklemesi açık mı (<c>Integrations:Enabiz:Trigger</c> = off ile
    /// kapatılır). Test ortamında tetiklemeyi gözlemeden kapatabilmek içindir.
    /// </summary>
    public bool TriggerEnabled =>
        !string.Equals(configuration["Integrations:Enabiz:Trigger"], "off", StringComparison.OrdinalIgnoreCase);

    public async Task<EnabizModeSnapshot> ResolveAsync(long tenantId, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(tenantId, IntegrationKey, ct);

        var settings = Parse(snapshot?.SettingsJson);

        // Ayar kaydı yoksa entegrasyon KAPALIDIR. e-Nabız gönderimi ÇKYS kodu ve USS kimliği
        // olmadan zaten yapılamaz; ayar girmemiş bir kiracıda sessizce paket biriktirmek yerine
        // açıkça kapalı kalmak doğrudur (mod ayar ekranından bilinçli olarak Held'e alınır).
        var requested = snapshot is null || !snapshot.IsEnabled
            ? EnabizMode.Disabled
            : settings?.Mode ?? EnabizMode.Held;

        var effective = requested == EnabizMode.Live && !KtsRegistered
            ? EnabizMode.TestOnly
            : requested;

        return new EnabizModeSnapshot(
            effective,
            requested,
            KtsRegistered,
            settings?.CkysCode,
            settings?.UssUsername,
            !string.IsNullOrWhiteSpace(settings?.UssPassword),
            settings?.ApplicationCode,
            snapshot?.ProviderKey,
            snapshot?.Environment ?? "Test");
    }

    private static EnabizTenantSettings? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<EnabizTenantSettings>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <param name="Mode">Uygulanacak mod (KTS bayrağıyla kısıtlanmış hâli).</param>
/// <param name="RequestedMode">Kiracının ayarındaki ham mod.</param>
public sealed record EnabizModeSnapshot(
    EnabizMode Mode,
    EnabizMode RequestedMode,
    bool KtsRegistered,
    string? CkysCode,
    string? UssUsername,
    bool HasPassword,
    string? ApplicationCode,
    string? ProviderKey,
    string Environment)
{
    /// <summary>Paket üretilecek mi (Disabled dışındaki tüm modlarda evet).</summary>
    public bool ShouldProduce => Mode != EnabizMode.Disabled;

    /// <summary>Gönderim yapılacak mı (Held'de paket üretilir ama gönderilmez).</summary>
    public bool ShouldSend => Mode is EnabizMode.TestOnly or EnabizMode.Live;
}

/// <summary>TenantIntegrationSettings içindeki şifreli JSON'un şeması.</summary>
public sealed record EnabizTenantSettings(
    string? CkysCode,
    string? UssUsername,
    string? UssPassword,
    string? ApplicationCode,
    EnabizMode? Mode);
