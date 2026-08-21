namespace Dental.EDocument.Ubl;

public static class UblVersions
{
    public const string UblVersionId = "2.1";

    /// <summary>UBL Invoice (e-Fatura / e-Arşiv) özelleştirme kimliği.</summary>
    public const string CustomizationId = "TR1.2";

    /// <summary>
    /// UBL CreditNote ailesi (e-SMM / e-Müstahsil / e-Gider Pusulası / e-Döviz) özelleştirme kimliği.
    /// F aşaması araştırması: GİB'in yayımlanmış üç CreditNote kılavuzunun (e-Müstahsil Makbuzu V1.1,
    /// e-Gider Pusulası V1.0, e-Döviz Alım-Satım V1.0) üçü de <c>TR1.2.1</c> yazar — Invoice tarafındaki
    /// TR1.2 ile karıştırılmamalıdır. e-SMM'in kendi kılavuzu GİB tarafından yayımlanmamıştır.
    /// </summary>
    public const string CreditNoteCustomizationId = "TR1.2.1";
}

public static class UblProfileIds
{
    public const string TemelFatura = "TEMELFATURA";
    public const string TicariFatura = "TICARIFATURA";
    public const string EArsivFatura = "EARSIVFATURA";

    /// <summary>
    /// (c)-2: e-SMM ProfileID boş bırakılmaz.
    /// F aşaması araştırması: GİB e-SMM için teknik kılavuz/XSD/schematron YAYIMLAMAMIŞTIR.
    /// En güçlü gösterge, aynı e-Arşiv ailesindeki kardeş belge e-Müstahsil Makbuzu Teknik Kılavuzu
    /// V1.1'dir: "ProfileID zorunlu, EARSIVBELGE girilecektir". UBL-TR Kod Listeleri'ndeki ProfileID
    /// listesi yalnız /Invoice/ProfileID içindir, bu yüzden EARSIVBELGE'nin orada olmaması beklenendir.
    /// AÇIK MADDE: entegratörden yazılı teyit alınmalı.
    /// </summary>
    public const string ESmm = "EARSIVBELGE";
}

/// <summary>
/// UBL CreditNote belge tipi kodları (<c>cbc:CreditNoteTypeCode</c>).
/// GİB'in yayımladığı CreditNote kılavuzlarının üçü de bu alanı tanımlar
/// (e-Müstahsil: MUSTAHSILMAKBUZ, e-Gider Pusulası: SATIS/IADE, e-Döviz: DOVIZALIMBELGESI...).
/// e-SMM için beklenen değer desene göre aşağıdakidir; AÇIK MADDE olduğu için varsayılan olarak
/// YAZILMAZ — <see cref="Models.EDocumentModel.CreditNoteTypeCode"/> doldurulursa yazılır.
/// </summary>
public static class UblCreditNoteTypeCodes
{
    /// <summary>DOĞRULANMADI — hiçbir resmî kaynakta bu değer teyit edilemedi.</summary>
    public const string SerbestMeslekMakbuzu = "SERBESTMESLEKMAKBUZU";
}

/// <summary>UBL-TR InvoiceTypeCode değerleri.</summary>
public static class UblTypeCodes
{
    public const string Satis = "SATIS";
    public const string Iade = "IADE";
    public const string Tevkifat = "TEVKIFAT";
    public const string Istisna = "ISTISNA";
}

/// <summary>GİB vergi türü kodları (UBL-TR Kod Listeleri).</summary>
public static class UblTaxTypeCodes
{
    /// <summary>Gerçek usulde KDV.</summary>
    public const string Kdv = "0015";

    /// <summary>
    /// KDV tevkifatı (eski kod). UBL-TR 1.2'de WithholdingTaxTotal altında 9015 yerine
    /// tevkifat kodunun kendisi (ör. 616) gönderilir — UBL-TR Kod Listeleri V1.18.
    /// </summary>
    public const string KdvTevkifat = "9015";

    /// <summary>GV stopajı (e-SMM) — UBL-TR Kod Listeleri V1.18: "0003 GELİR VERGİSİ STOPAJI".</summary>
    public const string GvStopaj = "0003";
}

public static class UblExemptionCodes
{
    /// <summary>KDVK 13/l — yabancılara verilen sağlık hizmetlerinde istisna.</summary>
    public const string HealthTourism = "334";
}

public static class UblWithholdingCodes
{
    /// <summary>5018 sayılı kanuna tabi kamu idarelerine "diğer hizmetler" — 5/10.</summary>
    public const string PublicOtherServices = "616";
}
