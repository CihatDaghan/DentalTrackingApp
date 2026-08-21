using System.Text.Json;

namespace Dental.Integrations.Common;

/// <summary>Tenant ayar JSON'u çözümleme seçenekleri (ayar anahtarları büyük/küçük harf duyarsızdır).</summary>
internal static class IntegrationSettingsJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
