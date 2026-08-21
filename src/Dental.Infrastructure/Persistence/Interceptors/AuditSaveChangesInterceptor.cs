using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dental.Infrastructure.Persistence.Interceptors;

/// <summary>
/// SaveChanges kesicisi:
/// 1) CreatedAtUtc/UpdatedAtUtc damgalar,
/// 2) yeni ITenantOwned kayıtlara bağlamdaki TenantId'yi yazar; bağlam-dışı tenant'a yazmayı reddeder,
/// 3) hard delete'i soft delete'e çevirir (ISoftDelete),
/// 4) iş tablosu değişikliklerini AuditLog'a düşürür (hassas alanlar maskeli).
/// </summary>
public sealed class AuditSaveChangesInterceptor(ITenantContext tenant, IClock clock) : SaveChangesInterceptor
{
    private static readonly HashSet<string> SensitiveProperties = ["TcknEncrypted", "TcknHash", "PasswordHash", "SettingsJsonEncrypted", "TokenHash", "SecurityStamp"];
    private static readonly HashSet<Type> UnauditedTypes = [typeof(AuditLog), typeof(IntegrationCallLog), typeof(RefreshToken), typeof(Notification)];

    // Insert edilen kayıtların Id'si SaveChanges sonrası belli olur; audit'ler iki fazlı yazılır.
    private readonly List<(EntityEntry Entry, AuditLog Audit)> _pending = [];
    private bool _savingAudits;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null && !_savingAudits) Process(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && !_savingAudits) Process(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAudits(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// SaveChanges hata verirse bekleyen denetim kayıtları atılır; aksi halde sonraki
    /// (başarılı) kaydetmede kaydedilmemiş değişikliklerin denetimi yazılırdı.
    /// </summary>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FlushAuditsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void FlushAudits(DbContext? context)
    {
        if (context is null || _savingAudits || _pending.Count == 0) return;
        PreparePendingAudits(context);
        _savingAudits = true;
        try { context.SaveChanges(); }
        finally { _savingAudits = false; }
    }

    private async Task FlushAuditsAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null || _savingAudits || _pending.Count == 0) return;
        PreparePendingAudits(context);
        _savingAudits = true;
        try { await context.SaveChangesAsync(ct); }
        finally { _savingAudits = false; }
    }

    private void PreparePendingAudits(DbContext context)
    {
        foreach (var (entry, audit) in _pending)
        {
            audit.EntityId = entry.Entity is BaseEntity b ? b.Id : (entry.Entity as AppUser)?.Id;
            context.Set<AuditLog>().Add(audit);
        }
        _pending.Clear();
    }

    private void Process(DbContext context)
    {
        var now = clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            if (entry.Entity is BaseEntity be)
            {
                if (entry.State == EntityState.Added) be.CreatedAtUtc = now;
                else if (entry.State == EntityState.Modified) be.UpdatedAtUtc = now;
            }

            if (entry.Entity is ITenantOwned owned)
            {
                if (entry.State == EntityState.Added)
                {
                    if (owned.TenantId == 0)
                    {
                        owned.TenantId = tenant.TenantId
                            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan kiracıya ait kayıt eklenemez.");
                    }
                    else if (!tenant.IsSuperAdmin && tenant.TenantId is not null && owned.TenantId != tenant.TenantId)
                    {
                        throw new InvalidOperationException("Farklı bir kiracı adına kayıt eklenemez.");
                    }
                }
                else if (entry.State == EntityState.Modified && !tenant.IsSuperAdmin
                         && tenant.TenantId is not null && owned.TenantId != tenant.TenantId)
                {
                    throw new InvalidOperationException("Farklı bir kiracının kaydı değiştirilemez.");
                }
            }

            if (entry is { State: EntityState.Deleted, Entity: ISoftDelete sd })
            {
                entry.State = EntityState.Modified;
                sd.IsDeleted = true;
                sd.DeletedAtUtc = now;
                sd.DeletedByUserId = tenant.UserId;
            }

            if (!UnauditedTypes.Contains(entry.Entity.GetType()) && entry.Entity is BaseEntity or AppUser)
                _pending.Add((entry, BuildAudit(entry, now)));
        }
    }

    private AuditLog BuildAudit(EntityEntry entry, DateTime now)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditActionType.Create,
            _ when entry.Entity is ISoftDelete { IsDeleted: true } && WasJustSoftDeleted(entry) => AuditActionType.SoftDelete,
            _ => AuditActionType.Update,
        };

        Dictionary<string, object?>? oldValues = null;
        Dictionary<string, object?>? newValues = null;

        foreach (var prop in entry.Properties)
        {
            if (entry.State == EntityState.Modified && !prop.IsModified) continue;
            var name = prop.Metadata.Name;
            var masked = SensitiveProperties.Contains(name);
            if (entry.State == EntityState.Modified)
                (oldValues ??= [])[name] = masked ? "***" : prop.OriginalValue;
            (newValues ??= [])[name] = masked ? "***" : prop.CurrentValue;
        }

        return new AuditLog
        {
            TenantId = (entry.Entity as ITenantOwned)?.TenantId ?? tenant.TenantId,
            UserId = tenant.UserId,
            ActionType = action,
            EntityName = entry.Metadata.ClrType.Name,
            EntityId = entry.Entity is BaseEntity b ? b.Id : (entry.Entity as AppUser)?.Id,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            AtUtc = now,
        };
    }

    private static bool WasJustSoftDeleted(EntityEntry entry)
    {
        var prop = entry.Property(nameof(ISoftDelete.IsDeleted));
        return prop.IsModified && Equals(prop.CurrentValue, true) && Equals(prop.OriginalValue, false);
    }
}
