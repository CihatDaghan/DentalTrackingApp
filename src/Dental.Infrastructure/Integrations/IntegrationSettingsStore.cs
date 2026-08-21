using Dental.Application.Abstractions;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Integrations;

/// <summary>
/// TenantIntegrationSettings okuma/yazma + ASP.NET Data Protection ile sır şifreleme.
/// Job'lar tenant scope'u dışında da çağırabildiği için sorgu global filtreyi atlar ve
/// TenantId'yi AÇIKÇA filtreler (yanlış kiracıya sızmayı imkânsız kılan tek yol).
/// </summary>
public sealed class IntegrationSettingsStore(
    AppDbContext db,
    IDataProtectionProvider protectionProvider,
    ILogger<IntegrationSettingsStore> logger) : IIntegrationSettingsStore
{
    private const string Purpose = "Dental.TenantIntegrationSettings.v1";

    private readonly IDataProtector _protector = protectionProvider.CreateProtector(Purpose);

    public async Task<IntegrationSettingsSnapshot?> GetAsync(
        long tenantId, string integrationKey, CancellationToken ct = default)
    {
        var setting = await db.TenantIntegrationSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId && s.IntegrationKey == integrationKey && !s.IsDeleted, ct);

        if (setting is null) return null;

        string? json = null;
        if (!string.IsNullOrWhiteSpace(setting.SettingsJsonEncrypted))
        {
            try
            {
                json = _protector.Unprotect(setting.SettingsJsonEncrypted);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                // Anahtar halkası değişmişse ayar okunamaz; sürücü varsayılana düşsün diye kayıt devre dışı sayılır.
                logger.LogError(ex,
                    "Tenant {TenantId} {Key} entegrasyon ayarı çözülemedi (Data Protection anahtarı değişmiş olabilir).",
                    tenantId, integrationKey);
                return new IntegrationSettingsSnapshot(setting.ProviderKey, setting.Environment, null, IsEnabled: false);
            }
        }

        return new IntegrationSettingsSnapshot(setting.ProviderKey, setting.Environment, json, setting.IsEnabled);
    }

    public async Task UpsertAsync(
        long tenantId,
        string integrationKey,
        string providerKey,
        string environment,
        string settingsJson,
        bool isEnabled = true,
        CancellationToken ct = default)
    {
        var setting = await db.TenantIntegrationSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.IntegrationKey == integrationKey && !s.IsDeleted, ct);

        if (setting is null)
        {
            setting = new TenantIntegrationSetting
            {
                TenantId = tenantId,
                IntegrationKey = integrationKey,
                ProviderKey = providerKey,
            };
            db.TenantIntegrationSettings.Add(setting);
        }

        setting.ProviderKey = providerKey;
        setting.Environment = environment;
        setting.SettingsJsonEncrypted = _protector.Protect(settingsJson);
        setting.IsEnabled = isEnabled;
        await db.SaveChangesAsync(ct);
    }
}
