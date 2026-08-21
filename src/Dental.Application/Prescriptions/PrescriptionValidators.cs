using FluentValidation;

namespace Dental.Application.Prescriptions;

public sealed class DrugCreateValidator : AbstractValidator<DrugCreateRequest>
{
    public DrugCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Barcode).MaximumLength(20);
        RuleFor(x => x.AtcCode).MaximumLength(10);
        RuleFor(x => x.Form).MaximumLength(50);
        RuleFor(x => x.DefaultDose).MaximumLength(100);
        RuleFor(x => x.DefaultUsage).MaximumLength(100);
    }
}

public sealed class PrescriptionTemplateUpsertValidator : AbstractValidator<PrescriptionTemplateUpsertRequest>
{
    public PrescriptionTemplateUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Şablonda en az bir ilaç kalemi olmalıdır.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DrugId).GreaterThan(0);
            item.RuleFor(i => i.BoxCount).InclusiveBetween(1, 99);
            item.RuleFor(i => i.Dose).MaximumLength(100);
            item.RuleFor(i => i.Frequency).MaximumLength(50);
            item.RuleFor(i => i.Duration).MaximumLength(50);
            item.RuleFor(i => i.UsageNote).MaximumLength(300);
        });
    }
}

public sealed class PrescriptionCreateValidator : AbstractValidator<PrescriptionCreateRequest>
{
    public PrescriptionCreateValidator()
    {
        RuleFor(x => x.DoctorUserId).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.TemplateId is not null || x.Items is { Count: > 0 })
            .WithMessage("Şablon veya en az bir ilaç kalemi verilmelidir.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DrugId).GreaterThan(0);
            item.RuleFor(i => i.BoxCount).InclusiveBetween(1, 99);
            item.RuleFor(i => i.Dose).MaximumLength(100);
            item.RuleFor(i => i.Frequency).MaximumLength(50);
            item.RuleFor(i => i.Duration).MaximumLength(50);
            item.RuleFor(i => i.UsageNote).MaximumLength(300);
        });
    }
}

public sealed class PrescriptionSaveAsTemplateValidator : AbstractValidator<PrescriptionSaveAsTemplateRequest>
{
    public PrescriptionSaveAsTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
