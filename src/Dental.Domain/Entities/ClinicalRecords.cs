using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>Anamnez form şablonu. Varsayılan şablon yeni kiracı açılışında seed'den kopyalanır.</summary>
public class AnamnesisTemplate : TenantEntity
{
    public required string Name { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<AnamnesisQuestion> Questions { get; set; } = [];
}

/// <summary>Anamnez sorusu. IsCritical işaretli sorulara verilen olumlu yanıt hasta kartında kırmızı rozet üretir.</summary>
public class AnamnesisQuestion : TenantEntity
{
    public long TemplateId { get; set; }
    public int SortOrder { get; set; }
    public required string QuestionText { get; set; }
    public string? QuestionTextEn { get; set; }
    public AnamnesisAnswerType AnswerType { get; set; } = AnamnesisAnswerType.YesNo;
    /// <summary>MultiSelect seçenekleri (JSON dizi).</summary>
    public string? OptionsJson { get; set; }
    /// <summary>Alerji/kalp gibi kritik soru — olumlu yanıt hasta başlığında kırmızı rozet.</summary>
    public bool IsCritical { get; set; }

    public AnamnesisTemplate? Template { get; set; }
}

/// <summary>
/// Doldurulmuş anamnez formu (versiyonlu: her doldurma yeni Response'tur, eski yanıtlar
/// üzerine yazılmaz — klinik geçmiş kanıt olarak korunur).
/// </summary>
public class AnamnesisResponse : TenantEntity
{
    public long PatientId { get; set; }
    public long TemplateId { get; set; }
    public long FilledByUserId { get; set; }
    public DateTime FilledAtUtc { get; set; }

    public ICollection<AnamnesisAnswer> Answers { get; set; } = [];
}

/// <summary>Tek soru yanıtı. YesNo* için BoolValue, Text/MultiSelect ve YesNoDetail açıklaması için TextValue.</summary>
public class AnamnesisAnswer : TenantEntity
{
    public long ResponseId { get; set; }
    public long QuestionId { get; set; }
    public bool? BoolValue { get; set; }
    public string? TextValue { get; set; }

    public AnamnesisResponse? Response { get; set; }
}

/// <summary>
/// Hasta notu. Yalnız yazar veya Owner/Manager düzenler-siler (servis kuralı);
/// IsPinned notlar listede üstte gösterilir.
/// </summary>
public class PatientNote : TenantEntity
{
    public long PatientId { get; set; }
    public long AuthorUserId { get; set; }
    public required string NoteText { get; set; }
    public bool IsPinned { get; set; }
    public string? ColorHex { get; set; }
}
