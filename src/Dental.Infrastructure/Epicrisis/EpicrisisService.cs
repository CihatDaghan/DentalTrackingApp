using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Epicrisis;
using Dental.Application.Media;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Epicrisis;

/// <summary>
/// Epikriz: oluşturma anında seçilen tedavi kayıtlarının özetleri ve ICD tanıları JSON
/// snapshot olarak sabitlenir (kayıtlar sonradan değişse de belge kanıt olarak sabit kalır).
/// PDF ilk istekte üretilip MediaFile arşivine yazılır.
/// </summary>
public sealed class EpicrisisService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IMediaService media,
    IValidator<EpicrisisCreateRequest> createValidator) : IEpicrisisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EpicrisisDto> CreateAsync(
        long patientId, EpicrisisCreateRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        await EnsureDoctorIsDentistAsync(request.DoctorUserId, ct);

        var treatments = await SnapshotTreatmentsAsync(patientId, request.TreatmentRecordIds, ct);

        var document = new EpicrisisDocument
        {
            ClinicId = patient.ClinicId,
            PatientId = patientId,
            DoctorUserId = request.DoctorUserId,
            Title = request.Title.Trim(),
            DiagnosisJson = JsonSerializer.Serialize(request.Diagnoses, JsonOptions),
            TreatmentsJson = JsonSerializer.Serialize(treatments, JsonOptions),
            BodyText = request.BodyText,
        };
        db.EpicrisisDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return await GetAsync(document.Id, ct);
    }

    public async Task<IReadOnlyList<EpicrisisDto>> ListForPatientAsync(
        long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        var documents = await db.EpicrisisDocuments.AsNoTracking()
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        return await ToDtosAsync(documents, ct);
    }

    public async Task<EpicrisisDto> GetAsync(long id, CancellationToken ct = default)
    {
        var document = await db.EpicrisisDocuments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Epikriz bulunamadı.");
        return (await ToDtosAsync([document], ct))[0];
    }

    public async Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default)
    {
        var document = await db.EpicrisisDocuments.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Epikriz bulunamadı.");

        if (document.PdfFileId is null)
        {
            var patient = await db.Patients.AsNoTracking().FirstAsync(p => p.Id == document.PatientId, ct);
            var doctor = await db.Users.AsNoTracking().FirstAsync(u => u.Id == document.DoctorUserId, ct);
            var clinicName = await db.Clinics.AsNoTracking()
                .Where(c => c.Id == document.ClinicId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Klinik";

            var diagnoses = Deserialize<EpicrisisDiagnosis>(document.DiagnosisJson);
            var treatments = Deserialize<EpicrisisTreatmentLine>(document.TreatmentsJson);

            var pdfBytes = EpicrisisPdfGenerator.Generate(new EpicrisisPdfModel(
                clinicName,
                document.Title,
                patient.FullName,
                patient.FileNo,
                patient.BirthDate,
                CalculateAge(patient.BirthDate),
                GenderText(patient.Gender),
                doctor.FullName,
                doctor.DiplomaNo,
                ToLocal(document.CreatedAtUtc),
                [.. diagnoses.Select(d => new EpicrisisPdfDiagnosis(d.Code, d.Name))],
                [.. treatments.Select(t => new EpicrisisPdfTreatment(t.Date, t.ToothNumber, t.Name, t.DoctorName))],
                document.BodyText));

            var pdfFile = await media.SaveGeneratedAsync(new GeneratedFileRequest(
                document.ClinicId, document.PatientId, MediaCategory.EpicrisisPdf,
                $"epikriz-{document.Id}.pdf", "application/pdf", pdfBytes,
                Description: $"Epikriz #{document.Id} PDF"), ct);

            document.PdfFileId = pdfFile.Id;
            await db.SaveChangesAsync(ct);
        }

        return await media.OpenDownloadAsync(document.PdfFileId.Value, ct);
    }

    // ---- Yardımcılar ----

    /// <summary>Verilen tedavi id'lerinin (hastaya ait olmalı) özet satırlarını snapshot olarak üretir.</summary>
    private async Task<IReadOnlyList<EpicrisisTreatmentLine>> SnapshotTreatmentsAsync(
        long patientId, IReadOnlyList<long> treatmentRecordIds, CancellationToken ct)
    {
        if (treatmentRecordIds.Count == 0) return [];
        var ids = treatmentRecordIds.Distinct().ToList();

        var records = await (
            from t in db.TreatmentRecords.AsNoTracking()
            where ids.Contains(t.Id) && t.PatientId == patientId
            join u in db.Users on t.DoctorUserId equals u.Id
            orderby t.PerformedAtUtc ?? t.CreatedAtUtc, t.Id
            select new
            {
                t.Id,
                PerformedAtUtc = (DateTime?)t.PerformedAtUtc,
                t.CreatedAtUtc,
                t.ToothNumber,
                Name = t.TreatmentDefinition!.Name,
                DoctorName = u.FirstName + " " + u.LastName,
            }).ToListAsync(ct);

        if (records.Count != ids.Count)
            throw new KeyNotFoundException("Tedavi kaydı bulunamadı (hastaya ait olmayan veya silinmiş kayıt).");

        return [.. records.Select(r => new EpicrisisTreatmentLine(
            r.Id,
            DateOnly.FromDateTime(r.PerformedAtUtc ?? r.CreatedAtUtc),
            r.ToothNumber,
            r.Name,
            r.DoctorName))];
    }

    /// <summary>Epikriz tıbbi belgedir: yalnız aktif diş hekimi adına düzenlenebilir.</summary>
    private async Task EnsureDoctorIsDentistAsync(long doctorUserId, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        var isDentist = await db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == doctorUserId && u.TenantId == tenantId && u.UserType == UserType.Dentist && u.IsActive, ct);
        if (!isDentist)
            throw new InvalidOperationException("Epikriz yalnız hekim (diş hekimi) adına düzenlenebilir.");
    }

    private async Task<IReadOnlyList<EpicrisisDto>> ToDtosAsync(
        IReadOnlyList<EpicrisisDocument> documents, CancellationToken ct)
    {
        if (documents.Count == 0) return [];

        var patientIds = documents.Select(d => d.PatientId).Distinct().ToList();
        var doctorIds = documents.Select(d => d.DoctorUserId).Distinct().ToList();
        var patientNames = await db.Patients.AsNoTracking()
            .Where(p => patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        var doctorNames = await db.Users.AsNoTracking()
            .Where(u => doctorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);

        return [.. documents.Select(d => new EpicrisisDto(
            d.Id, d.PatientId, patientNames.GetValueOrDefault(d.PatientId, "-"),
            d.DoctorUserId, doctorNames.GetValueOrDefault(d.DoctorUserId, "-"),
            d.Title,
            Deserialize<EpicrisisDiagnosis>(d.DiagnosisJson),
            Deserialize<EpicrisisTreatmentLine>(d.TreatmentsJson),
            d.BodyText, d.PdfFileId, d.CreatedAtUtc))];
    }

    private static IReadOnlyList<T> Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<T>>(json, JsonOptions) ?? [];

    private static string? GenderText(Gender gender) => gender switch
    {
        Gender.Male => "Erkek",
        Gender.Female => "Kadın",
        _ => null,
    };

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
