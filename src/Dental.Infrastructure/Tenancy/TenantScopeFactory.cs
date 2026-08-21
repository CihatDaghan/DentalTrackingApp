using Dental.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dental.Infrastructure.Tenancy;

/// <summary>
/// Hangfire job'ları ve public/webhook uçları için tenant bağlamlı DI scope açar.
/// Job'lar çok-kiracıyı içeriden tararken her kiracı için ayrı scope kullanır.
/// </summary>
public sealed class TenantScopeFactory(IServiceScopeFactory scopeFactory) : ITenantScopeFactory
{
    public ITenantScope CreateScope(long tenantId, long? clinicId = null, long? userId = null)
    {
        var scope = scopeFactory.CreateScope();
        var setter = (ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        setter.Set(tenantId, clinicId, userId, isSuperAdmin: false);
        return new TenantScope(scope);
    }

    private sealed class TenantScope(IServiceScope inner) : ITenantScope
    {
        public IServiceProvider ServiceProvider => inner.ServiceProvider;
        public void Dispose() => inner.Dispose();
    }
}
