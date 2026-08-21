using Dental.Application.Common;
using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Patients;

public sealed class PatientUpsertValidator : AbstractValidator<PatientUpsertRequest>
{
    public PatientUpsertValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FileNo).MaximumLength(20);
        RuleFor(x => x.NationalityCode).NotEmpty().Length(3);

        RuleFor(x => x.Tckn)
            .Must(TcknValidator.IsValid)
            .When(x => x.IdentityType == IdentityType.Tckn && !string.IsNullOrEmpty(x.Tckn))
            .WithMessage("Geçersiz TCKN.");
        RuleFor(x => x.PassportNo)
            .NotEmpty().MaximumLength(20)
            .When(x => x.IdentityType == IdentityType.Passport)
            .WithMessage("Pasaportlu hasta için pasaport numarası zorunludur.");

        // +90 formatına zorlamadan makul telefon deseni
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9 ()\-]{7,20}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Geçersiz telefon numarası.");
        RuleFor(x => x.Phone2).Matches(@"^\+?[0-9 ()\-]{7,20}$").When(x => !string.IsNullOrEmpty(x.Phone2))
            .WithMessage("Geçersiz telefon numarası.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.BirthDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.BirthDate is not null)
            .WithMessage("Doğum tarihi gelecekte olamaz.");
        RuleFor(x => x.LastEntryDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.LastEntryDate is not null)
            .WithMessage("Son giriş tarihi gelecekte olamaz.");
    }
}
