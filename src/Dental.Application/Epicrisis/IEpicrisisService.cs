using Dental.Application.Media;

namespace Dental.Application.Epicrisis;

public interface IEpicrisisService
{
    /// <summary>
    /// Oluşturur: verilen tedavi id'lerinin özetleri snapshot olarak çekilir, tanılar JSON'a sabitlenir.
    /// Hekim = UserType.Dentist zorunlu.
    /// </summary>
    Task<EpicrisisDto> CreateAsync(long patientId, EpicrisisCreateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EpicrisisDto>> ListForPatientAsync(long patientId, CancellationToken ct = default);
    Task<EpicrisisDto> GetAsync(long id, CancellationToken ct = default);
    /// <summary>Antetli A4 PDF: ilk istekte üretilip MediaFile'a yazılır, sonraki isteklerde arşivden akar.</summary>
    Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default);
}
