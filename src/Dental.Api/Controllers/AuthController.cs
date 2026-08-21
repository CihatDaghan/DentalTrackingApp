using Dental.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ClientIp, Request.Headers.UserAgent, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenPairResponse>> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(await authService.RefreshAsync(request.RefreshToken, ClientIp, ct));

    [HttpPost("select-clinic")]
    [Authorize]
    public async Task<ActionResult<TokenPairResponse>> SelectClinic(SelectClinicRequest request, CancellationToken ct)
    {
        var userId = long.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        return Ok(await authService.SelectClinicAsync(userId, request.ClinicId, ClientIp, ct));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
