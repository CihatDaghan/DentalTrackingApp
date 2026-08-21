using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Invoices;

/// <summary>Belge tarihinde geçerli vergi kodları/oranları (hepsi TaxConfig'ten, hardcode değil).</summary>
public sealed record TaxConfigSet(
    decimal HealthVatRate,
    decimal AestheticVatRate,
    string HealthTourismExemptionCode,
    string HealthTourismExemptionReason,
    string PublicWithholdingCode,
    decimal PublicWithholdingPercent,
    decimal GvStopajPercent,
    string ServiceUnitCode);

public interface ITaxConfigService
{
    /// <summary>Verilen tarihte geçerli (ValidFrom ≤ tarih &lt; ValidTo) kod listesini döner.</summary>
    Task<TaxConfigSet> GetAsync(DateOnly onDate, CancellationToken ct = default);
}

/// <summary>
/// TaxConfig tablosundan okur; tablo boşsa (migration sonrası ilk açılış) yasal varsayılanlara düşer.
/// Böylece seed sırası yüzünden fatura kesilememesi gibi bir kilitlenme oluşmaz.
/// </summary>
public sealed class TaxConfigService(AppDbContext db) : ITaxConfigService
{
    public const decimal DefaultHealthVatRate = 10m;
    public const decimal DefaultAestheticVatRate = 20m;
    public const string HealthTourismExemptionCode = "334";
    public const string PublicWithholdingCode = "616";
    public const decimal DefaultWithholdingPercent = 50m;
    public const decimal DefaultGvStopajPercent = 20m;
    public const string ServiceUnitCode = "C62";

    public const string DefaultExemptionReason =
        "KDV Kanunu 13/l — Türkiye'de yerleşmiş olmayan yabancı uyruklulara verilen sağlık hizmetlerinde istisna";

    public async Task<TaxConfigSet> GetAsync(DateOnly onDate, CancellationToken ct = default)
    {
        var rows = await db.TaxConfigs.AsNoTracking()
            .Where(t => t.ValidFrom <= onDate && (t.ValidTo == null || t.ValidTo > onDate))
            .ToListAsync(ct);

        var exemption = Find(rows, TaxConfigType.ExemptionCode, HealthTourismExemptionCode);
        var withholding = Find(rows, TaxConfigType.WithholdingCode, PublicWithholdingCode);

        return new TaxConfigSet(
            HealthVatRate: Find(rows, TaxConfigType.VatRate, "10")?.Rate ?? DefaultHealthVatRate,
            AestheticVatRate: Find(rows, TaxConfigType.VatRate, "20")?.Rate ?? DefaultAestheticVatRate,
            HealthTourismExemptionCode: exemption?.Code ?? HealthTourismExemptionCode,
            HealthTourismExemptionReason: exemption?.Description ?? DefaultExemptionReason,
            PublicWithholdingCode: withholding?.Code ?? PublicWithholdingCode,
            PublicWithholdingPercent: withholding?.Rate ?? DefaultWithholdingPercent,
            // GV stopajı GVK 94'ten sabittir; ayrı bir kod listesi satırı yoktur.
            GvStopajPercent: DefaultGvStopajPercent,
            ServiceUnitCode: Find(rows, TaxConfigType.UnitCode, ServiceUnitCode)?.Code ?? ServiceUnitCode);
    }

    private static TaxConfig? Find(List<TaxConfig> rows, TaxConfigType type, string code) =>
        rows.FirstOrDefault(t => t.ConfigType == type && t.Code == code);
}
