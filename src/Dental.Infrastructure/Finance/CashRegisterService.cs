using Dental.Application.Finance;
using Dental.Domain.Common;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Finance;

/// <summary>
/// Kasa günlük özeti: yöntem bazında tahsilat toplamları + gider toplamı + net,
/// ve saat sıralı gün hareket listesi (tahsilatlar + giderler).
/// </summary>
public sealed class CashRegisterService(AppDbContext db) : ICashRegisterService
{
    public async Task<CashRegisterDailySummaryDto> GetDailySummaryAsync(
        DateOnly date, long? clinicId = null, CancellationToken ct = default)
    {
        // Kasa günü Türkiye yerel günüdür; UTC damgalar yerel gün aralığına çevrilir
        // (inceleme bulgusu: UTC gün sınırı 00:00-03:00 yerel tahsilatları yanlış güne yazıyordu).
        var (dayStart, dayEnd) = TrTime.DayRangeUtc(date);

        var paymentsQuery = db.Payments.AsNoTracking()
            .Where(p => p.ReceivedAtUtc >= dayStart && p.ReceivedAtUtc < dayEnd);
        var expensesQuery = db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate == date);
        if (clinicId is { } clinic)
        {
            paymentsQuery = paymentsQuery.Where(p => p.ClinicId == clinic);
            expensesQuery = expensesQuery.Where(e => e.ClinicId == clinic);
        }

        var payments = await (
            from p in paymentsQuery
            join u in db.Users on p.ReceivedByUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            join pt in db.Patients on p.PatientId equals pt.Id into ptj
            from pt in ptj.DefaultIfEmpty()
            join co in db.Companies on p.CompanyId equals co.Id into coj
            from co in coj.DefaultIfEmpty()
            select new CashMovementDto(
                p.ReceivedAtUtc, CashMovementKind.Payment,
                pt != null ? pt.FirstName + " " + pt.LastName : co != null ? co.Name : null,
                p.Method, p.Amount,
                u != null ? u.FirstName + " " + u.LastName : null,
                p.Note)).ToListAsync(ct);

        var expenses = await (
            from e in expensesQuery
            join u in db.Users on e.PaidByUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            select new CashMovementDto(
                e.CreatedAtUtc, CashMovementKind.Expense,
                e.Category!.Name,
                e.Method, e.Amount,
                u != null ? u.FirstName + " " + u.LastName : null,
                e.Description)).ToListAsync(ct);

        var byMethod = payments
            .GroupBy(p => p.Method)
            .OrderBy(g => g.Key)
            .Select(g => new CashMethodTotalDto(g.Key, g.Sum(p => p.Amount), g.Count()))
            .ToList();

        var totalCollections = payments.Sum(p => p.Amount);
        var totalExpenses = expenses.Sum(e => e.Amount);

        return new CashRegisterDailySummaryDto(
            date, clinicId, byMethod, totalCollections, totalExpenses,
            Net: totalCollections - totalExpenses,
            Movements: [.. payments.Concat(expenses).OrderBy(m => m.AtUtc)]);
    }
}
