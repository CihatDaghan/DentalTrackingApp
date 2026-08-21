using Dental.Application.Abstractions;
using Dental.Application.Media;
using Dental.Application.Reports;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Reports;

/// <summary>
/// Hasta kartı "Rapor" sekmesi: tedavi dökümü, durum bildirir rapor ve proforma (fiyat teklifi).
///
/// <para>PDF üretildiğinde belge MediaFile arşivine <see cref="MediaCategory.PatientReportPdf"/>
/// kategorisiyle yazılır — hasta dosyasında "Görüntü/Belge" sekmesinden de erişilebilir.</para>
///
/// <para><b>Proforma fatura DEĞİLDİR:</b> yalnız teklif belgesidir, numara/ETTN almaz, cari
/// hareket yaratmaz. Çıktıda "Bu belge fatura yerine geçmez." ibaresi zorunludur.</para>
/// </summary>
public sealed class PatientReportService(
    AppDbContext db,
    ITenantContext tenant,
    ITcknProtector tcknProtector,
    IMediaService media) : IPatientReportService
{
    private const int ProformaValidityDays = 30;
    private const string ProformaDisclaimer =
        "Bu belge bir FİYAT TEKLİFİDİR; fatura yerine geçmez ve mali belge niteliği taşımaz. " +
        "Teklif, geçerlilik tarihine kadar bağlayıcıdır; tedavi planındaki değişiklikler tutarı etkileyebilir.";

    // ---- Tedavi dökümü ----

    public async Task<PatientTreatmentReportDto> GetTreatmentReportAsync(
        long patientId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var patient = await LoadPatientAsync(patientId, ct);
        var rows = await LoadTreatmentRowsAsync(patientId, from, to, ct);

        return new PatientTreatmentReportDto(
            patient.Id, $"{patient.FirstName} {patient.LastName}", patient.FileNo, patient.ClinicName,
            from, to, TrTime.ToLocalDate(DateTime.UtcNow),
            rows,
            rows.Sum(r => r.Price),
            rows.Sum(r => r.DiscountAmount),
            rows.Sum(r => r.NetAmount));
    }

    public async Task<(PatientTreatmentReportDto Report, ReportFileDto File)> GetTreatmentReportPdfAsync(
        long patientId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var report = await GetTreatmentReportAsync(patientId, from, to, ct);
        var bytes = PatientReportPdfGenerator.TreatmentReport(report);
        var fileName = $"tedavi-dokumu-{report.FileNo}-{report.IssuedOn:yyyyMMdd}.pdf";
        var fileId = await ArchiveAsync(patientId, fileName, bytes, "Tedavi dökümü", ct);
        return (report with { PdfFileId = fileId }, new ReportFileDto(bytes, fileName, "application/pdf"));
    }

    // ---- Durum bildirir rapor ----

    public async Task<PatientStatusReportDto> GetStatusReportAsync(long patientId, CancellationToken ct = default)
    {
        var patient = await LoadPatientAsync(patientId, ct);
        var today = TrTime.ToLocalDate(DateTime.UtcNow);

        var teeth = await db.ToothStatuses.AsNoTracking()
            .Where(t => t.PatientId == patientId && t.Condition != ToothCondition.Present)
            .OrderBy(t => t.ToothNumber)
            .Select(t => new { t.ToothNumber, t.Condition })
            .ToListAsync(ct);

        var treatments = await LoadTreatmentRowsAsync(patientId, null, null, ct);
        var (doctorName, diplomaNo) = await ResolveSigningDoctorAsync(patientId, ct);

        var tckn = patient.TcknEncrypted is { } encrypted ? tcknProtector.Unprotect(encrypted) : null;

        return new PatientStatusReportDto(
            patient.Id, $"{patient.FirstName} {patient.LastName}", patient.FileNo, patient.ClinicName,
            patient.BirthDate, Age(patient.BirthDate, today), ReportLabels.Gender(patient.Gender),
            MaskIdentity(patient.IdentityType, tckn, patient.PassportNo),
            patient.Phone, today, doctorName, diplomaNo,
            [.. teeth.Select(t => new PatientToothStatusRowDto(
                t.ToothNumber, t.Condition, ReportLabels.ToothCondition(t.Condition)))],
            treatments);
    }

    public async Task<(PatientStatusReportDto Report, ReportFileDto File)> GetStatusReportPdfAsync(
        long patientId, CancellationToken ct = default)
    {
        var report = await GetStatusReportAsync(patientId, ct);
        var bytes = PatientReportPdfGenerator.StatusReport(report);
        var fileName = $"durum-raporu-{report.FileNo}-{report.IssuedOn:yyyyMMdd}.pdf";
        var fileId = await ArchiveAsync(patientId, fileName, bytes, "Durum bildirir rapor", ct);
        return (report with { PdfFileId = fileId }, new ReportFileDto(bytes, fileName, "application/pdf"));
    }

    // ---- Proforma ----

    public async Task<ProformaDto> CreateProformaAsync(
        long patientId, ProformaRequest request, CancellationToken ct = default)
    {
        var patient = await LoadPatientAsync(patientId, ct);
        if (request.TreatmentRecordIds is not { Count: > 0 })
            throw new InvalidOperationException("Teklife en az bir tedavi kalemi seçilmelidir.");

        var ids = request.TreatmentRecordIds.Distinct().ToList();
        // Global filtre kiracıyı, PatientId koşulu ise BAŞKA HASTANIN kaydının teklife
        // girmesini engeller (IDOR koruması).
        var records = await db.TreatmentRecords.AsNoTracking()
            .Where(t => ids.Contains(t.Id) && t.PatientId == patientId)
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.ToothNumber,
                t.Price,
                t.DiscountAmount,
                t.VatRate,
                t.Status,
                Name = t.TreatmentDefinition!.Name,
            })
            .ToListAsync(ct);

        if (records.Count != ids.Count)
            throw new KeyNotFoundException("Seçilen tedavi kayıtlarından bazıları bu hastaya ait değil.");

        var notPlanned = records.Where(r => r.Status != TreatmentRecordStatus.Planned).ToList();
        if (notPlanned.Count > 0)
            throw new InvalidOperationException("Teklife yalnız PLANLANMIŞ tedaviler eklenebilir.");

        var lines = records
            .Select((r, i) =>
            {
                var lineTotal = decimal.Round(r.Price - r.DiscountAmount, 2);
                var vat = decimal.Round(lineTotal * r.VatRate / 100m, 2);
                return new ProformaLineDto(i + 1, r.Name, r.ToothNumber, r.Price, r.DiscountAmount, r.VatRate, vat, lineTotal);
            })
            .ToList();

        var today = TrTime.ToLocalDate(DateTime.UtcNow);
        var subTotal = lines.Sum(l => l.UnitPrice);
        var discountTotal = lines.Sum(l => l.DiscountAmount);
        var vatTotal = lines.Sum(l => l.VatAmount);
        var net = lines.Sum(l => l.LineTotal);

        return new ProformaDto(
            patient.Id, $"{patient.FirstName} {patient.LastName}", patient.FileNo, patient.ClinicName,
            today, request.ValidUntil ?? today.AddDays(ProformaValidityDays),
            lines, subTotal, discountTotal, vatTotal, net + vatTotal, request.Note, ProformaDisclaimer);
    }

    public async Task<(ProformaDto Report, ReportFileDto File)> CreateProformaPdfAsync(
        long patientId, ProformaRequest request, CancellationToken ct = default)
    {
        var report = await CreateProformaAsync(patientId, request, ct);
        var bytes = PatientReportPdfGenerator.Proforma(report);
        var fileName = $"proforma-{report.FileNo}-{report.IssuedOn:yyyyMMdd}.pdf";
        var fileId = await ArchiveAsync(patientId, fileName, bytes, "Fiyat teklifi (proforma)", ct);
        return (report with { PdfFileId = fileId }, new ReportFileDto(bytes, fileName, "application/pdf"));
    }

    // ---- Yardımcılar ----

    private sealed record PatientHeader(
        long Id, long ClinicId, string ClinicName, string FileNo, string FirstName, string LastName,
        DateOnly? BirthDate, Gender Gender, string? Phone, IdentityType IdentityType,
        string? TcknEncrypted, string? PassportNo);

    private async Task<PatientHeader> LoadPatientAsync(long patientId, CancellationToken ct) =>
        await db.Patients.AsNoTracking()
            .Where(p => p.Id == patientId)
            .Select(p => new PatientHeader(
                p.Id, p.ClinicId,
                db.Clinics.Where(c => c.Id == p.ClinicId).Select(c => c.Name).FirstOrDefault() ?? "",
                p.FileNo, p.FirstName, p.LastName, p.BirthDate, p.Gender, p.Phone,
                p.IdentityType, p.TcknEncrypted, p.PassportNo))
            .FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Hasta bulunamadı.");

    private async Task<IReadOnlyList<PatientTreatmentRowDto>> LoadTreatmentRowsAsync(
        long patientId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        var source = db.TreatmentRecords.AsNoTracking()
            .Where(t => t.PatientId == patientId && t.Status == TreatmentRecordStatus.Done);
        if (from is { } f) source = source.Where(t => t.PerformedAtUtc >= TrTime.DayRangeUtc(f).StartUtc);
        if (to is { } t2) source = source.Where(t => t.PerformedAtUtc < TrTime.DayRangeUtc(t2).EndUtc);

        // Hekim adı yalnız kendi kiracımızdan gelsin (AppUser global filtreye tabi değil).
        return await (
            from t in source
            join u in db.Users.Where(x => x.TenantId == tenantId) on t.DoctorUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby t.PerformedAtUtc, t.Id
            select new PatientTreatmentRowDto(
                t.PerformedAtUtc == null ? null : DateOnly.FromDateTime(t.PerformedAtUtc.Value.AddHours(3)),
                u != null ? u.FirstName + " " + u.LastName : "",
                t.ToothNumber,
                t.TreatmentDefinition!.Name,
                t.Price,
                t.DiscountAmount,
                t.Price - t.DiscountAmount,
                t.Status)).ToListAsync(ct);
    }

    /// <summary>
    /// Raporu imzalayacak hekim: oturumdaki kullanıcı hekimse o, değilse hastanın en son
    /// tedavisini yapan hekim.
    /// </summary>
    private async Task<(string Name, string? DiplomaNo)> ResolveSigningDoctorAsync(long patientId, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenant.UserId is { } userId)
        {
            var current = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId && u.TenantId == tenantId && u.UserType == UserType.Dentist)
                .Select(u => new { u.FirstName, u.LastName, u.DiplomaNo })
                .FirstOrDefaultAsync(ct);
            if (current is not null) return ($"{current.FirstName} {current.LastName}", current.DiplomaNo);
        }

        var lastDoctorId = await db.TreatmentRecords.AsNoTracking()
            .Where(t => t.PatientId == patientId && t.Status == TreatmentRecordStatus.Done)
            .OrderByDescending(t => t.PerformedAtUtc)
            .Select(t => (long?)t.DoctorUserId)
            .FirstOrDefaultAsync(ct);
        if (lastDoctorId is not { } doctorId) return ("", null);

        var doctor = await db.Users.AsNoTracking()
            .Where(u => u.Id == doctorId && u.TenantId == tenantId)
            .Select(u => new { u.FirstName, u.LastName, u.DiplomaNo })
            .FirstOrDefaultAsync(ct);
        return doctor is null ? ("", null) : ($"{doctor.FirstName} {doctor.LastName}", doctor.DiplomaNo);
    }

    private async Task<long?> ArchiveAsync(
        long patientId, string fileName, byte[] content, string description, CancellationToken ct)
    {
        var clinicId = await db.Patients.AsNoTracking()
            .Where(p => p.Id == patientId).Select(p => p.ClinicId).FirstAsync(ct);
        var dto = await media.SaveGeneratedAsync(new GeneratedFileRequest(
            clinicId, patientId, MediaCategory.PatientReportPdf, fileName, "application/pdf", content, description), ct);
        return dto.Id;
    }

    /// <summary>Kimlik numarası belgede maskeli gösterilir (KVKK): 123*****45.</summary>
    private static string? MaskIdentity(IdentityType type, string? tckn, string? passportNo)
    {
        var value = type == IdentityType.Tckn ? tckn : passportNo;
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= 5 ? new string('*', value.Length) : $"{value[..3]}{new string('*', value.Length - 5)}{value[^2..]}";
    }

    private static int? Age(DateOnly? birthDate, DateOnly today)
    {
        if (birthDate is not { } birth) return null;
        var age = today.Year - birth.Year;
        if (today < birth.AddYears(age)) age--;
        return age < 0 ? null : age;
    }
}
