using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Clinical;

public sealed class AnamnesisTemplateUpsertValidator : AbstractValidator<AnamnesisTemplateUpsertRequest>
{
    public AnamnesisTemplateUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Questions).NotEmpty().WithMessage("Şablon en az bir soru içermelidir.");
        RuleForEach(x => x.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(500);
            q.RuleFor(x => x.QuestionTextEn).MaximumLength(500);
            q.RuleFor(x => x.AnswerType).IsInEnum();
            q.RuleFor(x => x.OptionsJson).NotEmpty()
                .When(x => x.AnswerType == AnamnesisAnswerType.MultiSelect)
                .WithMessage("MultiSelect soruda seçenek listesi (OptionsJson) zorunludur.");
        });
    }
}

public sealed class AnamnesisFillValidator : AbstractValidator<AnamnesisFillRequest>
{
    public AnamnesisFillValidator()
    {
        RuleFor(x => x.TemplateId).GreaterThan(0);
        RuleFor(x => x.Answers).NotEmpty().WithMessage("En az bir yanıt gönderilmelidir.");
        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(x => x.QuestionId).GreaterThan(0);
            a.RuleFor(x => x.TextValue).MaximumLength(1000);
            a.RuleFor(x => x)
                .Must(x => x.BoolValue is not null || !string.IsNullOrWhiteSpace(x.TextValue))
                .WithMessage("Yanıt boş olamaz: BoolValue veya TextValue gönderilmelidir.");
        });
    }
}

public sealed class PatientNoteUpsertValidator : AbstractValidator<PatientNoteUpsertRequest>
{
    public PatientNoteUpsertValidator()
    {
        RuleFor(x => x.NoteText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ColorHex).Matches("^#[0-9a-fA-F]{6}$")
            .When(x => !string.IsNullOrEmpty(x.ColorHex))
            .WithMessage("Renk #rrggbb biçiminde olmalıdır.");
    }
}
