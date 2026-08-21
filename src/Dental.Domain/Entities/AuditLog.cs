using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long? UserId { get; set; }
    public AuditActionType ActionType { get; set; }
    public required string EntityName { get; set; }
    public long? EntityId { get; set; }
    /// <summary>Yalnız değişen alanlar; hassas alanlar (TCKN vb.) maskeli yazılır.</summary>
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AtUtc { get; set; }
}
