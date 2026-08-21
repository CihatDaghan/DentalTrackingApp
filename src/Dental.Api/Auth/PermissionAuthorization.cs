using Dental.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Dental.Api.Auth;

/// <summary>[HasPermission("payment.delete")] — izin kodu JWT'deki "perm" claim'lerinden doğrulanır ("*" = süper admin).</summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";
    public HasPermissionAttribute(string permission) => Policy = PolicyPrefix + permission;
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var perms = context.User.FindAll(AppClaimTypes.Permission).Select(c => c.Value);
        if (perms.Any(p => p == "*" || p == requirement.Permission))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>"perm:*" politikalarını dinamik üretir; diğerlerini varsayılan sağlayıcıya bırakır.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

public static class AuthPolicies
{
    public const string SuperAdmin = "SuperAdmin";
}
