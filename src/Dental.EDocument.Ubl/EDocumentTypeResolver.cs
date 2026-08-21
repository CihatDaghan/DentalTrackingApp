using Dental.EDocument.Ubl.Models;

namespace Dental.EDocument.Ubl;

/// <summary>
/// Belge tipi karar motoru — saf fonksiyon, yan etkisiz.
/// Senaryo eşlemesi: tasarım §1.1 + denetim (c)-2/3/4 düzeltmeleri.
/// </summary>
public static class EDocumentTypeResolver
{
    public static EDocumentDecision Resolve(EDocumentResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        // Şahıs hekim SMM keser; şirket, alıcının GİB kaydına göre e-Fatura/e-Arşiv keser.
        var kind = request.SellerLegalType == SellerLegalType.SoleProprietor
            ? DocumentKind.ESmm
            : request.BuyerIsGibEInvoiceUser
                ? DocumentKind.EFatura
                : DocumentKind.EArsiv;

        var profileId = kind switch
        {
            DocumentKind.EFatura => UblProfileIds.TicariFatura, // varsayılan: alıcı yanıtlı senaryo
            DocumentKind.EArsiv => UblProfileIds.EArsivFatura,
            _ => UblProfileIds.ESmm, // (c)-2: boş bırakılmaz; Uyumsoft testinde doğrulanacak açık madde
        };

        var typeCode = UblTypeCodes.Satis;
        decimal? vatRateOverride = null;
        string? exemptionCode = null;
        string? exemptionReason = null;
        string? withholdingCode = null;
        decimal? withholdingPercent = null;

        // Kamu alıcı → KDV tevkifatı 616 (5/10). e-SMM'de KDV tevkifatı uygulanmaz.
        if (request.BuyerIsGovernment && kind != DocumentKind.ESmm)
        {
            typeCode = UblTypeCodes.Tevkifat;
            withholdingCode = UblWithholdingCodes.PublicOtherServices;
            withholdingPercent = 50m;
        }

        // Yabancı hasta — sağlık turizmi istisnası (KDVK 13/l, kod 334).
        if (request.BuyerIsForeign)
        {
            if (!request.TenantHasHealthTourismAuthorization)
            {
                // (c)-4: yetki belgesiz tesis 334 uygulayamaz.
                errors.Add("334 istisnası için tenant'ın sağlık turizmi yetki belgesi bulunmuyor.");
            }

            if (request.HasAestheticLines)
            {
                // (c)-4: estetik işlemler (KDV %20) istisna kapsamı dışıdır.
                errors.Add("334 istisnası estetik işlemle birleşemez.");
            }

            if (string.IsNullOrWhiteSpace(request.BuyerPassportNumber))
            {
                errors.Add("Yabancı hasta için pasaport numarası zorunludur.");
            }

            if (string.IsNullOrWhiteSpace(request.BuyerNationality))
            {
                errors.Add("Yabancı hasta için uyruk bilgisi zorunludur.");
            }

            if (request.BuyerLastEntryDate is null)
            {
                errors.Add("Yabancı hasta için Türkiye'ye son giriş tarihi zorunludur.");
            }

            if (errors.Count == 0)
            {
                typeCode = UblTypeCodes.Istisna;
                exemptionCode = UblExemptionCodes.HealthTourism;
                exemptionReason = "KDV Kanunu 13/l — Yabancılara verilen sağlık hizmetlerinde istisna";
                vatRateOverride = 0m;
            }
        }

        // İade tip kodunu ezer; kaynak belge zorunlu.
        if (request.IsRefund)
        {
            if (string.IsNullOrWhiteSpace(request.SourceInvoiceNumber) || request.SourceIssueDate is null)
            {
                errors.Add("İade belgesinde kaynak belge numarası ve tarihi zorunludur.");
            }

            typeCode = UblTypeCodes.Iade;

            // IADE senaryosu yalnız TEMELFATURA/EARSIVFATURA profilinde geçerlidir.
            if (kind == DocumentKind.EFatura)
            {
                profileId = UblProfileIds.TemelFatura;
            }
        }

        // (c)-3: GV stopajı %20 yalnız alıcı vergi mükellefiyse; bireysel hastaya stopaj YOK.
        var appliesGvStopaj = kind == DocumentKind.ESmm && request.BuyerIsVatRegistered;

        return new EDocumentDecision
        {
            DocumentKind = kind,
            ProfileId = profileId,
            TypeCode = typeCode,
            VatRateOverride = vatRateOverride,
            ExemptionCode = exemptionCode,
            ExemptionReason = exemptionReason,
            WithholdingCode = withholdingCode,
            WithholdingPercent = withholdingPercent,
            AppliesGvStopaj = appliesGvStopaj,
            Errors = errors,
        };
    }
}
