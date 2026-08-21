namespace Dental.Application.Abstractions;

/// <summary>
/// Geçerli isteğin/işin kiracı bağlamı. HTTP isteklerinde JWT claim'lerinden middleware doldurur;
/// Hangfire job'ları ve public uçlarda <see cref="ITenantScopeFactory"/> ile elle kurulur.
/// </summary>
public interface ITenantContext
{
    long? TenantId { get; }
    long? ClinicId { get; }
    long? UserId { get; }
    bool IsSuperAdmin { get; }
    /// <summary>Bağlam kuruldu mu? Kurulmadıysa tenant-scoped sorgular ve yazmalar reddedilir.</summary>
    bool IsResolved { get; }
}

/// <summary>Middleware ve scope factory'nin bağlamı doldurmak için kullandığı yazılabilir yüz.</summary>
public interface ITenantContextSetter
{
    void Set(long? tenantId, long? clinicId, long? userId, bool isSuperAdmin);
}

/// <summary>
/// JWT'siz ortamlar (Hangfire job'ları, webhook/public uçlar) için tenant bağlamlı DI scope açar.
/// Denetim düzeltmesi: global query filter'ların job'larda boş dönmesini/patlamasını önleyen zorunlu mekanizma.
/// </summary>
public interface ITenantScopeFactory
{
    /// <summary>
    /// Verilen kiracı için yeni bir DI scope açar; scope içindeki ITenantContext dolu gelir.
    /// <paramref name="userId"/> yalnız "işlemi yapan kullanıcı" alanı zorunlu olan servisler için
    /// verilir (ör. ödeme callback'inde tahsilatı linki oluşturan kullanıcı adına kaydetmek).
    /// </summary>
    ITenantScope CreateScope(long tenantId, long? clinicId = null, long? userId = null);
}

public interface ITenantScope : IDisposable
{
    IServiceProvider ServiceProvider { get; }
}
