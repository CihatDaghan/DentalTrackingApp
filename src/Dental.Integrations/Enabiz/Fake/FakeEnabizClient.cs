using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Integrations.Sms.Fake;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Enabiz.Fake;

/// <summary>
/// KTS tescili/USS kimliği olmadan uçtan uca akışı koşturmak için deterministik sahte sürücü.
///
/// <para>Davranış:
/// <list type="bullet">
///   <item>Paket XML'i iyi biçimli değilse reddeder — üretim hattı gerçekten doğrulansın diye.</item>
///   <item>Normalde kabul eder ve paket içeriğinden türetilmiş sahte bir SysTakipNo döner
///         (aynı girdi → aynı numara).</item>
///   <item><see cref="RejectTckn"/> TCKN'sini taşıyan paketi <b>iş reddiyle</b> geri çevirir;
///         düzeltme kuyruğu yolunun testi için.</item>
///   <item><see cref="TransientFailureTckn"/> TCKN'sini taşıyan paket <b>taşıma hatası</b> fırlatır;
///         artan aralıklı yeniden deneme ve ManualReview yolunun testi için.</item>
/// </list></para>
/// </summary>
public sealed class FakeEnabizClient(ILogger<FakeEnabizClient> logger) : IEnabizClient
{
    /// <summary>Bu TCKN'yi içeren paket iş kuralıyla reddedilir (yeniden denenmez).</summary>
    public const string RejectTckn = "11111111110";

    /// <summary>Bu TCKN'yi içeren pakette taşıma hatası simüle edilir (yeniden denenir).</summary>
    public const string TransientFailureTckn = "22222222220";

    /// <summary>
    /// TCKN'den bağımsız senaryo işaretleri: paketin herhangi bir yerinde (ör. hasta soyadında)
    /// geçmesi yeter. TCKN kiracı içinde tekil olduğu için her testin kendi hastasını
    /// oluşturabilmesi adına ad tabanlı işaret de desteklenir.
    /// </summary>
    public const string RejectMarker = "USSRED";

    public const string TransientFailureMarker = "USSGECICI";

    public Task<EnabizSendResult> SendPacketAsync(EnabizPacket packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(packet.PayloadXml))
            throw new EnabizClientException("FAKE: gönderilecek paket gövdesi boş.");

        try
        {
            XDocument.Parse(packet.PayloadXml);
        }
        catch (System.Xml.XmlException ex)
        {
            return Task.FromResult(new EnabizSendResult(false,
                ErrorCode: "XML", ErrorMessage: $"FAKE: paket XML olarak çözümlenemedi: {ex.Message}"));
        }

        if (packet.PayloadXml.Contains(TransientFailureTckn, StringComparison.Ordinal) ||
            packet.PayloadXml.Contains(TransientFailureMarker, StringComparison.Ordinal))
        {
            throw new EnabizClientException(
                "FAKE: USS servisine ulaşılamadı (simüle edilen geçici taşıma hatası).");
        }

        if (packet.PayloadXml.Contains(RejectTckn, StringComparison.Ordinal) ||
            packet.PayloadXml.Contains(RejectMarker, StringComparison.Ordinal))
        {
            logger.LogWarning("FAKE USS paketi reddetti. Paket={PacketType}", packet.PacketType);
            return Task.FromResult(new EnabizSendResult(false,
                ErrorCode: "1001",
                ErrorMessage: "FAKE: Hasta kimlik bilgisi doğrulanamadı.",
                RawResponse: "<sonuc><hataKodu>1001</hataKodu></sonuc>"));
        }

        var takipNo = FakeSmsProvider
            .DeterministicId("F", packet.PacketType.ToString(), packet.PayloadXml)
            .Replace("-", "", StringComparison.Ordinal)
            .ToUpperInvariant();

        logger.LogInformation("FAKE USS paketi kabul etti. Paket={PacketType} TakipNo={TakipNo} Tesis={Facility}",
            packet.PacketType, takipNo, packet.FacilityCode);

        return Task.FromResult(new EnabizSendResult(true, takipNo,
            RawResponse: $"<sonuc><durum>1</durum><sysTakipNo>{takipNo}</sysTakipNo></sonuc>"));
    }
}
