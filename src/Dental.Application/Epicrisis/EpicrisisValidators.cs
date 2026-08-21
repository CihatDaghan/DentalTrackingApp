using FluentValidation;

namespace Dental.Application.Epicrisis;

public sealed class EpicrisisCreateValidator : AbstractValidator<EpicrisisCreateRequest>
{
    public EpicrisisCreateValidator()
    {
        RuleFor(x => x.DoctorUserId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BodyText).MaximumLength(8000);
        RuleForEach(x => x.Diagnoses).ChildRules(d =>
        {
            d.RuleFor(i => i.Code).NotEmpty().MaximumLength(10);
            d.RuleFor(i => i.Name).NotEmpty().MaximumLength(300);
        });
    }
}
