using Dental.Application.Abstractions;
using Dental.Application.Appointments;
using Dental.Application.Notifications;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Appointments;

public sealed class AppointmentService(
    AppDbContext db,
    ITenantContext tenant,
    INotificationService notifications,
    IValidator<AppointmentUpsertRequest> validator,
    IValidator<WorkingHoursSaveRequest> workingHoursValidator) : IAppointmentService
{
    public async Task<AppointmentDto> CreateAsync(AppointmentUpsertRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureTenantReferencesAsync(request.ClinicId, request.DoctorUserId, ct);
        await EnsurePatientExistsAsync(request.PatientId, ct);

        // Çakışma kontrolü + ekleme tek transaction'da; hekim kilidi çifte rezervasyonu önler.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureNoOverlapAsync(request.DoctorUserId, request.StartUtc, request.EndUtc, excludeId: null, ct);

        var appointment = new Appointment
        {
            ClinicId = request.ClinicId,
            PatientId = request.PatientId,
            DoctorUserId = request.DoctorUserId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Type = request.Type,
            Title = request.Title,
            Note = request.Note,
            Color = request.Color,
            SourceTreatmentRecordId = request.SourceTreatmentRecordId,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var dto = await GetAsync(appointment.Id, ct);
        await notifications.PublishAsync(new NotificationCreateRequest(
            NotificationEvents.AppointmentCreated,
            "Yeni randevu",
            $"{dto.PatientName ?? dto.Title ?? "Randevu"} — " +
            $"{(dto.StartUtc + TrTime.Offset):dd.MM.yyyy HH:mm} ({dto.DoctorName})",
            // I aşamasında tüm olay bildirimleri kiracı geneli yayındır (UserId = null):
            // klinik ekibi ortak çalışır, resepsiyon da hekim de aynı akışı görür.
            LinkPath: $"/appointments?id={dto.Id}"), ct);
        return dto;
    }

    public async Task<AppointmentDto> UpdateAsync(long id, AppointmentUpsertRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var appointment = await FindAsync(id, ct);
        await EnsureTenantReferencesAsync(request.ClinicId, request.DoctorUserId, ct);
        await EnsurePatientExistsAsync(request.PatientId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureNoOverlapAsync(request.DoctorUserId, request.StartUtc, request.EndUtc, excludeId: id, ct);

        if (request.RowVersion is not null)
            db.Entry(appointment).Property(a => a.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);

        appointment.ClinicId = request.ClinicId;
        appointment.PatientId = request.PatientId;
        appointment.DoctorUserId = request.DoctorUserId;
        appointment.StartUtc = request.StartUtc;
        appointment.EndUtc = request.EndUtc;
        appointment.Type = request.Type;
        appointment.Title = request.Title;
        appointment.Note = request.Note;
        appointment.Color = request.Color;
        appointment.SourceTreatmentRecordId = request.SourceTreatmentRecordId;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> GetAsync(long id, CancellationToken ct = default)
    {
        var row = await ProjectRows(db.Appointments.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Randevu bulunamadı.");
        return row.ToDto();
    }

    public async Task<IReadOnlyList<AppointmentDto>> ListAsync(AppointmentListQuery query, CancellationToken ct = default)
    {
        var source = db.Appointments.AsNoTracking()
            .Where(a => a.StartUtc < query.To && a.EndUtc > query.From);
        if (query.ClinicId is { } clinicId)
            source = source.Where(a => a.ClinicId == clinicId);
        if (query.DoctorIds is { Count: > 0 } doctorIds)
            source = source.Where(a => doctorIds.Contains(a.DoctorUserId));

        var rows = await ProjectRows(source.OrderBy(a => a.StartUtc)).ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<AppointmentDto> UpdateStatusAsync(long id, AppointmentStatusRequest request, CancellationToken ct = default)
    {
        var appointment = await FindAsync(id, ct);

        if (request.Status == AppointmentStatus.Cancelled)
        {
            appointment.CancelReason = request.CancelReason;
            appointment.CancelledByUserId = tenant.UserId;
        }
        appointment.Status = request.Status;

        await db.SaveChangesAsync(ct);
        var dto = await GetAsync(id, ct);

        if (request.Status == AppointmentStatus.Cancelled)
        {
            await notifications.PublishAsync(new NotificationCreateRequest(
                NotificationEvents.AppointmentCancelled,
                "Randevu iptal edildi",
                $"{dto.PatientName ?? dto.Title ?? "Randevu"} — " +
                $"{(dto.StartUtc + TrTime.Offset):dd.MM.yyyy HH:mm}" +
                (string.IsNullOrWhiteSpace(request.CancelReason) ? "" : $" ({request.CancelReason})"),
                LinkPath: $"/appointments?id={dto.Id}"), ct);
        }
        return dto;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var appointment = await FindAsync(id, ct);
        db.Appointments.Remove(appointment); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorWorkingHourDto>> GetWorkingHoursAsync(long? doctorUserId, CancellationToken ct = default)
    {
        return await db.DoctorWorkingHours.AsNoTracking()
            .Where(w => doctorUserId == null || w.DoctorUserId == doctorUserId)
            .OrderBy(w => w.DoctorUserId).ThenBy(w => w.DayOfWeek).ThenBy(w => w.StartTime)
            .Select(w => new DoctorWorkingHourDto(w.Id, w.DoctorUserId, w.ClinicId, w.DayOfWeek, w.StartTime, w.EndTime))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorWorkingHourDto>> SaveWorkingHoursAsync(WorkingHoursSaveRequest request, CancellationToken ct = default)
    {
        await workingHoursValidator.ValidateAndThrowAsync(request, ct);

        var existing = await db.DoctorWorkingHours
            .Where(w => w.DoctorUserId == request.DoctorUserId)
            .ToListAsync(ct);
        db.DoctorWorkingHours.RemoveRange(existing);
        foreach (var item in request.Items)
        {
            db.DoctorWorkingHours.Add(new DoctorWorkingHour
            {
                DoctorUserId = request.DoctorUserId,
                ClinicId = item.ClinicId,
                DayOfWeek = item.DayOfWeek,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
            });
        }
        await db.SaveChangesAsync(ct);
        return await GetWorkingHoursAsync(request.DoctorUserId, ct);
    }

    private async Task<Appointment> FindAsync(long id, CancellationToken ct) =>
        await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Randevu bulunamadı.");

    private async Task EnsurePatientExistsAsync(long? patientId, CancellationToken ct)
    {
        if (patientId is { } id && !await db.Patients.AnyAsync(p => p.Id == id, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
    }

    /// <summary>
    /// İstekten gelen klinik ve hekim kimliklerinin bu kiracıya ait olduğunu doğrular.
    /// Clinic global filtreye tabi; AppUser Identity tablosu olduğundan TenantId elle süzülür
    /// (inceleme bulgusu: yabancı kiracının hekim/klinik kimliği enjekte edilebiliyordu).
    /// </summary>
    private async Task EnsureTenantReferencesAsync(long clinicId, long doctorUserId, CancellationToken ct)
    {
        if (!await db.Clinics.AnyAsync(c => c.Id == clinicId, ct))
            throw new KeyNotFoundException("Klinik bulunamadı.");

        var tenantId = tenant.TenantId;
        if (!await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Id == doctorUserId && u.TenantId == tenantId && u.IsActive, ct))
            throw new KeyNotFoundException("Hekim bulunamadı.");
    }

    /// <summary>
    /// Aynı hekimde zaman kesişimi (Cancelled/NoShow hariç) randevuyu reddeder.
    /// Kontrol ile ekleme arasındaki yarışta çifte rezervasyon oluşabiliyordu (inceleme bulgusu):
    /// hekim bazında uygulama kilidi (sp_getapplock) alınarak aynı hekim için eşzamanlı
    /// yazmalar sıraya sokulur; kilit transaction sonunda otomatik bırakılır.
    /// </summary>
    private async Task EnsureNoOverlapAsync(long doctorUserId, DateTime startUtc, DateTime endUtc, long? excludeId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            var lockName = $"appointment-doctor-{tenant.TenantId}-{doctorUserId}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_getapplock @Resource = {lockName}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                ct);
        }

        var overlaps = await db.Appointments.AnyAsync(a =>
            a.DoctorUserId == doctorUserId
            && (excludeId == null || a.Id != excludeId)
            && a.Status != AppointmentStatus.Cancelled
            && a.Status != AppointmentStatus.NoShow
            && a.StartUtc < endUtc && a.EndUtc > startUtc, ct);
        if (overlaps)
            throw new InvalidOperationException("Hekimin bu saat aralığında başka bir randevusu var.");
    }

    private IQueryable<AppointmentRow> ProjectRows(IQueryable<Appointment> source) =>
        from a in source
        join p in db.Patients on a.PatientId equals p.Id into pj
        from p in pj.DefaultIfEmpty()
        // Kullanıcı tablosu global filtreye tabi değil; hekim adı yalnız kendi kiracımızdan gelsin.
        join u in db.Users.Where(x => x.TenantId == tenant.TenantId) on a.DoctorUserId equals u.Id into uj
        from u in uj.DefaultIfEmpty()
        select new AppointmentRow(a,
            p != null ? p.FirstName + " " + p.LastName : null,
            u != null ? u.FirstName + " " + u.LastName : "");

    private sealed record AppointmentRow(Appointment Appointment, string? PatientName, string DoctorName)
    {
        public AppointmentDto ToDto() => new(
            Appointment.Id, Appointment.ClinicId, Appointment.PatientId, PatientName,
            Appointment.DoctorUserId, DoctorName, Appointment.StartUtc, Appointment.EndUtc,
            Appointment.Type, Appointment.Status, Appointment.Title, Appointment.Note, Appointment.Color,
            Appointment.SourceTreatmentRecordId, Appointment.ReminderState, Appointment.CancelReason,
            Convert.ToBase64String(Appointment.RowVersion));
    }
}
