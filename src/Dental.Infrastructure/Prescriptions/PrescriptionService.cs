using Dental.Application.Abstractions;
using Dental.Application.Media;
using Dental.Application.Prescriptions;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Prescriptions;

/// <summary>
/// Reçete modülü: ilaç arama (merkezi + kiracı özel), şablon CRUD, reçete oluşturma
/// (şablondan doldurma, hekim doğrulaması, tenant içi RX numarası) ve A5 PDF üretimi.
/// Drug ITenantOwned olmadığı için görünürlük her sorguda elle filtrelenir
/// (TenantId == null || TenantId == current).
/// </summary>
public sealed class PrescriptionService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IMediaService media,
    IValidator<DrugCreateRequest> drugValidator,
    IValidator<PrescriptionTemplateUpsertRequest> templateValidator,
    IValidator<PrescriptionCreateRequest> createValidator,
    IValidator<PrescriptionSaveAsTemplateRequest> saveAsTemplateValidator) : IPrescriptionService
{
    private const int DrugSearchLimit = 50;

    public const string ControlledWarningText =
        "Kontrole tabi ilaç içerir — Renkli Reçete sistemi üzerinden yazılması gerekir.";

    // ---- İlaçlar ----

    public async Task<IReadOnlyList<DrugDto>> SearchDrugsAsync(string? search, CancellationToken ct = default)
    {
        var query = VisibleDrugs();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => d.Name.Contains(term) || (d.Barcode != null && d.Barcode.StartsWith(term)));
        }
        return await query.OrderBy(d => d.Name).Take(DrugSearchLimit)
            .Select(d => ToDto(d)).ToListAsync(ct);
    }

    public async Task<DrugDto> CreateDrugAsync(DrugCreateRequest request, CancellationToken ct = default)
    {
        await drugValidator.ValidateAndThrowAsync(request, ct);
        var tenantId = RequireTenantId();

        var name = request.Name.Trim();
        if (await VisibleDrugs().AnyAsync(d => d.Name == name, ct))
            throw new InvalidOperationException($"'{name}' adında bir ilaç zaten listede var.");

        var drug = new Drug
        {
            TenantId = tenantId,
            Barcode = request.Barcode?.Trim(),
            Name = name,
            AtcCode = request.AtcCode?.Trim(),
            Form = request.Form?.Trim(),
            DefaultDose = request.DefaultDose?.Trim(),
            DefaultUsage = request.DefaultUsage?.Trim(),
            IsControlled = request.IsControlled,
        };
        db.Drugs.Add(drug);
        await db.SaveChangesAsync(ct);
        return ToDto(drug);
    }

    // ---- Şablonlar ----

    public async Task<IReadOnlyList<PrescriptionTemplateDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await db.PrescriptionTemplates.AsNoTracking()
            .Include(t => t.Items.OrderBy(i => i.Id)).ThenInclude(i => i.Drug)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return [.. templates.Select(ToTemplateDto)];
    }

    public async Task<PrescriptionTemplateDto> GetTemplateAsync(long id, CancellationToken ct = default) =>
        ToTemplateDto(await FindTemplateAsync(id, tracking: false, ct));

    public async Task<PrescriptionTemplateDto> CreateTemplateAsync(
        PrescriptionTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        var name = request.Name.Trim();
        if (await db.PrescriptionTemplates.AnyAsync(t => t.Name == name, ct))
            throw new InvalidOperationException($"'{name}' adında bir reçete şablonu zaten var.");
        await EnsureDrugsVisibleAsync(request.Items.Select(i => i.DrugId), ct);

        var template = new PrescriptionTemplate { Name = name };
        foreach (var item in request.Items)
            template.Items.Add(ToTemplateItem(item));
        db.PrescriptionTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return await GetTemplateAsync(template.Id, ct);
    }

    public async Task<PrescriptionTemplateDto> UpdateTemplateAsync(
        long id, PrescriptionTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        var template = await FindTemplateAsync(id, tracking: true, ct);
        var name = request.Name.Trim();
        if (await db.PrescriptionTemplates.AnyAsync(t => t.Name == name && t.Id != id, ct))
            throw new InvalidOperationException($"'{name}' adında bir reçete şablonu zaten var.");
        await EnsureDrugsVisibleAsync(request.Items.Select(i => i.DrugId), ct);

        template.Name = name;
        db.PrescriptionTemplateItems.RemoveRange(template.Items); // interceptor soft delete'e çevirir
        template.Items.Clear();
        foreach (var item in request.Items)
            template.Items.Add(ToTemplateItem(item));
        await db.SaveChangesAsync(ct);
        return await GetTemplateAsync(id, ct);
    }

    public async Task DeleteTemplateAsync(long id, CancellationToken ct = default)
    {
        var template = await FindTemplateAsync(id, tracking: true, ct);
        db.PrescriptionTemplateItems.RemoveRange(template.Items);
        db.PrescriptionTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    // ---- Reçeteler ----

    public async Task<PrescriptionDto> CreateAsync(
        long patientId, PrescriptionCreateRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        await EnsureDoctorIsDentistAsync(request.DoctorUserId, ct);
        if (request.VisitId is { } visitId &&
            !await db.Visits.AnyAsync(v => v.Id == visitId && v.PatientId == patientId, ct))
            throw new KeyNotFoundException("Ziyaret (visit) bulunamadı.");

        // Kalemler: istekte doluysa istekten, boşsa şablondan.
        IReadOnlyList<PrescriptionItemRequest> items;
        if (request.Items is { Count: > 0 })
        {
            items = request.Items;
        }
        else
        {
            var template = await FindTemplateAsync(request.TemplateId!.Value, tracking: false, ct);
            items = [.. template.Items.Select(i => new PrescriptionItemRequest(
                i.DrugId, i.BoxCount, i.Dose, i.Frequency, i.Duration, i.UsageNote))];
            if (items.Count == 0)
                throw new InvalidOperationException("Şablonda ilaç kalemi yok.");
        }
        await EnsureDrugsVisibleAsync(items.Select(i => i.DrugId), ct);

        var prescription = new Prescription
        {
            ClinicId = patient.ClinicId,
            PatientId = patientId,
            DoctorUserId = request.DoctorUserId,
            VisitId = request.VisitId,
            PrescriptionNo = await NextPrescriptionNoAsync(ct),
            Status = PrescriptionStatus.Draft,
        };
        foreach (var item in items)
        {
            prescription.Items.Add(new PrescriptionItem
            {
                DrugId = item.DrugId,
                BoxCount = item.BoxCount,
                Dose = item.Dose?.Trim(),
                Frequency = item.Frequency?.Trim(),
                Duration = item.Duration?.Trim(),
                UsageNote = item.UsageNote?.Trim(),
            });
        }
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(ct);
        return await GetAsync(prescription.Id, ct);
    }

    public async Task<IReadOnlyList<PrescriptionDto>> ListForPatientAsync(
        long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        var prescriptions = await db.Prescriptions.AsNoTracking()
            .Include(p => p.Items.OrderBy(i => i.Id)).ThenInclude(i => i.Drug)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .ToListAsync(ct);
        return await ToDtosAsync(prescriptions, ct);
    }

    public async Task<PrescriptionDto> GetAsync(long id, CancellationToken ct = default)
    {
        var prescription = await db.Prescriptions.AsNoTracking()
                .Include(p => p.Items.OrderBy(i => i.Id)).ThenInclude(i => i.Drug)
                .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Reçete bulunamadı.");
        return (await ToDtosAsync([prescription], ct))[0];
    }

    public async Task<PrescriptionTemplateDto> SaveAsTemplateAsync(
        long id, PrescriptionSaveAsTemplateRequest request, CancellationToken ct = default)
    {
        await saveAsTemplateValidator.ValidateAndThrowAsync(request, ct);
        var prescription = await db.Prescriptions.AsNoTracking()
                .Include(p => p.Items.OrderBy(i => i.Id))
                .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Reçete bulunamadı.");

        return await CreateTemplateAsync(new PrescriptionTemplateUpsertRequest(
            request.Name,
            [.. prescription.Items.Select(i => new PrescriptionTemplateItemRequest(
                i.DrugId, i.BoxCount, i.Dose, i.Frequency, i.Duration, i.UsageNote))]), ct);
    }

    public async Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default)
    {
        var prescription = await db.Prescriptions
                .Include(p => p.Items.OrderBy(i => i.Id)).ThenInclude(i => i.Drug)
                .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Reçete bulunamadı.");

        if (prescription.PdfFileId is null)
        {
            var patient = await db.Patients.AsNoTracking().FirstAsync(p => p.Id == prescription.PatientId, ct);
            var doctor = await db.Users.AsNoTracking().FirstAsync(u => u.Id == prescription.DoctorUserId, ct);
            var clinicName = await db.Clinics.AsNoTracking()
                .Where(c => c.Id == prescription.ClinicId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Klinik";

            var pdfBytes = PrescriptionPdfGenerator.Generate(new PrescriptionPdfModel(
                clinicName,
                doctor.FullName,
                doctor.DiplomaNo,
                patient.FullName,
                CalculateAge(patient.BirthDate),
                prescription.PrescriptionNo,
                ToLocal(clock.UtcNow),
                [.. prescription.Items.Select(i => new PrescriptionPdfItem(
                    i.Drug!.Name,
                    i.Drug.Form,
                    i.BoxCount,
                    i.Dose ?? i.Drug.DefaultDose,
                    BuildUsageText(i),
                    i.Drug.IsControlled))]));

            var pdfFile = await media.SaveGeneratedAsync(new GeneratedFileRequest(
                prescription.ClinicId, prescription.PatientId, MediaCategory.PrescriptionPdf,
                $"recete-{prescription.PrescriptionNo}.pdf", "application/pdf", pdfBytes,
                Description: $"Reçete {prescription.PrescriptionNo} PDF"), ct);

            prescription.PdfFileId = pdfFile.Id;
            if (prescription.Status == PrescriptionStatus.Draft)
                prescription.Status = PrescriptionStatus.Printed;
            await db.SaveChangesAsync(ct);
        }

        return await media.OpenDownloadAsync(prescription.PdfFileId.Value, ct);
    }

    // ---- Yardımcılar ----

    private IQueryable<Drug> VisibleDrugs()
    {
        var tenantId = RequireTenantId();
        return db.Drugs.AsNoTracking().Where(d => d.TenantId == null || d.TenantId == tenantId);
    }

    private long RequireTenantId() => tenant.TenantId
        ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan reçete işlemi yapılamaz.");

    private async Task EnsureDrugsVisibleAsync(IEnumerable<long> drugIds, CancellationToken ct)
    {
        var ids = drugIds.Distinct().ToList();
        var visibleCount = await VisibleDrugs().CountAsync(d => ids.Contains(d.Id), ct);
        if (visibleCount != ids.Count)
            throw new KeyNotFoundException("İlaç bulunamadı (listede olmayan veya başka kiracıya ait ilaç).");
    }

    /// <summary>Hekim doğrulaması: aynı kiracıda aktif UserType.Dentist olmalı — sekreter/asistan hekim olarak reçete yazamaz.</summary>
    private async Task EnsureDoctorIsDentistAsync(long doctorUserId, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var isDentist = await db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == doctorUserId && u.TenantId == tenantId && u.UserType == UserType.Dentist && u.IsActive, ct);
        if (!isDentist)
            throw new InvalidOperationException("Reçete yalnız hekim (diş hekimi) adına yazılabilir.");
    }

    /// <summary>Tenant içi yıl bazlı sıra: 'RX-2026-000001' (soft-delete dahil max+1 — numara geri kullanılmaz).</summary>
    private async Task<string> NextPrescriptionNoAsync(CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var prefix = $"RX-{clock.UtcNow.Year}-";
        var numbers = await db.Prescriptions.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.PrescriptionNo.StartsWith(prefix))
            .Select(p => p.PrescriptionNo)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D6}";
    }

    private async Task<PrescriptionTemplate> FindTemplateAsync(long id, bool tracking, CancellationToken ct)
    {
        var query = db.PrescriptionTemplates
            .Include(t => t.Items.OrderBy(i => i.Id)).ThenInclude(i => i.Drug)
            .AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Reçete şablonu bulunamadı.");
    }

    private async Task<IReadOnlyList<PrescriptionDto>> ToDtosAsync(
        IReadOnlyList<Prescription> prescriptions, CancellationToken ct)
    {
        if (prescriptions.Count == 0) return [];

        var patientIds = prescriptions.Select(p => p.PatientId).Distinct().ToList();
        var doctorIds = prescriptions.Select(p => p.DoctorUserId).Distinct().ToList();
        var patientNames = await db.Patients.AsNoTracking()
            .Where(p => patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        var doctorNames = await db.Users.AsNoTracking()
            .Where(u => doctorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);

        return [.. prescriptions.Select(p =>
        {
            var hasControlled = p.Items.Any(i => i.Drug?.IsControlled == true);
            return new PrescriptionDto(
                p.Id, p.PatientId, patientNames.GetValueOrDefault(p.PatientId, "-"),
                p.DoctorUserId, doctorNames.GetValueOrDefault(p.DoctorUserId, "-"),
                p.VisitId, p.PrescriptionNo, p.Status, p.RecetemCode, p.PdfFileId,
                hasControlled, hasControlled ? ControlledWarningText : null,
                [.. p.Items.Select(ToItemDto)], p.CreatedAtUtc);
        })];
    }

    private static PrescriptionItemDto ToItemDto(PrescriptionItem i) => new(
        i.Id, i.DrugId, i.Drug?.Name ?? "-", i.Drug?.Form, i.Drug?.IsControlled ?? false,
        i.BoxCount, i.Dose, i.Frequency, i.Duration, i.UsageNote);

    private static PrescriptionTemplateDto ToTemplateDto(PrescriptionTemplate t) => new(
        t.Id, t.Name,
        [.. t.Items.Select(i => new PrescriptionTemplateItemDto(
            i.Id, i.DrugId, i.Drug?.Name ?? "-", i.Drug?.Form, i.Drug?.IsControlled ?? false,
            i.BoxCount, i.Dose, i.Frequency, i.Duration, i.UsageNote))]);

    private static PrescriptionTemplateItem ToTemplateItem(PrescriptionTemplateItemRequest item) => new()
    {
        DrugId = item.DrugId,
        BoxCount = item.BoxCount,
        Dose = item.Dose?.Trim(),
        Frequency = item.Frequency?.Trim(),
        Duration = item.Duration?.Trim(),
        UsageNote = item.UsageNote?.Trim(),
    };

    private static DrugDto ToDto(Drug d) => new(
        d.Id, d.TenantId, d.Barcode, d.Name, d.AtcCode, d.Form, d.DefaultDose, d.DefaultUsage, d.IsControlled);

    private static string? BuildUsageText(PrescriptionItem item)
    {
        var parts = new[] { item.Frequency ?? item.Drug?.DefaultUsage, item.Duration, item.UsageNote }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var text = string.Join(", ", parts);
        return text.Length == 0 ? null : text;
    }

    private int? CalculateAge(DateOnly? birthDate)
    {
        if (birthDate is not { } birth) return null;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var age = today.Year - birth.Year;
        if (today < birth.AddYears(age)) age--;
        return age;
    }

    private static DateTime ToLocal(DateTime utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(3); // TR sabit UTC+3
        }
    }
}
