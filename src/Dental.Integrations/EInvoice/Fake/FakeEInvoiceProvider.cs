using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Integrations.Sms.Fake;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.EInvoice.Fake;

/// <summary>
/// Entegratörsüz geliştirme/test sürücüsü. Deterministiktir: aynı ETTN aynı sahte referansı üretir,
/// gönderim her zaman başarılıdır ve durum sorgusu <see cref="EDocProviderStatus.Succeeded"/> döner.
/// UBL iyi biçimli değilse gönderim başarısız döner — testlerin üretim hattını gerçekten doğrulaması için.
/// </summary>
public sealed class FakeEInvoiceProvider(ILogger<FakeEInvoiceProvider> logger) : IEInvoiceProvider
{
    /// <summary>Sahte GİB mükellef aynası — GibTaxpayerSyncJob'ın entegratörsüz doğrulaması için.</summary>
    public static readonly IReadOnlyList<(string Vkn, string Title, string Alias)> SampleTaxpayers =
    [
        ("1234567801", "Örnek Sigorta A.Ş.", "urn:mail:defaultpk@ornek-sigorta.com.tr"),
        ("9876543210", "Anadolu Sağlık Hizmetleri Ltd. Şti.", "urn:mail:defaultpk@anadolusaglik.com.tr"),
        ("5555555555", "Kamu Hastaneleri Birliği", "urn:mail:defaultpk@khb.gov.tr"),
    ];

    public Task<EDocumentSendResult> SendDocumentAsync(EDocumentEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        try
        {
            var root = XDocument.Parse(envelope.UblXml).Root!;
            var expected = envelope.DocType == EDocType.ESmm ? "CreditNote" : "Invoice";
            if (root.Name.LocalName != expected)
            {
                return Task.FromResult(new EDocumentSendResult(false,
                    Error: $"Belge tipi uyuşmuyor: {expected} bekleniyordu, {root.Name.LocalName} geldi."));
            }
        }
        catch (System.Xml.XmlException ex)
        {
            return Task.FromResult(new EDocumentSendResult(false, Error: $"UBL çözümlenemedi: {ex.Message}"));
        }

        var reference = FakeSmsProvider.DeterministicId("fake-edoc", envelope.Ettn, envelope.DocType.ToString());
        logger.LogInformation("FAKE e-belge gönderildi. Tip={DocType} Ettn={Ettn} Ref={Ref} Alias={Alias} Mod={Mode}",
            envelope.DocType, envelope.Ettn, reference, envelope.TargetAlias, envelope.SendMode);
        return Task.FromResult(new EDocumentSendResult(true, reference));
    }

    public Task<EDocumentStatusResult> GetStatusAsync(string documentId, EDocType type, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new EDocumentStatusResult(
            EDocProviderStatus.Succeeded, "FAKE: belge GİB tarafından kabul edildi."));
    }

    public Task<byte[]> GetPdfAsync(string documentId, EDocType type, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Gerçek PDF üretmez; içerik doğrulanabilir bir yer tutucudur (%PDF başlığıyla).
        var content = $"%PDF-1.4\n% FAKE e-belge çıktısı\n% Belge: {documentId} ({type})\n";
        return Task.FromResult(Encoding.UTF8.GetBytes(content));
    }

    public Task<Stream> DownloadGibUserListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Gerçek entegratörle aynı biçim: içinde CSV bulunan zip.
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("gib-users.csv");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.WriteLine("Identifier;Title;Alias;Type");
            foreach (var (vkn, title, alias) in SampleTaxpayers)
                writer.WriteLine($"{vkn};{title};{alias};PK");
        }

        buffer.Position = 0;
        return Task.FromResult<Stream>(buffer);
    }

    public Task CancelEArchiveAsync(string documentId, string reason, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation("FAKE e-Arşiv iptali. Ref={Ref} Gerekçe={Reason}", documentId, reason);
        return Task.CompletedTask;
    }
}
