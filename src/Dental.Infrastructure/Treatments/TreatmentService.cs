using Dental.Application.Abstractions;
using Dental.Application.Enabiz;
using Dental.Application.Finance;
using Dental.Application.Treatments;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Treatments;

public sealed class TreatmentService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    ICatalogService catalog,
    ILedgerService ledger,
    IEnabizSubmissionQueue enabiz,
    ILogger<TreatmentService> logger,
    IValidator<TreatmentAddRequest> addValidator,
    IValidator<TreatmentUpdateRequest> updateValidator,
    IValidator<TreatmentBulkPriceRequest> bulkPriceValidator) : ITreatmentService
{
    public async Task<PatientTeethDto> GetPatientTeethAsync(long patientId, CancellationToken ct = default)
    {
        await EnsurePatientExistsAsync(patientId, ct);

        var statuses = await db.ToothStatuses.AsNoTracking()
            .Where(t => t.PatientId == patientId)
            .ToListAsync(ct);

        var marks = await db.TreatmentRecords.AsNoTracking()
            .Where(t => t.PatientId == patientId && t.Status != TreatmentRecordStatus.Cancelled)
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.ToothNumber,
                Mark = new ToothMarkDto(t.Id, t.TreatmentDefinitionId, t.TreatmentDefinition!.Name,
                    t.Status, t.TreatmentDefinition.Category!.ColorHex, t.Surfaces, t.RootCanalCount),
            })
            .ToListAsync(ct);

        var conditionByTooth = statuses.ToDictionary(s => s.ToothNumber.Trim(), s => s.Condition);
        var marksByTooth = marks.Where(m => m.ToothNumber != null)
            .ToLookup(m => m.ToothNumber!.Trim(), m => m.Mark);

        var teeth = conditionByTooth.Keys.Union(marksByTooth.Select(g => g.Key))
            .OrderBy(t => t)
            .Select(t => new ToothDto(t,
                conditionByTooth.TryGetValue(t, out var condition) ? condition : ToothCondition.Present,
                [.. marksByTooth[t]]))
            .ToList();

        var mouthWide = marks.Where(m => m.ToothNumber is null).Select(m => m.Mark).ToList();
        return new PatientTeethDto(patientId, teeth, mouthWide);
    }

    public async Task<IReadOnlyList<TreatmentRecordDto>> AddTreatmentsAsync(
        long patientId, TreatmentAddRequest request, CancellationToken ct = default)
    {
        await addValidator.ValidateAndThrowAsync(request, ct);
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");

        var clinicId = request.ClinicId ?? tenant.ClinicId ?? patient.ClinicId;
        var definitionIds = request.Items.Select(i => i.TreatmentDefinitionId).Distinct().ToList();
        var definitions = await db.TreatmentDefinitions.AsNoTracking()
            .Where(d => definitionIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var records = new List<TreatmentRecord>();
        foreach (var item in request.Items)
        {
            if (!definitions.TryGetValue(item.TreatmentDefinitionId, out var definition))
                throw new KeyNotFoundException($"Tedavi tanımı bulunamadı: {item.TreatmentDefinitionId}.");
            if (!definition.IsActive)
                throw new InvalidOperationException($"'{definition.Name}' pasif; katalogdan aktifleştirmeden eklenemez.");

            var toothNumber = ValidateToothScope(definition, item);

            var record = new TreatmentRecord
            {
                ClinicId = clinicId,
                PatientId = patientId,
                DoctorUserId = item.DoctorUserId ?? tenant.UserId
                    ?? throw new InvalidOperationException("Hekim belirtilmedi ve oturum kullanıcısı yok."),
                VisitId = item.VisitId,
                TreatmentDefinitionId = definition.Id,
                ToothNumber = toothNumber,
                Surfaces = item.Surfaces,
                RootCanalCount = item.RootCanalCount,
                Status = item.Status,
                DiagnosisIcdCode = item.DiagnosisIcdCode?.Trim(),
                PlanGroupNo = item.PlanGroupNo,
                Price = item.Price ?? await catalog.ResolvePriceAsync(definition.Id, request.PriceListId, ct),
                DiscountAmount = item.DiscountAmount,
                VatRate = definition.VatRate,
                Description = item.Description,
            };
            if (record.Status == TreatmentRecordStatus.Done)
                record.PerformedAtUtc = clock.UtcNow;

            records.Add(record);
            db.TreatmentRecords.Add(record);
        }

        // Done kayıtlar ledger'a borç yazar; kayıt + borç + bakiye tek transaction'da olsun.
        var hasDone = records.Any(r => r.Status == TreatmentRecordStatus.Done);
        await using var tx = hasDone ? await db.Database.BeginTransactionAsync(ct) : null;

        await db.SaveChangesAsync(ct);

        // Done olarak eklenenlerin diş durumu + cari borç etkisini işle.
        if (hasDone)
        {
            var statusMap = await GetToothStatusMapAsync(patientId, ct);
            foreach (var record in records.Where(r => r.Status == TreatmentRecordStatus.Done))
            {
                var definition = definitions[record.TreatmentDefinitionId];
                ApplyToothStatusEffect(record, definition, statusMap);
                record.LedgerEntryId = await AddTreatmentChargeAsync(record, definition.Name, ct);
            }
            await db.SaveChangesAsync(ct);
        }

        if (tx is not null) await tx.CommitAsync(ct);

        // H: Done tedaviler e-Nabız kuyruğuna girer. Transaction'ın DIŞINDA çalışır — e-Nabız
        // kuyruğundaki bir sorun tedavi/borç kaydını geri almamalıdır.
        foreach (var record in records.Where(r => r.Status == TreatmentRecordStatus.Done))
            await QueueForEnabizAsync(record.Id, ct);

        var ids = records.Select(r => r.Id).ToList();
        return await ProjectRecords(db.TreatmentRecords.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).OrderBy(t => t.Id)).ToListAsync(ct);
    }

    public async Task<TreatmentRecordDto> UpdateAsync(long id, TreatmentUpdateRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var record = await FindAsync(id, ct);
        if (record.Status == TreatmentRecordStatus.Cancelled)
            throw new InvalidOperationException("İptal edilmiş tedavi kaydı güncellenemez.");

        var oldNet = Math.Max(0m, record.Price - record.DiscountAmount);
        if (request.Price is { } price) record.Price = price;
        if (request.DiscountAmount is { } discount) record.DiscountAmount = discount;
        if (request.Description is not null) record.Description = request.Description;
        if (request.Surfaces is { } surfaces)
        {
            var definition = await db.TreatmentDefinitions.AsNoTracking()
                .FirstAsync(d => d.Id == record.TreatmentDefinitionId, ct);
            if (definition.RequiresSurface && surfaces == ToothSurfaces.None)
                throw new InvalidOperationException($"'{definition.Name}' için en az bir yüzey seçilmelidir.");
            record.Surfaces = surfaces;
        }
        if (request.RootCanalCount is { } rootCanalCount) record.RootCanalCount = rootCanalCount;
        if (request.DiagnosisIcdCode is not null) record.DiagnosisIcdCode = request.DiagnosisIcdCode.Trim();
        if (request.PlanGroupNo is { } planGroupNo) record.PlanGroupNo = planGroupNo;
        if (request.DoctorUserId is { } doctorUserId) record.DoctorUserId = doctorUserId;

        // Done kayıtta net tutar değiştiyse fark ledger'a yazılır (inceleme bulgusu:
        // fiyat değişimi cari borca yansımıyordu).
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SyncDoneChargeDeltaAsync(record, oldNet, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Done kaydın net tutarı değiştiyse farkı Correction olarak borçlandırır/alacaklandırır.</summary>
    private async Task SyncDoneChargeDeltaAsync(TreatmentRecord record, decimal oldNet, CancellationToken ct)
    {
        if (record.Status != TreatmentRecordStatus.Done || record.LedgerEntryId is null) return;
        var newNet = Math.Max(0m, record.Price - record.DiscountAmount);
        var delta = newNet - oldNet;
        if (delta == 0m) return;

        await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            LedgerAccountType.Patient, record.PatientId, CompanyId: null,
            LedgerEntryType.Correction,
            Debit: delta > 0m ? delta : 0m,
            Credit: delta < 0m ? -delta : 0m,
            Description: $"Tedavi fiyat düzeltmesi (kayıt #{record.Id}: {oldNet:F2} → {newNet:F2})",
            ClinicId: record.ClinicId,
            RefType: nameof(TreatmentRecord), RefId: record.Id,
            CurrencyCode: record.CurrencyCode.Trim()), ct);
    }

    public async Task<TreatmentRecordDto> UpdateStatusAsync(long id, TreatmentStatusRequest request, CancellationToken ct = default)
    {
        var record = await FindAsync(id, ct);
        EnsureTransitionAllowed(record.Status, request.Status);

        // D aşaması: Done geçişi borçlandırır, Done→Cancelled ters kayıt atar —
        // ledger + bakiye + kayıt güncellemesi tek transaction'da.
        var charges = request.Status == TreatmentRecordStatus.Done && record.Status != TreatmentRecordStatus.Done;
        var reverses = request.Status == TreatmentRecordStatus.Cancelled
            && record.Status == TreatmentRecordStatus.Done && record.LedgerEntryId is not null;
        await using var tx = charges || reverses ? await db.Database.BeginTransactionAsync(ct) : null;

        if (charges)
        {
            record.PerformedAtUtc = request.PerformedAtUtc ?? clock.UtcNow;
            var definition = await db.TreatmentDefinitions.AsNoTracking()
                .FirstAsync(d => d.Id == record.TreatmentDefinitionId, ct);
            ApplyToothStatusEffect(record, definition, await GetToothStatusMapAsync(record.PatientId, ct));
            record.LedgerEntryId = await AddTreatmentChargeAsync(record, definition.Name, ct);
        }
        else if (reverses)
        {
            await ReverseTreatmentChargeAsync(record, ct);
        }
        record.Status = request.Status;

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

        if (charges) await QueueForEnabizAsync(record.Id, ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// e-Nabız tetiklemesi. Kuyruk hatası klinik akışı KESMEZ: tedavi kaydı ve borç zaten
    /// işlenmiştir, paket üretimi sonradan (elle ya da EnabizQueueJob ile) tamamlanabilir.
    /// </summary>
    private async Task QueueForEnabizAsync(long treatmentRecordId, CancellationToken ct)
    {
        try
        {
            await enabiz.OnTreatmentDoneAsync(treatmentRecordId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "e-Nabız kuyruğa alma başarısız. TedaviKaydı={Id}", treatmentRecordId);
        }
    }

    public async Task<IReadOnlyList<TreatmentRecordDto>> ListTreatmentsAsync(
        long patientId, TreatmentRecordStatus? status = null, CancellationToken ct = default)
    {
        await EnsurePatientExistsAsync(patientId, ct);
        var q = db.TreatmentRecords.AsNoTracking().Where(t => t.PatientId == patientId);
        if (status is { } s) q = q.Where(t => t.Status == s);
        return await ProjectRecords(q.OrderByDescending(t => t.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TreatmentRecordDto>> BulkUpdatePricesAsync(
        TreatmentBulkPriceRequest request, CancellationToken ct = default)
    {
        await bulkPriceValidator.ValidateAndThrowAsync(request, ct);

        var ids = request.Items.Select(i => i.Id).ToList();
        var records = await db.TreatmentRecords.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var item in request.Items)
        {
            if (!records.TryGetValue(item.Id, out var record))
                throw new KeyNotFoundException($"Tedavi kaydı bulunamadı: {item.Id}.");
            if (record.Status == TreatmentRecordStatus.Cancelled)
                throw new InvalidOperationException($"İptal edilmiş tedavi kaydının fiyatı değiştirilemez: {item.Id}.");
            var oldNet = Math.Max(0m, record.Price - record.DiscountAmount);
            record.Price = item.Price;
            record.DiscountAmount = item.DiscountAmount;
            await SyncDoneChargeDeltaAsync(record, oldNet, ct); // Done'da fark ledger'a
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ProjectRecords(db.TreatmentRecords.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).OrderBy(t => t.Id)).ToListAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var record = await FindAsync(id, ct);
        if (record.Status == TreatmentRecordStatus.Done)
            throw new InvalidOperationException("Yapıldı (Done) tedavi kaydı silinemez; yalnız iptal edilebilir.");
        db.TreatmentRecords.Remove(record); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    // ---- Yardımcılar ----

    /// <summary>
    /// Done geçişinde hastayı borçlandırır: TreatmentCharge, Debit = Price - DiscountAmount.
    /// Bakiye güncellemesi LedgerService içinde aynı transaction'da satır kilidiyle yapılır.
    /// </summary>
    private async Task<long> AddTreatmentChargeAsync(TreatmentRecord record, string treatmentName, CancellationToken ct)
    {
        var net = Math.Max(0m, record.Price - record.DiscountAmount);
        var description = record.ToothNumber is null ? treatmentName : $"{treatmentName} (diş {record.ToothNumber})";
        return await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            LedgerAccountType.Patient, record.PatientId, CompanyId: null,
            LedgerEntryType.TreatmentCharge, Debit: net, Credit: 0m,
            Description: description,
            EntryDate: Dental.Domain.Common.TrTime.ToLocalDate(record.PerformedAtUtc ?? clock.UtcNow),
            ClinicId: record.ClinicId,
            RefType: nameof(TreatmentRecord), RefId: record.Id,
            CurrencyCode: record.CurrencyCode.Trim()), ct);
    }

    /// <summary>Done→Cancelled: borç kaydının net tutarı kadar ters (Correction, Credit) kayıt atar.</summary>
    private async Task ReverseTreatmentChargeAsync(TreatmentRecord record, CancellationToken ct)
    {
        // Orijinal borç + sonraki fiyat düzeltmeleri = güncel net; iptal güncel neti ters çevirir.
        var net = Math.Max(0m, record.Price - record.DiscountAmount);
        if (net == 0m) return;

        await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            LedgerAccountType.Patient, record.PatientId, CompanyId: null,
            LedgerEntryType.Correction, Debit: 0m, Credit: net,
            Description: $"Tedavi iptali (kayıt #{record.Id})",
            ClinicId: record.ClinicId,
            RefType: nameof(TreatmentRecord), RefId: record.Id,
            CurrencyCode: record.CurrencyCode.Trim()), ct);
    }

    private async Task<TreatmentRecordDto> GetAsync(long id, CancellationToken ct) =>
        await ProjectRecords(db.TreatmentRecords.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Tedavi kaydı bulunamadı.");

    private async Task<TreatmentRecord> FindAsync(long id, CancellationToken ct) =>
        await db.TreatmentRecords.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Tedavi kaydı bulunamadı.");

    private async Task EnsurePatientExistsAsync(long patientId, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
    }

    /// <summary>ToothScope + RequiresSurface + FDI doğrulaması; normalize diş numarası döner.</summary>
    private static string? ValidateToothScope(TreatmentDefinition definition, TreatmentAddItem item)
    {
        var toothNumber = string.IsNullOrWhiteSpace(item.ToothNumber) ? null : item.ToothNumber.Trim();

        switch (definition.ToothScope)
        {
            case ToothScope.PerTooth when toothNumber is null:
                throw new InvalidOperationException($"'{definition.Name}' diş bazlı bir işlemdir; diş numarası zorunludur.");
            case ToothScope.WholeMouth when toothNumber is not null:
                throw new InvalidOperationException($"'{definition.Name}' ağız geneli bir işlemdir; diş numarası gönderilmez.");
        }
        if (toothNumber is not null && !FdiTeeth.IsValid(toothNumber))
            throw new InvalidOperationException($"Geçersiz FDI diş numarası: {toothNumber}.");
        if (definition.RequiresSurface && item.Surfaces == ToothSurfaces.None)
            throw new InvalidOperationException($"'{definition.Name}' için en az bir yüzey seçilmelidir.");
        return toothNumber;
    }

    private async Task<Dictionary<string, ToothStatus>> GetToothStatusMapAsync(long patientId, CancellationToken ct)
    {
        var rows = await db.ToothStatuses.Where(t => t.PatientId == patientId).ToListAsync(ct);
        return rows.ToDictionary(r => r.ToothNumber.Trim());
    }

    /// <summary>Done geçişinde tedavi tanımının etkisini ToothStatus'a işler (upsert).</summary>
    private void ApplyToothStatusEffect(TreatmentRecord record, TreatmentDefinition definition,
        Dictionary<string, ToothStatus> statusMap)
    {
        if (definition.ToothStatusEffect == ToothStatusEffect.None || record.ToothNumber is null) return;

        var condition = definition.ToothStatusEffect switch
        {
            ToothStatusEffect.Extracted => ToothCondition.Extracted,
            ToothStatusEffect.Implant => ToothCondition.Implant,
            ToothStatusEffect.Crown => ToothCondition.Crown,
            ToothStatusEffect.RootCanal => ToothCondition.RootCanalTreated,
            ToothStatusEffect.Bridge => ToothCondition.Bridge,
            _ => ToothCondition.Present,
        };

        var toothNumber = record.ToothNumber.Trim();
        if (statusMap.TryGetValue(toothNumber, out var status))
        {
            status.Condition = condition;
            status.UpdatedFromTreatmentRecordId = record.Id;
        }
        else
        {
            var created = new ToothStatus
            {
                PatientId = record.PatientId,
                ToothNumber = toothNumber,
                Condition = condition,
                UpdatedFromTreatmentRecordId = record.Id,
            };
            statusMap[toothNumber] = created;
            db.ToothStatuses.Add(created);
        }
    }

    /// <summary>Cancelled her durumdan; Done yalnız Diagnosis/Planned'dan; geri gidiş yok.</summary>
    private static void EnsureTransitionAllowed(TreatmentRecordStatus from, TreatmentRecordStatus to)
    {
        if (from == to) return;
        var allowed = to switch
        {
            TreatmentRecordStatus.Cancelled => true,
            TreatmentRecordStatus.Planned => from == TreatmentRecordStatus.Diagnosis,
            TreatmentRecordStatus.Done => from is TreatmentRecordStatus.Diagnosis or TreatmentRecordStatus.Planned,
            _ => false,
        };
        if (!allowed)
            throw new InvalidOperationException($"Geçersiz durum geçişi: {from} → {to}.");
    }

    private IQueryable<TreatmentRecordDto> ProjectRecords(IQueryable<TreatmentRecord> source) =>
        from t in source
        join u in db.Users on t.DoctorUserId equals u.Id into uj
        from u in uj.DefaultIfEmpty()
        select new TreatmentRecordDto(
            t.Id, t.ClinicId, t.PatientId, t.DoctorUserId,
            u != null ? u.FirstName + " " + u.LastName : "",
            t.TreatmentDefinitionId, t.TreatmentDefinition!.Name, t.TreatmentDefinition.Code,
            t.TreatmentDefinition.Category!.ColorHex,
            t.ToothNumber, t.Surfaces, t.RootCanalCount, t.Status, t.DiagnosisIcdCode, t.PlanGroupNo,
            t.Price, t.DiscountAmount, t.VatRate, t.CurrencyCode.Trim(), t.PerformedAtUtc, t.Description,
            t.EnabizStatus, t.VisitId, t.CreatedAtUtc);
}
