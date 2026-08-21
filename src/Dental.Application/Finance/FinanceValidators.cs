using FluentValidation;

namespace Dental.Application.Finance;

public sealed class PaymentCreateValidator : AbstractValidator<PaymentCreateRequest>
{
    public PaymentCreateValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x)
            .Must(x => (x.PatientId is not null) ^ (x.CompanyId is not null))
            .WithMessage("PatientId veya CompanyId'den tam biri gönderilmelidir.");
        RuleFor(x => x.InstallmentCount).InclusiveBetween((byte)2, (byte)24)
            .When(x => x.InstallmentCount is not null);
        RuleFor(x => x.InstallmentCount).Null()
            .When(x => x.Method != Domain.Enums.PaymentMethod.CreditCardPos)
            .WithMessage("Taksit sayısı yalnız kredi kartı (POS) tahsilatında gönderilebilir.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
    }
}

public sealed class DiscountValidator : AbstractValidator<DiscountRequest>
{
    public DiscountValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => (x.PatientId is not null) ^ (x.CompanyId is not null))
            .WithMessage("PatientId veya CompanyId'den tam biri gönderilmelidir.");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class ManualLedgerEntryValidator : AbstractValidator<ManualLedgerEntryRequest>
{
    public ManualLedgerEntryValidator()
    {
        RuleFor(x => x.EntryType).IsInEnum()
            .Must(t => t is Domain.Enums.LedgerEntryType.OpeningBalance or Domain.Enums.LedgerEntryType.Correction
                or Domain.Enums.LedgerEntryType.Refund or Domain.Enums.LedgerEntryType.CompanyTransfer)
            .WithMessage("Elle yalnız OpeningBalance/Correction/Refund/CompanyTransfer kaydı girilebilir " +
                         "(tedavi borcu ve tahsilat ilgili servislerden doğar).");
        RuleFor(x => x.Debit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Credit).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => (x.Debit > 0) ^ (x.Credit > 0))
            .WithMessage("Debit veya Credit'ten tam biri sıfırdan büyük olmalıdır.");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class PaymentPlanCreateValidator : AbstractValidator<PaymentPlanCreateRequest>
{
    public PaymentPlanCreateValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.InstallmentCount).InclusiveBetween(2, 36);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CompanyUpsertValidator : AbstractValidator<CompanyUpsertRequest>
{
    public CompanyUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxOffice).MaximumLength(100);
        RuleFor(x => x.Vkn).Matches("^[0-9]{10}$").When(x => !string.IsNullOrEmpty(x.Vkn))
            .WithMessage("VKN 10 haneli rakam olmalıdır.");
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.EInvoiceAlias).MaximumLength(150);
    }
}

public sealed class ExpenseCategoryUpsertValidator : AbstractValidator<ExpenseCategoryUpsertRequest>
{
    public ExpenseCategoryUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class ExpenseUpsertValidator : AbstractValidator<ExpenseUpsertRequest>
{
    public ExpenseUpsertValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
