using System.Security.Cryptography;
using System.Text;
using Dental.Application.Abstractions;

namespace Dental.UnitTests.Integrations;

public sealed class WaWebhookParserTests
{
    private const string StatusUpdateJson = """
    {
      "object": "whatsapp_business_account",
      "entry": [{
        "id": "102290129340398",
        "changes": [{
          "value": {
            "messaging_product": "whatsapp",
            "metadata": { "display_phone_number": "15550123456", "phone_number_id": "111222333" },
            "statuses": [{
              "id": "wamid.HBgMOTA1NTUxMTEyMjMz",
              "status": "delivered",
              "timestamp": "1755690000",
              "recipient_id": "905551112233",
              "conversation": { "id": "conv1", "origin": { "type": "utility" } },
              "pricing": { "billable": true, "pricing_model": "PMP", "category": "utility" }
            }]
          },
          "field": "messages"
        }]
      }]
    }
    """;

    private const string FailedStatusJson = """
    {
      "object": "whatsapp_business_account",
      "entry": [{
        "id": "102290129340398",
        "changes": [{
          "value": {
            "messaging_product": "whatsapp",
            "metadata": { "display_phone_number": "15550123456", "phone_number_id": "111222333" },
            "statuses": [{
              "id": "wamid.FAILED1",
              "status": "failed",
              "timestamp": "1755690100",
              "recipient_id": "905559998877",
              "errors": [{
                "code": 131026,
                "title": "Message undeliverable",
                "message": "Message undeliverable: recipient not on WhatsApp"
              }]
            }]
          },
          "field": "messages"
        }]
      }]
    }
    """;

    private const string IncomingMessageJson = """
    {
      "object": "whatsapp_business_account",
      "entry": [{
        "id": "102290129340398",
        "changes": [{
          "value": {
            "messaging_product": "whatsapp",
            "metadata": { "display_phone_number": "15550123456", "phone_number_id": "111222333" },
            "contacts": [{ "profile": { "name": "Ayşe Yılmaz" }, "wa_id": "905551112233" }],
            "messages": [{
              "from": "905551112233",
              "id": "wamid.INBOUND1",
              "timestamp": "1755690200",
              "text": { "body": "Randevumu onaylıyorum" },
              "type": "text"
            }]
          },
          "field": "messages"
        }]
      }]
    }
    """;

    [Fact]
    public void Parse_StatusUpdate_ExtractsFields()
    {
        var evt = WaWebhookParser.Parse(StatusUpdateJson);

        Assert.Equal("111222333", evt.PhoneNumberId);
        Assert.Empty(evt.IncomingMessages);
        var status = Assert.Single(evt.StatusUpdates);
        Assert.Equal("wamid.HBgMOTA1NTUxMTEyMjMz", status.ProviderMessageId);
        Assert.Equal("delivered", status.Status);
        Assert.Equal("905551112233", status.RecipientId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1755690000).UtcDateTime, status.TimestampUtc);
        Assert.Null(status.ErrorCode);
        Assert.Null(status.ErrorDetail);
    }

    [Fact]
    public void Parse_FailedStatus_ExtractsErrorInfo()
    {
        var evt = WaWebhookParser.Parse(FailedStatusJson);

        var status = Assert.Single(evt.StatusUpdates);
        Assert.Equal("failed", status.Status);
        Assert.Equal("131026", status.ErrorCode);
        Assert.Contains("recipient not on WhatsApp", status.ErrorDetail);
    }

    [Fact]
    public void Parse_IncomingMessage_ExtractsFields()
    {
        var evt = WaWebhookParser.Parse(IncomingMessageJson);

        Assert.Equal("111222333", evt.PhoneNumberId);
        Assert.Empty(evt.StatusUpdates);
        var msg = Assert.Single(evt.IncomingMessages);
        Assert.Equal("wamid.INBOUND1", msg.ProviderMessageId);
        Assert.Equal("905551112233", msg.From);
        Assert.Equal("text", msg.Type);
        Assert.Equal("Randevumu onaylıyorum", msg.Text);
        Assert.Equal("Ayşe Yılmaz", msg.SenderName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1755690200).UtcDateTime, msg.TimestampUtc);
    }

    [Fact]
    public void Parse_EmptyEntry_ReturnsEmptyEvent()
    {
        var evt = WaWebhookParser.Parse("""{ "object": "whatsapp_business_account" }""");

        Assert.Null(evt.PhoneNumberId);
        Assert.Empty(evt.StatusUpdates);
        Assert.Empty(evt.IncomingMessages);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsWhatsAppProviderException()
    {
        Assert.Throws<WhatsAppProviderException>(() => WaWebhookParser.Parse("bu json degil"));
    }

    [Fact]
    public void VerifySignature_CorrectSignature_ReturnsTrue()
    {
        const string appSecret = "cok-gizli-app-secret";
        var payload = Encoding.UTF8.GetBytes(IncomingMessageJson);
        var signature = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), payload));

        Assert.True(WaWebhookParser.VerifySignature(payload, signature, appSecret));
    }

    [Fact]
    public void VerifySignature_WrongSecretOrTamperedPayload_ReturnsFalse()
    {
        const string appSecret = "cok-gizli-app-secret";
        var payload = Encoding.UTF8.GetBytes(IncomingMessageJson);
        var signature = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), payload));

        Assert.False(WaWebhookParser.VerifySignature(payload, signature, "yanlis-secret"));

        var tampered = Encoding.UTF8.GetBytes(IncomingMessageJson + " ");
        Assert.False(WaWebhookParser.VerifySignature(tampered, signature, appSecret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=zzzz-gecersiz-hex")]
    [InlineData("md5=abcdef")]
    public void VerifySignature_MalformedHeader_ReturnsFalse(string? header)
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        Assert.False(WaWebhookParser.VerifySignature(payload, header, "secret"));
    }
}
