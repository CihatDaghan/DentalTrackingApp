using Dental.Application.Consents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

/// <summary>
/// Auth'suz onam imza uçları (hasta telefonundaki /p/consent/{token} sayfası bunları çağırır).
/// Token doğrulama + tenant çözümlemesi IPublicConsentService'te ((c)-6); IP bazlı "public"
/// rate limit politikası uygulanır ((c)-6 ek maddesi). Kullanılmış/süresi dolmuş token → 410 Gone.
/// </summary>
[ApiController]
[Route("api/v1/public/consents")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class PublicConsentsController(IPublicConsentService publicConsents) : ControllerBase
{
    [HttpGet("{token:guid}")]
    public async Task<ActionResult<PublicConsentViewDto>> Get(Guid token, CancellationToken ct)
        => Ok(await publicConsents.GetByTokenAsync(token, ct));

    [HttpPost("{token:guid}/sign")]
    public async Task<ActionResult<PublicConsentViewDto>> Sign(
        Guid token, PublicConsentSignRequest request, CancellationToken ct)
        => Ok(await publicConsents.SignByTokenAsync(token, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(), ct));
}
