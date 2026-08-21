using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>Tedavi kategorisi (TDB başlıkları). ColorHex diş şeması işaret rengidir.</summary>
public class TreatmentCategory : TenantEntity
{
    public required string Name { get; set; }
    public string? NameEn { get; set; }
    /// <summary>Diş şemasında bu kategorideki tedavi işaretlerinin rengi (#rrggbb).</summary>
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Tedavi kataloğu kalemi (TDB Asgari Ücret Tarifesi yapısında).</summary>
public class TreatmentDefinition : TenantEntity
{
    public long CategoryId { get; set; }
    /// <summary>Klinik içi kod (DIS-001 gibi); tenant içinde benzersiz (filtered unique index).</summary>
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? NameEn { get; set; }
    /// <summary>SKRS/SUT işlem kodu — e-Nabız 102 paketi eşlemesi.</summary>
    public string? SutCode { get; set; }
    public decimal DefaultPrice { get; set; }
    /// <summary>KDV oranı (%) — sağlık %10, estetik/beyazlatma %20.</summary>
    public decimal VatRate { get; set; } = 10m;
    public ToothScope ToothScope { get; set; } = ToothScope.PerTooth;
    /// <summary>Dolgu gibi yüzey seçimi zorunlu işlemler.</summary>
    public bool RequiresSurface { get; set; }
    /// <summary>Done'a geçişte ToothStatus'a uygulanan etki (çekim→Extracted vb.).</summary>
    public ToothStatusEffect ToothStatusEffect { get; set; }
    public int? DefaultDurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public TreatmentCategory? Category { get; set; }
}

/// <summary>Fiyat listesi (tarife): 'Standart Tarife 2026', 'X Sigorta 2026'... Tek IsDefault.</summary>
public class PriceList : TenantEntity
{
    public required string Name { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public DateOnly? ValidFrom { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<PriceListItem> Items { get; set; } = [];
}

/// <summary>Tarife kalemi. Kalemi olmayan tedavide DefaultPrice geçerlidir (ResolvePrice).</summary>
public class PriceListItem : TenantEntity
{
    public long PriceListId { get; set; }
    public long TreatmentDefinitionId { get; set; }
    public decimal Price { get; set; }

    public PriceList? PriceList { get; set; }
    public TreatmentDefinition? TreatmentDefinition { get; set; }
}

/// <summary>ICD-10 tanı kodu (K00–K14 diş bloğu) — tenant'sız global referans tablosu.</summary>
public class IcdCode : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? NameEn { get; set; }
}
