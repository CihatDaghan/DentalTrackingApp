using Dental.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Common;

/// <summary>
/// Keyed DI tabanlı sürücü seçimi (design-2 §1.0).
///
/// Akış: tenant'ın <c>TenantIntegrationSettings</c> kaydı okunur → Data Protection ile ayar JSON'u
/// çözülür → AYNI SCOPE'taki ayar nesnesi (<see cref="IIntegrationSettings"/>) doldurulur →
/// keyed servis çözülür. Kayıt yoksa/devre dışıysa config'teki varsayılan sürücüye düşülür.
///
/// Ayar nesneleri scoped kaydedilir; bu yüzden doldurulan örnek, aynı scope'ta oluşturulan
/// sürücünün ctor'una geçen örnekle aynıdır.
/// </summary>
public sealed class IntegrationProviderFactory(
    IServiceProvider services,
    IIntegrationSettingsStore store,
    IntegrationProviderOptions options,
    ILogger<IntegrationProviderFactory> logger) : IIntegrationProviderFactory
{
    public async Task<ResolvedProvider<T>> ResolveAsync<T>(long tenantId, CancellationToken ct = default)
        where T : notnull
    {
        var registration = options.RequireRegistration(typeof(T));
        var snapshot = await store.GetAsync(tenantId, registration.IntegrationKey, ct).ConfigureAwait(false);

        string providerKey;
        string environment;
        string? settingsJson;

        // Ortam düzeyi zorlama (Integrations:ProviderOverride:{IntegrationKey}): kiracı ayarını ezer.
        // Testler ve olay müdahalesi (bir entegratör çöktüğünde tümünü fake/yedeğe alma) içindir.
        if (registration.ForcedProviderKey is { } forced)
        {
            providerKey = forced;
            environment = snapshot?.Environment ?? "Test";
            settingsJson = string.Equals(forced, snapshot?.ProviderKey, StringComparison.OrdinalIgnoreCase)
                ? snapshot?.SettingsJson
                : null;
        }
        else if (snapshot is { IsEnabled: true } && options.IsKnownProvider(typeof(T), snapshot.ProviderKey))
        {
            providerKey = snapshot.ProviderKey;
            environment = snapshot.Environment;
            settingsJson = snapshot.SettingsJson;
        }
        else
        {
            providerKey = registration.DefaultProviderKey;
            environment = snapshot?.Environment ?? "Test";
            settingsJson = null;
            if (snapshot is not null)
            {
                logger.LogWarning(
                    "Tenant {TenantId} için {Key} sürücüsü '{Provider}' kullanılamadı (kayıtlı değil ya da kapalı); " +
                    "varsayılan '{Default}' kullanılıyor.",
                    tenantId, registration.IntegrationKey, snapshot.ProviderKey, providerKey);
            }
        }

        // Ayar nesnesi sürücüden ÖNCE doldurulur (sürücü ctor'da ayarı okuyabilir).
        if (registration.SettingsTypes.TryGetValue(providerKey, out var settingsType) &&
            services.GetService(settingsType) is IIntegrationSettings settings)
        {
            settings.Apply(settingsJson, environment);
        }

        var instance = services.GetRequiredKeyedService<T>(providerKey);
        return new ResolvedProvider<T>(instance, providerKey, environment);
    }
}

/// <summary>Port tipi → entegrasyon anahtarı, varsayılan sürücü ve sürücü başına ayar tipi eşlemesi.</summary>
public sealed class IntegrationProviderOptions
{
    private readonly Dictionary<Type, PortRegistration> _ports = [];

    public void Register<T>(string integrationKey, string defaultProviderKey,
        IReadOnlyDictionary<string, Type>? settingsTypes = null, IEnumerable<string>? providerKeys = null,
        string? forcedProviderKey = null)
        where T : notnull
    {
        var keys = providerKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [.. (settingsTypes?.Keys ?? []), defaultProviderKey];
        keys.Add(defaultProviderKey);
        _ports[typeof(T)] = new PortRegistration(
            integrationKey,
            defaultProviderKey,
            settingsTypes ?? new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase),
            keys,
            string.IsNullOrWhiteSpace(forcedProviderKey) ? null : forcedProviderKey);
    }

    public PortRegistration RequireRegistration(Type portType) =>
        _ports.TryGetValue(portType, out var registration)
            ? registration
            : throw new InvalidOperationException(
                $"{portType.Name} için entegrasyon sürücüsü kaydı yok (AddIntegrationDrivers eksik).");

    public bool IsKnownProvider(Type portType, string providerKey) =>
        _ports.TryGetValue(portType, out var registration) && registration.ProviderKeys.Contains(providerKey);

    public sealed record PortRegistration(
        string IntegrationKey,
        string DefaultProviderKey,
        IReadOnlyDictionary<string, Type> SettingsTypes,
        HashSet<string> ProviderKeys,
        string? ForcedProviderKey);
}
