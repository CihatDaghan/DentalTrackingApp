using Dental.Api.Auth;
using Dental.Application.Finance;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class PaymentPlansController(IPaymentPlanService plans) : ControllerBase
{
    /// <summary>Taksit planı oluşturur (eşit aylık taksitler; kuruş farkı son taksitte).</summary>
    [HttpPost("payment-plans")]
    [HasPermission("payment.create")]
    public async Task<ActionResult<PaymentPlanDto>> Create(PaymentPlanCreateRequest request, CancellationToken ct)
    {
        var dto = await plans.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("payment-plans/{id:long}")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<PaymentPlanDto>> Get(long id, CancellationToken ct)
        => Ok(await plans.GetAsync(id, ct));

    /// <summary>Ödemesi başlamış plan silinemez.</summary>
    [HttpDelete("payment-plans/{id:long}")]
    [HasPermission("payment.create")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await plans.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Hastanın taksit planları (vadesi geçmiş taksitler Overdue görünür).</summary>
    [HttpGet("patients/{patientId:long}/payment-plans")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<IReadOnlyList<PaymentPlanDto>>> ListByPatient(long patientId, CancellationToken ct)
        => Ok(await plans.ListByPatientAsync(patientId, ct));
}
