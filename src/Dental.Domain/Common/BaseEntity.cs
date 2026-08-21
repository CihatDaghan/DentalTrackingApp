namespace Dental.Domain.Common;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>Kiracıya ait tüm varlıklar; global query filter + SaveChanges damgalaması bu arayüz üzerinden uygulanır.</summary>
public interface ITenantOwned
{
    long TenantId { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    long? DeletedByUserId { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantOwned, ISoftDelete
{
    public long TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedByUserId { get; set; }
}
