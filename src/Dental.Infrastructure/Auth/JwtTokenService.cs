using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dental.Application.Auth;
using Dental.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dental.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "DentalTrackingApp";
    public string Audience { get; set; } = "DentalTrackingApp";
    /// <summary>En az 32 bayt; dev değeri appsettings.Development.json'da, üretimde secret.</summary>
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public static class AppClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string ClinicId = "clinic_id";
    public const string Permission = "perm";
    public const string UserType = "user_type";
    public const string SuperAdmin = "super_admin";
    public const string Locale = "locale";
    /// <summary>Kimliğe bürünen süper adminin kullanıcı kimliği — token'ın izidir.</summary>
    public const string ImpersonatedBy = "impersonated_by";
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public string CreateAccessToken(AppUser user, long? clinicId, IReadOnlyCollection<string> permissions) =>
        Write(BuildClaims(user, clinicId, permissions), TimeSpan.FromMinutes(_opt.AccessTokenMinutes));

    public string CreateImpersonationToken(
        AppUser targetUser,
        long? clinicId,
        IReadOnlyCollection<string> permissions,
        long impersonatorUserId,
        TimeSpan lifetime)
    {
        var claims = BuildClaims(targetUser, clinicId, permissions);
        // İz: token hedef kullanıcı adına düzenlense de kimin bürüldüğü claim'de kalır.
        claims.Add(new Claim(AppClaimTypes.ImpersonatedBy, impersonatorUserId.ToString()));
        // Süper admin bayrağı ASLA taşınmaz: bürünen oturum hedef kiracının yetkileriyle sınırlıdır.
        claims.RemoveAll(c => c.Type == AppClaimTypes.SuperAdmin);
        return Write(claims, lifetime);
    }

    private List<Claim> BuildClaims(AppUser user, long? clinicId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(AppClaimTypes.UserType, ((byte)user.UserType).ToString()),
            new(AppClaimTypes.Locale, user.Locale),
        };
        if (user.TenantId is { } tenantId) claims.Add(new(AppClaimTypes.TenantId, tenantId.ToString()));
        else claims.Add(new(AppClaimTypes.SuperAdmin, "true"));
        if (clinicId is { } cid) claims.Add(new(AppClaimTypes.ClinicId, cid.ToString()));
        claims.AddRange(permissions.Select(p => new Claim(AppClaimTypes.Permission, p)));
        return claims;
    }

    private string Write(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, string Hash) CreateRefreshToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return (token, HashRefreshToken(token));
    }

    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
