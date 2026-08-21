namespace Dental.Application.Abstractions;

/// <summary>
/// USS'ye gönderilecek tek bir veri paketi.
/// </summary>
/// <param name="PacketType">Bakanlık paket numarası (101/102/103/203/200/402/405).</param>
/// <param name="PayloadXml">Paket gövdesi. SYS sözleşmesinde bu XML, tek string parametre olarak taşınır.</param>
/// <param name="FacilityCode">ÇKYS tesis kodu.</param>
/// <param name="ParentSysTakipNo">Bağımlı paketlerde 101'den dönen sistem takip numarası.</param>
public sealed record EnabizPacket(
    short PacketType,
    string PayloadXml,
    string? FacilityCode = null,
    string? ParentSysTakipNo = null);

/// <summary>
/// Gönderim sonucu. <paramref name="Accepted"/> false ise iş reddidir (yeniden denenmez);
/// taşıma hataları istisna olarak fırlatılır (<see cref="EnabizClientException"/>) ve üst katman
/// bunları artan aralıkla yeniden dener.
/// </summary>
public sealed record EnabizSendResult(
    bool Accepted,
    string? SysTakipNo = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? RawResponse = null);

/// <summary>
/// USS (Sağlık.NET SYS) veri gönderim portu.
///
/// <para>Servis sözleşmesi systest WSDL'inden doğrulanmıştır
/// (<c>systest.sagliknet.saglik.gov.tr/SYS/SYSWS.svc?wsdl</c>): tek operasyon
/// <c>SYSSendMessage(input: string) → string</c>, SOAP 1.1 document/literal, hedef ad alanı
/// <c>https://sys.sagliknet.saglik.gov.tr/SYS/</c>, SOAPAction
/// <c>https://sys.sagliknet.saglik.gov.tr/SYS/ISYSWS/SYSSendMessage</c>. Kimlik, canlı ortama
/// yapılan ölçümle WS-Security UsernameToken başlığında taşınır (gövdede değil).</para>
///
/// <para>Retry YOKTUR — yeniden deneme <c>EnabizDispatcher</c> + <c>NextAttemptAtUtc</c> ile üst katmandadır.</para>
/// </summary>
public interface IEnabizClient
{
    Task<EnabizSendResult> SendPacketAsync(EnabizPacket packet, CancellationToken ct = default);
}

/// <summary>Taşıma/altyapı hatası — geçici kabul edilir, artan aralıkla yeniden denenir.</summary>
public sealed class EnabizClientException : Exception
{
    public EnabizClientException(string message) : base(message) { }
    public EnabizClientException(string message, Exception innerException) : base(message, innerException) { }
}
