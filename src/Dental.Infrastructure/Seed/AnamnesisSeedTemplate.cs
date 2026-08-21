using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Kiracı şablonu: varsayılan anamnez formu (~22 soru, kritikler işaretli). Yeni kiracı
/// açılışında kopyalanır; mevcut kiracılara DbSeeder idempotent uygular (kiracıda herhangi
/// bir anamnez şablonu varsa dokunmaz — ExpenseCategoryTemplate deseni).
/// </summary>
public static class AnamnesisSeedTemplate
{
    public const string DefaultTemplateName = "Genel Anamnez Formu";

    private sealed record Q(string Text, string? TextEn, AnamnesisAnswerType Type, bool Critical);

    private static readonly IReadOnlyList<Q> Questions =
    [
        new("Kalp hastalığınız var mı? (ritim bozukluğu, kapak, geçirilmiş enfarktüs)",
            "Do you have any heart disease?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("Kalp pili veya stent taşıyor musunuz?",
            "Do you have a pacemaker or stent?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Yüksek tansiyonunuz (hipertansiyon) var mı?",
            "Do you have high blood pressure?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Şeker hastalığınız (diyabet) var mı?",
            "Do you have diabetes?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("Kanama/pıhtılaşma bozukluğunuz var mı?",
            "Do you have a bleeding or clotting disorder?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Kan sulandırıcı ilaç kullanıyor musunuz? (aspirin, kumadin vb.)",
            "Do you take blood thinners?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("Herhangi bir ilaca alerjiniz var mı? (Evet ise hangi ilaç?)",
            "Are you allergic to any medication?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("Lokal anestezi sonrası alerjik reaksiyon veya fenalaşma yaşadınız mı?",
            "Have you had a reaction to local anesthesia?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("Penisilin alerjiniz var mı?",
            "Are you allergic to penicillin?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Sürekli kullandığınız ilaçlar nelerdir?",
            "Which medications do you take regularly?", AnamnesisAnswerType.Text, Critical: false),
        new("Astım veya kronik solunum yolu hastalığınız var mı?",
            "Do you have asthma or chronic respiratory disease?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Hepatit (sarılık) geçirdiniz mi veya taşıyıcı mısınız?",
            "Have you had hepatitis or are you a carrier?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("HIV/AIDS tanınız var mı?",
            "Have you been diagnosed with HIV/AIDS?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Böbrek hastalığınız var mı?",
            "Do you have kidney disease?", AnamnesisAnswerType.YesNo, Critical: false),
        new("Karaciğer hastalığınız var mı?",
            "Do you have liver disease?", AnamnesisAnswerType.YesNo, Critical: false),
        new("Epilepsi (sara) nöbeti geçirdiniz mi?",
            "Have you ever had an epileptic seizure?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Tiroid hastalığınız var mı?",
            "Do you have a thyroid disorder?", AnamnesisAnswerType.YesNo, Critical: false),
        new("Osteoporoz için bifosfonat türü ilaç kullandınız mı? (kemik erimesi iğnesi/hapı)",
            "Have you used bisphosphonates for osteoporosis?", AnamnesisAnswerType.YesNoDetail, Critical: true),
        new("Gebelik söz konusu mu veya olma ihtimali var mı?",
            "Are you pregnant or possibly pregnant?", AnamnesisAnswerType.YesNo, Critical: true),
        new("Emzirme döneminde misiniz?",
            "Are you breastfeeding?", AnamnesisAnswerType.YesNo, Critical: false),
        new("Sigara veya tütün ürünü kullanıyor musunuz?",
            "Do you smoke or use tobacco products?", AnamnesisAnswerType.YesNo, Critical: false),
        new("Son 6 ay içinde ameliyat geçirdiniz mi veya hastanede yattınız mı?",
            "Have you had surgery or hospitalization in the last 6 months?", AnamnesisAnswerType.YesNoDetail,
            Critical: false),
        new("Diş tedavisi sırasında bayılma/fenalaşma yaşadınız mı?",
            "Have you ever fainted during dental treatment?", AnamnesisAnswerType.YesNo, Critical: false),
    ];

    /// <summary>Kiracıda hiç anamnez şablonu yoksa varsayılan formu ekler. Eklenen soru sayısını döner.</summary>
    public static async Task<int> ApplyToTenantAsync(AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var hasTemplate = await db.AnamnesisTemplates.IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct);
        if (hasTemplate) return 0;

        var template = new AnamnesisTemplate { TenantId = tenantId, Name = DefaultTemplateName, IsDefault = true };
        var sortOrder = 0;
        foreach (var q in Questions)
        {
            template.Questions.Add(new AnamnesisQuestion
            {
                TenantId = tenantId,
                SortOrder = ++sortOrder,
                QuestionText = q.Text,
                QuestionTextEn = q.TextEn,
                AnswerType = q.Type,
                IsCritical = q.Critical,
            });
        }
        db.AnamnesisTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Questions.Count;
    }
}
