using Dental.Application.Common;

namespace Dental.Application.Patients;

public interface IPatientService
{
    Task<PatientDto> CreateAsync(PatientUpsertRequest request, CancellationToken ct = default);
    Task<PatientDto> UpdateAsync(long id, PatientUpsertRequest request, CancellationToken ct = default);
    Task<PatientDto> GetAsync(long id, CancellationToken ct = default);
    Task<PatientSummaryDto> GetSummaryAsync(long id, CancellationToken ct = default);
    Task<PagedResult<PatientListItemDto>> ListAsync(PatientListQuery query, CancellationToken ct = default);
    Task SoftDeleteAsync(long id, CancellationToken ct = default);
}
