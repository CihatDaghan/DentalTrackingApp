using Dental.Application.Abstractions;
using Dental.Application.Finance;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Finance;

/// <summary>
/// Tek cari defter çekirdeği. Denormalize bakiye (Patient.Balance / Company.Balance)
/// kayıt ekleme ile AYNI transaction'da <c>UPDATE ... SET Balance = Balance + @delta</c>
/// (satır kilidi) ile güncellenir — denetim (c)-10 yarış koşulu düzeltmesi.
/// </summary>
public sealed class LedgerService(AppDbContext db, ITenantContext tenant, IClock clock) : ILedgerService
{
    public async Task<long> AddEntryAsync(LedgerEntryCreateRequest request, CancellationToken ct = default)
    {
        ValidateAccountRef(request);
        if (request.Debit < 0 || request.Credit < 0)
            throw new InvalidOperationException("Debit/Credit negatif olamaz.");
        if (request.ExchangeRate <= 0)
            throw new InvalidOperationException("Kur sıfır veya negatif olamaz.");

        // Defter tek para biriminde (TRY) tutulur: dövizli kayıtlar giriş anındaki kurla
        // çevrilir; orijinal tutar açıklamada saklanır (inceleme bulgusu: karışık para
        // birimlerinin kur çevrimsiz toplanması bakiyeyi bozuyordu).
        var debit = request.Debit;
        var credit = request.Credit;
        var description = request.Description;
        if (!string.Equals(request.CurrencyCode, "TRY", StringComparison.OrdinalIgnoreCase))
        {
            debit = Math.Round(request.Debit * request.ExchangeRate, 2, MidpointRounding.AwayFromZero);
            credit = Math.Round(request.Credit * request.ExchangeRate, 2, MidpointRounding.AwayFromZero);
            var original = request.Debit > 0 ? request.Debit : request.Credit;
            description = $"{description} [{original:F2} {request.CurrencyCode.ToUpperInvariant()} @ {request.ExchangeRate:F4}]";
        }

        var clinicId = await ResolveClinicIdAsync(request, ct);

        // Çağıran transaction başlatmışsa ona katıl; yoksa kendi transaction'ımızı aç.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var entry = new LedgerEntry
            {
                ClinicId = clinicId,
                AccountType = request.AccountType,
                PatientId = request.PatientId,
                CompanyId = request.CompanyId,
                EntryType = request.EntryType,
                Debit = debit,
                Credit = credit,
                CurrencyCode = request.CurrencyCode,
                ExchangeRate = request.ExchangeRate,
                EntryDate = request.EntryDate ?? DateOnly.FromDateTime(clock.UtcNow + TrTime.Offset),
                Description = description,
                RefType = request.RefType,
                RefId = request.RefId,
            };
            db.LedgerEntries.Add(entry);
            await db.SaveChangesAsync(ct);

            // Bakiye = borç(+) / alacak(-) toplamı; atomik artırım satır kilidiyle.
            var delta = debit - credit;
            if (delta != 0m)
            {
                var affected = request.AccountType == LedgerAccountType.Patient
                    ? await db.Patients.Where(p => p.Id == request.PatientId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Balance, p => p.Balance + delta), ct)
                    : await db.Companies.Where(c => c.Id == request.CompanyId)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance + delta), ct);
                if (affected == 0)
                    throw new KeyNotFoundException(request.AccountType == LedgerAccountType.Patient
                        ? "Hasta bulunamadı." : "Kurum bulunamadı.");
            }

            if (tx is not null) await tx.CommitAsync(ct);
            return entry.Id;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    public async Task<LedgerStatementDto> GetStatementAsync(LedgerStatementQuery query, CancellationToken ct = default)
    {
        var (accountName, currentBalance) = await ResolveAccountAsync(query.AccountType, query.AccountId, ct);

        var scoped = db.LedgerEntries.AsNoTracking().Where(e => query.AccountType == LedgerAccountType.Patient
            ? e.PatientId == query.AccountId
            : e.CompanyId == query.AccountId);

        // Açılış bakiyesi: from öncesi tüm hareketlerin net toplamı.
        var openingBalance = 0m;
        if (query.From is { } from)
        {
            openingBalance = await scoped.Where(e => e.EntryDate < from)
                .SumAsync(e => (decimal?)(e.Debit - e.Credit), ct) ?? 0m;
        }

        var ranged = scoped;
        if (query.From is { } f) ranged = ranged.Where(e => e.EntryDate >= f);
        if (query.To is { } to) ranged = ranged.Where(e => e.EntryDate <= to);

        var entries = await ranged.OrderBy(e => e.EntryDate).ThenBy(e => e.Id).ToListAsync(ct);

        var running = openingBalance;
        var lines = new List<LedgerLineDto>(entries.Count);
        foreach (var e in entries)
        {
            running += e.Debit - e.Credit;
            lines.Add(new LedgerLineDto(e.Id, e.EntryDate, e.EntryType, e.Description,
                e.Debit, e.Credit, running, e.RefType, e.RefId, e.CreatedAtUtc));
        }

        // Silinen tahsilatların ters (Correction/Payment) kayıtları toplamdan düşülür;
        // aksi halde özet, iptal edilmiş tahsilatları da sayıyordu (inceleme bulgusu).
        var reversedPayments = entries
            .Where(e => e.EntryType == LedgerEntryType.Correction && e.RefType == nameof(Payment))
            .Sum(e => e.Debit);

        var summary = new LedgerSummaryDto(
            TotalTreatment: entries.Where(e => e.EntryType == LedgerEntryType.TreatmentCharge).Sum(e => e.Debit),
            TotalPayment: entries.Where(e => e.EntryType == LedgerEntryType.PaymentIn).Sum(e => e.Credit) - reversedPayments,
            TotalDiscount: entries.Where(e => e.EntryType == LedgerEntryType.Discount).Sum(e => e.Credit),
            TotalDebit: entries.Sum(e => e.Debit),
            TotalCredit: entries.Sum(e => e.Credit),
            Balance: currentBalance);

        return new LedgerStatementDto(query.AccountType, query.AccountId, accountName,
            query.From, query.To, openingBalance, lines, summary);
    }

    // ---- Yardımcılar ----

    private static void ValidateAccountRef(LedgerEntryCreateRequest request)
    {
        var valid = request.AccountType switch
        {
            LedgerAccountType.Patient => request.PatientId is not null && request.CompanyId is null,
            LedgerAccountType.Company => request.CompanyId is not null && request.PatientId is null,
            _ => false,
        };
        if (!valid)
            throw new InvalidOperationException("Hesap türüne göre PatientId veya CompanyId'den tam biri dolu olmalıdır.");
    }

    private async Task<(string Name, decimal Balance)> ResolveAccountAsync(
        LedgerAccountType accountType, long accountId, CancellationToken ct)
    {
        if (accountType == LedgerAccountType.Patient)
        {
            var patient = await db.Patients.AsNoTracking()
                .Where(p => p.Id == accountId)
                .Select(p => new { Name = p.FirstName + " " + p.LastName, p.Balance })
                .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Hasta bulunamadı.");
            return (patient.Name, patient.Balance);
        }

        var company = await db.Companies.AsNoTracking()
            .Where(c => c.Id == accountId)
            .Select(c => new { c.Name, c.Balance })
            .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Kurum bulunamadı.");
        return (company.Name, company.Balance);
    }

    private async Task<long> ResolveClinicIdAsync(LedgerEntryCreateRequest request, CancellationToken ct)
    {
        if (request.ClinicId is { } explicitClinic) return explicitClinic;
        if (tenant.ClinicId is { } contextClinic) return contextClinic;

        if (request.AccountType == LedgerAccountType.Patient)
        {
            return await db.Patients.Where(p => p.Id == request.PatientId)
                .Select(p => (long?)p.ClinicId).FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        }

        // Kurum kaydında klinik bağlamı yoksa kiracının ilk kliniği kullanılır.
        return await db.Clinics.OrderBy(c => c.Id).Select(c => (long?)c.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Kiracının kliniği yok; cari kaydı için klinik gereklidir.");
    }
}
