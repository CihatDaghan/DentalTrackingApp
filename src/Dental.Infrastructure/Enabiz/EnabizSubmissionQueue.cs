using Dental.Application.Abstractions;
using Dental.Application.Enabiz;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Invoices;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Enabiz;

/// <summary>
/// Klinik iş akışının e-Nabız'a bağlandığı tek nokta: ziyaret oluşturma + paket kuyruğa alma.
///
/// <para><b>Paket kapsamı kararı</b> (idempotanlığın temeli):
/// <list type="bullet">
///   <item><b>101 ve 103 ziyaret başınadır</b> — bir başvuru bir kez kaydedilir, tanılar ziyaretin
///         tamamından toplanır.</item>
///   <item><b>203 ve 102 tedavi kaydı başınadır</b> — resmi alan tanımı her işlem için tekil
///         <c>ISLEM_REFERANS_NUMARASI</c> ister; tedavi kaydı kimliği doğal olarak tekildir.
///         Böylece aynı ziyarete sonradan eklenen tedavi, önceki paketleri bozmadan kendi paketini
///         alır ve tekrar gönderim (çift kayıt) riski oluşmaz.</item>
///   <item>Diş numarası olan işlem 203'e, ağız geneli işlem 102'ye gider — 203 diş bazlı bir settir
///         ve diş numarası olmadan üretilemez.</item>
/// </list></para>
/// </summary>
public sealed class EnabizSubmissionQueue(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    INumberSequenceService sequences,
    EnabizModeResolver modes,
    ILogger<EnabizSubmissionQueue> logger) : IEnabizSubmissionQueue
{
    public async Task OnTreatmentDoneAsync(long treatmentRecordId, CancellationToken ct = default)
    {
        if (tenant.TenantId is not { } tenantId || !modes.TriggerEnabled) return;

        var mode = await modes.ResolveAsync(tenantId, ct);
        if (!mode.ShouldProduce) return;

        var record = await db.TreatmentRecords
            .Include(t => t.TreatmentDefinition)
            .FirstOrDefaultAsync(t => t.Id == treatmentRecordId, ct);
        if (record is null || record.Status != TreatmentRecordStatus.Done) return;

        var visit = await EnsureVisitAsync(record, ct);
        record.VisitId = visit.Id;
        if (record.EnabizStatus == EnabizStatus.NotRequired)
            record.EnabizStatus = EnabizStatus.Pending;

        await QueuePacketsAsync(visit, record, mode, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task OnPrescriptionSubmittedAsync(long prescriptionId, CancellationToken ct = default)
    {
        if (tenant.TenantId is not { } tenantId) return;

        var mode = await modes.ResolveAsync(tenantId, ct);
        if (!mode.ShouldProduce) return;

        var prescription = await db.Prescriptions.FirstOrDefaultAsync(p => p.Id == prescriptionId, ct);
        if (prescription is null) return;

        // Reçete bir ziyarete bağlı değilse USS'ye bağlanacağı takip numarası da yoktur.
        if (prescription.VisitId is not { } visitId)
        {
            logger.LogWarning(
                "Reçete #{Id} bir ziyarete bağlı olmadığı için e-Nabız'a gönderilemez.", prescriptionId);
            return;
        }

        var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId, ct);
        if (visit is null) return;

        var packet101 = await EnsureSubmissionAsync(
            visit, EnabizPacketType.HastaKayit101, null, null, mode, ct);

        // Reçete bilgisi 103 (Muayene) paketinin HASTA_RECETE_BILGILERI setinde taşınır.
        var submission = await EnsureSubmissionAsync(
            visit, EnabizPacketType.Muayene103, null, packet101, mode, ct);
        submission.PrescriptionId = prescriptionId;

        prescription.Status = PrescriptionStatus.SubmittedToUss;
        await db.SaveChangesAsync(ct);
    }

    public async Task<EnabizQueueResultDto> QueueVisitAsync(long visitId, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan e-Nabız kuyruğu kullanılamaz.");

        var mode = await modes.ResolveAsync(tenantId, ct);
        var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId, ct)
            ?? throw new KeyNotFoundException("Ziyaret bulunamadı.");

        if (!mode.ShouldProduce)
            return new EnabizQueueResultDto(visit.Id, visit.ProtocolNo, [], mode.Mode);

        var records = await db.TreatmentRecords
            .Include(t => t.TreatmentDefinition)
            .Where(t => t.VisitId == visitId && t.Status == TreatmentRecordStatus.Done)
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        var ids = new List<long>();
        var packet101 = await EnsureSubmissionAsync(visit, EnabizPacketType.HastaKayit101, null, null, mode, ct);
        ids.Add(packet101.Id);

        if (records.Count > 0)
        {
            var packet103 = await EnsureSubmissionAsync(
                visit, EnabizPacketType.Muayene103, null, packet101, mode, ct);
            ids.Add(packet103.Id);

            foreach (var record in records)
            {
                var dependent = await QueueRecordPacketAsync(visit, record, packet101, mode, ct);
                if (dependent is not null) ids.Add(dependent.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        return new EnabizQueueResultDto(visit.Id, visit.ProtocolNo, [.. ids.Where(i => i > 0)], mode.Mode);
    }

    // ---- İç akış ----

    private async Task QueuePacketsAsync(
        Visit visit, TreatmentRecord record, EnabizModeSnapshot mode, CancellationToken ct)
    {
        var packet101 = await EnsureSubmissionAsync(visit, EnabizPacketType.HastaKayit101, null, null, mode, ct);
        await EnsureSubmissionAsync(visit, EnabizPacketType.Muayene103, null, packet101, mode, ct);
        await QueueRecordPacketAsync(visit, record, packet101, mode, ct);
    }

    /// <summary>Tedavi kaydına ait paketi (203 diş bazlı / 102 ağız geneli) kuyruğa alır.</summary>
    private async Task<EnabizSubmission?> QueueRecordPacketAsync(
        Visit visit, TreatmentRecord record, EnabizSubmission parent, EnabizModeSnapshot mode, CancellationToken ct)
    {
        // 203 MUDAHALE (SUT kodu) zorunlu kılar; kodu olmayan tedavi paket üretemez.
        if (string.IsNullOrWhiteSpace(record.TreatmentDefinition?.SutCode))
        {
            logger.LogWarning(
                "Tedavi #{Id} ('{Name}') SUT/SKRS kodu olmadığı için e-Nabız paketi üretilemedi.",
                record.Id, record.TreatmentDefinition?.Name);
            return null;
        }

        var type = string.IsNullOrWhiteSpace(record.ToothNumber)
            ? EnabizPacketType.HizmetKayit102
            : EnabizPacketType.AgizDisSagligi203;

        return await EnsureSubmissionAsync(visit, type, record.Id, parent, mode, ct);
    }

    /// <summary>
    /// (Ziyaret, paket tipi, tedavi kaydı) üçlüsü için gönderim satırını bulur ya da oluşturur.
    /// Zaten kabul edilmiş bir paket varsa DOKUNULMAZ — çift kayıt USS'de mükerrer veri demektir.
    /// </summary>
    private async Task<EnabizSubmission> EnsureSubmissionAsync(
        Visit visit,
        EnabizPacketType type,
        long? treatmentRecordId,
        EnabizSubmission? dependsOn,
        EnabizModeSnapshot mode,
        CancellationToken ct)
    {
        var existing = await db.EnabizSubmissions.FirstOrDefaultAsync(s =>
            s.VisitId == visit.Id &&
            s.PacketType == type &&
            s.TreatmentRecordId == treatmentRecordId, ct);

        if (existing is not null)
        {
            // Reddedilmiş/vazgeçilmiş paket, tetikleme tekrarında yeniden denenebilir hâle gelir.
            if (existing.State is EnabizSubmissionState.Held && mode.ShouldSend)
                existing.State = EnabizSubmissionState.Queued;
            return existing;
        }

        var submission = new EnabizSubmission
        {
            TenantId = visit.TenantId,
            ClinicId = visit.ClinicId,
            FacilityCode = mode.CkysCode ?? await GetClinicCkysAsync(visit.ClinicId, ct),
            PacketType = type,
            VisitId = visit.Id,
            TreatmentRecordId = treatmentRecordId,
            DependsOnSubmissionId = dependsOn?.Id,
            State = mode.ShouldSend ? EnabizSubmissionState.Queued : EnabizSubmissionState.Held,
            NextAttemptAtUtc = mode.ShouldSend ? clock.UtcNow : null,
        };

        db.EnabizSubmissions.Add(submission);
        // Bağımlılık kimliği verilebilmesi için kimlik hemen üretilir.
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "e-Nabız paketi kuyruğa alındı. Paket={PacketType} Ziyaret={VisitId} Durum={State} Mod={Mode}",
            type, visit.Id, submission.State, mode.Mode);
        return submission;
    }

    /// <summary>Tedavi için ziyaret bulur ya da oluşturur (aynı gün + aynı hekim + aynı klinik).</summary>
    private async Task<Visit> EnsureVisitAsync(TreatmentRecord record, CancellationToken ct)
    {
        if (record.VisitId is { } existingId)
        {
            var existing = await db.Visits.FirstOrDefaultAsync(v => v.Id == existingId, ct);
            if (existing is not null) return existing;
        }

        var visitDate = TrTime.ToLocalDate(record.PerformedAtUtc ?? clock.UtcNow);
        var sameDay = await db.Visits.FirstOrDefaultAsync(v =>
            v.PatientId == record.PatientId &&
            v.ClinicId == record.ClinicId &&
            v.DoctorUserId == record.DoctorUserId &&
            v.VisitDate == visitDate, ct);
        if (sameDay is not null) return sameDay;

        var visit = new Visit
        {
            TenantId = record.TenantId,
            ClinicId = record.ClinicId,
            PatientId = record.PatientId,
            DoctorUserId = record.DoctorUserId,
            VisitDate = visitDate,
            ProtocolNo = await NextProtocolNoAsync(record.TenantId, visitDate, ct),
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Ziyaret oluşturuldu. Id={Id} Protokol={ProtocolNo} Hasta={PatientId}",
            visit.Id, visit.ProtocolNo, visit.PatientId);
        return visit;
    }

    /// <summary>Protokol numarası: yıl bazlı atomik sıra ('2026-000001').</summary>
    private async Task<string> NextProtocolNoAsync(long tenantId, DateOnly visitDate, CancellationToken ct)
    {
        var next = await sequences.NextAsync(
            tenantId, NumberSequenceType.ProtocolNo, "P", visitDate.Year, ct);
        return $"{visitDate.Year}-{next:D6}";
    }

    private async Task<string?> GetClinicCkysAsync(long clinicId, CancellationToken ct) =>
        await db.Clinics.AsNoTracking().Where(c => c.Id == clinicId).Select(c => c.CkysCode)
            .FirstOrDefaultAsync(ct);
}
