namespace Dental.Infrastructure.Messaging;

/// <summary>
/// Meta webhook doğrulama ayarları (Integrations:WhatsApp bölümü).
/// App secret ve verify token UYGULAMA düzeyindedir (kiracı başına değil): tek webhook
/// adresine gelen olaylar imzayla doğrulanır, kiracı çözümlemesi mesaj kimliğinden yapılır.
/// </summary>
public sealed class WhatsAppWebhookOptions
{
    public const string SectionName = "Integrations:WhatsApp";

    /// <summary>GET doğrulamasında beklenen hub.verify_token.</summary>
    public string VerifyToken { get; set; } = "";

    /// <summary>X-Hub-Signature-256 HMAC-SHA256 anahtarı (Meta uygulama sırrı).</summary>
    public string AppSecret { get; set; } = "";
}
