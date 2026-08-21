using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Labs;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Labs;

/// <summary>
/// Laboratuvar takibi: firma CRUD, vaka CRUD + durum makinesi (her geçiş
/// LabCaseStatusHistory'ye yazılır), filtreli liste ve sorgu bazlı gecikmiş bayrağı
/// (DueDate &lt; bugün &amp;&amp; Status &lt; Received).
/// </summary>
public sealed class LabService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IValidator<LaboratoryUpsertRequest> laboratoryValidator,
    IValidator<LabCaseUpsertRequest> caseValidator,
    IValidator<LabCaseStatusChangeRequest> statusValidator) : ILabService
{
    // ---- Laboratuvar firmaları ----

    public async Task<IReadOnlyList<LaboratoryDto>> ListLaboratoriesAsync(CancellationToken ct = default) =>
        await db.Laboratories.AsNoTracking().OrderBy(l => l.Name)
            .Select(l => new LaboratoryDto(l.Id, l.Name, l.Phone, l.Email, l.Address, l.ContactPerson))
            .ToListAsync(ct);

    public async Task<LaboratoryDto> GetLaboratoryAsync(long id, CancellationToken ct = default)
    {
        var l = await db.Laboratories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Laboratuvar bulunamadı.");
        return new LaboratoryDto(l.Id, l.Name, l.Phone, l.Email, l.Address, l.ContactPerson);
    }

    public async Task<LaboratoryDto> CreateLaboratoryAsync(
        LaboratoryUpsertRequest request, CancellationToken ct = default)
    {
        await laboratoryValidator.ValidateAndThrowAsync(request, ct);
        var name = request.Name.Trim();
        if (await db.Laboratories.AnyAsync(l => l.Name == name, ct))
            throw new InvalidOperationException($"'{name}' adında bir laboratuvar zaten var.");

        var laboratory = new Laboratory
        {
            Name = name,
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
        };
        db.Laboratories.Add(laboratory);
        await db.SaveChangesAsync(ct);
        return await GetLaboratoryAsync(laboratory.Id, ct);
    }

    public async Task<LaboratoryDto> UpdateLaboratoryAsync(
        long id, LaboratoryUpsertRequest request, CancellationToken ct = default)
    {
        await laboratoryValidator.ValidateAndThrowAsync(request, ct);
        var laboratory = await db.Laboratories.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new KeyNotFoundException("Laboratuvar bulunamadı.");
        var name = request.Name.Trim();
        if (await db.Laboratories.AnyAsync(l => l.Name == name && l.Id != id, ct))
            throw new InvalidOperationException($"'{name}' adında bir laboratuvar zaten var.");

        laboratory.Name = name;
        laboratory.Phone = request.Phone?.Trim();
        laboratory.Email = request.Email?.Trim();
        laboratory.Address = request.Address?.Trim();
        laboratory.ContactPerson = request.ContactPerson?.Trim();
        await db.SaveChangesAsync(ct);
        return await GetLaboratoryAsync(id, ct);
    }

    public async Task DeleteLaboratoryAsync(long id, CancellationToken ct = default)
    {
        var laboratory = await db.Laboratories.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new KeyNotFoundException("Laboratuvar bulunamadı.");
        if (await db.LabCases.AnyAsync(c => c.LaboratoryId == id && c.Status != LabCaseStatus.Delivered, ct))
            throw new InvalidOperationException("Açık vakası olan laboratuvar silinemez.");

        db.Laboratories.Remove(laboratory); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    // ---- Vakalar ----

    public async Task<LabCaseDto> CreateCaseAsync(LabCaseUpsertRequest request, CancellationToken ct = default)
    {
        await caseValidator.ValidateAndThrowAsync(request, ct);
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        await EnsureLaboratoryExistsAsync(request.LaboratoryId, ct);
        await EnsureDoctorExistsAsync(request.DoctorUserId, ct);

        var labCase = new LabCase
        {
            ClinicId = request.ClinicId ?? patient.ClinicId,
            PatientId = request.PatientId,
            DoctorUserId = request.DoctorUserId,
            LaboratoryId = request.LaboratoryId,
            CaseNo = await NextCaseNoAsync(ct),
            WorkType = request.WorkType.Trim(),
            TeethCsv = NormalizeTeethCsv(request.TeethCsv),
            Shade = request.Shade?.Trim(),
            Material = request.Material?.Trim(),
            Status = LabCaseStatus.Draft,
            SentDate = request.SentDate,
            DueDate = request.DueDate,
            Price = request.Price,
            Note = request.Note,
        };
        db.LabCases.Add(labCase);
        await db.SaveChangesAsync(ct);

        // İlk geçmiş satırı: Draft (vaka açılışı).
        db.LabCaseStatusHistories.Add(NewHistory(labCase.Id, LabCaseStatus.Draft, "Vaka oluşturuldu."));
        await db.SaveChangesAsync(ct);
        return await GetCaseAsync(labCase.Id, ct);
    }

    public async Task<LabCaseDto> UpdateCaseAsync(
        long id, LabCaseUpsertRequest request, CancellationToken ct = default)
    {
        await caseValidator.ValidateAndThrowAsync(request, ct);
        var labCase = await db.LabCases.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Laboratuvar vakası bulunamadı.");
        await EnsureLaboratoryExistsAsync(request.LaboratoryId, ct);
        await EnsureDoctorExistsAsync(request.DoctorUserId, ct);
        if (labCase.PatientId != request.PatientId)
            throw new InvalidOperationException("Vakanın hastası değiştirilemez.");

        labCase.DoctorUserId = request.DoctorUserId;
        labCase.LaboratoryId = request.LaboratoryId;
        labCase.WorkType = request.WorkType.Trim();
        labCase.TeethCsv = NormalizeTeethCsv(request.TeethCsv);
        labCase.Shade = request.Shade?.Trim();
        labCase.Material = request.Material?.Trim();
        labCase.SentDate = request.SentDate ?? labCase.SentDate;
        labCase.DueDate = request.DueDate;
        labCase.Price = request.Price;
        labCase.Note = request.Note;
        if (request.ClinicId is { } clinicId) labCase.ClinicId = clinicId;
        await db.SaveChangesAsync(ct);
        return await GetCaseAsync(id, ct);
    }

    public async Task<LabCaseDto> GetCaseAsync(long id, CancellationToken ct = default) =>
        await Project(db.LabCases.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Laboratuvar vakası bulunamadı.");

    public async Task DeleteCaseAsync(long id, CancellationToken ct = default)
    {
        var labCase = await db.LabCases.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Laboratuvar vakası bulunamadı.");
        if (labCase.Status is not (LabCaseStatus.Draft or LabCaseStatus.Delivered))
            throw new InvalidOperationException("Yalnız taslak veya teslim edilmiş vaka silinebilir.");
        db.LabCases.Remove(labCase); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    public async Task<LabCaseDto> ChangeStatusAsync(
        long id, LabCaseStatusChangeRequest request, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);
        var labCase = await db.LabCases.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Laboratuvar vakası bulunamadı.");
        if (labCase.Status == request.Status)
            throw new InvalidOperationException("Vaka zaten bu durumda.");

        labCase.Status = request.Status;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        if (request.Status == LabCaseStatus.Sent) labCase.SentDate ??= today;
        if (request.Status == LabCaseStatus.Received) labCase.ReceivedDate ??= today;

        db.LabCaseStatusHistories.Add(NewHistory(labCase.Id, request.Status, request.Note));
        await db.SaveChangesAsync(ct);
        return await GetCaseAsync(id, ct);
    }

    public async Task<IReadOnlyList<LabCaseHistoryDto>> GetHistoryAsync(long caseId, CancellationToken ct = default)
    {
        if (!await db.LabCases.AnyAsync(c => c.Id == caseId, ct))
            throw new KeyNotFoundException("Laboratuvar vakası bulunamadı.");
        return await (
            from h in db.LabCaseStatusHistories.AsNoTracking()
            where h.LabCaseId == caseId
            join u in db.Users on h.ChangedByUserId equals (long?)u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby h.ChangedAtUtc, h.Id
            select new LabCaseHistoryDto(h.Id, h.Status, h.ChangedAtUtc, h.ChangedByUserId,
                u != null ? u.FirstName + " " + u.LastName : null, h.Note))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<LabCaseDto>> ListCasesAsync(
        LabCaseListQuery query, CancellationToken ct = default)
    {
        var q = db.LabCases.AsNoTracking().AsQueryable();
        if (query.Status is { } status) q = q.Where(c => c.Status == status);
        if (query.LaboratoryId is { } laboratoryId) q = q.Where(c => c.LaboratoryId == laboratoryId);
        if (query.DoctorUserId is { } doctorUserId) q = q.Where(c => c.DoctorUserId == doctorUserId);
        if (query.PatientId is { } patientId) q = q.Where(c => c.PatientId == patientId);
        if (query.DueFrom is { } dueFrom) q = q.Where(c => c.DueDate >= dueFrom);
        if (query.DueTo is { } dueTo) q = q.Where(c => c.DueDate <= dueTo);
        if (query.OverdueOnly)
        {
            var today = DateOnly.FromDateTime(clock.UtcNow);
            q = q.Where(c => c.DueDate != null && c.DueDate < today && c.Status < LabCaseStatus.Received);
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var totalCount = await q.CountAsync(ct);
        var items = await Project(q
                .OrderBy(c => c.DueDate == null).ThenBy(c => c.DueDate).ThenByDescending(c => c.Id)
                .Skip(page.Skip).Take(page.EffectivePageSize))
            .ToListAsync(ct);
        return new PagedResult<LabCaseDto>(items, page.Page, page.EffectivePageSize, totalCount);
    }

    public async Task<IReadOnlyList<LabCaseDto>> ListForPatientAsync(
        long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        return await Project(db.LabCases.AsNoTracking()
                .Where(c => c.PatientId == patientId)
                .OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id))
            .ToListAsync(ct);
    }

    // ---- Yardımcılar ----

    private LabCaseStatusHistory NewHistory(long labCaseId, LabCaseStatus status, string? note) => new()
    {
        LabCaseId = labCaseId,
        Status = status,
        ChangedAtUtc = clock.UtcNow,
        ChangedByUserId = tenant.UserId,
        Note = note,
    };

    /// <summary>Tenant içi yıl bazlı sıra: 'LAB-2026-0001' (soft-delete dahil max+1).</summary>
    private async Task<string> NextCaseNoAsync(CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan vaka açılamaz.");
        var prefix = $"LAB-{clock.UtcNow.Year}-";
        var numbers = await db.LabCases.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.CaseNo.StartsWith(prefix))
            .Select(c => c.CaseNo)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    private async Task EnsureLaboratoryExistsAsync(long laboratoryId, CancellationToken ct)
    {
        if (!await db.Laboratories.AnyAsync(l => l.Id == laboratoryId, ct))
            throw new KeyNotFoundException("Laboratuvar bulunamadı.");
    }

    private async Task EnsureDoctorExistsAsync(long doctorUserId, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        var exists = await db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == doctorUserId && u.TenantId == tenantId && u.UserType == UserType.Dentist && u.IsActive, ct);
        if (!exists)
            throw new InvalidOperationException("Vaka hekimi aktif bir diş hekimi olmalıdır.");
    }

    private static string? NormalizeTeethCsv(string? teethCsv) =>
        string.IsNullOrWhiteSpace(teethCsv)
            ? null
            : string.Join(',', teethCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private IQueryable<LabCaseDto> Project(IQueryable<LabCase> source)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return from c in source
            join p in db.Patients on c.PatientId equals p.Id
            join u in db.Users on c.DoctorUserId equals u.Id
            join l in db.Laboratories.IgnoreQueryFilters() on c.LaboratoryId equals l.Id
            select new LabCaseDto(
                c.Id, c.CaseNo, c.ClinicId,
                c.PatientId, p.FirstName + " " + p.LastName,
                c.DoctorUserId, u.FirstName + " " + u.LastName,
                c.LaboratoryId, l.Name,
                c.WorkType, c.TeethCsv, c.Shade, c.Material, c.Status,
                c.SentDate, c.DueDate, c.ReceivedDate, c.Price, c.Note,
                c.DueDate != null && c.DueDate < today && c.Status < LabCaseStatus.Received,
                c.CreatedAtUtc);
    }
}
