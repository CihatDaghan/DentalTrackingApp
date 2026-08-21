using Dental.Application.Abstractions;
using Dental.Application.Finance;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Finance;

/// <summary>
/// Taksit planı. Eşit aylık taksitler üretimde bölünür (kuruş farkı son taksite eklenir);
/// Overdue kalıcı yazılmaz, sunumda DueDate &lt; bugün olan Pending/Partial → Overdue.
/// </summary>
public sealed class PaymentPlanService(
    AppDbContext db,
    IClock clock,
    IValidator<PaymentPlanCreateRequest> createValidator) : IPaymentPlanService
{
    public async Task<PaymentPlanDto> CreateAsync(PaymentPlanCreateRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        if (!await db.Patients.AnyAsync(p => p.Id == request.PatientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");

        var plan = new PaymentPlan
        {
            PatientId = request.PatientId,
            TotalAmount = request.TotalAmount,
            StartDate = request.StartDate,
            Note = request.Note,
        };

        // Eşit taksit: 2 hane aşağı yuvarla, kuruş farkını son taksite ekle.
        var baseAmount = Math.Round(request.TotalAmount / request.InstallmentCount, 2, MidpointRounding.ToZero);
        for (var i = 0; i < request.InstallmentCount; i++)
        {
            var isLast = i == request.InstallmentCount - 1;
            plan.Installments.Add(new PaymentPlanInstallment
            {
                SeqNo = i + 1,
                DueDate = request.StartDate.AddMonths(i),
                Amount = isLast ? request.TotalAmount - baseAmount * (request.InstallmentCount - 1) : baseAmount,
            });
        }

        db.PaymentPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return await GetAsync(plan.Id, ct);
    }

    public async Task<PaymentPlanDto> GetAsync(long id, CancellationToken ct = default)
    {
        var plan = await db.PaymentPlans.AsNoTracking()
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Taksit planı bulunamadı.");
        var patientName = await db.Patients.Where(p => p.Id == plan.PatientId)
            .Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct) ?? "";
        return ToDto(plan, patientName, Today());
    }

    public async Task<IReadOnlyList<PaymentPlanDto>> ListByPatientAsync(long patientId, CancellationToken ct = default)
    {
        var patientName = await db.Patients.Where(p => p.Id == patientId)
            .Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");

        var plans = await db.PaymentPlans.AsNoTracking()
            .Include(p => p.Installments)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);
        var today = Today();
        return [.. plans.Select(p => ToDto(p, patientName, today))];
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var plan = await db.PaymentPlans.Include(p => p.Installments).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Taksit planı bulunamadı.");
        if (plan.Installments.Any(i => i.PaidAmount > 0m))
            throw new InvalidOperationException("Ödemesi başlamış taksit planı silinemez.");

        db.PaymentPlanInstallments.RemoveRange(plan.Installments); // interceptor soft delete'e çevirir
        db.PaymentPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    // ---- Yardımcılar ----

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow);

    private static PaymentPlanDto ToDto(PaymentPlan plan, string patientName, DateOnly today) =>
        new(plan.Id, plan.PatientId, patientName, plan.TotalAmount,
            plan.Installments.Sum(i => i.PaidAmount), plan.StartDate, plan.Note,
            [.. plan.Installments.OrderBy(i => i.SeqNo)
                .Select(i => new InstallmentDto(i.Id, i.SeqNo, i.DueDate, i.Amount, i.PaidAmount,
                    EffectiveStatus(i, today)))]);

    /// <summary>Gecikme sorgu bazlıdır: vadesi geçmiş Pending/Partial taksit Overdue görünür.</summary>
    private static InstallmentStatus EffectiveStatus(PaymentPlanInstallment installment, DateOnly today) =>
        installment.Status is InstallmentStatus.Pending or InstallmentStatus.Partial && installment.DueDate < today
            ? InstallmentStatus.Overdue
            : installment.Status;
}
