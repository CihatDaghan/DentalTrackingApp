using System.Text.Json.Nodes;
using Dental.Application.Abstractions;
using Dental.Integrations.WhatsApp.MetaCloud;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dental.UnitTests.Integrations;

public sealed class MetaWhatsAppProviderTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly HttpClient _http = new();

    private MetaWhatsAppProvider CreateProvider() => new(
        _http,
        new MetaWhatsAppSettings
        {
            AccessToken = "test-token",
            PhoneNumberId = "111222333",
            AppSecret = "app-secret",
            GraphApiBase = _server.Url! + "/v21.0",
        },
        NullLogger<MetaWhatsAppProvider>.Instance);

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _http.Dispose();
    }

    [Fact]
    public async Task SendTemplateAsync_PostsExpectedTemplatePayload()
    {
        _server.Given(Request.Create().WithPath("/v21.0/111222333/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                messaging_product = "whatsapp",
                contacts = new[] { new { input = "905551112233", wa_id = "905551112233" } },
                messages = new[] { new { id = "wamid.HBgMOTA1NTU=" } },
            }));

        var result = await CreateProvider().SendTemplateAsync(new WaTemplateMessage(
            To: "905551112233",
            TemplateName: "randevu_hatirlatma",
            Language: "tr",
            BodyParams: ["Ayşe Yılmaz", "21.08.2026 14:00"]));

        Assert.True(result.Success);
        Assert.Equal("wamid.HBgMOTA1NTU=", result.ProviderMessageId);

        var entry = Assert.Single(_server.LogEntries);
        Assert.Equal("Bearer test-token", entry.RequestMessage!.Headers!["Authorization"].Single());

        var body = JsonNode.Parse(entry.RequestMessage!.Body!)!.AsObject();
        Assert.Equal("whatsapp", (string?)body["messaging_product"]);
        Assert.Equal("905551112233", (string?)body["to"]);
        Assert.Equal("template", (string?)body["type"]);

        var template = body["template"]!.AsObject();
        Assert.Equal("randevu_hatirlatma", (string?)template["name"]);
        Assert.Equal("tr", (string?)template["language"]!["code"]);

        var component = template["components"]![0]!.AsObject();
        Assert.Equal("body", (string?)component["type"]);
        var parameters = component["parameters"]!.AsArray();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("text", (string?)parameters[0]!["type"]);
        Assert.Equal("Ayşe Yılmaz", (string?)parameters[0]!["text"]);
        Assert.Equal("21.08.2026 14:00", (string?)parameters[1]!["text"]);
    }

    [Fact]
    public async Task SendTemplateAsync_NoBodyParams_OmitsComponents()
    {
        _server.Given(Request.Create().WithPath("/v21.0/111222333/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { messages = new[] { new { id = "wamid.X" } } }));

        var result = await CreateProvider().SendTemplateAsync(
            new WaTemplateMessage("905551112233", "hos_geldiniz", "tr", []));

        Assert.True(result.Success);
        var body = JsonNode.Parse(Assert.Single(_server.LogEntries).RequestMessage!.Body!)!.AsObject();
        Assert.False(body["template"]!.AsObject().ContainsKey("components"));
    }

    [Fact]
    public async Task SendTemplateAsync_GraphError_IsParsedIntoResult()
    {
        _server.Given(Request.Create().WithPath("/v21.0/111222333/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400).WithBodyAsJson(new
            {
                error = new
                {
                    message = "Template name does not exist in the translation",
                    type = "OAuthException",
                    code = 132001,
                    error_subcode = 2593006,
                    fbtrace_id = "AbC123",
                },
            }));

        var result = await CreateProvider().SendTemplateAsync(
            new WaTemplateMessage("905551112233", "olmayan_sablon", "tr", ["x"]));

        Assert.False(result.Success);
        Assert.Null(result.ProviderMessageId);
        Assert.Contains("132001", result.Error);
        Assert.Contains("Template name does not exist", result.Error);
        Assert.Contains("AbC123", result.Error);
    }

    [Fact]
    public async Task SendTemplateAsync_Timeout_ThrowsWhatsAppProviderException()
    {
        _server.Given(Request.Create().WithPath("/v21.0/111222333/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { messages = new[] { new { id = "wamid.X" } } })
                .WithDelay(TimeSpan.FromSeconds(5)));

        using var slowHttp = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
        var provider = new MetaWhatsAppProvider(
            slowHttp,
            new MetaWhatsAppSettings { AccessToken = "t", PhoneNumberId = "111222333", GraphApiBase = _server.Url! + "/v21.0" },
            NullLogger<MetaWhatsAppProvider>.Instance);

        await Assert.ThrowsAsync<WhatsAppProviderException>(
            () => provider.SendTemplateAsync(new WaTemplateMessage("905551112233", "t", "tr", [])));
    }
}
