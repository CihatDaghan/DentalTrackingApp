using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Integrations.Common;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Enabiz;

/// <summary>
/// Sağlık Bakanlığı USS (Sağlık.NET SYS) veri gönderim sürücüsü.
///
/// <para><b>Sözleşme systest WSDL'inden ÇIKARILMIŞTIR</b> (varsayım değil):
/// <c>systest.sagliknet.saglik.gov.tr/SYS/SYSWS.svc?wsdl</c> → <c>?wsdl=wsdl0</c> / <c>?xsd=xsd0</c>.
/// <list type="bullet">
///   <item>Tek operasyon: <c>SYSSendMessage</c>, girdi <c>input</c> (xs:string, nillable),
///         çıktı <c>SYSSendMessageResult</c> (xs:string).</item>
///   <item>Hedef ad alanı: <c>https://sys.sagliknet.saglik.gov.tr/SYS/</c> (servis adresindeki
///         <c>ns.sagliknet…</c> ad alanı YALNIZ binding'e aittir — gövde bu ad alanını kullanmaz).</item>
///   <item>SOAPAction: <c>https://sys.sagliknet.saglik.gov.tr/SYS/ISYSWS/SYSSendMessage</c></item>
///   <item>BasicHttpBinding → SOAP 1.1, document/literal.</item>
/// </list></para>
///
/// <para><b>Kimlik doğrulama WS-Security UsernameToken başlığındadır</b> — bu, systest ortamına
/// yapılan gerçek ölçümle saptanmıştır: başlıksız istek <c>"Kullanıcı adı veya şifre tanimli değil!"</c>
/// Fault'u döndürürken, UsernameToken başlıklı istek <c>"Kullanıcı adı veya şifre yanlış!"</c>
/// döndürür. Yani kimlik gövdede değil, başlıkta taşınır.</para>
///
/// <para>Paket XML'i <c>input</c> içine kaçışlanmış (escaped) string olarak konur; XElement değeri
/// atadığımızda System.Xml kaçışlamayı kendisi yapar.</para>
///
/// <para>Retry YOKTUR — yeniden deneme EnabizDispatcher + NextAttemptAtUtc ile üst katmandadır.</para>
/// </summary>
public sealed class SysSoapClient(
    HttpClient http,
    EnabizSettings settings,
    ILogger<SysSoapClient> logger) : IEnabizClient
{
    internal static readonly XNamespace SysNs = "https://sys.sagliknet.saglik.gov.tr/SYS/";

    internal const string SendMessageAction = "https://sys.sagliknet.saglik.gov.tr/SYS/ISYSWS/SYSSendMessage";

    private readonly SoapTransport _transport = new(http);

    public async Task<EnabizSendResult> SendPacketAsync(EnabizPacket packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (string.IsNullOrWhiteSpace(packet.PayloadXml))
            throw new EnabizClientException("Gönderilecek paket gövdesi boş.");

        if (!settings.HasCredentials)
        {
            // Kimlik yoksa çağrı yapmak anlamsızdır; USS zaten Fault döner. Açık mesajla erken dur.
            throw new EnabizClientException(
                "USS kullanıcı adı/şifresi tanımlı değil; e-Nabız gönderimi yapılamaz. " +
                "Kimlik bilgileri İl Sağlık Müdürlüğü'nden alınıp e-Nabız ayarlarına girilmelidir.");
        }

        var body = new XElement(SysNs + "SYSSendMessage", BuildInput(packet.PayloadXml, settings.EmbedPayloadAsRawXml));

        XElement response;
        try
        {
            response = await _transport.SendAsync(
                new Uri(settings.EndpointUrl),
                SendMessageAction,
                body,
                [WsSecurity.UsernameToken(settings.UssUsername!, settings.UssPassword!)],
                ct).ConfigureAwait(false);
        }
        catch (SoapFaultException ex)
        {
            // SYS iş/kimlik reddini Fault olarak döner ("Kullanıcı adı veya şifre yanlış!" gibi).
            // Kimlik hataları geçici DEĞİLDİR: yeniden denemek aynı sonucu verir, elle düzeltme ister.
            logger.LogWarning("USS SYSSendMessage Fault. Paket={PacketType} Kod={Code} Mesaj={Message}",
                packet.PacketType, ex.FaultCode, ex.FaultString);
            return new EnabizSendResult(false, ErrorCode: ex.FaultCode, ErrorMessage: ex.FaultString,
                RawResponse: ex.Detail);
        }
        catch (SoapTransportException ex)
        {
            throw new EnabizClientException(
                $"USS SYSSendMessage çağrısı başarısız ({settings.EndpointUrl}): {ex.Message}", ex);
        }

        var raw = ExtractResult(response);
        var result = ParseResult(raw);
        logger.LogInformation("USS paketi gönderildi. Paket={PacketType} Kabul={Accepted} TakipNo={TakipNo}",
            packet.PacketType, result.Accepted, result.SysTakipNo);
        return result;
    }

    /// <summary>
    /// <c>input</c> öğesini kurar.
    ///
    /// <para><b>Bilinçli belirsizlik:</b> WSDL <c>input</c>'u <c>xs:string</c> olarak tanımlar
    /// (makineyle doğrulandı), yani paket XML'i KAÇIŞLANMIŞ metin olarak taşınmalıdır — varsayılan
    /// davranış budur. Buna karşılık Bakanlığın resmi örnek XML'lerinde <c>input</c> altında
    /// SYSMessage öğesi İÇ İÇE (kaçışlanmamış) gösterilir. İkisi aynı anda doğru olamaz ve hangisinin
    /// kabul edildiği, KTS tescili olmadan kimlik doğrulanmış bir çağrı yapılamadığı için
    /// ölçülememiştir (servis kimliği gövdeden ÖNCE denetliyor). Bu yüzden davranış
    /// <c>Integrations:Enabiz:EmbedPayloadAsRawXml</c> ile tek satırda değiştirilebilir; tescil
    /// sonrası ilk gerçek çağrıda hangisinin doğru olduğu görülüp bayrak sabitlenmelidir.</para>
    /// </summary>
    internal static XElement BuildInput(string payloadXml, bool raw)
    {
        var input = new XElement(SysNs + "input");
        if (raw)
        {
            // Örnek XML biçimi: paket öğesi doğrudan input'un çocuğu olur.
            input.Add(XElement.Parse(payloadXml));
        }
        else
        {
            // WSDL biçimi: paket XML'i string değeridir; System.Xml kaçışlamayı kendisi yapar.
            input.Value = payloadXml;
        }

        return input;
    }

    /// <summary>Yanıt zarfından <c>SYSSendMessageResult</c> string'ini çıkarır.</summary>
    internal static string ExtractResult(XElement response) =>
        (response.Name.LocalName == "SYSSendMessageResult"
            ? response
            : response.Descendants().FirstOrDefault(e => e.Name.LocalName == "SYSSendMessageResult"))
        ?.Value ?? "";

    /// <summary>USS başarı kodu — resmi örnek yanıtta <c>&lt;sonucKodu value="S0000"/&gt;</c>.</summary>
    internal const string SuccessCode = "S0000";

    /// <summary>
    /// SYS yanıtını çözümler.
    ///
    /// <para>Biçim Bakanlığın resmi örnek yanıtından alınmıştır:</para>
    /// <code>
    /// &lt;SYSMessage&gt;&lt;recordData&gt;&lt;KayitCevabi&gt;
    ///   &lt;sonucKodu value="S0000"/&gt;
    ///   &lt;sonucMesaji value="İşlem Başarı ile Sonuçlandı."/&gt;
    ///   &lt;SYSTakipNo value="..."/&gt;
    /// &lt;/KayitCevabi&gt;&lt;/recordData&gt;&lt;/SYSMessage&gt;
    /// </code>
    ///
    /// <para><b>Değerler nitelikte taşınır</b> (öğe metninde değil). Tanınmayan yanıt "kabul edilmedi"
    /// sayılır — sessizce başarı varsaymaktansa elle incelemeye düşmek doğrudur. Ham yanıt her
    /// hâlükârda saklanır.</para>
    /// </summary>
    internal static EnabizSendResult ParseResult(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new EnabizSendResult(false, ErrorMessage: "USS boş yanıt döndürdü.", RawResponse: raw);

        XElement root;
        try
        {
            root = XElement.Parse(raw);
        }
        catch (System.Xml.XmlException)
        {
            return new EnabizSendResult(false,
                ErrorMessage: "USS yanıtı XML olarak çözümlenemedi.", RawResponse: raw);
        }

        var resultCode = FindValue(root, "sonucKodu");
        var resultMessage = FindValue(root, "sonucMesaji");
        var takipNo = FindValue(root, "SYSTakipNo");

        // S0000 = başarı. Başka bir kod geldiyse iş reddidir.
        var accepted = string.Equals(resultCode, SuccessCode, StringComparison.OrdinalIgnoreCase);

        if (accepted)
            return new EnabizSendResult(true, takipNo, RawResponse: raw);

        return new EnabizSendResult(false,
            takipNo,
            resultCode,
            resultMessage ?? "USS paketi kabul etmedi.",
            raw);
    }

    /// <summary>
    /// Ad alanından bağımsız olarak verilen adı arar ve <c>value</c> NİTELİĞİNİ döner
    /// (USS biçiminde değer nitelikte taşınır); nitelik yoksa öğe metnine düşer.
    /// </summary>
    private static string? FindValue(XElement root, string name)
    {
        var match = root.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)
            ? root
            : root.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is null) return null;

        var value = match.Attribute("value")?.Value ?? match.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
