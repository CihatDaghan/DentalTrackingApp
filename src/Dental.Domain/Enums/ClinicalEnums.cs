namespace Dental.Domain.Enums;

public enum AnamnesisAnswerType : byte
{
    YesNo = 1,
    /// <summary>Evet/Hayır + Evet'te açıklama metni (örn. alerji detayı).</summary>
    YesNoDetail = 2,
    Text = 3,
    /// <summary>Seçenekler soru üzerindeki OptionsJson'dan gelir.</summary>
    MultiSelect = 4,
}

public enum MediaCategory : byte
{
    Xray = 1,
    IntraoralPhoto = 2,
    Document = 3,
    ConsentPdf = 4,
    LabAttachment = 5,
    InvoiceUbl = 6,
    InvoicePdf = 7,
    Logo = 8,
    SignatureImage = 9,
    PrescriptionPdf = 10,
    EpicrisisPdf = 11,
    /// <summary>Hasta kartı "Rapor" sekmesinden üretilen belge (tedavi dökümü, durum raporu, proforma).</summary>
    PatientReportPdf = 12,
}

public enum ConsentFormStatus : byte
{
    Draft = 1,
    SentBySms = 2,
    Signed = 3,
    Expired = 4,
    Declined = 5,
}

public enum ConsentSignChannel : byte
{
    Tablet = 1,
    SmsLink = 2,
}
