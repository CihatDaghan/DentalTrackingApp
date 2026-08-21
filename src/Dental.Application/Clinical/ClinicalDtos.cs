using Dental.Domain.Enums;

namespace Dental.Application.Clinical;

// ---- Anamnez şablonları ----

/// <summary>Id verilirse mevcut soru güncellenir; verilmezse yeni soru eklenir. İstekte olmayan sorular soft-delete edilir.</summary>
public sealed record AnamnesisQuestionUpsert(
    string QuestionText,
    AnamnesisAnswerType AnswerType,
    int SortOrder,
    bool IsCritical = false,
    string? QuestionTextEn = null,
    string? OptionsJson = null,
    long? Id = null);

public sealed record AnamnesisTemplateUpsertRequest(
    string Name,
    bool IsDefault,
    IReadOnlyList<AnamnesisQuestionUpsert> Questions);

public sealed record AnamnesisQuestionDto(
    long Id,
    int SortOrder,
    string QuestionText,
    string? QuestionTextEn,
    AnamnesisAnswerType AnswerType,
    string? OptionsJson,
    bool IsCritical);

public sealed record AnamnesisTemplateDto(
    long Id,
    string Name,
    bool IsDefault,
    IReadOnlyList<AnamnesisQuestionDto> Questions);

public sealed record AnamnesisTemplateListItemDto(long Id, string Name, bool IsDefault, int QuestionCount);

// ---- Hasta anamnez yanıtları ----

public sealed record AnamnesisAnswerInput(long QuestionId, bool? BoolValue = null, string? TextValue = null);

public sealed record AnamnesisFillRequest(long TemplateId, IReadOnlyList<AnamnesisAnswerInput> Answers);

public sealed record AnamnesisAnswerDto(
    long QuestionId,
    string QuestionText,
    AnamnesisAnswerType AnswerType,
    bool IsCritical,
    bool? BoolValue,
    string? TextValue);

public sealed record AnamnesisResponseDto(
    long Id,
    long TemplateId,
    string TemplateName,
    long FilledByUserId,
    string FilledByName,
    DateTime FilledAtUtc,
    IReadOnlyList<AnamnesisAnswerDto> Answers);

/// <summary>Hasta başlığı kırmızı rozet verisi: en son doldurmadaki olumlu kritik yanıtlar.</summary>
public sealed record CriticalFlagDto(long QuestionId, string QuestionText, bool? BoolValue, string? TextValue);

// ---- Hasta notları ----

public sealed record PatientNoteUpsertRequest(string NoteText, bool IsPinned = false, string? ColorHex = null);

public sealed record PatientNoteDto(
    long Id,
    long PatientId,
    long AuthorUserId,
    string AuthorName,
    string NoteText,
    bool IsPinned,
    string? ColorHex,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
