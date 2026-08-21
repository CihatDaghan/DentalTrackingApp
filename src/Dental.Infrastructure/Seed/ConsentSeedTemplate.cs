using Dental.Domain.Entities;
using Dental.Infrastructure.Consents;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Kiracı şablonu: 5 varsayılan onam formu (Genel Tedavi, İmplant, Çekim, Kanal, Ortodonti).
/// Yer tutucular: {{HastaAdi}} {{HekimAdi}} {{KlinikAdi}} {{Tarih}} {{Tedavi}}.
/// Gövdeler eklenirken de sanitize edilir (savunma katmanı). Ada göre idempotent.
/// </summary>
public static class ConsentSeedTemplate
{
    private sealed record T(string Name, string BodyHtml);

    private static readonly IReadOnlyList<T> Templates =
    [
        new("Genel Tedavi Onamı", """
            <h2>Genel Dental Tedavi Onam Formu</h2>
            <p>Sayın <strong>{{HastaAdi}}</strong>, {{KlinikAdi}} bünyesinde {{HekimAdi}} tarafından
            planlanan <strong>{{Tedavi}}</strong> işlemi ve genel diş tedavileriniz hakkında
            bilgilendirildim.</p>
            <p>Muayene, teşhis ve tedavi sürecinde uygulanacak işlemler; lokal anestezi, dolgu,
            diş taşı temizliği ve benzeri girişimlerin olası riskleri (geçici hassasiyet, ağrı,
            şişlik, nadiren alerjik reaksiyon) tarafıma anlatıldı.</p>
            <ul>
            <li>Tedavinin alternatifleri ve tedavi olmamanın sonuçları açıklandı.</li>
            <li>Sorularımı sorma fırsatı buldum ve tatmin edici yanıtlar aldım.</li>
            <li>Tedavi planının klinik bulgulara göre güncellenebileceğini biliyorum.</li>
            </ul>
            <p>Bu formu okuyup anladığımı ve tedaviyi <strong>kendi rızamla</strong> kabul ettiğimi
            beyan ederim. Tarih: {{Tarih}}</p>
            """),
        new("İmplant Onamı", """
            <h2>Dental İmplant Cerrahisi Onam Formu</h2>
            <p>Sayın <strong>{{HastaAdi}}</strong>, {{KlinikAdi}} kliniğinde {{HekimAdi}} tarafından
            planlanan <strong>{{Tedavi}}</strong> (implant) işlemi hakkında bilgilendirildim.</p>
            <p>İmplant cerrahisinin riskleri tarafıma anlatıldı: enfeksiyon, kanama, şişlik,
            geçici veya kalıcı his kaybı, sinüs komplikasyonları, implantın kemikle
            kaynaşmaması (başarısızlık) ve bu durumda implantın çıkarılması gerekebileceği.</p>
            <ul>
            <li>Sigara kullanımının ve kontrolsüz diyabetin başarı oranını düşürdüğünü biliyorum.</li>
            <li>Ağız bakımı ve düzenli kontrol randevularının başarı için şart olduğunu anladım.</li>
            <li>Gerekirse kemik grefti/sinüs kaldırma uygulanabileceği açıklandı.</li>
            </ul>
            <p>İşlemi kendi rızamla kabul ediyorum. Tarih: {{Tarih}}</p>
            """),
        new("Diş Çekimi Onamı", """
            <h2>Diş Çekimi Onam Formu</h2>
            <p>Sayın <strong>{{HastaAdi}}</strong>, {{KlinikAdi}} kliniğinde {{HekimAdi}} tarafından
            yapılacak <strong>{{Tedavi}}</strong> (diş çekimi) işlemi hakkında bilgilendirildim.</p>
            <p>Çekim sonrası kanama, ağrı, şişlik, enfeksiyon, kuru soket (alveolit), komşu diş ve
            dolgularda hasar, alt çenede geçici/kalıcı dudak-çene uyuşukluğu, üst çenede sinüs
            perforasyonu gibi riskler tarafıma anlatıldı.</p>
            <ul>
            <li>Çekim sonrası bakım talimatlarına uymam gerektiğini biliyorum.</li>
            <li>Kullandığım ilaçları ve sağlık geçmişimi eksiksiz bildirdim.</li>
            </ul>
            <p>İşlemi kendi rızamla kabul ediyorum. Tarih: {{Tarih}}</p>
            """),
        new("Kanal Tedavisi Onamı", """
            <h2>Kanal Tedavisi (Endodonti) Onam Formu</h2>
            <p>Sayın <strong>{{HastaAdi}}</strong>, {{KlinikAdi}} kliniğinde {{HekimAdi}} tarafından
            planlanan <strong>{{Tedavi}}</strong> (kanal tedavisi) hakkında bilgilendirildim.</p>
            <p>Kanal tedavisinin birden fazla seans sürebileceği; tedavi sırasında alet kırılması,
            kök perforasyonu, tedavi sonrası ağrı/hassasiyet olabileceği ve tedavinin
            başarısız olması halinde yeniden tedavi, kök ucu ameliyatı veya çekim
            gerekebileceği tarafıma anlatıldı.</p>
            <ul>
            <li>Kanal tedavili dişin kırılganlaşabileceğini ve kuron önerilebileceğini biliyorum.</li>
            <li>Tedavi tamamlanmadan ara verilmesinin dişin kaybına yol açabileceğini anladım.</li>
            </ul>
            <p>Tedaviyi kendi rızamla kabul ediyorum. Tarih: {{Tarih}}</p>
            """),
        new("Ortodonti Onamı", """
            <h2>Ortodontik Tedavi Onam Formu</h2>
            <p>Sayın <strong>{{HastaAdi}}</strong>, {{KlinikAdi}} kliniğinde {{HekimAdi}} tarafından
            planlanan <strong>{{Tedavi}}</strong> (ortodontik tedavi) hakkında bilgilendirildim.</p>
            <p>Tedavi süresinin kişiye göre değişebileceği; braket/plak kullanımında ağrı,
            konuşma güçlüğü, ağız yaraları olabileceği; ağız hijyeni yetersizse çürük ve
            mine lekeleri gelişebileceği; kök kısalması (rezorpsiyon) ve tedavi sonrası
            pekiştirme aparatı kullanılmazsa nüks riski tarafıma anlatıldı.</p>
            <ul>
            <li>Düzenli kontrol randevularına gelmem gerektiğini biliyorum.</li>
            <li>Sert/yapışkan gıdalardan kaçınma gibi kurallara uyacağım.</li>
            </ul>
            <p>Tedaviyi kendi rızamla kabul ediyorum. Tarih: {{Tarih}}</p>
            """),
    ];

    /// <summary>Eksik varsayılan onam şablonlarını kiracıya ekler (ada göre idempotent). Eklenen sayıyı döner.</summary>
    public static async Task<int> ApplyToTenantAsync(AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var existing = await db.ConsentTemplates.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => t.Name)
            .ToHashSetAsync(ct);

        var count = 0;
        foreach (var template in Templates.Where(t => !existing.Contains(t.Name)))
        {
            db.ConsentTemplates.Add(new ConsentTemplate
            {
                TenantId = tenantId,
                Name = template.Name,
                BodyHtml = ConsentHtml.Sanitize(template.BodyHtml),
                Locale = "tr",
                Version = 1,
                IsActive = true,
            });
            count++;
        }
        if (count > 0) await db.SaveChangesAsync(ct);
        return count;
    }
}
