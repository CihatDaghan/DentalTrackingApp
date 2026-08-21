using System.Xml.Linq;

namespace Dental.Integrations.Common;

/// <summary>
/// WS-Security UsernameToken (PasswordText) başlığı — OASIS WSS 1.0.
/// Hem Uyumsoft (e-belge) hem SYS (e-Nabız) bu başlıkla kimlik doğrular.
/// </summary>
public static class WsSecurity
{
    public static readonly XNamespace Wsse =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    public const string PasswordTextType =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

    public static XElement UsernameToken(string username, string password) =>
        new(Wsse + "Security",
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse),
            new XAttribute(SoapTransport.SoapNs + "mustUnderstand", "1"),
            new XElement(Wsse + "UsernameToken",
                new XElement(Wsse + "Username", username),
                new XElement(Wsse + "Password", new XAttribute("Type", PasswordTextType), password)));
}
