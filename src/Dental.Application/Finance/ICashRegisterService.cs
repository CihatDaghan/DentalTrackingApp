namespace Dental.Application.Finance;

/// <summary>Kasa: günlük tahsilat (yöntem bazında) + gider + net ve gün hareket listesi.</summary>
public interface ICashRegisterService
{
    /// <summary>
    /// Gün özeti. Tahsilatlar ReceivedAtUtc'nin UTC gününe, giderler ExpenseDate'e göre
    /// eşlenir (TR sunumunda saat dönüşümü istemcide). clinicId NULL ise tüm klinikler.
    /// </summary>
    Task<CashRegisterDailySummaryDto> GetDailySummaryAsync(DateOnly date, long? clinicId = null, CancellationToken ct = default);
}
