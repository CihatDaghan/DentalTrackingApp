using Dental.Api.Auth;
using Dental.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

/// <summary>Sanal POS ödeme linki uçları (klinik tarafı).</summary>
[ApiController]
[Route("api/v1/payment-links")]
public sealed class PaymentLinksController(IPaymentLinkService links) : ControllerBase
{
    [HttpPost]
    [HasPermission("payment.create")]
    public async Task<ActionResult<PaymentLinkDto>> Create(PaymentLinkCreateRequest request, CancellationToken ct)
    {
        var dto = await links.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet]
    [HasPermission("payment.read")]
    public async Task<ActionResult<IReadOnlyList<PaymentLinkDto>>> List(
        [FromQuery] long? patientId, CancellationToken ct)
        => Ok(await links.ListAsync(patientId, ct));

    [HttpGet("{id:long}")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<PaymentLinkDto>> Get(long id, CancellationToken ct)
        => Ok(await links.GetAsync(id, ct));
}

/// <summary>
/// Auth'suz ödeme sayfası uçları (hastanın telefonundaki /p/payment/{token} sayfası bunları çağırır).
/// Token doğrulama + tenant çözümlemesi IPublicPaymentService'te; IP bazlı "public" hız sınırı uygulanır.
/// </summary>
[ApiController]
[Route("api/v1/public/payments")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class PublicPaymentsController(IPublicPaymentService publicPayments) : ControllerBase
{
    [HttpGet("{token:guid}")]
    public async Task<ActionResult<PublicPaymentViewDto>> Get(Guid token, CancellationToken ct)
        => Ok(await publicPayments.GetByTokenAsync(token, ct));

    /// <summary>Ödeme sayfasının kısa aralıklı yokladığı hafif durum ucu.</summary>
    [HttpGet("{token:guid}/status")]
    public async Task<ActionResult<PublicPaymentStatusDto>> Status(Guid token, CancellationToken ct)
        => Ok(await publicPayments.GetStatusByTokenAsync(token, ct));
}
