using System.IdentityModel.Tokens.Jwt;
using Dental.Application.Abstractions;
using Dental.Infrastructure.Auth;

namespace Dental.Api.Middleware;

/// <summary>Kimliği doğrulanmış isteklerde JWT claim'lerinden tenant bağlamını kurar.</summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextSetter setter)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            long? tenantId = long.TryParse(user.FindFirst(AppClaimTypes.TenantId)?.Value, out var t) ? t : null;
            long? clinicId = long.TryParse(user.FindFirst(AppClaimTypes.ClinicId)?.Value, out var c) ? c : null;
            long? userId = long.TryParse(
                user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u) ? u : null;
            var isSuperAdmin = user.FindFirst(AppClaimTypes.SuperAdmin)?.Value == "true";
            setter.Set(tenantId, clinicId, userId, isSuperAdmin);
        }
        await next(context);
    }
}
