using FluentValidation;

namespace Dental.Application.Consents;

public sealed class ConsentTemplateUpsertValidator : AbstractValidator<ConsentTemplateUpsertRequest>
{
    public ConsentTemplateUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BodyHtml).NotEmpty();
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(5);
    }
}

public sealed class ConsentCreateValidator : AbstractValidator<ConsentCreateRequest>
{
    public ConsentCreateValidator()
    {
        RuleFor(x => x.TemplateId).GreaterThan(0);
    }
}

public sealed class ConsentSignValidator : AbstractValidator<ConsentSignRequest>
{
    public ConsentSignValidator()
    {
        RuleFor(x => x.SignaturePngBase64).NotEmpty();
    }
}

public sealed class PublicConsentSignValidator : AbstractValidator<PublicConsentSignRequest>
{
    public PublicConsentSignValidator()
    {
        RuleFor(x => x.SignaturePngBase64).NotEmpty()
            .When(x => !x.Declined)
            .WithMessage("İmza görüntüsü zorunludur (reddetme dışında).");
    }
}
