using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Appointments;

public sealed class AppointmentUpsertValidator : AbstractValidator<AppointmentUpsertRequest>
{
    public AppointmentUpsertValidator()
    {
        RuleFor(x => x.ClinicId).GreaterThan(0);
        RuleFor(x => x.DoctorUserId).GreaterThan(0);
        RuleFor(x => x.EndUtc).GreaterThan(x => x.StartUtc)
            .WithMessage("Randevu bitişi başlangıçtan sonra olmalıdır.");
        RuleFor(x => x.PatientId).NotNull()
            .When(x => x.Type is not (AppointmentType.EmptySlot or AppointmentType.Blocked))
            .WithMessage("Boş slot/blok dışındaki randevular için hasta zorunludur.");
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Color).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrEmpty(x.Color));
    }
}

public sealed class WorkingHoursSaveValidator : AbstractValidator<WorkingHoursSaveRequest>
{
    public WorkingHoursSaveValidator()
    {
        RuleFor(x => x.DoctorUserId).GreaterThan(0);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ClinicId).GreaterThan(0);
            item.RuleFor(i => i.EndTime).GreaterThan(i => i.StartTime)
                .WithMessage("Çalışma saati bitişi başlangıçtan sonra olmalıdır.");
        });
    }
}
