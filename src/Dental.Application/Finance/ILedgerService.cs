namespace Dental.Application.Finance;

/// <summary>
/// Tek cari defter (hasta + kurum) çekirdeği. Tüm borç/alacak kayıtları buradan geçer;
/// Patient.Balance / Company.Balance denormalize bakiyeleri YALNIZ buradan güncellenir.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Cari kaydı ekler ve hesabın denormalize bakiyesini AYNI transaction'da atomik
    /// <c>UPDATE ... SET Balance = Balance + @delta</c> (satır kilidi) ile günceller —
    /// denetim (c)-10 yarış koşulu düzeltmesi. Çağıran açık bir transaction başlatmışsa
    /// ona katılır; yoksa kendi transaction'ını açar. Eklenen kaydın Id'sini döner.
    /// </summary>
    Task<long> AddEntryAsync(LedgerEntryCreateRequest request, CancellationToken ct = default);

    /// <summary>Tarih sıralı ekstre + koşan bakiye + özet (toplam tedavi, toplam tahsilat, bakiye).</summary>
    Task<LedgerStatementDto> GetStatementAsync(LedgerStatementQuery query, CancellationToken ct = default);
}
