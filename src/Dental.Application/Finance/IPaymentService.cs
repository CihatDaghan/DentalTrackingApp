using Dental.Application.Common;

namespace Dental.Application.Finance;

/// <summary>Tahsilat: kayıt + PaymentIn ledger kaydı + vadesi gelen taksitlere FIFO dağıtım.</summary>
public interface IPaymentService
{
    /// <summary>
    /// Tahsilat oluşturur: Payment satırı + PaymentIn (Credit) ledger kaydı + bakiye güncelleme
    /// tek transaction'da. Hasta tahsilatında tutar, hastanın açık taksitlerine vade sırasıyla
    /// (FIFO) dağıtılır (PaidAmount/Status güncellenir).
    /// </summary>
    Task<PaymentDto> CreateAsync(PaymentCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Tahsilat silme (payment.delete izni): soft-delete + ters (Correction, Debit) ledger
    /// kaydı ile bakiye geri alınır. Taksit dağıtımı geri alınmaz (dağıtım eşlemesi tutulmuyor).
    /// </summary>
    Task DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>İndirim (payment.discount izni): Discount (Credit) ledger kaydı + bakiye güncelleme.</summary>
    Task<long> ApplyDiscountAsync(DiscountRequest request, CancellationToken ct = default);

    Task<PaymentDto> GetAsync(long id, CancellationToken ct = default);

    Task<PagedResult<PaymentDto>> ListAsync(PaymentListQuery query, CancellationToken ct = default);
}
