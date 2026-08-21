using System.Security.Cryptography;
using System.Text;
using Dental.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Security;

/// <summary>
/// TCKN koruması: Data Protection ("tckn" purpose) ile şifreleme + config anahtarlı ("Security:TcknHmacKey")
/// HMAC-SHA256 özeti. Özet deterministiktir; UQ(TenantId, TcknHash) indeksi ve arama bununla çalışır.
/// </summary>
public sealed class TcknProtector : ITcknProtector
{
    private readonly IDataProtector _protector;
    private readonly byte[] _hmacKey;
    private readonly ILogger<TcknProtector> _logger;

    public TcknProtector(IDataProtectionProvider provider, IConfiguration configuration, ILogger<TcknProtector> logger)
    {
        _logger = logger;
        _protector = provider.CreateProtector("tckn");
        var key = configuration["Security:TcknHmacKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Security:TcknHmacKey yapılandırması eksik.");
        _hmacKey = Encoding.UTF8.GetBytes(key);
    }

    public (string Encrypted, string Hash) Protect(string tckn) => (_protector.Protect(tckn), Hash(tckn));

    public string? Unprotect(string encrypted)
    {
        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch (CryptographicException ex)
        {
            // Anahtar halkası değişmiş/eksik: TCKN okunamaz ama kaydın kalanı kullanılabilir kalmalı.
            // Arama hâlâ çalışır (HMAC özeti ayrı anahtarla üretilir ve etkilenmez).
            _logger.LogWarning(ex, "Şifreli TCKN çözülemedi; Data Protection anahtar halkası değişmiş olabilir.");
            return null;
        }
    }

    public string Hash(string tckn) =>
        Convert.ToHexString(HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(tckn)));
}
