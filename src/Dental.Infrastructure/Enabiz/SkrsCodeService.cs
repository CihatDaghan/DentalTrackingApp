using Dental.Application.Abstractions;
using Dental.Application.Enabiz;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Dental.Integrations.Enabiz;
using Dental.Integrations.Enabiz.PacketBuilders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Enabiz;

/// <summary>
/// SKRS kod setlerinin yerel aynası.
///
/// <para>Kimlik bilgisi varsa canlı SKRS'den senkronlanır; yoksa <b>tohum listelerle</b> çalışır
/// (<see cref="SkrsSource.Seed"/>). Tohum listeler ürünün USS kimliği gelmeden de çalışabilmesi
/// içindir: diş tedavilerinde kullanılan FDI diş kodları ve klinikte sık geçen ICD-10 tanıları
/// yerelde tanımlıdır. Kimlik geldiğinde aynı tablolar canlı veriyle üzerine yazılır ve
/// <see cref="SkrsSource.Live"/> olarak işaretlenir.</para>
/// </summary>
public sealed class SkrsCodeService(
    AppDbContext db,
    IClock clock,
    SkrsSyncService sync,
    ILogger<SkrsCodeService> logger) : ISkrsCodeService
{
    public async Task<IReadOnlyList<SkrsCodeDto>> SearchAsync(
        string? systemName = null, string? search = null, int limit = 50, CancellationToken ct = default)
    {
        var query =
            from code in db.SkrsCodes.AsNoTracking()
            join system in db.SkrsCodeSystems.AsNoTracking()
                on code.CodeSystemGuid equals system.CodeSystemGuid
            select new { code, system };

        if (!string.IsNullOrWhiteSpace(systemName))
        {
            var name = systemName.Trim();
            query = query.Where(x => EF.Functions.Like(x.system.Name, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.code.Code, $"{term}%") || EF.Functions.Like(x.code.Name, $"%{term}%"));
        }

        return await query
            .Where(x => x.code.IsActive)
            .OrderBy(x => x.code.Code)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(x => new SkrsCodeDto(x.code.Code, x.code.Name, x.code.ParentCode, x.code.IsActive, x.system.Name))
            .ToListAsync(ct);
    }

    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        if (!sync.HasCredentials)
        {
            var seeded = await SeedAsync(ct);
            logger.LogInformation(
                "SKRS kimlik bilgisi yok; tohum listeler kullanıldı. Kayıt={Count}", seeded);
            return seeded;
        }

        try
        {
            return await SyncLiveAsync(ct);
        }
        catch (EnabizClientException ex)
        {
            // Kimlik reddi/erişim hatası: tohum listelerle çalışmaya devam et, gönderimi durdurma.
            logger.LogError(ex, "SKRS canlı senkronu başarısız; tohum listeler korunuyor.");
            return await SeedAsync(ct);
        }
    }

    /// <summary>Canlı SKRS'den ürün için gereken kod sistemlerini çeker.</summary>
    private async Task<int> SyncLiveAsync(CancellationToken ct)
    {
        // Ürünün ihtiyaç duyduğu kod sistemleri (paket alan tanımlarındaki GUID'ler).
        var wanted = new (Guid Guid, string Name)[]
        {
            (Guid.Parse(EnabizCodeSystems.Icd10), "ICD-10 Tanı"),
            (Guid.Parse(EnabizCodeSystems.Sut), "SUT İşlem"),
            (Guid.Parse(EnabizCodeSystems.ToothCode), "Diş Kodu"),
            (Guid.Parse(EnabizCodeSystems.ToothStatus), "Mevcut Diş Durumu"),
            (Guid.Parse(EnabizCodeSystems.ClinicCode), "Klinik Kodu"),
        };

        var total = 0;
        foreach (var (guid, name) in wanted)
        {
            ct.ThrowIfCancellationRequested();

            var rows = await sync.GetCodesAsync(guid, ct);
            if (rows.Count == 0) continue;

            await UpsertSystemAsync(guid, name, SkrsSource.Live, rows, ct);
            total += rows.Count;
        }

        logger.LogInformation("SKRS canlı senkronu tamamlandı. Kayıt={Count}", total);
        return total;
    }

    /// <summary>
    /// Kimlik yokken kullanılan yerel tohum listeler: FDI diş kodları (tamamı, üretilebilir) +
    /// diş hekimliğinde en sık kullanılan ICD-10 tanıları.
    /// </summary>
    private async Task<int> SeedAsync(CancellationToken ct)
    {
        var total = 0;

        // Diş kodları: FDI numaralandırması tam olarak bilinir, uydurma değildir.
        var teeth = FdiTeeth.All
            .OrderBy(t => t, StringComparer.Ordinal)
            .Select(t => new SkrsCodeRow(t, $"{t} numaralı diş", null, true))
            .ToList();
        total += await UpsertSystemAsync(
            Guid.Parse(EnabizCodeSystems.ToothCode), "Diş Kodu", SkrsSource.Seed, teeth, ct);

        // Diş hekimliğinde sık kullanılan ICD-10 tanıları (K00-K14 ağız/diş/çene bölümü).
        var diagnoses = DentalIcd10Seed
            .Select(d => new SkrsCodeRow(d.Code, d.Name, null, true))
            .ToList();
        total += await UpsertSystemAsync(
            Guid.Parse(EnabizCodeSystems.Icd10), "ICD-10 Tanı", SkrsSource.Seed, diagnoses, ct);

        return total;
    }

    private async Task<int> UpsertSystemAsync(
        Guid codeSystemGuid, string name, SkrsSource source, IReadOnlyList<SkrsCodeRow> rows, CancellationToken ct)
    {
        var system = await db.SkrsCodeSystems.FirstOrDefaultAsync(s => s.CodeSystemGuid == codeSystemGuid, ct);
        if (system is null)
        {
            system = new SkrsCodeSystem { CodeSystemGuid = codeSystemGuid, Name = name };
            db.SkrsCodeSystems.Add(system);
        }

        system.Name = name;
        system.Source = source;
        system.LastSyncAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        var existing = await db.SkrsCodes
            .Where(c => c.CodeSystemGuid == codeSystemGuid)
            .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.Code, out var code))
            {
                code.Name = row.Name;
                code.ParentCode = row.ParentCode;
                code.IsActive = row.IsActive;
            }
            else
            {
                db.SkrsCodes.Add(new SkrsCode
                {
                    CodeSystemGuid = codeSystemGuid,
                    Code = row.Code,
                    Name = row.Name,
                    ParentCode = row.ParentCode,
                    IsActive = row.IsActive,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return rows.Count;
    }

    /// <summary>
    /// Ağız-diş hekimliğinde en sık kullanılan ICD-10 tanıları (WHO ICD-10 K00-K14 bölümü).
    /// Kimlik gelene kadar tanı seçimi bu listeden yapılır; canlı senkron bunları genişletir.
    /// </summary>
    internal static readonly (string Code, string Name)[] DentalIcd10Seed =
    [
        ("K00.0", "Anodonti"),
        ("K00.1", "Süpernümerer dişler"),
        ("K00.6", "Diş sürmesi bozuklukları"),
        ("K01.0", "Gömülü dişler"),
        ("K01.1", "Sürmemiş (impakte) dişler"),
        ("K02.0", "Mine ile sınırlı diş çürüğü"),
        ("K02.1", "Dentin çürüğü"),
        ("K02.2", "Sement çürüğü"),
        ("K02.3", "Duraklamış diş çürüğü"),
        ("K02.9", "Diş çürüğü, tanımlanmamış"),
        ("K03.0", "Dişlerde aşırı atrizyon"),
        ("K03.6", "Dişlerde birikintiler (tartar)"),
        ("K04.0", "Pulpitis"),
        ("K04.1", "Pulpa nekrozu"),
        ("K04.4", "Pulpa kaynaklı akut apikal periodontit"),
        ("K04.5", "Kronik apikal periodontit"),
        ("K04.6", "Periapikal apse, sinüs yolu ile"),
        ("K04.7", "Periapikal apse, sinüs yolu olmadan"),
        ("K05.0", "Akut gingivitis"),
        ("K05.1", "Kronik gingivitis"),
        ("K05.3", "Kronik periodontitis"),
        ("K06.1", "Dişeti büyümesi"),
        ("K07.3", "Dişlerin pozisyon anomalileri"),
        ("K08.1", "Kaza, çekme veya lokal periodontal hastalığa bağlı diş kaybı"),
        ("K08.3", "Tutulmuş diş kökü"),
        ("K12.0", "Tekrarlayan oral aftlar"),
        ("K13.7", "Ağız mukozasının diğer ve tanımlanmamış lezyonları"),
    ];
}
