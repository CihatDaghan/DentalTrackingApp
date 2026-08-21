namespace Dental.Application.Abstractions;

/// <param name="To">Alıcı numara (örn. 905XXXXXXXXX).</param>
/// <param name="TemplateName">Meta tarafında onaylı şablon adı.</param>
/// <param name="Language">Şablon dili (örn. "tr").</param>
/// <param name="BodyParams">Gövde değişkenleri; sırasıyla {{1}}, {{2}}... yerine geçer.</param>
public sealed record WaTemplateMessage(
    string To,
    string TemplateName,
    string Language,
    IReadOnlyList<string> BodyParams);

public sealed record WaSendResult(
    bool Success,
    string? ProviderMessageId = null,
    string? Error = null);

/// <summary>
/// WhatsApp gönderim portu. Sürücüler geçici hatalarda retry YAPMAZ; retry/outbox üst katmanın işidir.
/// Sağlayıcının iş reddi (onaysız şablon, geçersiz numara vb.) <see cref="WaSendResult.Error"/> ile döner;
/// taşıma/protokol hatası <see cref="WhatsAppProviderException"/> fırlatır.
/// </summary>
public interface IWhatsAppProvider
{
    Task<WaSendResult> SendTemplateAsync(WaTemplateMessage message, CancellationToken ct = default);
}

public sealed class WhatsAppProviderException : Exception
{
    public WhatsAppProviderException(string message) : base(message) { }
    public WhatsAppProviderException(string message, Exception innerException) : base(message, innerException) { }
}
