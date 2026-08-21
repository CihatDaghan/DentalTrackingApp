using Dental.Domain.Enums;

namespace Dental.Application.Auth;

public sealed record LoginRequest(string Email, string Password, string? TurnstileToken, bool RememberMe = false);

public sealed record ClinicChoiceDto(long Id, string Name, bool IsDefault);

public sealed record AuthUserDto(
    long Id,
    string Email,
    string FirstName,
    string LastName,
    UserType UserType,
    string Locale,
    long? TenantId,
    bool IsSuperAdmin,
    IReadOnlyList<string> Permissions);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    bool RequiresClinicSelection,
    IReadOnlyList<ClinicChoiceDto> Clinics,
    AuthUserDto User);

public sealed record TokenPairResponse(string AccessToken, string RefreshToken);

public sealed record RefreshRequest(string RefreshToken);

public sealed record SelectClinicRequest(long ClinicId);

/// <summary>JWT üretimi Infrastructure'da uygulanır.</summary>
public interface ITokenService
{
    string CreateAccessToken(Domain.Entities.AppUser user, long? clinicId, IReadOnlyCollection<string> permissions);

    /// <summary>
    /// Süper adminin hedef kiracı adına kullandığı KISA ÖMÜRLÜ token. <c>impersonated_by</c>
    /// claim'i taşır; refresh token üretilmez (oturum uzatılamaz).
    /// </summary>
    string CreateImpersonationToken(
        Domain.Entities.AppUser targetUser,
        long? clinicId,
        IReadOnlyCollection<string> permissions,
        long impersonatorUserId,
        TimeSpan lifetime);

    /// <returns>(düz token, SHA-256 özeti) — düz token yalnız istemciye döner, DB'ye özet yazılır.</returns>
    (string Token, string Hash) CreateRefreshToken();
    string HashRefreshToken(string token);
}

/// <summary>Cloudflare Turnstile doğrulaması; dev'de SecretKey boşsa her token geçer.</summary>
public interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default);
}
