using Dental.Domain.Common;
using FluentValidation;

namespace Dental.Application.Treatments;

public sealed class TreatmentCategoryUpsertValidator : AbstractValidator<TreatmentCategoryUpsertRequest>
{
    public TreatmentCategoryUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).MaximumLength(100);
        RuleFor(x => x.ColorHex).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrEmpty(x.ColorHex))
            .WithMessage("Renk #rrggbb biçiminde olmalıdır.");
    }
}

public sealed class TreatmentDefinitionUpsertValidator : AbstractValidator<TreatmentDefinitionUpsertRequest>
{
    public TreatmentDefinitionUpsertValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.SutCode).MaximumLength(20);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
        RuleFor(x => x.ToothScope).IsInEnum();
        RuleFor(x => x.ToothStatusEffect).IsInEnum();
        RuleFor(x => x.DefaultDurationMinutes).GreaterThan(0).When(x => x.DefaultDurationMinutes is not null);
    }
}

public sealed class PriceListUpsertValidator : AbstractValidator<PriceListUpsertRequest>
{
    public PriceListUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3)
            .WithMessage("Para birimi 3 harfli ISO kodu olmalıdır (TRY, USD, EUR...).");
    }
}

public sealed class PriceListItemsSaveValidator : AbstractValidator<PriceListItemsSaveRequest>
{
    public PriceListItemsSaveValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TreatmentDefinitionId).GreaterThan(0);
            item.RuleFor(i => i.Price).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.TreatmentDefinitionId).Distinct().Count() == items.Count)
            .WithMessage("Aynı tedavi tanımı listede birden fazla kez yer alamaz.");
    }
}

public sealed class TreatmentAddValidator : AbstractValidator<TreatmentAddRequest>
{
    public TreatmentAddValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("En az bir tedavi kalemi gönderin.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TreatmentDefinitionId).GreaterThan(0);
            item.RuleFor(i => i.Status).IsInEnum()
                .Must(s => s != Domain.Enums.TreatmentRecordStatus.Cancelled)
                .WithMessage("Tedavi Cancelled durumuyla eklenemez.");
            item.RuleFor(i => i.ToothNumber)
                .Must(t => t is null || FdiTeeth.IsValid(t))
                .WithMessage("Geçersiz FDI diş numarası.");
            item.RuleFor(i => i.Price).GreaterThanOrEqualTo(0).When(i => i.Price is not null);
            item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.Description).MaximumLength(1000);
            item.RuleFor(i => i.DiagnosisIcdCode).MaximumLength(10);
            item.RuleFor(i => i.RootCanalCount).InclusiveBetween((byte)1, (byte)6).When(i => i.RootCanalCount is not null);
        });
    }
}

public sealed class TreatmentUpdateValidator : AbstractValidator<TreatmentUpdateRequest>
{
    public TreatmentUpdateValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price is not null);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).When(x => x.DiscountAmount is not null);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DiagnosisIcdCode).MaximumLength(10);
        RuleFor(x => x.RootCanalCount).InclusiveBetween((byte)1, (byte)6).When(x => x.RootCanalCount is not null);
    }
}

public sealed class TreatmentBulkPriceValidator : AbstractValidator<TreatmentBulkPriceRequest>
{
    public TreatmentBulkPriceValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Id).GreaterThan(0);
            item.RuleFor(i => i.Price).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0);
        });
    }
}
