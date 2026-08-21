namespace Dental.Domain.Enums;

/// <summary>
/// Kiracının e-Nabız/USS gönderim modu.
///
/// <para><b>Held birinci sınıf tasarım öğesidir:</b> ürünün KTS/DHBS tescili tamamlanana kadar
/// hiçbir tesis adına canlıya veri gönderilemez (SBYS Yönetmeliği, RG 25.08.2022). Bu sürede
/// paketler yine de üretilir ve kuyrukta bekletilir; tescil gelince <c>EnabizBackfillJob</c>
/// bekleyenleri ziyaret sırasına göre gönderir. Böylece tescil öncesi dönemin klinik verisi
/// kaybolmaz.</para>
/// </summary>
public enum EnabizMode : byte
{
    /// <summary>Paket üretilmez; entegrasyon tamamen kapalı.</summary>
    Disabled = 0,
    /// <summary>Paket üretilir, <see cref="EnabizSubmissionState.Held"/> olarak bekletilir; gönderim yok.</summary>
    Held = 1,
    /// <summary>systest.sagliknet.saglik.gov.tr ortamına gönderilir.</summary>
    TestOnly = 2,
    /// <summary>Canlıya gönderir; YALNIZCA sistem düzeyi KtsRegistered bayrağı açıkken seçilebilir.</summary>
    Live = 3,
}

/// <summary>USS veri paketi tipi. Değerler Bakanlığın resmi paket numaralarıdır (rehber.enabiz.gov.tr).</summary>
public enum EnabizPacketType : short
{
    /// <summary>Hasta Kayıt — her başvuruda ilk gönderilen paket; SysTakipNo bu pakete atanır.</summary>
    HastaKayit101 = 101,
    /// <summary>Hizmet/İlaç/Malzeme Kayıt — yapılan işlemler (SUT/SKRS işlem kodları).</summary>
    HizmetKayit102 = 102,
    /// <summary>Muayene Bilgisi Kayıt — ICD-10 tanı.</summary>
    Muayene103 = 103,
    /// <summary>Ağız ve Diş Sağlığı Veri Seti — FDI diş no + tedavi + tanı. Ürünün asıl paketi.</summary>
    AgizDisSagligi203 = 203,
    /// <summary>Veri Paketi Silme/Düzeltme.</summary>
    Silme200 = 200,
    /// <summary>SYS Takip No Sorgulama.</summary>
    TakipNoSorgu402 = 402,
    /// <summary>Günlük Veri Sorgulama — mutabakat (reconcile) için.</summary>
    GunlukVeriSorgu405 = 405,
}

/// <summary>
/// Gönderim kuyruğu durum makinesi.
/// Held → Queued → Sending → Accepted | Rejected; taşıma hatasında Queued'a geri dönüp
/// artan aralıkla yeniden denenir, 6. denemede ManualReview.
/// </summary>
public enum EnabizSubmissionState : byte
{
    Draft = 1,
    Queued = 2,
    /// <summary>Mod Held: paket üretildi ama gönderilmiyor (tescil bekleniyor).</summary>
    Held = 3,
    Sending = 4,
    Accepted = 5,
    /// <summary>USS iş kuralıyla reddetti — yeniden DENENMEZ, düzeltme kuyruğuna düşer.</summary>
    Rejected = 6,
    /// <summary>Reddedilen paket için 200 (silme/düzeltme) gönderilip yenisi üretildi.</summary>
    Corrected = 7,
    ManualReview = 8,
    GaveUp = 9,
}

/// <summary>
/// Hekim e-imza/mobil imza onay durumu. MBYS/DHBS tarafında veri gönderiminin hekim imzasıyla
/// onaylanması istenir; imza altyapısı (NES token) ürün dışıdır, bu yüzden alan durum takibi içindir.
/// </summary>
public enum EnabizPhysicianSignState : byte
{
    NotRequired = 0,
    Pending = 1,
    Signed = 2,
}

/// <summary>SKRS kod listesinin kaynağı: yerel tohum mu, canlı servisten mi çekildi.</summary>
public enum SkrsSource : byte
{
    /// <summary>USS kimlik bilgisi yokken kullanılan yerel tohum liste.</summary>
    Seed = 1,
    /// <summary>skrs.saglik.gov.tr'den senkronlandı.</summary>
    Live = 2,
}
