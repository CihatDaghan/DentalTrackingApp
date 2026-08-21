namespace Dental.Application.Appointments;

public interface IAppointmentService
{
    Task<AppointmentDto> CreateAsync(AppointmentUpsertRequest request, CancellationToken ct = default);
    Task<AppointmentDto> UpdateAsync(long id, AppointmentUpsertRequest request, CancellationToken ct = default);
    Task<AppointmentDto> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>> ListAsync(AppointmentListQuery query, CancellationToken ct = default);
    /// <summary>Durum geçişi; Cancelled'da CancelReason + iptal eden kullanıcı damgalanır.</summary>
    Task<AppointmentDto> UpdateStatusAsync(long id, AppointmentStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<DoctorWorkingHourDto>> GetWorkingHoursAsync(long? doctorUserId, CancellationToken ct = default);
    /// <summary>Hekimin tüm çalışma saatlerini toplu değiştirir (sil + yeniden yaz).</summary>
    Task<IReadOnlyList<DoctorWorkingHourDto>> SaveWorkingHoursAsync(WorkingHoursSaveRequest request, CancellationToken ct = default);
}
