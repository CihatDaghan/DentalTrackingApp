namespace Dental.Application.Abstractions;

/// <summary>TenantIntegrationSettings kaydının çözülmüş (şifresi açılmış) hâli.</summary>
/// <param name="ProviderKey">'uyumsoft' | 'nes' | 'netgsm' | 'meta' | 'iyzico' | 'fake'</param>
/// <param name="Environment">'Test' | 'Live'</param>
/// <param name="SettingsJson">Data Protection ile çözülmüş ayar JSON'u; kayıt yoksa/boşsa null.</param>
public sealed record IntegrationSettingsSnapshot(
    string ProviderKey,
    string Environment,
    string? SettingsJson,
    bool IsEnabled);

/// <summary>
/// Tenant'ın entegrasyon ayarlarını okuyup şifresini çözen depo.
/// Infrastructure'da AppDbContext + Data Protection üzerinden uygulanır; sürücü paketi DB bilmez.
/// </summary>
public interface IIntegrationSettingsStore
{
    /// <param name="integrationKey">'EInvoice' | 'Sms' | 'WhatsApp' | 'Payment' | 'Enabiz'</param>
    Task<IntegrationSettingsSnapshot?> GetAsync(long tenantId, string integrationKey, CancellationToken ct = default);

    /// <summary>Ayar JSON'unu Data Protection ile şifreleyip kaydeder (seed/ayar ekranı için).</summary>
    Task UpsertAsync(
        long tenantId,
        string integrationKey,
        string providerKey,
        string environment,
        string settingsJson,
        bool isEnabled = true,
        CancellationToken ct = default);
}

/// <summary>Çözümlenen sürücü + hangi ayarla çözüldüğü (loglama ve Invoice.IntegratorProvider için).</summary>
public sealed record ResolvedProvider<T>(T Instance, string ProviderKey, string Environment) where T : notnull;

/// <summary>
/// Keyed DI üzerinden tenant'ın seçtiği sürücüyü döndürür.
/// Kayıt yoksa (veya devre dışıysa) config'teki varsayılan sürücüye düşer
/// (Integrations:DefaultEInvoiceProvider / DefaultSmsProvider ...).
/// </summary>
public interface IIntegrationProviderFactory
{
    Task<ResolvedProvider<T>> ResolveAsync<T>(long tenantId, CancellationToken ct = default) where T : notnull;
}

/// <summary>
/// Tenant ayarıyla çalışma zamanında doldurulan sürücü ayar sınıfı.
/// Factory, keyed servisi çözmeden ÖNCE aynı scope'taki ayar nesnesini doldurur.
/// </summary>
public interface IIntegrationSettings
{
    /// <summary>Şifresi çözülmüş ayar JSON'u + ortam (Test/Live) uygulanır; JSON null olabilir.</summary>
    void Apply(string? settingsJson, string environment);
}
