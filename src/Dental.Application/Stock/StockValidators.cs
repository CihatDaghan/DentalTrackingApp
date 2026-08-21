using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Stock;

public sealed class StockCategoryUpsertValidator : AbstractValidator<StockCategoryUpsertRequest>
{
    public StockCategoryUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class StockItemUpsertValidator : AbstractValidator<StockItemUpsertRequest>
{
    public StockItemUpsertValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Barcode).MaximumLength(30);
        RuleFor(x => x.MinQty).GreaterThanOrEqualTo(0);
    }
}

public sealed class StockMovementCreateValidator : AbstractValidator<StockMovementCreateRequest>
{
    public StockMovementCreateValidator()
    {
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.RefType).IsInEnum();
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.Note).MaximumLength(500);
        // In/Out: pozitif miktar; Adjustment: yeni mutlak değer >= 0.
        RuleFor(x => x.Qty).GreaterThan(0)
            .When(x => x.Direction != StockMovementDirection.Adjustment)
            .WithMessage("Giriş/çıkış miktarı sıfırdan büyük olmalıdır.");
        RuleFor(x => x.Qty).GreaterThanOrEqualTo(0)
            .When(x => x.Direction == StockMovementDirection.Adjustment)
            .WithMessage("Sayım değeri negatif olamaz.");
    }
}
