using Dental.Domain.Enums;

namespace Dental.Application.Treatments;

public sealed record TreatmentCategoryDto(
    long Id, string Name, string? NameEn, string? ColorHex, int SortOrder, int TreatmentCount);

public sealed record TreatmentCategoryUpsertRequest(
    string Name, string? NameEn = null, string? ColorHex = null, int SortOrder = 0);

public sealed record TreatmentDefinitionDto(
    long Id,
    long CategoryId,
    string CategoryName,
    string? CategoryColorHex,
    string Code,
    string Name,
    string? NameEn,
    string? SutCode,
    decimal DefaultPrice,
    decimal VatRate,
    ToothScope ToothScope,
    bool RequiresSurface,
    ToothStatusEffect ToothStatusEffect,
    int? DefaultDurationMinutes,
    bool IsActive);

public sealed record TreatmentDefinitionUpsertRequest(
    long CategoryId,
    string Code,
    string Name,
    decimal DefaultPrice,
    string? NameEn = null,
    string? SutCode = null,
    decimal VatRate = 10m,
    ToothScope ToothScope = ToothScope.PerTooth,
    bool RequiresSurface = false,
    ToothStatusEffect ToothStatusEffect = ToothStatusEffect.None,
    int? DefaultDurationMinutes = null,
    bool IsActive = true);

public sealed record TreatmentCatalogQuery(
    string? Search = null,
    long? CategoryId = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 25);

public sealed record PriceListDto(
    long Id, string Name, string CurrencyCode, DateOnly? ValidFrom, bool IsDefault, int ItemCount);

public sealed record PriceListUpsertRequest(
    string Name, string CurrencyCode = "TRY", DateOnly? ValidFrom = null, bool IsDefault = false);

public sealed record PriceListItemDto(
    long TreatmentDefinitionId, string TreatmentCode, string TreatmentName, decimal Price);

public sealed record PriceListItemSaveDto(long TreatmentDefinitionId, decimal Price);

/// <summary>Tarife kalemlerinin toplu güncellemesi (upsert; listede olmayan mevcut kalemler korunur).</summary>
public sealed record PriceListItemsSaveRequest(IReadOnlyList<PriceListItemSaveDto> Items);

public sealed record IcdCodeDto(long Id, string Code, string Name, string? NameEn);
