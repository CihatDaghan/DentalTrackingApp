using Dental.Application.Common;

namespace Dental.Application.Appointments;

public interface IRecallService
{
    Task<RecallDto> CreateAsync(RecallCreateRequest request, CancellationToken ct = default);
    Task<PagedResult<RecallDto>> ListAsync(RecallListQuery query, CancellationToken ct = default);
    Task<RecallDto> UpdateStatusAsync(long id, RecallStatusRequest request, CancellationToken ct = default);
    /// <summary>Kontrol randevusu oluşturur, AppointmentId'yi bağlar, Status=ConvertedToAppointment yapar.</summary>
    Task<RecallDto> ConvertToAppointmentAsync(long id, RecallConvertRequest request, CancellationToken ct = default);
}
