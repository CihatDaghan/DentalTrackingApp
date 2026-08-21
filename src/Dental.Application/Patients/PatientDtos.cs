using Dental.Domain.Enums;

namespace Dental.Application.Patients;

public sealed record ConsentUpsertDto(ConsentType ConsentType, bool IsGranted, ConsentSource Source = ConsentSource.Verbal);

public sealed record ConsentDto(ConsentType ConsentType, bool IsGranted, DateTime? GrantedAtUtc, ConsentSource Source);

public sealed record PatientUpsertRequest(
    string FirstName,
    string LastName,
    IdentityType IdentityType = IdentityType.Tckn,
    /// <summary>Düz TCKN — yalnız istek gövdesinde; null=değiştirme, ""=temizle.</summary>
    string? Tckn = null,
    string? PassportNo = null,
    string NationalityCode = "TUR",
    DateOnly? LastEntryDate = null,
    DateOnly? BirthDate = null,
    Gender Gender = Gender.Unknown,
    string? Phone = null,
    string? Phone2 = null,
    string? Email = null,
    string? Address = null,
    string? City = null,
    string? District = null,
    string? ReferralSource = null,
    string? Profession = null,
    string? GeneralNote = null,
    string? FileNo = null,
    long? ClinicId = null,
    IReadOnlyList<ConsentUpsertDto>? Consents = null);

public sealed record PatientDto(
    long Id,
    long ClinicId,
    string FileNo,
    string FirstName,
    string LastName,
    IdentityType IdentityType,
    string? Tckn,
    string? PassportNo,
    string NationalityCode,
    DateOnly? LastEntryDate,
    DateOnly? BirthDate,
    Gender Gender,
    string? Phone,
    string? Phone2,
    string? Email,
    string? Address,
    string? City,
    string? District,
    string? ReferralSource,
    string? Profession,
    string? GeneralNote,
    decimal Balance,
    DateTime CreatedAtUtc,
    IReadOnlyList<ConsentDto> Consents);

public sealed record PatientListItemDto(
    long Id,
    string FileNo,
    string FirstName,
    string LastName,
    string? Phone,
    string? City,
    DateOnly? BirthDate,
    decimal Balance,
    DateTime CreatedAtUtc);

public sealed record PatientSummaryDto(long Id, string FullName, int? Age, decimal Balance, string? Phone);

public sealed record PatientListQuery(string? Search = null, string? Sort = null, int Page = 1, int PageSize = 25);
