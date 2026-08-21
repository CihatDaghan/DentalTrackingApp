using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Finance;
using Dental.Application.Notifications;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Finance;

/// <summary>
/// Tahsilat servisi. Payment satırı + PaymentIn ledger kaydı + bakiye güncelleme tek
/// transaction'dadır; hasta tahsilatı açık taksitlere vade sırasıyla (FIFO) dağıtılır.
/// </summary>
public sealed class PaymentService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    ILedgerService ledger,
    INotificationService notifications,
    IValidator<PaymentCreateRequest> createValidator,
    IValidator<DiscountRequest> discountValidator) : IPaymentService
{
    public async Task<PaymentDto> CreateAsync(PaymentCreateRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var accountType = request.PatientId is not null ? LedgerAccountType.Patient : LedgerAccountType.Company;
        var clinicId = await ResolveClinicIdAsync(request.ClinicId, request.PatientId, ct);
        var receivedBy = tenant.UserId
            ?? throw new InvalidOperationException("Tahsilatı alan kullanıcı bağlamı yok.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var receivedAt = request.ReceivedAtUtc ?? clock.UtcNow;
        // Önce ledger kaydı (bakiye aynı transaction'da satır kilidiyle güncellenir),
        // sonra Payment; RefId iki yönlü bağ için sonda yazılır.
        var entryId = await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            accountType, request.PatientId, request.CompanyId,
            LedgerEntryType.PaymentIn, Debit: 0m, Credit: request.Amount,
            Description: BuildDescription(request.Method, request.InstallmentCount, request.Note),
            EntryDate: Dental.Domain.Common.TrTime.ToLocalDate(receivedAt),
            ClinicId: clinicId,
            RefType: nameof(Payment),
            CurrencyCode: request.CurrencyCode,
            ExchangeRate: request.ExchangeRate), ct);

        var payment = new Payment
        {
            ClinicId = clinicId,
            PatientId = request.PatientId,
            CompanyId = request.CompanyId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            Method = request.Method,
            InstallmentCount = request.InstallmentCount,
            ReceivedAtUtc = receivedAt,
            ReceivedByUserId = receivedBy,
            LedgerEntryId = entryId,
            Note = request.Note,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        await db.LedgerEntries.Where(e => e.Id == entryId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RefId, payment.Id), ct);

        // Hasta tahsilatını açık taksitlere vade sırasıyla (FIFO) dağıt.
        if (request.PatientId is { } patientId)
            await AllocateToInstallmentsAsync(patientId, request.Amount, ct);

        await tx.CommitAsync(ct);

        var dto = await GetAsync(payment.Id, ct);
        await notifications.PublishAsync(new NotificationCreateRequest(
            NotificationEvents.PaymentReceived,
            "Tahsilat alındı",
            $"{dto.PatientName ?? dto.CompanyName ?? "Cari"} — {dto.Amount:#,##0.00} {dto.CurrencyCode}",
            LinkPath: dto.PatientId is { } pid ? $"/patients/{pid}/payments" : "/finance/payments"), ct);
        return dto;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Tahsilat bulunamadı.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Eşzamanlı iki silme isteği çift ters kayıt atıyordu (inceleme bulgusu):
        // soft-delete bayrağını önce atomik olarak sahiplen, yalnız kazanan devam etsin.
        var claimed = await db.Payments
            .Where(p => p.Id == id && !p.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAtUtc, clock.UtcNow)
                .SetProperty(p => p.DeletedByUserId, tenant.UserId), ct);
        if (claimed == 0)
            throw new KeyNotFoundException("Tahsilat bulunamadı.");

        // Ters kayıt: PaymentIn Credit'ti → Correction Debit; bakiye aynı transaction'da düzelir.
        await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            payment.PatientId is not null ? LedgerAccountType.Patient : LedgerAccountType.Company,
            payment.PatientId, payment.CompanyId,
            LedgerEntryType.Correction, Debit: payment.Amount, Credit: 0m,
            Description: $"Tahsilat silme (Payment #{payment.Id})",
            ClinicId: payment.ClinicId,
            RefType: nameof(Payment),
            RefId: payment.Id,
            CurrencyCode: payment.CurrencyCode,
            ExchangeRate: payment.ExchangeRate), ct);

        // Taksit dağıtımını geri çöz (inceleme bulgusu: dağıtım geri alınmayınca taksitler
        // "ödendi" kalıyor, sonraki tahsilatlar onları atlıyordu). Dağıtım eşleme tablosu
        // olmadığından silinen tutar kadar pay son taksitten geriye (LIFO) çözülür.
        if (payment.PatientId is { } deletedPatientId)
            await UnwindInstallmentsAsync(deletedPatientId, payment.Amount, ct);

        // Soft-delete yukarıdaki atomik sahiplenmede yazıldı; izlenen varlığı senkronla.
        db.Entry(payment).State = EntityState.Detached;
        await tx.CommitAsync(ct);
    }

    /// <summary>Silinen tahsilat tutarı kadar taksit payını ters sırayla (LIFO) geri çözer.</summary>
    private async Task UnwindInstallmentsAsync(long patientId, decimal amount, CancellationToken ct)
    {
        var allocated = await db.PaymentPlanInstallments
            .Where(i => i.Plan!.PatientId == patientId && i.PaidAmount > 0m)
            .OrderByDescending(i => i.DueDate).ThenByDescending(i => i.SeqNo)
            .ToListAsync(ct);

        var remaining = amount;
        foreach (var installment in allocated)
        {
            if (remaining <= 0m) break;
            var release = Math.Min(installment.PaidAmount, remaining);
            installment.PaidAmount -= release;
            installment.Status = installment.PaidAmount <= 0m
                ? InstallmentStatus.Pending
                : InstallmentStatus.Partial;
            remaining -= release;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<long> ApplyDiscountAsync(DiscountRequest request, CancellationToken ct = default)
    {
        await discountValidator.ValidateAndThrowAsync(request, ct);
        return await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            request.PatientId is not null ? LedgerAccountType.Patient : LedgerAccountType.Company,
            request.PatientId, request.CompanyId,
            LedgerEntryType.Discount, Debit: 0m, Credit: request.Amount,
            Description: request.Description ?? "İndirim",
            ClinicId: request.ClinicId), ct);
    }

    public async Task<PaymentDto> GetAsync(long id, CancellationToken ct = default) =>
        await Project(db.Payments.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Tahsilat bulunamadı.");

    public async Task<PagedResult<PaymentDto>> ListAsync(PaymentListQuery query, CancellationToken ct = default)
    {
        var q = db.Payments.AsNoTracking().AsQueryable();
        if (query.PatientId is { } patientId) q = q.Where(p => p.PatientId == patientId);
        if (query.CompanyId is { } companyId) q = q.Where(p => p.CompanyId == companyId);
        if (query.From is { } from) q = q.Where(p => p.ReceivedAtUtc >= Dental.Domain.Common.TrTime.DayRangeUtc(from).StartUtc);
        if (query.To is { } to) q = q.Where(p => p.ReceivedAtUtc < Dental.Domain.Common.TrTime.DayRangeUtc(to).EndUtc);
        if (query.Method is { } method) q = q.Where(p => p.Method == method);

        var page = new PageRequest(query.Page, query.PageSize);
        var totalCount = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(p => p.ReceivedAtUtc).ThenByDescending(p => p.Id)
            .Skip(page.Skip).Take(page.EffectivePageSize)).ToListAsync(ct);
        return new PagedResult<PaymentDto>(items, page.Page, page.EffectivePageSize, totalCount);
    }

    // ---- Yardımcılar ----

    /// <summary>
    /// Tutarı hastanın açık (Paid olmayan) taksitlerine vade sırasıyla dağıtır.
    /// Taksitleri aşan pay serbest tahsilat olarak kalır (yalnız cari bakiyeye yansır).
    /// </summary>
    private async Task AllocateToInstallmentsAsync(long patientId, decimal amount, CancellationToken ct)
    {
        var open = await db.PaymentPlanInstallments
            .Where(i => i.Plan!.PatientId == patientId && i.Status != InstallmentStatus.Paid)
            .OrderBy(i => i.DueDate).ThenBy(i => i.SeqNo)
            .ToListAsync(ct);

        var remaining = amount;
        foreach (var installment in open)
        {
            if (remaining <= 0m) break;
            var due = installment.Amount - installment.PaidAmount;
            if (due <= 0m) continue;

            var applied = Math.Min(due, remaining);
            installment.PaidAmount += applied;
            installment.Status = installment.PaidAmount >= installment.Amount
                ? InstallmentStatus.Paid
                : InstallmentStatus.Partial;
            remaining -= applied;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<long> ResolveClinicIdAsync(long? requestClinicId, long? patientId, CancellationToken ct)
    {
        if (requestClinicId is { } explicitClinic) return explicitClinic;
        if (tenant.ClinicId is { } contextClinic) return contextClinic;
        if (patientId is { } pid)
        {
            return await db.Patients.Where(p => p.Id == pid).Select(p => (long?)p.ClinicId).FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        }
        return await db.Clinics.OrderBy(c => c.Id).Select(c => (long?)c.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Kiracının kliniği yok; tahsilat için klinik gereklidir.");
    }

    private static string BuildDescription(PaymentMethod method, byte? installmentCount, string? note)
    {
        var methodText = method switch
        {
            PaymentMethod.Cash => "Nakit",
            PaymentMethod.CreditCardPos => installmentCount is { } n ? $"Kredi kartı ({n} taksit)" : "Kredi kartı",
            PaymentMethod.BankTransfer => "Havale/EFT",
            PaymentMethod.OnlineLink => "Online ödeme linki",
            PaymentMethod.Check => "Çek",
            _ => method.ToString(),
        };
        return string.IsNullOrWhiteSpace(note) ? $"Tahsilat — {methodText}" : $"Tahsilat — {methodText}: {note}";
    }

    private IQueryable<PaymentDto> Project(IQueryable<Payment> source) =>
        from p in source
        join u in db.Users on p.ReceivedByUserId equals u.Id into uj
        from u in uj.DefaultIfEmpty()
        join pt in db.Patients on p.PatientId equals pt.Id into ptj
        from pt in ptj.DefaultIfEmpty()
        join co in db.Companies on p.CompanyId equals co.Id into coj
        from co in coj.DefaultIfEmpty()
        select new PaymentDto(
            p.Id, p.ClinicId,
            p.PatientId, pt != null ? pt.FirstName + " " + pt.LastName : null,
            p.CompanyId, co != null ? co.Name : null,
            p.Amount, p.CurrencyCode.Trim(), p.Method, p.InstallmentCount,
            p.ReceivedAtUtc, p.ReceivedByUserId,
            u != null ? u.FirstName + " " + u.LastName : "",
            p.LedgerEntryId, p.Note);
}
