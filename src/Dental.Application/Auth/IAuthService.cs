namespace Dental.Application.Auth;

public interface IAuthService
{
    /// <exception cref="AuthFailedException">Kimlik doğrulama başarısız (mesaj kasıtlı olarak geneldir).</exception>
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default);

    Task<TokenPairResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default);

    Task<TokenPairResponse> SelectClinicAsync(long userId, long clinicId, string? ip, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}

public sealed class AuthFailedException(string message) : Exception(message);
