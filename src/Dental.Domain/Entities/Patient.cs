using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Hasta kartı. TCKN yalnız şifreli saklanır (TcknEncrypted, Data Protection) ve eşitlik
/// araması/tekillik anahtarlı HMAC-SHA256 özeti (TcknHash) üzerinden çalışır (denetim (a)-9 kararı).
/// </summary>
public class Patient : TenantEntity
{
    public long ClinicId { get; set; }
    /// <summary>Klinik içi dosya numarası; tenant içinde benzersiz (filtered unique index).</summary>
    public required string FileNo { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    /// <summary>Computed persisted kolon: UPPER(FirstName + ' ' + LastName) — liste araması bu kolondan.</summary>
    public string SearchName { get; private set; } = "";
    public IdentityType IdentityType { get; set; } = IdentityType.Tckn;
    public string? TcknEncrypted { get; set; }
    public string? TcknHash { get; set; }
    public string? PassportNo { get; set; }
    /// <summary>SKRS uyruk kodu (alfa-3).</summary>
    public string NationalityCode { get; set; } = "TUR";
    /// <summary>Yabancı hasta — 334 istisna faturası için son Türkiye'ye giriş tarihi.</summary>
    public DateOnly? LastEntryDate { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender Gender { get; set; }
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    /// <summary>Anlaşmalı kurum — FK, Company tablosuyla sonraki aşamada bağlanacak.</summary>
    public long? CompanyId { get; set; }
    public string? ReferralSource { get; set; }
    public string? Profession { get; set; }
    public string? GeneralNote { get; set; }
    /// <summary>Denormalize cari bakiye — LedgerEntry aşamasında güncellenecek; şimdilik 0.</summary>
    public decimal Balance { get; set; }
    public long? PhotoFileId { get; set; }

    public ICollection<CommunicationConsent> Consents { get; set; } = [];
    public ICollection<PatientTagAssignment> Tags { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>KVKK/iletişim izinleri. Pazarlama gönderimi SmsMarketing izni + İYS kontrolüne bağlıdır.</summary>
public class CommunicationConsent : TenantEntity
{
    public long PatientId { get; set; }
    public ConsentType ConsentType { get; set; }
    public bool IsGranted { get; set; }
    public DateTime? GrantedAtUtc { get; set; }
    public ConsentSource Source { get; set; } = ConsentSource.Verbal;
    /// <summary>İmzalı form bağlantısı — ConsentForm tablosuyla sonraki aşamada bağlanacak.</summary>
    public long? ConsentFormId { get; set; }

    public Patient? Patient { get; set; }
}

/// <summary>Hasta etiketi (toplu mesaj filtreleri ve liste rozetleri için).</summary>
public class PatientTag : TenantEntity
{
    public required string Name { get; set; }
    public string? ColorHex { get; set; }
}

public class PatientTagAssignment
{
    public long PatientId { get; set; }
    public long PatientTagId { get; set; }

    public Patient? Patient { get; set; }
    public PatientTag? Tag { get; set; }
}
