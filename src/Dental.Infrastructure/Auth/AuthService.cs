using Dental.Application.Abstractions;
using Dental.Application.Auth;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dental.Infrastructure.Auth;

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ITokenService tokenService,
    ITurnstileVerifier turnstile,
    IClock clock,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private const string GenericError = "E-posta veya şifre hatalı.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        if (!await turnstile.VerifyAsync(request.TurnstileToken, ip, ct))
            throw new AuthFailedException("Güvenlik doğrulaması başarısız. Lütfen tekrar deneyin.");

        // Login, tenant bağlamı kurulmadan çalışır: kullanıcı e-postayla globalde aranır.
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(request.Email), ct);

        if (user is null || !user.IsActive)
        {
            LogAuth(null, AuditActionType.LoginFailed, ip, userAgent, request.Email);
            await db.SaveChangesAsync(ct);
            throw new AuthFailedException(GenericError);
        }

        if (await userManager.IsLockedOutAsync(user))
            throw new AuthFailedException("Hesap geçici olarak kilitlendi. Birkaç dakika sonra tekrar deneyin.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            LogAuth(user, AuditActionType.LoginFailed, ip, userAgent);
            await db.SaveChangesAsync(ct);
            throw new AuthFailedException(GenericError);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var clinics = await db.UserClinics.IgnoreQueryFilters()
            .Where(uc => uc.UserId == user.Id)
            .Join(db.Clinics.IgnoreQueryFilters().Where(c => !c.IsDeleted),
                uc => uc.ClinicId, c => c.Id,
                (uc, c) => new ClinicChoiceDto(c.Id, c.Name, uc.IsDefault))
            .ToListAsync(ct);

        long? clinicId = clinics.Count switch
        {
            0 => null,
            1 => clinics[0].Id,
            _ => clinics.FirstOrDefault(c => c.IsDefault)?.Id, // varsayılan yoksa seçim istenir
        };
        var requiresSelection = clinics.Count > 1 && clinics.All(c => !c.IsDefault);

        var permissions = await GetPermissionsAsync(user, ct);
        var pair = await IssueTokenPairAsync(user, clinicId, permissions, ip, ct);

        LogAuth(user, AuditActionType.Login, ip, userAgent);
        await db.SaveChangesAsync(ct);

        return new LoginResponse(
            pair.AccessToken,
            pair.RefreshToken,
            requiresSelection,
            clinics,
            new AuthUserDto(user.Id, user.Email ?? "", user.FirstName, user.LastName, user.UserType,
                user.Locale, user.TenantId, user.TenantId is null, permissions));
    }

    public async Task<TokenPairResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var (requestedClinicId, rawToken) = ParseRefreshToken(refreshToken);
        var hash = tokenService.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.User is null || !stored.User.IsActive)
            throw new AuthFailedException("Oturum geçersiz.");

        if (!stored.IsActive)
        {
            // Yeniden kullanım tespiti: iptal edilmiş token tekrar geldiyse kullanıcının tüm oturumları kapatılır.
            if (stored.RevokedAtUtc is not null)
            {
                await db.RefreshTokens
                    .Where(t => t.UserId == stored.UserId && t.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, clock.UtcNow), ct);
            }
            throw new AuthFailedException("Oturum süresi doldu.");
        }

        var user = stored.User;
        // Token'da taşınan klinik, erişim yetkisi doğrulanarak korunur; yoksa varsayılana düşülür.
        var clinicId = requestedClinicId is { } requested
            && await db.UserClinics.IgnoreQueryFilters()
                .AnyAsync(uc => uc.UserId == user.Id && uc.ClinicId == requested, ct)
            ? requested
            : await db.UserClinics.IgnoreQueryFilters()
                .Where(uc => uc.UserId == user.Id)
                .OrderByDescending(uc => uc.IsDefault)
                .Select(uc => (long?)uc.ClinicId)
                .FirstOrDefaultAsync(ct);

        var permissions = await GetPermissionsAsync(user, ct);
        var pair = await IssueTokenPairAsync(user, clinicId, permissions, ip, ct, replacing: stored);
        await db.SaveChangesAsync(ct);
        return pair;
    }

    public async Task<TokenPairResponse> SelectClinicAsync(long userId, long clinicId, string? ip, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AuthFailedException("Oturum geçersiz.");

        // Pasif/kilitli kullanıcı bu uçtan yeni token zinciri kuramasın (inceleme bulgusu:
        // refresh'teki IsActive kontrolü buradan dolaşılabiliyordu).
        if (!user.IsActive || await userManager.IsLockedOutAsync(user))
            throw new AuthFailedException("Oturum geçersiz.");

        var hasAccess = await db.UserClinics.IgnoreQueryFilters()
            .AnyAsync(uc => uc.UserId == userId && uc.ClinicId == clinicId, ct);
        if (!hasAccess) throw new AuthFailedException("Bu kliniğe erişim yetkiniz yok.");

        var permissions = await GetPermissionsAsync(user, ct);
        var pair = await IssueTokenPairAsync(user, clinicId, permissions, ip, ct);
        await db.SaveChangesAsync(ct);
        return pair;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = tokenService.HashRefreshToken(ParseRefreshToken(refreshToken).Token);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, clock.UtcNow), ct);
    }

    private async Task<TokenPairResponse> IssueTokenPairAsync(
        AppUser user, long? clinicId, IReadOnlyList<string> permissions, string? ip,
        CancellationToken ct, RefreshToken? replacing = null)
    {
        var access = tokenService.CreateAccessToken(user, clinicId, permissions);
        var (refreshPlain, refreshHash) = tokenService.CreateRefreshToken();
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = clock.UtcNow,
            ExpiresAtUtc = clock.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = ip,
        };
        db.RefreshTokens.Add(refresh);
        if (replacing is not null)
        {
            replacing.RevokedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct); // refresh.Id için
            replacing.ReplacedByTokenId = refresh.Id;
        }
        // Seçili klinik istemciye dönen token dizesinde taşınır; yenilemede sunucu
        // üyeliği yeniden doğrular (inceleme bulgusu: refresh seçili kliniği unutuyordu).
        return new TokenPairResponse(access, ComposeRefreshToken(clinicId, refreshPlain));
    }

    // Refresh token biçimi: "{clinicId}.{rastgele}" (klinik yoksa yalnız rastgele).
    // Base64 alfabesinde '.' bulunmadığından ayraç güvenlidir; DB'de yalnız rastgele
    // kısmın SHA-256 özeti tutulur, klinik kimliği her istekte üyelikle doğrulanır.
    private static string ComposeRefreshToken(long? clinicId, string token) =>
        clinicId is { } id ? $"{id}.{token}" : token;

    private static (long? ClinicId, string Token) ParseRefreshToken(string raw)
    {
        var separator = raw.IndexOf('.');
        if (separator <= 0) return (null, raw);
        return long.TryParse(raw[..separator], out var clinicId)
            ? (clinicId, raw[(separator + 1)..])
            : (null, raw);
    }

    private async Task<IReadOnlyList<string>> GetPermissionsAsync(AppUser user, CancellationToken ct)
    {
        if (user.TenantId is null) return ["*"]; // süper admin

        return await db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id)
            .SelectMany(ur => ur.Role!.Permissions)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    private void LogAuth(AppUser? user, AuditActionType action, string? ip, string? userAgent, string? attemptedEmail = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = user?.TenantId,
            UserId = user?.Id,
            ActionType = action,
            EntityName = nameof(AppUser),
            EntityId = user?.Id,
            // Kaçışsız interpolasyon audit JSON'unu bozabiliyordu (inceleme bulgusu).
            NewValuesJson = attemptedEmail is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(new { attemptedEmail }),
            Ip = ip,
            UserAgent = userAgent,
            AtUtc = clock.UtcNow,
        });
    }
}
