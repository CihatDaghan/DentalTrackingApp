using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dental.Application.Abstractions;
using Dental.Application.Authorization;
using Dental.Application.Settings;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Settings;

/// <summary>
/// Kiracı yöneticisinin ayar ekranı: klinik künyesi, çalışma saatleri, personel, yetki matrisi
/// ve entegrasyon kimlik bilgileri.
///
/// <para><b>Sırlar yazma-tek-yönlüdür:</b> entegrasyon ayarları yanıtta yalnız maskeli
/// (<c>••••1234</c>) döner. Güncellemede maskeli/boş gelen sır alanı MEVCUT değeri korur —
/// böylece ekran, sırrı hiç görmeden diğer alanları düzenleyebilir.</para>
/// </summary>
public sealed class SettingsService(
    AppDbContext db,
    ITenantContext tenant,
    UserManager<AppUser> userManager,
    IIntegrationSettingsStore integrationStore,
    IIntegrationProviderFactory providerFactory,
    IClock clock,
    ILogger<SettingsService> logger) : ISettingsService
{
    private const string SecretMaskPrefix = "••••";

    private long TenantId => tenant.TenantId
        ?? throw new InvalidOperationException("Kiracı bağlamı kurulmadan ayarlara erişilemez.");

    // ---- Klinik künyesi ----

    public async Task<ClinicSettingsDto> GetClinicAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var tenantRow = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new KeyNotFoundException("Kiracı bulunamadı.");
        var clinic = await ResolveClinicAsync(null, tracked: false, ct);
        return Map(tenantRow, clinic);
    }

    public async Task<ClinicSettingsDto> UpdateClinicAsync(
        ClinicSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var tenantRow = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new KeyNotFoundException("Kiracı bulunamadı.");
        var clinic = await ResolveClinicAsync(request.ClinicId, tracked: true, ct);

        if (string.IsNullOrWhiteSpace(request.TenantName))
            throw new InvalidOperationException("Klinik unvanı zorunludur.");
        if (string.IsNullOrWhiteSpace(request.ClinicName))
            throw new InvalidOperationException("Klinik adı zorunludur.");

        var taxNumber = Clean(request.TaxNumber);
        if (taxNumber is { } tn && tn.Length is not (10 or 11))
            throw new InvalidOperationException("VKN 10, TCKN 11 haneli olmalıdır.");
        // Şahıs hekimde belgeler TCKN ile kesilir; şirkette VKN zorunludur (e-belge karar motoru).
        if (request.LegalType == TenantLegalType.Company && taxNumber is { Length: 11 })
            throw new InvalidOperationException("Şirket türünde 10 haneli VKN girilmelidir.");

        if (request.LogoFileId is { } logoId
            && !await db.MediaFiles.AnyAsync(m => m.Id == logoId, ct))
            throw new KeyNotFoundException("Logo dosyası bulunamadı.");

        tenantRow.Name = request.TenantName.Trim();
        tenantRow.LegalType = request.LegalType;
        tenantRow.TaxNumber = taxNumber;
        tenantRow.TaxOffice = Clean(request.TaxOffice);
        tenantRow.HasHealthTourismAuthorization = request.HasHealthTourismAuthorization;

        clinic.Name = request.ClinicName.Trim();
        clinic.Address = Clean(request.Address);
        clinic.City = Clean(request.City);
        clinic.District = Clean(request.District);
        clinic.Phone = Clean(request.Phone);
        clinic.Email = Clean(request.Email);
        clinic.CkysCode = Clean(request.CkysCode);
        clinic.LogoFileId = request.LogoFileId;

        await db.SaveChangesAsync(ct);
        return Map(tenantRow, clinic);
    }

    private static ClinicSettingsDto Map(Tenant t, Clinic c) => new(
        t.Id, t.Name, t.LegalType, t.TaxNumber, t.TaxOffice, t.HasHealthTourismAuthorization,
        t.DefaultLocale, t.Status, t.PlanCode, t.TrialEndsAtUtc,
        c.Id, c.Name, c.Address, c.City, c.District, c.Phone, c.Email, c.CkysCode, c.LogoFileId);

    private async Task<Clinic> ResolveClinicAsync(long? clinicId, bool tracked, CancellationToken ct)
    {
        var source = tracked ? db.Clinics : db.Clinics.AsNoTracking();
        var id = clinicId ?? tenant.ClinicId;
        var clinic = id is { } explicitId
            ? await source.FirstOrDefaultAsync(c => c.Id == explicitId, ct)
            : await source.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        return clinic ?? throw new KeyNotFoundException("Klinik bulunamadı.");
    }

    // ---- Klinik çalışma saatleri ----

    public async Task<IReadOnlyList<ClinicWorkingHourDto>> GetClinicWorkingHoursAsync(
        long? clinicId, CancellationToken ct = default)
    {
        var clinic = await ResolveClinicAsync(clinicId, tracked: false, ct);
        return await db.ClinicWorkingHours.AsNoTracking()
            .Where(w => w.ClinicId == clinic.Id)
            .OrderBy(w => w.DayOfWeek)
            .Select(w => new ClinicWorkingHourDto(w.Id, w.ClinicId, w.DayOfWeek, w.OpenTime, w.CloseTime, w.IsClosed))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClinicWorkingHourDto>> SaveClinicWorkingHoursAsync(
        ClinicWorkingHoursSaveRequest request, CancellationToken ct = default)
    {
        var clinic = await ResolveClinicAsync(request.ClinicId, tracked: false, ct);
        foreach (var item in request.Items.Where(i => !i.IsClosed))
        {
            if (item.OpenTime is not { } open || item.CloseTime is not { } close)
                throw new InvalidOperationException("Açık günlerde açılış ve kapanış saati zorunludur.");
            if (close <= open)
                throw new InvalidOperationException("Kapanış saati açılıştan sonra olmalıdır.");
        }

        // Gün başına tek kayıt: mevcutlar tamamen değiştirilir (hekim saatleriyle aynı desen).
        var existing = await db.ClinicWorkingHours.Where(w => w.ClinicId == clinic.Id).ToListAsync(ct);
        db.ClinicWorkingHours.RemoveRange(existing);
        foreach (var item in request.Items.DistinctBy(i => i.DayOfWeek))
        {
            db.ClinicWorkingHours.Add(new ClinicWorkingHour
            {
                ClinicId = clinic.Id,
                DayOfWeek = item.DayOfWeek,
                OpenTime = item.IsClosed ? null : item.OpenTime,
                CloseTime = item.IsClosed ? null : item.CloseTime,
                IsClosed = item.IsClosed,
            });
        }
        await db.SaveChangesAsync(ct);
        return await GetClinicWorkingHoursAsync(clinic.Id, ct);
    }

    // ---- Personel ----

    public async Task<IReadOnlyList<StaffDto>> ListStaffAsync(bool includeInactive, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var users = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && (includeInactive || u.IsActive))
            .OrderByDescending(u => u.IsActive).ThenBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync(ct);
        return await MapStaffAsync(users, ct);
    }

    public async Task<StaffDto> GetStaffAsync(long id, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, tracked: false, ct);
        return (await MapStaffAsync([user], ct))[0];
    }

    public async Task<StaffInviteResultDto> InviteStaffAsync(
        StaffInviteRequest request, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("E-posta zorunludur.");
        if (await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(email), ct))
            throw new InvalidOperationException("Bu e-posta adresi başka bir hesapta kayıtlı.");

        var roles = await ResolveRolesAsync(request.RoleIds, ct);
        var clinic = await ResolveClinicAsync(request.ClinicId, tracked: false, ct);
        var temporaryPassword = GenerateTemporaryPassword();

        var user = new AppUser
        {
            TenantId = tenantId,
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            UserType = request.UserType,
            EmailConfirmed = true,
            // Davet edilen kullanıcı ilk girişte şifresini değiştirmek zorundadır.
            MustChangePassword = true,
            Color = Clean(request.Color),
            Branch = Clean(request.Branch),
            DiplomaNo = Clean(request.DiplomaNo),
        };
        var result = await userManager.CreateAsync(user, temporaryPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Kullanıcı oluşturulamadı: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));

        foreach (var role in roles) db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.UserClinics.Add(new UserClinic { UserId = user.Id, ClinicId = clinic.Id, IsDefault = true });
        await db.SaveChangesAsync(ct);

        return new StaffInviteResultDto(await GetStaffAsync(user.Id, ct), temporaryPassword);
    }

    public async Task<StaffDto> UpdateStaffAsync(long id, StaffUpdateRequest request, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, tracked: true, ct);
        var roles = await ResolveRolesAsync(request.RoleIds, ct);

        if (!request.IsActive && user.IsActive) await EnsureCanDeactivateAsync(user, ct);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.UserType = request.UserType;
        user.IsActive = request.IsActive;
        user.Color = Clean(request.Color);
        user.Branch = Clean(request.Branch);
        user.DiplomaNo = Clean(request.DiplomaNo);
        user.UpdatedAtUtc = clock.UtcNow;

        var current = await db.UserRoles.Where(ur => ur.UserId == id).ToListAsync(ct);
        db.UserRoles.RemoveRange(current.Where(ur => roles.All(r => r.Id != ur.RoleId)));
        foreach (var role in roles.Where(r => current.All(ur => ur.RoleId != r.Id)))
            db.UserRoles.Add(new UserRole { UserId = id, RoleId = role.Id });

        await db.SaveChangesAsync(ct);
        return await GetStaffAsync(id, ct);
    }

    public async Task<TemporaryPasswordDto> ResetStaffPasswordAsync(long id, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, tracked: true, ct);
        var temporaryPassword = GenerateTemporaryPassword();

        // Yönetici sıfırlaması token'sız yapılır: AddIdentityCore varsayılan token sağlayıcı
        // KAYDETMEZ ve "unutan kullanıcı" akışı (e-posta token'ı) bu aşamanın kapsamında değildir.
        // Üretilen şifre Identity politikasını sağladığı için doğrulayıcılar önce koşturulur;
        // ancak bundan sonra mevcut şifre kaldırılır (kullanıcı şifresiz kalmasın).
        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, user, temporaryPassword);
            if (!validation.Succeeded)
                throw new InvalidOperationException("Geçici şifre politikayı sağlamıyor: " +
                    string.Join("; ", validation.Errors.Select(e => e.Description)));
        }

        if (await userManager.HasPasswordAsync(user))
        {
            var removed = await userManager.RemovePasswordAsync(user);
            if (!removed.Succeeded)
                throw new InvalidOperationException("Şifre sıfırlanamadı: " +
                    string.Join("; ", removed.Errors.Select(e => e.Description)));
        }
        var result = await userManager.AddPasswordAsync(user, temporaryPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Şifre sıfırlanamadı: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = true;
        // Açık oturumlar kapatılır: şifre sıfırlandıysa eski refresh zinciri geçersizdir.
        await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, clock.UtcNow), ct);
        await db.SaveChangesAsync(ct);

        return new TemporaryPasswordDto(temporaryPassword);
    }

    public async Task DeactivateStaffAsync(long id, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, tracked: true, ct);
        if (!user.IsActive) return;

        await EnsureCanDeactivateAsync(user, ct);
        user.IsActive = false;
        user.UpdatedAtUtc = clock.UtcNow;
        await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, clock.UtcNow), ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Kilitlenme koruması: kullanıcı kendini ve son aktif Owner'ı pasife alamaz.</summary>
    private async Task EnsureCanDeactivateAsync(AppUser user, CancellationToken ct)
    {
        if (tenant.UserId == user.Id)
            throw new InvalidOperationException("Kendi hesabınızı pasife alamazsınız.");

        if (user.UserType != UserType.Owner) return;
        var tenantId = TenantId;
        var otherOwners = await db.Users.IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId && u.UserType == UserType.Owner
                             && u.IsActive && u.Id != user.Id, ct);
        if (otherOwners == 0)
            throw new InvalidOperationException("Kiracının son aktif sahibi (Owner) pasife alınamaz.");
    }

    private async Task<AppUser> FindStaffAsync(long id, bool tracked, CancellationToken ct)
    {
        var tenantId = TenantId;
        var source = tracked ? db.Users : db.Users.AsNoTracking();
        return await source.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
    }

    private async Task<IReadOnlyList<Role>> ResolveRolesAsync(IReadOnlyList<long> roleIds, CancellationToken ct)
    {
        if (roleIds is not { Count: > 0 })
            throw new InvalidOperationException("En az bir rol seçilmelidir.");

        var tenantId = TenantId;
        var ids = roleIds.Distinct().ToList();
        var roles = await db.Roles.Where(r => r.TenantId == tenantId && ids.Contains(r.Id)).ToListAsync(ct);
        if (roles.Count != ids.Count)
            throw new KeyNotFoundException("Seçilen rollerden bazıları bu kiracıya ait değil.");
        return roles;
    }

    private async Task<IReadOnlyList<StaffDto>> MapStaffAsync(
        IReadOnlyList<AppUser> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();

        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ids.Contains(ur.UserId))
            .Select(ur => new { ur.UserId, ur.RoleId, ur.Role!.Name, ur.Role.IsSystem })
            .ToListAsync(ct);

        var clinics = await db.UserClinics.AsNoTracking()
            .Where(uc => ids.Contains(uc.UserId))
            .Select(uc => new { uc.UserId, uc.ClinicId })
            .ToListAsync(ct);

        // Son giriş ayrı kolon değil; denetim kaydından (AuditLog, Login) okunur.
        var lastLogins = await db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId != null && ids.Contains(a.UserId.Value) && a.ActionType == AuditActionType.Login)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, LastLoginUtc = g.Max(a => a.AtUtc) })
            .ToListAsync(ct);

        return [.. users.Select(u => new StaffDto(
            u.Id, u.Email ?? "", u.FirstName, u.LastName, u.FullName, u.UserType, u.IsActive,
            u.MustChangePassword, u.Color, u.Branch, u.DiplomaNo,
            [.. roles.Where(r => r.UserId == u.Id).Select(r => new StaffRoleDto(r.RoleId, r.Name, r.IsSystem))],
            [.. clinics.Where(c => c.UserId == u.Id).Select(c => c.ClinicId)],
            lastLogins.FirstOrDefault(l => l.UserId == u.Id)?.LastLoginUtc,
            u.CreatedAtUtc))];
    }

    /// <summary>Identity politikasını (8+ hane, büyük/küçük harf, rakam) garanti eden geçici şifre.</summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        Span<char> buffer = stackalloc char[12];
        buffer[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        buffer[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        buffer[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < buffer.Length; i++) buffer[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        return new string(buffer);
    }

    // ---- Yetki matrisi ----

    public async Task<IReadOnlyList<RolePermissionsDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        return await db.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .Select(r => new RolePermissionsDto(
                r.Id, r.Name, r.IsSystem,
                r.Permissions.Select(p => p.Permission!.Code).OrderBy(c => c).ToList(),
                db.UserRoles.Count(ur => ur.RoleId == r.Id)))
            .ToListAsync(ct);
    }

    public async Task<RolePermissionsDto> UpdateRolePermissionsAsync(
        long roleId, RolePermissionsUpdateRequest request, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var role = await db.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Rol bulunamadı.");

        var codes = (request.Permissions ?? []).Select(c => c.Trim()).Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal).ToList();

        // KİLİTLENME KORUMASI: Owner rolünden settings.staff kaldırılamaz; aksi hâlde kiracıda
        // hiç kimse yetki matrisini geri açamaz.
        if (string.Equals(role.Name, "Owner", StringComparison.Ordinal)
            && !codes.Contains("settings.staff", StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Owner rolünden 'settings.staff' izni kaldırılamaz (yetki kilitlenmesi koruması).");

        var permissions = await db.Permissions.Where(p => codes.Contains(p.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Count)
        {
            var unknown = codes.Except(permissions.Select(p => p.Code), StringComparer.Ordinal);
            throw new KeyNotFoundException($"Bilinmeyen izin kodu: {string.Join(", ", unknown)}");
        }

        var currentIds = role.Permissions.Select(p => p.PermissionId).ToHashSet();
        var targetIds = permissions.Select(p => p.Id).ToHashSet();
        db.RolePermissions.RemoveRange(role.Permissions.Where(p => !targetIds.Contains(p.PermissionId)));
        foreach (var permission in permissions.Where(p => !currentIds.Contains(p.Id)))
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });

        await db.SaveChangesAsync(ct);

        // İzin değişikliği denetim kaydı; mevcut token'lar süresi dolana dek eski izinleri taşır
        // (yeni izinler bir sonraki access token'da geçerli olur).
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = tenant.UserId,
            ActionType = AuditActionType.PermissionChange,
            EntityName = nameof(Role),
            EntityId = roleId,
            NewValuesJson = JsonSerializer.Serialize(new { role = role.Name, permissions = codes }),
            AtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return (await ListRolesAsync(ct)).First(r => r.Id == roleId);
    }

    public PermissionCatalogDto GetPermissionCatalog() =>
        new(PermissionCatalog.ByModule.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)[.. kv.Value.Select(a => $"{kv.Key}.{a}")]));

    // ---- Entegrasyon ayarları ----

    public async Task<IReadOnlyList<IntegrationSettingDto>> ListIntegrationsAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var rows = await db.TenantIntegrationSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new { s.IntegrationKey, s.ProviderKey, s.Environment, s.IsEnabled, s.UpdatedAtUtc, s.UpdatedByUserId })
            .ToListAsync(ct);

        var result = new List<IntegrationSettingDto>(IntegrationCatalog.Keys.Count);
        foreach (var key in IntegrationCatalog.Keys)
        {
            var row = rows.FirstOrDefault(r => r.IntegrationKey == key);
            var providerKey = row?.ProviderKey ?? "fake";
            var snapshot = await integrationStore.GetAsync(tenantId, key, ct);
            result.Add(new IntegrationSettingDto(
                key, providerKey, row?.Environment ?? "Test", row?.IsEnabled ?? false,
                MaskSettings(key, providerKey, snapshot?.SettingsJson),
                [.. IntegrationCatalog.Fields(key, providerKey).Where(f => f.IsSecret).Select(f => f.Name)],
                IntegrationCatalog.Providers[key],
                row?.UpdatedAtUtc, row?.UpdatedByUserId));
        }
        return result;
    }

    public async Task<IntegrationSettingDto> UpdateIntegrationAsync(
        string integrationKey, IntegrationSettingUpdateRequest request, CancellationToken ct = default)
    {
        var key = Normalize(integrationKey);
        if (!IntegrationCatalog.IsKnownProvider(key, request.ProviderKey))
            throw new KeyNotFoundException($"'{request.ProviderKey}' sağlayıcısı '{key}' entegrasyonunda tanımlı değil.");

        var environment = string.Equals(request.Environment, "Live", StringComparison.OrdinalIgnoreCase)
            ? "Live" : "Test";
        var tenantId = TenantId;
        var existing = await integrationStore.GetAsync(tenantId, key, ct);
        var merged = MergeSettings(key, request.ProviderKey, existing?.SettingsJson, request.Settings);

        await integrationStore.UpsertAsync(
            tenantId, key, request.ProviderKey, environment, merged, request.IsEnabled, ct);

        await db.TenantIntegrationSettings
            .Where(s => s.TenantId == tenantId && s.IntegrationKey == key)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedByUserId, tenant.UserId), ct);

        return (await ListIntegrationsAsync(ct)).First(i => i.IntegrationKey == key);
    }

    public async Task<IntegrationTestResultDto> TestIntegrationAsync(
        string integrationKey, CancellationToken ct = default)
    {
        var key = Normalize(integrationKey);
        var tenantId = TenantId;
        var snapshot = await integrationStore.GetAsync(tenantId, key, ct);
        var providerKey = snapshot?.ProviderKey ?? "fake";
        var started = clock.UtcNow;

        try
        {
            var message = key switch
            {
                // SMS sürücüsünün gerçek ve ucuz bir okuma çağrısı var: kontör sorgusu.
                IntegrationCatalog.Sms => await TestSmsAsync(tenantId, ct),
                // Diğer ailelerde yan etkisiz "hafif çağrı" yoktur (gönderim/ödeme yaratır).
                // Bu yüzden test = sürücü çözümü + zorunlu kimlik alanlarının varlığı.
                _ => await TestCredentialsAsync(key, providerKey, snapshot?.SettingsJson, tenantId, ct),
            };
            return new IntegrationTestResultDto(true, message, Elapsed(started), providerKey);
        }
        catch (Exception ex) when (ex is SmsProviderException or EInvoiceProviderException
                                       or WhatsAppProviderException or PaymentProviderException
                                       or EnabizClientException or InvalidOperationException
                                       or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Entegrasyon testi başarısız: Tenant={TenantId} Key={Key}", tenantId, key);
            return new IntegrationTestResultDto(false, ex.Message, Elapsed(started), providerKey);
        }
    }

    private async Task<string> TestSmsAsync(long tenantId, CancellationToken ct)
    {
        var resolved = await providerFactory.ResolveAsync<ISmsProvider>(tenantId, ct);
        var balance = await resolved.Instance.GetBalanceAsync(ct);
        return $"Bağlantı başarılı ({resolved.ProviderKey}/{resolved.Environment}). Kontör: {balance:0.##}";
    }

    private async Task<string> TestCredentialsAsync(
        string key, string providerKey, string? settingsJson, long tenantId, CancellationToken ct)
    {
        // Sürücü çözümü ayar JSON'unun şifresini çözer ve ayar nesnesine uygular; bozuk ayar burada patlar.
        var resolvedProviderKey = key switch
        {
            IntegrationCatalog.EInvoice => (await providerFactory.ResolveAsync<IEInvoiceProvider>(tenantId, ct)).ProviderKey,
            IntegrationCatalog.WhatsApp => (await providerFactory.ResolveAsync<IWhatsAppProvider>(tenantId, ct)).ProviderKey,
            IntegrationCatalog.Payment => (await providerFactory.ResolveAsync<IPaymentProvider>(tenantId, ct)).ProviderKey,
            IntegrationCatalog.Enabiz => (await providerFactory.ResolveAsync<IEnabizClient>(tenantId, ct)).ProviderKey,
            _ => throw new KeyNotFoundException($"'{key}' entegrasyonu tanımlı değil."),
        };

        var required = IntegrationCatalog.RequiredFields(key, providerKey);
        var values = ParseJson(settingsJson);
        var missing = required
            .Where(f => string.IsNullOrWhiteSpace(values.TryGetValue(f, out var v) ? v : null))
            .ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Eksik kimlik bilgisi: {string.Join(", ", missing)}");

        return required.Count == 0
            ? $"Sürücü çözüldü ({resolvedProviderKey}); bu sağlayıcı için kimlik bilgisi gerekmiyor."
            : $"Kimlik bilgileri eksiksiz ve sürücü çözüldü ({resolvedProviderKey}).";
    }

    private int Elapsed(DateTime started) => (int)Math.Max(0, (clock.UtcNow - started).TotalMilliseconds);

    /// <summary>Sır alanları maskeler; diğer alanlar olduğu gibi döner.</summary>
    private static IReadOnlyDictionary<string, string?> MaskSettings(
        string key, string providerKey, string? settingsJson)
    {
        var values = ParseJson(settingsJson);
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in IntegrationCatalog.Fields(key, providerKey))
        {
            values.TryGetValue(field.Name, out var value);
            result[field.Name] = field.IsSecret ? Mask(value) : value;
        }
        return result;
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length <= 4 ? SecretMaskPrefix : SecretMaskPrefix + value[^4..];
    }

    /// <summary>Gelen ayarları mevcutlarla birleştirir; maskeli/boş sır alanı mevcudu korur.</summary>
    private static string MergeSettings(
        string key, string providerKey, string? existingJson, IReadOnlyDictionary<string, string?>? incoming)
    {
        var existing = ParseJson(existingJson);
        var node = new JsonObject();

        foreach (var field in IntegrationCatalog.Fields(key, providerKey))
        {
            existing.TryGetValue(field.Name, out var current);
            string? value = current;

            if (incoming is not null && TryGetIgnoreCase(incoming, field.Name, out var supplied))
            {
                var isMasked = supplied is not null && supplied.StartsWith(SecretMaskPrefix, StringComparison.Ordinal);
                // Sır: boş ya da maskeli gelirse mevcut korunur (yazma-tek-yönlü).
                value = field.IsSecret
                    ? (string.IsNullOrWhiteSpace(supplied) || isMasked ? current : supplied)
                    : supplied;
            }

            if (!string.IsNullOrWhiteSpace(value)) node[field.Name] = value;
        }

        return node.ToJsonString();
    }

    private static bool TryGetIgnoreCase(
        IReadOnlyDictionary<string, string?> source, string key, out string? value)
    {
        foreach (var pair in source)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            value = pair.Value;
            return true;
        }
        value = null;
        return false;
    }

    private static Dictionary<string, string?> ParseJson(string? json)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => property.Value.ToString(),
                };
            }
        }
        catch (JsonException)
        {
            // Bozuk/çözülemeyen ayar JSON'u boş kabul edilir; ekran yeniden doldurabilsin.
        }
        return result;
    }

    private static string Normalize(string integrationKey)
    {
        var match = IntegrationCatalog.Keys
            .FirstOrDefault(k => string.Equals(k, integrationKey, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new KeyNotFoundException($"'{integrationKey}' entegrasyonu tanımlı değil.");
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
