using System.Net.Http.Json;
using Dental.Application.Auth;
using Microsoft.Extensions.Options;

namespace Dental.Infrastructure.Auth;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";
    /// <summary>Boşsa doğrulama atlanır (dev modu).</summary>
    public string SecretKey { get; set; } = "";
    public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}

public sealed class TurnstileVerifier(IHttpClientFactory httpClientFactory, IOptions<TurnstileOptions> options) : ITurnstileVerifier
{
    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.SecretKey)) return true; // dev bypass

        if (string.IsNullOrWhiteSpace(token)) return false;
        var client = httpClientFactory.CreateClient(nameof(TurnstileVerifier));
        using var response = await client.PostAsync(opt.VerifyUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = opt.SecretKey,
            ["response"] = token,
            ["remoteip"] = remoteIp ?? "",
        }), ct);
        if (!response.IsSuccessStatusCode) return false;
        var payload = await response.Content.ReadFromJsonAsync<TurnstileResponse>(ct);
        return payload?.Success == true;
    }

    private sealed record TurnstileResponse(bool Success);
}
