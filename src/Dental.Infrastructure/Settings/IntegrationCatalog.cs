namespace Dental.Infrastructure.Settings;

/// <param name="IsSecret">Sır alanı: yanıtta maskeli döner, boş/maskeli gelirse mevcut değer korunur.</param>
public sealed record IntegrationFieldSpec(string Name, bool IsSecret = false);

/// <summary>
/// Entegrasyon ayar şeması — hangi sağlayıcının hangi alanları var ve hangileri sırdır.
/// Sürücü ayar sınıflarının (UyumsoftSettings, NetgsmSettings...) JSON şemasıyla birebir eşleşir.
/// </summary>
public static class IntegrationCatalog
{
    public const string EInvoice = "EInvoice";
    public const string Sms = "Sms";
    public const string WhatsApp = "WhatsApp";
    public const string Payment = "Payment";
    public const string Enabiz = "Enabiz";

    public static readonly IReadOnlyList<string> Keys = [EInvoice, Sms, WhatsApp, Payment, Enabiz];

    public static readonly IReadOnlyDictionary<string, string[]> Providers =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [EInvoice] = ["uyumsoft", "nes", "fake"],
            [Sms] = ["netgsm", "fake"],
            [WhatsApp] = ["meta", "fake"],
            [Payment] = ["iyzico", "fake"],
            [Enabiz] = ["sys", "fake"],
        };

    private static readonly IReadOnlyDictionary<string, IntegrationFieldSpec[]> FieldsByProvider =
        new Dictionary<string, IntegrationFieldSpec[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["EInvoice:uyumsoft"] =
            [
                new("Username"), new("Password", IsSecret: true),
                new("TestUrl"), new("LiveUrl"), new("SmmUrl"),
            ],
            ["EInvoice:nes"] =
            [
                new("Username"), new("Password", IsSecret: true),
                new("TestUrl"), new("LiveUrl"), new("SenderVknTckn"),
            ],
            ["Sms:netgsm"] =
            [
                new("UserCode"), new("Password", IsSecret: true), new("MsgHeader"), new("BaseUrl"),
            ],
            ["WhatsApp:meta"] =
            [
                new("AccessToken", IsSecret: true), new("PhoneNumberId"),
                new("AppSecret", IsSecret: true), new("GraphApiBase"),
            ],
            ["Payment:iyzico"] =
            [
                new("ApiKey", IsSecret: true), new("SecretKey", IsSecret: true), new("BaseUrl"),
            ],
        };

    /// <summary>
    /// e-Nabız alanları sürücüden BAĞIMSIZDIR: mod ve ÇKYS kodu sahte sürücüde de anlamlıdır
    /// (Held modunda paket üretilir ama gönderilmez).
    /// </summary>
    private static readonly IntegrationFieldSpec[] EnabizFields =
    [
        new("CkysCode"), new("UssUsername"), new("UssPassword", IsSecret: true),
        new("ApplicationCode"), new("Mode"),
    ];

    public static IReadOnlyList<IntegrationFieldSpec> Fields(string integrationKey, string providerKey)
    {
        if (string.Equals(integrationKey, Enabiz, StringComparison.OrdinalIgnoreCase)) return EnabizFields;
        return FieldsByProvider.TryGetValue($"{integrationKey}:{providerKey}", out var fields) ? fields : [];
    }

    /// <summary>Bağlantı testinde dolu olması beklenen alanlar (sahte sürücülerde boş liste).</summary>
    public static IReadOnlyList<string> RequiredFields(string integrationKey, string providerKey) =>
        (integrationKey, providerKey.ToLowerInvariant()) switch
        {
            (EInvoice, "uyumsoft") => ["Username", "Password"],
            (EInvoice, "nes") => ["Username", "Password"],
            (Sms, "netgsm") => ["UserCode", "Password", "MsgHeader"],
            (WhatsApp, "meta") => ["AccessToken", "PhoneNumberId"],
            (Payment, "iyzico") => ["ApiKey", "SecretKey"],
            (Enabiz, "sys") => ["CkysCode", "UssUsername", "UssPassword"],
            _ => [],
        };

    public static bool IsKnownKey(string integrationKey) =>
        Keys.Contains(integrationKey, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownProvider(string integrationKey, string providerKey) =>
        Providers.TryGetValue(integrationKey, out var providers)
        && providers.Contains(providerKey, StringComparer.OrdinalIgnoreCase);
}
