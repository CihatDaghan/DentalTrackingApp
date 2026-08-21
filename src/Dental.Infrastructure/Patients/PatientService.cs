using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Patients;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Patients;

public sealed class PatientService(
    AppDbContext db,
    ITenantContext tenant,
    ITcknProtector tcknProtector,
    IClock clock,
    IValidator<PatientUpsertRequest> validator) : IPatientService
{
    public async Task<PatientDto> CreateAsync(PatientUpsertRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var clinicId = await ResolveClinicIdAsync(request.ClinicId, ct);
        var explicitFileNo = !string.IsNullOrWhiteSpace(request.FileNo);

        // Otomatik dosya no max+1 ile üretilir; eşzamanlı kayıtta filtreli unique index
        // ihlali 500'e dönüşüyordu (inceleme bulgusu) — çakışmada yeniden numaralandır.
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var patient = new Patient
            {
                ClinicId = clinicId,
                FileNo = explicitFileNo ? request.FileNo!.Trim() : await NextFileNoAsync(ct),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
            };
            if (await db.Patients.AnyAsync(p => p.FileNo == patient.FileNo, ct))
            {
                if (explicitFileNo)
                    throw new InvalidOperationException($"'{patient.FileNo}' dosya numarası zaten kullanımda.");
                if (attempt >= maxAttempts) throw new InvalidOperationException("Dosya numarası üretilemedi; tekrar deneyin.");
                continue;
            }

            ApplyScalars(patient, request);
            await ApplyTcknAsync(patient, request, ct);
            UpsertConsents(patient, request.Consents);

            db.Patients.Add(patient);
            try
            {
                await db.SaveChangesAsync(ct);
                return await GetAsync(patient.Id, ct);
            }
            catch (DbUpdateException) when (!explicitFileNo && attempt < maxAttempts)
            {
                db.Entry(patient).State = EntityState.Detached;
                foreach (var consent in patient.Consents) db.Entry(consent).State = EntityState.Detached;
            }
        }
    }

    /// <summary>İstekten gelen klinik kimliğinin bu kiracıya ait olduğunu doğrular.</summary>
    private async Task<long> ResolveClinicIdAsync(long? requestedClinicId, CancellationToken ct)
    {
        if (requestedClinicId is not { } clinicId)
            return tenant.ClinicId ?? throw new InvalidOperationException("Klinik bağlamı yok; ClinicId gönderin.");

        if (!await db.Clinics.AnyAsync(c => c.Id == clinicId, ct)) // global filtre kiracıyı zorlar
            throw new KeyNotFoundException("Klinik bulunamadı.");
        return clinicId;
    }

    public async Task<PatientDto> UpdateAsync(long id, PatientUpsertRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var patient = await db.Patients.Include(p => p.Consents).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");

        if (!string.IsNullOrWhiteSpace(request.FileNo) && request.FileNo.Trim() != patient.FileNo)
        {
            var fileNo = request.FileNo.Trim();
            if (await db.Patients.AnyAsync(p => p.FileNo == fileNo && p.Id != id, ct))
                throw new InvalidOperationException($"'{fileNo}' dosya numarası zaten kullanımda.");
            patient.FileNo = fileNo;
        }
        if (request.ClinicId is not null) patient.ClinicId = await ResolveClinicIdAsync(request.ClinicId, ct);
        patient.FirstName = request.FirstName.Trim();
        patient.LastName = request.LastName.Trim();

        ApplyScalars(patient, request);
        await ApplyTcknAsync(patient, request, ct);
        UpsertConsents(patient, request.Consents);

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PatientDto> GetAsync(long id, CancellationToken ct = default)
    {
        var patient = await db.Patients.AsNoTracking()
                .Include(p => p.Consents)
                .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");

        return new PatientDto(
            patient.Id, patient.ClinicId, patient.FileNo, patient.FirstName, patient.LastName,
            patient.IdentityType,
            patient.TcknEncrypted is null ? null : tcknProtector.Unprotect(patient.TcknEncrypted),
            patient.PassportNo, patient.NationalityCode.Trim(), patient.LastEntryDate,
            patient.BirthDate, patient.Gender, patient.Phone, patient.Phone2, patient.Email,
            patient.Address, patient.City, patient.District, patient.ReferralSource,
            patient.Profession, patient.GeneralNote, patient.Balance, patient.CreatedAtUtc,
            patient.Consents.OrderBy(c => c.ConsentType)
                .Select(c => new ConsentDto(c.ConsentType, c.IsGranted, c.GrantedAtUtc, c.Source))
                .ToList());
    }

    public async Task<PatientSummaryDto> GetSummaryAsync(long id, CancellationToken ct = default)
    {
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        return new PatientSummaryDto(patient.Id, patient.FullName, CalculateAge(patient.BirthDate),
            patient.Balance, patient.Phone);
    }

    public async Task<PagedResult<PatientListItemDto>> ListAsync(PatientListQuery query, CancellationToken ct = default)
    {
        var q = db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            if (term.Length == 11 && term.All(char.IsAsciiDigit))
            {
                // TCKN tam eşleşme — düz metin saklanmaz, arama HMAC özeti üzerinden.
                var hash = tcknProtector.Hash(term);
                q = q.Where(p => p.TcknHash == hash);
            }
            else
            {
                // Ad araması veritabanının büyük/küçük harf duyarsız harmanlamasına bırakılır:
                // C# ToUpperInvariant ile SQL UPPER, Türkçe İ/ı için farklı sonuç üretiyor ve
                // arama kaçırıyordu (inceleme bulgusu). LIKE joker karakterleri kaçırılır.
                var pattern = $"%{term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")}%";
                q = q.Where(p => EF.Functions.Like(p.FirstName + " " + p.LastName, pattern)
                    || (p.Phone != null && p.Phone.Contains(term))
                    || p.FileNo == term);
            }
        }

        q = ApplySort(q, query.Sort);

        var page = new PageRequest(query.Page, query.PageSize);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(page.Skip).Take(page.EffectivePageSize)
            .Select(p => new PatientListItemDto(p.Id, p.FileNo, p.FirstName, p.LastName,
                p.Phone, p.City, p.BirthDate, p.Balance, p.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<PatientListItemDto>(items, Math.Max(query.Page, 1), page.EffectivePageSize, total);
    }

    public async Task SoftDeleteAsync(long id, CancellationToken ct = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        db.Patients.Remove(patient); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    private static void ApplyScalars(Patient patient, PatientUpsertRequest request)
    {
        patient.IdentityType = request.IdentityType;
        patient.PassportNo = request.IdentityType == IdentityType.Passport ? request.PassportNo?.Trim() : null;
        patient.NationalityCode = request.NationalityCode.Trim().ToUpperInvariant();
        patient.LastEntryDate = request.LastEntryDate;
        patient.BirthDate = request.BirthDate;
        patient.Gender = request.Gender;
        patient.Phone = request.Phone?.Trim();
        patient.Phone2 = request.Phone2?.Trim();
        patient.Email = request.Email?.Trim();
        patient.Address = request.Address;
        patient.City = request.City?.Trim();
        patient.District = request.District?.Trim();
        patient.ReferralSource = request.ReferralSource?.Trim();
        patient.Profession = request.Profession?.Trim();
        patient.GeneralNote = request.GeneralNote;
    }

    /// <summary>TCKN kuralı: null = değiştirme, "" = temizle, dolu = şifrele + özet (duplikasyon kontrolüyle).</summary>
    private async Task ApplyTcknAsync(Patient patient, PatientUpsertRequest request, CancellationToken ct)
    {
        if (request.IdentityType != IdentityType.Tckn || request.Tckn == string.Empty)
        {
            patient.TcknEncrypted = null;
            patient.TcknHash = null;
            return;
        }
        if (request.Tckn is null) return;

        var hash = tcknProtector.Hash(request.Tckn);
        if (hash == patient.TcknHash) return;

        if (await db.Patients.AnyAsync(p => p.TcknHash == hash && p.Id != patient.Id, ct))
            throw new InvalidOperationException("Bu TCKN ile kayıtlı başka bir hasta zaten var.");

        (patient.TcknEncrypted, patient.TcknHash) = tcknProtector.Protect(request.Tckn);
    }

    private void UpsertConsents(Patient patient, IReadOnlyList<ConsentUpsertDto>? consents)
    {
        if (consents is null) return;
        foreach (var dto in consents)
        {
            var existing = patient.Consents.FirstOrDefault(c => c.ConsentType == dto.ConsentType);
            if (existing is null)
            {
                patient.Consents.Add(new CommunicationConsent
                {
                    ConsentType = dto.ConsentType,
                    IsGranted = dto.IsGranted,
                    GrantedAtUtc = dto.IsGranted ? clock.UtcNow : null,
                    Source = dto.Source,
                });
            }
            else
            {
                if (dto.IsGranted && !existing.IsGranted) existing.GrantedAtUtc = clock.UtcNow;
                existing.IsGranted = dto.IsGranted;
                existing.Source = dto.Source;
            }
        }
    }

    /// <summary>Tenant içi max+1 (soft-delete dahil, numara geri kullanılmasın). NumberSequence sonraki aşamada.</summary>
    private async Task<string> NextFileNoAsync(CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan hasta eklenemez.");
        var fileNos = await db.Patients.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.FileNo)
            .ToListAsync(ct);
        var max = fileNos.Select(f => long.TryParse(f, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return (max + 1).ToString();
    }

    private static IQueryable<Patient> ApplySort(IQueryable<Patient> q, string? sort)
    {
        var desc = sort?.StartsWith('-') == true;
        return (sort?.TrimStart('-').ToLowerInvariant()) switch
        {
            "name" or "lastname" => desc ? q.OrderByDescending(p => p.SearchName) : q.OrderBy(p => p.SearchName),
            "fileno" => desc ? q.OrderByDescending(p => p.FileNo) : q.OrderBy(p => p.FileNo),
            "createdat" => desc ? q.OrderByDescending(p => p.CreatedAtUtc) : q.OrderBy(p => p.CreatedAtUtc),
            _ => q.OrderByDescending(p => p.Id),
        };
    }

    private int? CalculateAge(DateOnly? birthDate)
    {
        if (birthDate is not { } birth) return null;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        return age;
    }
}
