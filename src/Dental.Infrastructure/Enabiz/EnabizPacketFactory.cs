using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Dental.Integrations.Enabiz;
using Dental.Integrations.Enabiz.PacketBuilders;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Enabiz;

/// <summary>
/// Domain kayıtlarından paket bağlamı (<see cref="EnabizPacketContext"/>) ve paket XML'i üretir.
///
/// <para>Üretim GÖNDERİM ANINDA yapılabilsin diye ayrı bir sınıftır: <c>RegenerateOnSend</c> açıkken
/// dispatcher paketi buradan yeniden üretir, böylece Held modunda aylarca bekleyen bir paket güncel
/// SKRS kod seti ve düzeltilmiş hasta verisiyle gider.</para>
/// </summary>
public sealed class EnabizPacketFactory(
    AppDbContext db,
    ITcknProtector tckn,
    EnabizSettings settings)
{
    private static readonly Packet101Builder Builder101 = new();
    private static readonly Packet102Builder Builder102 = new();
    private static readonly Packet103Builder Builder103 = new();
    private static readonly Packet203Builder Builder203 = new();
    private static readonly Packet405Builder Builder405 = new();

    /// <summary>
    /// Paket XML'ini üretir. <paramref name="sysTakipNo"/> bağımlı paketlerde zorunludur.
    /// </summary>
    public async Task<string> BuildAsync(EnabizSubmission submission, string? sysTakipNo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var context = await BuildContextAsync(submission, sysTakipNo, ct)
            ?? throw new EnabizPacketException(
                $"Paket bağlamı kurulamadı (gönderim #{submission.Id}); ziyaret veya hasta kaydı bulunamadı.");

        var element = submission.PacketType switch
        {
            EnabizPacketType.HastaKayit101 => Builder101.Build(context),
            EnabizPacketType.HizmetKayit102 => Builder102.Build(context),
            EnabizPacketType.Muayene103 => Builder103.Build(context),
            EnabizPacketType.AgizDisSagligi203 => Builder203.Build(context),
            EnabizPacketType.GunlukVeriSorgu405 => Builder405.Build(context),
            _ => throw new EnabizPacketException(
                $"{submission.PacketType} paketi için üretici tanımlı değil."),
        };

        return element.ToString(SaveOptions.None);
    }

    /// <summary>Ziyaret + hasta + hekim + işlem verisinden paket bağlamı kurar.</summary>
    public async Task<EnabizPacketContext?> BuildContextAsync(
        EnabizSubmission submission, string? sysTakipNo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.VisitId is not { } visitId) return null;

        var visit = await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == visitId, ct);
        if (visit is null) return null;

        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == visit.PatientId, ct);
        if (patient is null) return null;

        var doctor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == visit.DoctorUserId, ct);
        var clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == visit.ClinicId, ct);

        // 203/102 tek tedavi kaydına, 101/103 ziyaretin tamamına bakar.
        var recordsQuery = db.TreatmentRecords.AsNoTracking()
            .Include(t => t.TreatmentDefinition)
            .Where(t => t.VisitId == visitId && t.Status == TreatmentRecordStatus.Done);

        if (submission.TreatmentRecordId is { } recordId)
            recordsQuery = recordsQuery.Where(t => t.Id == recordId);

        var records = await recordsQuery.OrderBy(t => t.Id).ToListAsync(ct);

        var procedures = records.Select(record => new EnabizProcedure(
            ToothNumber: record.ToothNumber?.Trim(),
            SutCode: record.TreatmentDefinition?.SutCode?.Trim(),
            Name: record.TreatmentDefinition?.Name ?? "Tedavi",
            PerformedAtLocal: ToLocal(record.PerformedAtUtc ?? record.CreatedAtUtc),
            // İşlemin tesis içi tekil referansı; tedavi kaydı kimliği doğal olarak tekildir.
            ReferenceNo: record.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EndsAtLocal: ToLocal(record.PerformedAtUtc ?? record.CreatedAtUtc),
            Quantity: 1,
            DiagnosisIcdCode: record.DiagnosisIcdCode?.Trim(),
            Surfaces: DescribeSurfaces(record.Surfaces),
            RootCanalCount: record.RootCanalCount)).ToList();

        // 103 tanıları ziyaretin tüm Done kayıtlarından toplanır (tekilleştirilmiş).
        var diagnoses = records
            .Select(r => r.DiagnosisIcdCode?.Trim())
            .Where(code => EnabizPacketXml.IsValidIcd10(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => new EnabizDiagnosis(code!))
            .ToList();

        var prescriptions = await BuildPrescriptionsAsync(submission, visitId, ct);

        return new EnabizPacketContext
        {
            FacilityCode = submission.FacilityCode ?? clinic?.CkysCode ?? settings.CkysCode,
            FacilityName = string.IsNullOrWhiteSpace(settings.FacilityName) ? clinic?.Name : settings.FacilityName,
            SoftwareCompanyCode = settings.SoftwareCompanyCode,
            ProtocolNo = visit.ProtocolNo,
            FacilityReferenceNo = visit.ProtocolNo,
            SysTakipNo = sysTakipNo ?? visit.SysTakipNo,
            LocalTimestamp = ToLocal(DateTime.UtcNow),
            AdmissionAtLocal = ToLocal(visit.CreatedAtUtc),
            VisitDate = visit.VisitDate,
            Patient = MapPatient(patient),
            Physician = new EnabizPhysician(
                DecryptTckn(doctor?.TcknEncrypted),
                doctor is null ? "" : $"{doctor.FirstName} {doctor.LastName}".Trim()),
            Procedures = procedures,
            Diagnoses = diagnoses,
            Prescriptions = prescriptions,
        };
    }

    private async Task<IReadOnlyList<EnabizPrescription>> BuildPrescriptionsAsync(
        EnabizSubmission submission, long visitId, CancellationToken ct)
    {
        // Reçete bilgisi 103 paketine gömülür; yalnız reçete gönderimi tetiklendiyse yazılır.
        if (submission.PrescriptionId is not { } prescriptionId) return [];

        var prescription = await db.Prescriptions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.VisitId == visitId, ct);
        if (prescription is null) return [];

        var items = await db.PrescriptionItems.AsNoTracking()
            .Include(i => i.Drug)
            .Where(i => i.PrescriptionId == prescriptionId)
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        var doctor = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == prescription.DoctorUserId, ct);

        return
        [
            new EnabizPrescription(
                prescription.PrescriptionNo,
                ToLocal(prescription.CreatedAtUtc),
                DecryptTckn(doctor?.TcknEncrypted),
                // SKRS reçete türü: normal (beyaz) reçete.
                TypeCode: "1",
                Drugs: [.. items.Select(i => new EnabizPrescribedDrug(
                    i.Drug?.Barcode,
                    i.Drug?.Name ?? "İlaç",
                    i.BoxCount,
                    i.Dose,
                    Description: i.UsageNote))]),
        ];
    }

    private EnabizPatient MapPatient(Patient patient) => new(
        Tckn: patient.IdentityType == IdentityType.Tckn ? DecryptTckn(patient.TcknEncrypted) : null,
        PassportNo: patient.PassportNo,
        FirstName: patient.FirstName,
        LastName: patient.LastName,
        BirthDate: patient.BirthDate,
        Gender: MapGender(patient.Gender),
        NationalityCode: MapNationality(patient.NationalityCode),
        Address: patient.Address,
        District: patient.District,
        Phone: patient.Phone,
        Email: patient.Email);

    private string? DecryptTckn(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            return tckn.Unprotect(encrypted);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            // Anahtar halkası değiştiyse kimlik çözülemez; paket kimliksiz gitmesin diye null döner
            // ve zorunlu alan doğrulaması gönderimi durdurur.
            return null;
        }
    }

    /// <summary>SKRS cinsiyet kodu (1 erkek, 2 kadın, 3 bilinmiyor).</summary>
    internal static string MapGender(Gender gender) => gender switch
    {
        Gender.Male => "1",
        Gender.Female => "2",
        _ => "3",
    };

    /// <summary>
    /// SKRS uyruk kodu. Alfa-3 kod saklıyoruz; SKRS uyruk listesi kendi kodlamasını kullanır.
    /// Türkiye için sabit "1"; diğer uyruklarda saklanan kod olduğu gibi geçirilir ve
    /// SKRS senkronu sonrası eşleme netleştirilir.
    /// </summary>
    internal static string MapNationality(string? alpha3) =>
        string.IsNullOrWhiteSpace(alpha3) || alpha3.Equals("TUR", StringComparison.OrdinalIgnoreCase)
            ? "1"
            : alpha3.Trim();

    /// <summary>Yüzey bit bayrağını harf dizisine çevirir (MOD, OB...).</summary>
    internal static string? DescribeSurfaces(ToothSurfaces surfaces)
    {
        if (surfaces == ToothSurfaces.None) return null;
        var text = string.Concat(
            surfaces.HasFlag(ToothSurfaces.M) ? "M" : "",
            surfaces.HasFlag(ToothSurfaces.O) ? "O" : "",
            surfaces.HasFlag(ToothSurfaces.D) ? "D" : "",
            surfaces.HasFlag(ToothSurfaces.B) ? "B" : "",
            surfaces.HasFlag(ToothSurfaces.L) ? "L" : "");
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static DateTime ToLocal(DateTime utc) => utc + TrTime.Offset;
}
