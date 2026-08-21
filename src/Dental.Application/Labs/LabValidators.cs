using Dental.Domain.Common;
using FluentValidation;

namespace Dental.Application.Labs;

public sealed class LaboratoryUpsertValidator : AbstractValidator<LaboratoryUpsertRequest>
{
    public LaboratoryUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.ContactPerson).MaximumLength(100);
    }
}

public sealed class LabCaseUpsertValidator : AbstractValidator<LabCaseUpsertRequest>
{
    public LabCaseUpsertValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.DoctorUserId).GreaterThan(0);
        RuleFor(x => x.LaboratoryId).GreaterThan(0);
        RuleFor(x => x.WorkType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Shade).MaximumLength(10);
        RuleFor(x => x.Material).MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.TeethCsv)
            .Must(csv => csv!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .All(FdiTeeth.IsValid))
            .When(x => !string.IsNullOrWhiteSpace(x.TeethCsv))
            .WithMessage("TeethCsv yalnız geçerli FDI diş numaraları içermelidir ('11,12,21').");
    }
}

public sealed class LabCaseStatusChangeValidator : AbstractValidator<LabCaseStatusChangeRequest>
{
    public LabCaseStatusChangeValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
