namespace Dental.Application.Abstractions;

/// <summary>
/// TCKN saklama stratejisi (denetim (a)-9 kararı): Data Protection ile şifreleme (geri çözülebilir)
/// + anahtarlı HMAC-SHA256 özeti — tekillik indeksi ve eşitlik araması özet üzerinden çalışır.
/// </summary>
public interface ITcknProtector
{
    (string Encrypted, string Hash) Protect(string tckn);

    /// <summary>
    /// Şifreyi çözer. Anahtar halkası değiştiğinde/kaybolduğunda (taşıma, rotasyon, yedekten dönüş)
    /// çözme başarısız olabilir; bu durumda <c>null</c> döner — kaydın tamamı erişilemez hale gelmemeli.
    /// </summary>
    string? Unprotect(string encrypted);
    /// <summary>Yalnız arama için deterministik özet (hex, 64 karakter).</summary>
    string Hash(string tckn);
}
