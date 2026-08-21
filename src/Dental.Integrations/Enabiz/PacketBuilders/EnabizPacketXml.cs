using System.Globalization;
using System.Xml.Linq;
using Dental.Domain.Common;

namespace Dental.Integrations.Enabiz.PacketBuilders;

/// <summary>
/// SYSMessage zarfı ve öğe yazım kuralları.
///
/// <para><b>Yapı Bakanlığın resmi örnek XML'lerinden alınmıştır</b>
/// (rehber.enabiz.gov.tr/Home/PaketDetay → OrnekXML sekmesi), uydurulmamıştır:</para>
/// <code>
/// &lt;SYSMessage&gt;
///   &lt;messageGuid value="..."/&gt;
///   &lt;messageType version="1" codeSystemGuid="0a9ba485-..." code="203" value="..."/&gt;
///   &lt;documentGenerationTime value="201106240304"/&gt;
///   &lt;author&gt;&lt;healthcareProvider version="1" codeSystemGuid="c3eade04-..." code="KURUM" value="AD"/&gt;&lt;/author&gt;
///   &lt;firmaKodu value="ABCDE12345"/&gt;
///   &lt;recordData&gt; ... veri setleri ... &lt;/recordData&gt;
/// &lt;/SYSMessage&gt;
/// </code>
///
/// <para><b>Öğe yazımı iki biçimdedir:</b> SKRS'ye bağlı öğeler
/// <c>&lt;AD version="1" codeSystemGuid="..." code="..." value="..."/&gt;</c>, düz öğeler
/// <c>&lt;AD value="..."/&gt;</c>. Değer her zaman <b>nitelikte</b>dir, öğe metninde değil —
/// bu, formatın en kolay yanlış yapılan yanıdır.</para>
///
/// <para>Paket XML'i ad alanı KULLANMAZ (resmi örneklerde ad alanı bildirimi yoktur).</para>
/// </summary>
public static class EnabizPacketXml
{
    /// <summary>USVS tarih-saat biçimi: yyyyMMddHHmm (resmi örnek: <c>201106240304</c>).</summary>
    public const string DateTimeFormat = "yyyyMMddHHmm";

    /// <summary>USVS tarih biçimi: yyyyMMdd.</summary>
    public const string DateFormat = "yyyyMMdd";

    public const string CodeVersion = "1";

    public static string Format(DateTime value) =>
        value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string Format(DateOnly value) =>
        value.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Düz öğe: <c>&lt;AD value="..."/&gt;</c>.</summary>
    public static XElement Value(string name, string value) =>
        new(name, new XAttribute("value", value));

    /// <summary>Değer boşsa öğe hiç yazılmaz (USS boş niteliği hata sayabilir).</summary>
    public static XElement? Optional(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Value(name, value.Trim());

    /// <summary>SKRS'ye bağlı öğe: <c>&lt;AD version code codeSystemGuid value/&gt;</c>.</summary>
    public static XElement Coded(string name, string codeSystemGuid, string code, string? display = null) =>
        new(name,
            new XAttribute("version", CodeVersion),
            new XAttribute("codeSystemGuid", codeSystemGuid),
            new XAttribute("code", code),
            new XAttribute("value", display ?? code));

    public static XElement? OptionalCoded(string name, string codeSystemGuid, string? code, string? display = null) =>
        string.IsNullOrWhiteSpace(code) ? null : Coded(name, codeSystemGuid, code.Trim(), display);

    /// <summary>SYSMessage zarfı; <paramref name="dataSets"/> doğrudan recordData altına yazılır.</summary>
    public static XElement SysMessage(
        short packetType, string packetName, EnabizPacketContext context, params XElement[] dataSets)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dataSets);

        return new XElement("SYSMessage",
            Value("messageGuid", Guid.NewGuid().ToString("D")),
            Coded("messageType", EnabizCodeSystems.MessageType,
                packetType.ToString(CultureInfo.InvariantCulture), packetName),
            Value("documentGenerationTime", Format(context.LocalTimestamp)),
            new XElement("author",
                Coded("healthcareProvider", EnabizCodeSystems.HealthcareProvider,
                    context.FacilityCode ?? "", context.FacilityName ?? context.FacilityCode ?? "")),
            Optional("firmaKodu", context.SoftwareCompanyCode),
            new XElement("recordData", dataSets.Cast<object>().ToArray()));
    }

    /// <summary>
    /// HASTA_TAKIP_BILGISI veri seti — 101 dışındaki tüm paketlerin bağlandığı takip numarası.
    /// SysTakipNo yoksa paket üretilmemelidir; sessizce boş göndermek yerine burada durdurulur.
    /// </summary>
    public static XElement TakipBilgisi(EnabizPacketContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.SysTakipNo))
        {
            throw new EnabizPacketException(
                "Bağımlı paket SysTakipNo olmadan üretilemez; önce 101 (Hasta Kayıt) kabul edilmelidir.");
        }

        return new XElement("HASTA_TAKIP_BILGISI", Value("SYSTakipNo", context.SysTakipNo));
    }

    /// <summary>FDI diş numarasını doğrular; geçersizse üretimi durdurur.</summary>
    public static string RequireFdiTooth(string? toothNumber)
    {
        var trimmed = toothNumber?.Trim() ?? "";
        if (!FdiTeeth.IsValid(trimmed))
            throw new EnabizPacketException($"Geçersiz FDI diş numarası: '{toothNumber}'.");
        return trimmed;
    }

    /// <summary>ICD-10 biçimi: harf + 2 rakam, isteğe bağlı '.' + 1-2 karakter (K02.1, J98.9, M63.39).</summary>
    public static bool IsValidIcd10(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        System.Text.RegularExpressions.Regex.IsMatch(code.Trim(), @"^[A-TV-Z][0-9]{2}(\.[0-9A-Z]{1,2})?$");

    public static string RequireIcd10(string? code)
    {
        var trimmed = code?.Trim() ?? "";
        if (!IsValidIcd10(trimmed))
            throw new EnabizPacketException($"Geçersiz ICD-10 tanı kodu: '{code}'.");
        return trimmed;
    }
}

/// <summary>Paket üretiminde alan/biçim hatası — gönderilmeden önce yakalanır.</summary>
public sealed class EnabizPacketException : Exception
{
    public EnabizPacketException(string message) : base(message) { }
    public EnabizPacketException(string message, Exception innerException) : base(message, innerException) { }
}
