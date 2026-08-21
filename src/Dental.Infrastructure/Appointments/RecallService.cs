using Dental.Application.Appointments;
using Dental.Application.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Appointments;

public sealed class RecallService(
    AppDbContext db,
    IAppointmentService appointments,
    IValidator<RecallCreateRequest> createValidator,
    IValidator<RecallConvertRequest> convertValidator) : IRecallService
{
    public async Task<RecallDto> CreateAsync(RecallCreateRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        if (!await db.Patients.AnyAsync(p => p.Id == request.PatientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");

        var recall = new RecallPlan
        {
            PatientId = request.PatientId,
            SuggestedDate = request.SuggestedDate,
            Reason = request.Reason,
            DoctorUserId = request.DoctorUserId,
            SourceTreatmentRecordId = request.SourceTreatmentRecordId,
        };
        db.RecallPlans.Add(recall);
        await db.SaveChangesAsync(ct);
        return await GetAsync(recall.Id, ct);
    }

    public async Task<PagedResult<RecallDto>> ListAsync(RecallListQuery query, CancellationToken ct = default)
    {
        var q = db.RecallPlans.AsNoTracking();
        if (query.PatientId is { } patientId) q = q.Where(r => r.PatientId == patientId);
        if (query.Status is { } status) q = q.Where(r => r.Status == status);
        if (query.From is { } from) q = q.Where(r => r.SuggestedDate >= from);
        if (query.To is { } to) q = q.Where(r => r.SuggestedDate <= to);

        var page = new PageRequest(query.Page, query.PageSize);
        var total = await q.CountAsync(ct);
        var items = await ProjectDtos(q.OrderBy(r => r.SuggestedDate).ThenBy(r => r.Id)
                .Skip(page.Skip).Take(page.EffectivePageSize))
            .ToListAsync(ct);
        return new PagedResult<RecallDto>(items, Math.Max(query.Page, 1), page.EffectivePageSize, total);
    }

    public async Task<RecallDto> UpdateStatusAsync(long id, RecallStatusRequest request, CancellationToken ct = default)
    {
        var recall = await FindAsync(id, ct);
        recall.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<RecallDto> ConvertToAppointmentAsync(long id, RecallConvertRequest request, CancellationToken ct = default)
    {
        await convertValidator.ValidateAndThrowAsync(request, ct);
        var recall = await FindAsync(id, ct);
        if (recall.Status == RecallStatus.ConvertedToAppointment)
            throw new InvalidOperationException("Bu kontrol planı zaten randevuya çevrilmiş.");

        var appointment = await appointments.CreateAsync(new AppointmentUpsertRequest(
            ClinicId: request.ClinicId,
            DoctorUserId: request.DoctorUserId,
            StartUtc: request.StartUtc,
            EndUtc: request.EndUtc,
            Type: AppointmentType.Control,
            PatientId: recall.PatientId,
            Note: request.Note ?? recall.Reason,
            SourceTreatmentRecordId: recall.SourceTreatmentRecordId), ct);

        recall.AppointmentId = appointment.Id;
        recall.Status = RecallStatus.ConvertedToAppointment;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<RecallDto> GetAsync(long id, CancellationToken ct) =>
        await ProjectDtos(db.RecallPlans.AsNoTracking().Where(r => r.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Kontrol planı bulunamadı.");

    private async Task<RecallPlan> FindAsync(long id, CancellationToken ct) =>
        await db.RecallPlans.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Kontrol planı bulunamadı.");

    private IQueryable<RecallDto> ProjectDtos(IQueryable<RecallPlan> source) =>
        from r in source
        join p in db.Patients on r.PatientId equals p.Id into pj
        from p in pj.DefaultIfEmpty()
        select new RecallDto(r.Id, r.PatientId,
            p != null ? p.FirstName + " " + p.LastName : "",
            r.DoctorUserId, r.SuggestedDate, r.Reason, r.Status,
            r.AppointmentId, r.LastReminderAtUtc, r.CreatedAtUtc);
}
