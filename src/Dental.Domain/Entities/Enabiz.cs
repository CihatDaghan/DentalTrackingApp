using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// USS/e-Nabız gönderim kuyruğu satırı — bir ziyaret için üretilmiş tek bir veri paketi.
///
/// <para><b>PayloadXml neden nvarchar(max), MediaFile değil:</b> e-Nabız paketleri e-belge UBL'inden
/// çok küçüktür (tipik 1-4 KB; gömülü base64 XSLT gibi bir yükü yoktur). Buna karşılık paketlere
/// e-belgeden çok daha sık dokunulur: <see cref="RegenerateOnSend"/> ile her gönderimde güncel SKRS
/// koduyla yeniden üretilir, Held→Queued geri doldurmada toplu işlenir ve destek ekranında ham XML
/// gösterilir. Bunları blob deposundan okumak her gönderime bir I/O turu ekler ve toplu geri
/// doldurmayı N ayrı indirmeye çevirir. nvarchar(max) satır içi 8 KB'a kadar tutar, taşarsa
/// otomatik LOB'a gider — yani küçük paketler için ek okuma maliyeti yoktur.</para>
/// </summary>
public class EnabizSubmission : TenantEntity
{
    public long ClinicId { get; set; }
    /// <summary>ÇKYS tesis kodu — gönderim anındaki değer sabitlenir (klinik kodu sonradan değişse bile iz kalır).</summary>
    public string? FacilityCode { get; set; }
    public EnabizPacketType PacketType { get; set; }

    public long? VisitId { get; set; }
    public long? TreatmentRecordId { get; set; }
    public long? PrescriptionId { get; set; }

    /// <summary>Üretilmiş paket XML'i (SYSSendMessage input gövdesi).</summary>
    public string? PayloadXml { get; set; }

    public EnabizSubmissionState State { get; set; } = EnabizSubmissionState.Draft;

    /// <summary>USS'nin pakete atadığı sistem takip numarası (101 yanıtından gelir).</summary>
    public string? SysTakipNo { get; set; }

    /// <summary>
    /// Bağımlılık: bu paket, işaret edilen paket <see cref="EnabizSubmissionState.Accepted"/> olup
    /// SysTakipNo almadan gönderilemez. Ziyaret bazında 101 → (102/103/203) sırasını kurar.
    /// </summary>
    public long? DependsOnSubmissionId { get; set; }

    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }

    public EnabizPhysicianSignState PhysicianSignState { get; set; } = EnabizPhysicianSignState.NotRequired;
    public DateTime? SentAtUtc { get; set; }

    /// <summary>
    /// Gönderim anında paketin domain'den yeniden üretilmesi (varsayılan açık). Held modunda aylarca
    /// bekleyen bir paket, gönderileceği anda güncel SKRS kod setiyle yeniden üretilsin diye.
    /// </summary>
    public bool RegenerateOnSend { get; set; } = true;
}

/// <summary>
/// SKRS kod sistemi (ICD-10, SUT, uyruk...). GLOBAL — kiracıya ait değildir, bilerek
/// <see cref="ITenantOwned"/> uygulamaz: kod setleri Bakanlık referansıdır, tüm kiracılar aynısını görür.
/// </summary>
public class SkrsCodeSystem : BaseEntity
{
    /// <summary>SKRS'nin kod sistemi GUID'i (skrsCodeSystemGuid) — servis çağrılarının anahtarı.</summary>
    public Guid CodeSystemGuid { get; set; }
    public required string Name { get; set; }
    public string? Version { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }
    public SkrsSource Source { get; set; } = SkrsSource.Seed;

    public ICollection<SkrsCode> Codes { get; set; } = [];
}

/// <summary>SKRS kod satırı (global). Hiyerarşik listelerde <see cref="ParentCode"/> üst kodu gösterir.</summary>
public class SkrsCode : BaseEntity
{
    public Guid CodeSystemGuid { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? ParentCode { get; set; }
    public bool IsActive { get; set; } = true;

    public SkrsCodeSystem? CodeSystem { get; set; }
}
