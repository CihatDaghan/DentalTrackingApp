using System.Text;
using System.Text.Json.Nodes;
using Dental.Application.Abstractions;
using Dental.Integrations.Sms.Netgsm;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dental.UnitTests.Integrations;

public sealed class NetgsmSmsProviderTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly HttpClient _http = new();

    private NetgsmSmsProvider CreateProvider() => new(
        _http,
        new NetgsmSettings { UserCode = "8503021234", Password = "gizli", MsgHeader = "KLINIK", BaseUrl = _server.Url! },
        NullLogger<NetgsmSmsProvider>.Instance);

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _http.Dispose();
    }

    [Fact]
    public async Task SendAsync_Transactional_PostsExpectedBody_WithoutIysFilter()
    {
        _server.Given(Request.Create().WithPath("/sms/rest/v2/send").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { code = "00", jobid = "18446744", description = "queued" }));

        var provider = CreateProvider();
        var result = await provider.SendAsync(new SmsMessage(
            To: "905551112233",
            Body: "Sayin hastamiz, yarin 14:00 randevunuz vardir.",
            Header: "KLINIK",
            Kind: SmsKind.Transactional,
            ClientRef: "corr-1"));

        Assert.True(result.Success);
        Assert.Equal("18446744", result.ProviderMessageId);
        Assert.Null(result.Error);

        var entry = Assert.Single(_server.LogEntries);
        var body = JsonNode.Parse(entry.RequestMessage!.Body!)!.AsObject();
        Assert.Equal("KLINIK", (string?)body["msgheader"]);
        Assert.Equal("TR", (string?)body["encoding"]);
        Assert.Equal("905551112233", (string?)body["messages"]![0]!["no"]);
        Assert.Equal("Sayin hastamiz, yarin 14:00 randevunuz vardir.", (string?)body["messages"]![0]!["msg"]);
        Assert.False(body.ContainsKey("iysfilter"));

        var auth = entry.RequestMessage!.Headers!["Authorization"].Single();
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("8503021234:gizli"));
        Assert.Equal(expected, auth);
    }

    [Fact]
    public async Task SendAsync_Commercial_IncludesIysFilter()
    {
        _server.Given(Request.Create().WithPath("/sms/rest/v2/send").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { code = "00", jobid = "99", description = "queued" }));

        var provider = CreateProvider();
        var result = await provider.SendAsync(new SmsMessage(
            To: "905551112233", Body: "Kampanya!", Header: "KLINIK", Kind: SmsKind.Commercial));

        Assert.True(result.Success);
        var body = JsonNode.Parse(Assert.Single(_server.LogEntries).RequestMessage!.Body!)!.AsObject();
        Assert.Equal("11", (string?)body["iysfilter"]);
    }

    [Theory]
    [InlineData("20", "karakter")]
    [InlineData("30", "API erişim")]
    [InlineData("40", "msgheader")]
    [InlineData("70", "eksik parametre")]
    public async Task SendAsync_KnownErrorCode_MapsToMeaningfulError(string code, string expectedFragment)
    {
        _server.Given(Request.Create().WithPath("/sms/rest/v2/send").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(406)
                .WithBodyAsJson(new { code, description = "rejected" }));

        var result = await CreateProvider().SendAsync(new SmsMessage("905551112233", "test", "KLINIK"));

        Assert.False(result.Success);
        Assert.Null(result.ProviderMessageId);
        Assert.Contains(expectedFragment, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"({code})", result.Error);
    }

    [Fact]
    public async Task SendAsync_UnknownErrorCode_ReturnsCodeInError()
    {
        _server.Given(Request.Create().WithPath("/sms/rest/v2/send").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(406)
                .WithBodyAsJson(new { code = "99", description = "bilinmeyen" }));

        var result = await CreateProvider().SendAsync(new SmsMessage("905551112233", "test", "KLINIK"));

        Assert.False(result.Success);
        Assert.Contains("99", result.Error);
    }

    [Fact]
    public async Task SendAsync_NonJsonResponse_ThrowsSmsProviderException()
    {
        _server.Given(Request.Create().WithPath("/sms/rest/v2/send").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(502).WithBody("<html>Bad Gateway</html>"));

        await Assert.ThrowsAsync<SmsProviderException>(
            () => CreateProvider().SendAsync(new SmsMessage("905551112233", "test", "KLINIK")));
    }

    [Fact]
    public async Task SendAsync_Timeout_ThrowsSmsProviderException()
    {
        _server.Given(Request.Create().WithPath("/sms/rest/v2/send").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { code = "00", jobid = "1" })
                .WithDelay(TimeSpan.FromSeconds(5)));

        using var slowHttp = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
        var provider = new NetgsmSmsProvider(
            slowHttp,
            new NetgsmSettings { UserCode = "u", Password = "p", MsgHeader = "H", BaseUrl = _server.Url! },
            NullLogger<NetgsmSmsProvider>.Instance);

        var ex = await Assert.ThrowsAsync<SmsProviderException>(
            () => provider.SendAsync(new SmsMessage("905551112233", "test", "KLINIK")));
        Assert.Contains("zaman aşımı", ex.Message);
    }

    [Fact]
    public async Task GetBalanceAsync_ParsesPlainTextBalance()
    {
        _server.Given(Request.Create().WithPath("/balance/list/get").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("00 1250,75"));

        var balance = await CreateProvider().GetBalanceAsync();

        Assert.Equal(1250.75m, balance);
    }

    [Fact]
    public async Task GetBalanceAsync_ErrorCode_ThrowsSmsProviderException()
    {
        _server.Given(Request.Create().WithPath("/balance/list/get").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("30"));

        await Assert.ThrowsAsync<SmsProviderException>(() => CreateProvider().GetBalanceAsync());
    }
}
