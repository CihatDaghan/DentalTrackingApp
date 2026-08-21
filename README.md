# DentalTrackingApp

Çok kiracılı (multi-tenant) diş klinik yönetim yazılımı — Macrodental Cloud'un işlevsel eşdeğeri.
Randevudan tedavi planına, tahsilattan e-Faturaya kadar klinik iş akışının tamamı tek panelde.

## Yığın

| Katman | Teknoloji |
|---|---|
| Arka uç | .NET 10 · ASP.NET Core Web API · EF Core 10 · MSSQL 2022 |
| Ön yüz | Angular 20 · PrimeNG 20 · Tailwind 4 · Transloco (TR/EN) |
| Arka plan işleri | Hangfire (SQL Server deposu, `hangfire` şeması) |
| Kimlik | ASP.NET Identity + JWT (15 dk access, rotasyonlu refresh) |
| Dosya deposu | `IFileStorage` → yerel disk (dev) / S3-uyumlu (üretim) |

## Hızlı başlangıç

Gereksinimler: .NET 10 SDK, Node 22+, Docker Desktop (Apple Silicon'da **Settings → General → Use Rosetta** açık olmalı — MSSQL imajı yalnızca amd64).

```bash
docker compose up -d
```

```bash
dotnet run --project src/Dental.Api
```

```bash
npm start --prefix frontend
```

- API: <http://localhost:5210> · API dokümanı: <http://localhost:5210/scalar/v1> · Hangfire panosu (yalnız dev): <http://localhost:5210/hangfire>
- Uygulama: <http://localhost:4200>
- MSSQL: `localhost,14330` (kullanıcı `sa`, şifre `Dental!Dev2026`) · MinIO konsolu: <http://localhost:9001>

Geliştirme ortamında veritabanı ilk çalıştırmada otomatik oluşturulur, migration'lar uygulanır ve demo veri yüklenir.

### Demo hesaplar

| Rol | E-posta | Şifre |
|---|---|---|
| Klinik sahibi | `demo@dental.local` | `Demo!2026` |
| Hekim | `elif.kaya@dental.local` | `Demo!2026` |
| Sekreter | `zeynep.aydin@dental.local` | `Demo!2026` |
| Platform (süper admin) | `admin@dental.local` | `Admin!2026` |

Demo kiracıda 121 kalemlik TDB tedavi kataloğu, 109 ICD-10 kodu, 92 ilaç, anamnez/onam/reçete şablonları, örnek hastalar ve bu haftanın randevuları hazır gelir.

## Modüller

**Hasta kartı (11 sekme):** Hasta · Tedavi · Ödeme · Anamnez · Not · Reçete · Görüntü · Kontrol · Laboratuvar · Epikriz · Rapor

- **Randevu** — hekim renkli takvim, boş slot, sürükle-bırak, kontrol (recall) planları
- **Tedavi** — FDI diş şeması (daimi/süt, yüzey seçimi, tanı/plan/tedavi katmanları), tedavi kataloğu ve fiyat listeleri
- **Finans** — tek cari defter (hasta + kurum), tahsilat, taksit planı, indirim, gider, gün sonu kasası
- **e-Belge** — e-Fatura / e-Arşiv / **e-SMM** (UBL-TR 1.2; belge tipi karar motoru: şahıs hekim, kurum tevkifatı 616, yabancı hasta istisnası 334)
- **Klinik kayıtlar** — anamnez, dijital onam (tablet imza veya SMS linkiyle hastanın telefonunda), görüntü arşivi, reçete, laboratuvar, stok, epikriz

## Test

```bash
dotnet test
```

```bash
npm run build --prefix frontend
```

Entegrasyon testleri Testcontainers ile gerçek MSSQL konteynerinde koşar (Docker gerekir).

## Yapılandırma

Sırlar `appsettings.Development.json` içinde yalnız geliştirme değerleriyle bulunur. Üretimde ortam değişkeni veya secret deposu kullanın:

| Anahtar | Açıklama |
|---|---|
| `ConnectionStrings:Default` | MSSQL bağlantısı |
| `Jwt:SigningKey` | En az 32 baytlık imzalama anahtarı |
| `Security:TcknHmacKey` | TCKN arama özeti için HMAC anahtarı |
| `DataProtection:KeyRingPath` | Şifreleme anahtar halkası dizini (kalıcı olmalı) |
| `Turnstile:SecretKey` | Boşsa giriş doğrulaması atlanır (dev) |
| `Public:BaseUrl` | Onam/ödeme linklerinin gösterileceği adres |
| `Integrations:*` | e-Belge, SMS, WhatsApp, ödeme sağlayıcı uçları |

Entegrasyon kimlik bilgileri kiracı bazında `TenantIntegrationSettings` tablosunda şifreli saklanır; sağlayıcı seçimi (`uyumsoft`, `netgsm`, `meta`, `iyzico`, `fake`) kiracı ayarından yapılır. Hesap tanımlanmamışsa geliştirme sürücüleri (`fake`) devreye girer.

## Kapsam dışı

ÖKC (yazarkasa) entegrasyonu, MHRS (özel klinikler için teknik olarak mümkün değil), Medula e-reçete kanalı ve mobil uygulamalar bilinçli olarak kapsam dışıdır. e-Nabız/USS canlı gönderimi Sağlık Bakanlığı KTS'de DHBS tescili gerektirir; yazılım tescil tamamlanana kadar paketleri üretip kuyrukta bekletir (`Held` modu).
