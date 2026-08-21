using FluentValidation;

namespace Dental.Application.Appointments;

public sealed class RecallCreateValidator : AbstractValidator<RecallCreateRequest>
{
    public RecallCreateValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class RecallConvertValidator : AbstractValidator<RecallConvertRequest>
{
    public RecallConvertValidator()
    {
        RuleFor(x => x.ClinicId).GreaterThan(0);
        RuleFor(x => x.DoctorUserId).GreaterThan(0);
        RuleFor(x => x.EndUtc).GreaterThan(x => x.StartUtc)
            .WithMessage("Randevu bitişi başlangıçtan sonra olmalıdır.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
