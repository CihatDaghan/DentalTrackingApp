namespace Dental.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Lokal disk kökü (dev). Göreli yol çalışma dizinine göre çözülür; üretimde S3-uyumlu depo kullanılacak.</summary>
    public string LocalRoot { get; set; } = "./data/files";

    /// <summary>Kullanıcı yüklemesi üst sınırı (varsayılan 25 MB).</summary>
    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;
}
