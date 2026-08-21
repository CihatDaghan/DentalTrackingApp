using Dental.Domain.Enums;

namespace Dental.Application.Enabiz;

public sealed record EnabizSubmissionListItemDto(
    long Id,
    EnabizPacketType PacketType,
    EnabizSubmissionState State,
    long? VisitId,
    string? ProtocolNo,
    long? PatientId,
    string? PatientName,
    string? SysTakipNo,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    DateTime? SentAtUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime CreatedAtUtc);

/// <summary>Detay — ham paket XML'i dahil (destek/denetim ekranı).</summary>
public sealed record EnabizSubmissionDto(
    long Id,
    EnabizPacketType PacketType,
    EnabizSubmissionState State,
    long? VisitId,
    string? ProtocolNo,
    long? PatientId,
    string? PatientName,
    long? TreatmentRecordId,
    long? PrescriptionId,
    string? FacilityCode,
    string? SysTakipNo,
    long? DependsOnSubmissionId,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    DateTime? SentAtUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    EnabizPhysicianSignState PhysicianSignState,
    bool RegenerateOnSend,
    string? PayloadXml,
    DateTime CreatedAtUtc);

/// <summary>
/// Dashboard/ayarlar özeti. <paramref name="KtsRegistered"/> sistem düzeyi bayraktır —
/// kapalıyken <see cref="EnabizMode.Live"/> seçilemez.
/// </summary>
public sealed record EnabizStatusDto(
    EnabizMode Mode,
    bool KtsRegistered,
    bool CanGoLive,
    string? CkysCode,
    bool HasCredentials,
    int HeldCount,
    int QueuedCount,
    int SendingCount,
    int AcceptedCount,
    int RejectedCount,
    int ManualReviewCount,
    DateTime? LastSkrsSyncAtUtc,
    DateTime? LastSentAtUtc);

/// <param name="UssPassword">Boş bırakılırsa mevcut şifre korunur (ekranda geri gösterilmez).</param>
public sealed record EnabizSettingsRequest(
    EnabizMode Mode,
    string? CkysCode,
    string? UssUsername,
    string? UssPassword,
    string? ApplicationCode);

public sealed record EnabizSettingsDto(
    EnabizMode Mode,
    string? CkysCode,
    string? UssUsername,
    string? ApplicationCode,
    bool HasPassword,
    bool KtsRegistered,
    bool CanGoLive);

public sealed record SkrsCodeDto(
    string Code,
    string Name,
    string? ParentCode,
    bool IsActive,
    string SystemName);

/// <summary>Bir ziyaret için kuyruğa alınan paketlerin özeti (tetikleme sonucu).</summary>
public sealed record EnabizQueueResultDto(
    long VisitId,
    string ProtocolNo,
    IReadOnlyList<long> SubmissionIds,
    EnabizMode Mode);
