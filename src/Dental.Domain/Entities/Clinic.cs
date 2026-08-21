using Dental.Domain.Common;

namespace Dental.Domain.Entities;

public class Clinic : TenantEntity
{
    public required string Name { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>ÇKYS tesis kodu — e-Nabız/USS gönderiminde kullanılır.</summary>
    public string? CkysCode { get; set; }
    public string TimeZone { get; set; } = "Europe/Istanbul";
    public long? LogoFileId { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<ClinicWorkingHour> WorkingHours { get; set; } = [];
}

public class ClinicWorkingHour : TenantEntity
{
    public long ClinicId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public bool IsClosed { get; set; }

    public Clinic? Clinic { get; set; }
}
