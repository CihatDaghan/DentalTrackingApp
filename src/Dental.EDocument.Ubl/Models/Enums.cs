namespace Dental.EDocument.Ubl.Models;

/// <summary>Üretilecek e-belge türü.</summary>
public enum DocumentKind
{
    /// <summary>e-Fatura — GİB e-fatura mükellefi alıcı, UBL Invoice.</summary>
    EFatura,

    /// <summary>e-Arşiv Fatura — GİB kayıtsız alıcı (bireysel hasta vb.), UBL Invoice.</summary>
    EArsiv,

    /// <summary>e-Serbest Meslek Makbuzu — şahıs hekim, UBL CreditNote (Invoice DEĞİL).</summary>
    ESmm,
}

/// <summary>Satıcının (kliniğin) hukuki yapısı.</summary>
public enum SellerLegalType
{
    /// <summary>Şahıs diş hekimi muayenehanesi (serbest meslek erbabı) — e-SMM keser.</summary>
    SoleProprietor,

    /// <summary>Şirket/poliklinik (Ltd/AŞ) — e-Fatura / e-Arşiv keser.</summary>
    Company,
}

/// <summary>Alıcı tipi.</summary>
public enum BuyerKind
{
    IndividualPatient,
    Corporate,

    /// <summary>5018 sayılı kanuna tabi kamu idaresi — tevkifat (616) uygulanır.</summary>
    Government,
}
