using FluentValidation;

namespace Dental.Application.Invoices;

public sealed class InvoiceDraftRequestValidator : AbstractValidator<InvoiceDraftRequest>
{
    public InvoiceDraftRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.PatientId is > 0) ^ (x.CompanyId is > 0))
            .WithMessage("Fatura alıcısı ya hasta ya da kurum olmalıdır (ikisi birden veya hiçbiri olamaz).");

        RuleFor(x => x)
            .Must(x => x.TreatmentRecordIds.Count > 0 || x.ManualLines is { Count: > 0 })
            .WithMessage("Faturada en az bir tedavi kaydı veya serbest kalem olmalıdır.");

        RuleFor(x => x.TreatmentRecordIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Aynı tedavi kaydı birden fazla kez faturalanamaz.");

        RuleFor(x => x.SourceInvoiceId)
            .NotNull().When(x => x.IsRefund)
            .WithMessage("İade belgesinde kaynak fatura zorunludur.");

        RuleForEach(x => x.ManualLines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemName).NotEmpty().MaximumLength(300);
            line.RuleFor(l => l.Quantity).GreaterThan(0m);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m);
            line.RuleFor(l => l.DiscountAmount).GreaterThanOrEqualTo(0m);
            line.RuleFor(l => l.VatRate).InclusiveBetween(0m, 100m).When(l => l.VatRate is not null);
        });
    }
}

public sealed class InvoiceCancelRequestValidator : AbstractValidator<InvoiceCancelRequest>
{
    public InvoiceCancelRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}
