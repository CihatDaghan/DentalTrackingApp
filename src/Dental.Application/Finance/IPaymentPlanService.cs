namespace Dental.Application.Finance;

/// <summary>
/// Taksit planı: eşit aylık taksit üretimi + listeleme. Overdue kalıcı yazılmaz;
/// DueDate &lt; bugün olan Pending/Partial taksitler sorgu bazlı Overdue sunulur.
/// </summary>
public interface IPaymentPlanService
{
    /// <summary>Eşit taksit üretir: tutar farkı (kuruş) son taksite eklenir; vadeler aylık ilerler.</summary>
    Task<PaymentPlanDto> CreateAsync(PaymentPlanCreateRequest request, CancellationToken ct = default);

    Task<PaymentPlanDto> GetAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentPlanDto>> ListByPatientAsync(long patientId, CancellationToken ct = default);

    /// <summary>Ödemesi başlamış (PaidAmount &gt; 0) plan silinemez.</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);
}
