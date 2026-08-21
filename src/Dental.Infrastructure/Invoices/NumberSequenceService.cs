using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Invoices;

/// <summary>Kiracı içi atomik sıra numarası üreteci.</summary>
public interface INumberSequenceService
{
    /// <summary>
    /// Sayacı bir artırır ve YENİ değeri döner. Paralel çağrılarda numara asla tekrar etmez;
    /// artırım tek ifadelik <c>UPDATE ... OUTPUT</c> ile veritabanında yapılır.
    /// </summary>
    Task<long> NextAsync(long tenantId, NumberSequenceType type, string serial, int year, CancellationToken ct = default);
}

/// <summary>
/// (c)-1 gereği belge numarası yalnız Draft→UblGenerated geçişinde atanır ve geri alınmaz.
///
/// Atomiklik: oku-artır-yaz deseni paralel isteklerde AYNI numarayı üretir; bu yüzden artırım
/// tek ifadelik <c>UPDATE NumberSequences SET LastValue = LastValue + 1 OUTPUT INSERTED.LastValue</c>
/// ile yapılır — satır kilidi UPDATE'in kendisindedir, ayrıca transaction gerekmez.
/// Satır yoksa ilk çağrı onu oluşturur; iki istek aynı anda oluşturmaya kalkarsa filtered unique
/// index birini reddeder ve reddedilen taraf UPDATE'i tekrar dener.
/// </summary>
public sealed class NumberSequenceService(AppDbContext db) : INumberSequenceService
{
    private const string IncrementSql = """
        UPDATE [NumberSequences]
        SET [LastValue] = [LastValue] + 1, [UpdatedAtUtc] = SYSUTCDATETIME()
        OUTPUT INSERTED.[LastValue] AS [Value]
        WHERE [TenantId] = {0} AND [SequenceType] = {1} AND [Serial] = {2} AND [Year] = {3} AND [IsDeleted] = 0
        """;

    public async Task<long> NextAsync(
        long tenantId, NumberSequenceType type, string serial, int year, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var value = await TryIncrementAsync(tenantId, type, serial, year, ct);
        if (value is { } incremented) return incremented;

        // Sayaç henüz yok: oluştur. Yarışta kaybeden taraf UPDATE'e geri döner.
        try
        {
            db.NumberSequences.Add(new NumberSequence
            {
                TenantId = tenantId,
                SequenceType = type,
                Serial = serial,
                Year = year,
                LastValue = 1,
            });
            await db.SaveChangesAsync(ct);
            return 1;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await TryIncrementAsync(tenantId, type, serial, year, ct)
                ?? throw new InvalidOperationException(
                    $"Numara sayacı oluşturulamadı: {type}/{serial}/{year}.");
        }
    }

    private async Task<long?> TryIncrementAsync(
        long tenantId, NumberSequenceType type, string serial, int year, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQueryRaw<long>(IncrementSql, tenantId, (byte)type, serial, year)
            .ToListAsync(ct);
        return rows.Count > 0 ? rows[0] : null;
    }
}
