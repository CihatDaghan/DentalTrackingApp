using FluentValidation;

namespace Dental.Application.Messaging;

public sealed class MessageTemplateUpsertValidator : AbstractValidator<MessageTemplateUpsertRequest>
{
    public MessageTemplateUpsertValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(50)
            .Matches("^[a-z0-9_]+$").WithMessage("Şablon anahtarı yalnız küçük harf, rakam ve alt çizgi içerebilir.");
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(5);
        // SMS gövdesi çok uzun olursa çok parçalı gönderime düşer; 1000 karakter üst sınırdır.
        RuleFor(x => x.Body).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public sealed class WhatsAppTemplateUpsertValidator : AbstractValidator<WhatsAppTemplateUpsertRequest>
{
    private static readonly string[] Categories = ["utility", "marketing", "authentication"];

    public WhatsAppTemplateUpsertValidator()
    {
        RuleFor(x => x.TemplateName).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9_]+$").WithMessage("Meta şablon adı yalnız küçük harf, rakam ve alt çizgi içerebilir.");
        RuleFor(x => x.Language).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Category).NotEmpty()
            .Must(c => Categories.Contains(c.Trim().ToLowerInvariant()))
            .WithMessage("Kategori 'utility', 'marketing' ya da 'authentication' olmalıdır.");
        RuleFor(x => x.BodySpec).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.MetaStatus).IsInEnum();
    }
}

public sealed class MessageSendValidator : AbstractValidator<MessageSendRequest>
{
    public MessageSendValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BodyOverride).MaximumLength(1000);
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public sealed class BulkMessageValidator : AbstractValidator<BulkMessageRequest>
{
    public BulkMessageValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BodyOverride).MaximumLength(1000);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Filter.BirthMonth).InclusiveBetween(1, 12)
            .When(x => x.Filter.BirthMonth is not null);
    }
}
