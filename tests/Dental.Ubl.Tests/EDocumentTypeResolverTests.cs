using Dental.EDocument.Ubl;
using Dental.EDocument.Ubl.Models;

namespace Dental.Ubl.Tests;

/// <summary>Karar motoru — senaryo tablosunun her satırı ayrı test.</summary>
public sealed class EDocumentTypeResolverTests
{
    private static EDocumentResolutionRequest Company() => new()
    {
        SellerLegalType = SellerLegalType.Company,
    };

    private static EDocumentResolutionRequest SoleProprietor() => new()
    {
        SellerLegalType = SellerLegalType.SoleProprietor,
    };

    private static EDocumentResolutionRequest ForeignPatient() => new()
    {
        SellerLegalType = SellerLegalType.Company,
        TenantHasHealthTourismAuthorization = true,
        BuyerIsForeign = true,
        BuyerPassportNumber = "P1234567",
        BuyerNationality = "GBR",
        BuyerLastEntryDate = new DateOnly(2026, 8, 1),
    };

    [Fact]
    public void SoleProprietor_produces_esmm()
    {
        var decision = EDocumentTypeResolver.Resolve(SoleProprietor());

        Assert.True(decision.IsValid);
        Assert.Equal(DocumentKind.ESmm, decision.DocumentKind);
    }

    [Fact]
    public void Esmm_profile_is_earsivbelge_open_item()
    {
        // (c)-2: ProfileID boş bırakılmaz; EARSIVBELGE Uyumsoft testinde doğrulanacak açık madde.
        var decision = EDocumentTypeResolver.Resolve(SoleProprietor());

        Assert.False(string.IsNullOrWhiteSpace(decision.ProfileId));
        Assert.Equal("EARSIVBELGE", decision.ProfileId);
    }

    [Fact]
    public void SoleProprietor_with_vat_registered_buyer_applies_gv_stopaj()
    {
        var decision = EDocumentTypeResolver.Resolve(
            SoleProprietor() with { BuyerIsVatRegistered = true });

        Assert.True(decision.AppliesGvStopaj);
    }

    [Fact]
    public void SoleProprietor_with_individual_patient_does_not_apply_gv_stopaj()
    {
        // (c)-3: bireysel hastaya stopaj YOK.
        var decision = EDocumentTypeResolver.Resolve(
            SoleProprietor() with { BuyerIsVatRegistered = false });

        Assert.False(decision.AppliesGvStopaj);
    }

    [Fact]
    public void Company_seller_does_not_apply_gv_stopaj()
    {
        var decision = EDocumentTypeResolver.Resolve(
            Company() with { BuyerIsVatRegistered = true });

        Assert.False(decision.AppliesGvStopaj);
    }

    [Fact]
    public void Company_with_gib_registered_buyer_produces_efatura_ticari()
    {
        var decision = EDocumentTypeResolver.Resolve(
            Company() with { BuyerIsGibEInvoiceUser = true });

        Assert.True(decision.IsValid);
        Assert.Equal(DocumentKind.EFatura, decision.DocumentKind);
        Assert.Equal("TICARIFATURA", decision.ProfileId);
        Assert.Equal("SATIS", decision.TypeCode);
    }

    [Fact]
    public void Company_with_unregistered_buyer_produces_earsiv()
    {
        var decision = EDocumentTypeResolver.Resolve(
            Company() with { BuyerIsGibEInvoiceUser = false });

        Assert.True(decision.IsValid);
        Assert.Equal(DocumentKind.EArsiv, decision.DocumentKind);
        Assert.Equal("EARSIVFATURA", decision.ProfileId);
    }

    [Fact]
    public void Government_buyer_produces_tevkifat_616()
    {
        var decision = EDocumentTypeResolver.Resolve(Company() with
        {
            BuyerIsGibEInvoiceUser = true,
            BuyerIsGovernment = true,
            BuyerIsVatRegistered = true,
        });

        Assert.True(decision.IsValid);
        Assert.Equal("TEVKIFAT", decision.TypeCode);
        Assert.Equal("616", decision.WithholdingCode);
        Assert.Equal(50m, decision.WithholdingPercent); // 5/10
    }

    [Fact]
    public void Government_buyer_on_esmm_does_not_apply_kdv_tevkifat()
    {
        // e-SMM'de KDV tevkifatı uygulanmaz; kamu alıcı SMM'de tip kodunu değiştirmez.
        var decision = EDocumentTypeResolver.Resolve(SoleProprietor() with
        {
            BuyerIsGovernment = true,
            BuyerIsVatRegistered = true,
        });

        Assert.Equal(DocumentKind.ESmm, decision.DocumentKind);
        Assert.Null(decision.WithholdingCode);
        Assert.Equal("SATIS", decision.TypeCode);
        Assert.True(decision.AppliesGvStopaj);
    }

    [Fact]
    public void Foreign_patient_with_authorization_produces_istisna_334()
    {
        var decision = EDocumentTypeResolver.Resolve(ForeignPatient());

        Assert.True(decision.IsValid);
        Assert.Equal(DocumentKind.EArsiv, decision.DocumentKind);
        Assert.Equal("ISTISNA", decision.TypeCode);
        Assert.Equal("334", decision.ExemptionCode);
        Assert.Equal(0m, decision.VatRateOverride);
        Assert.NotNull(decision.ExemptionReason);
    }

    [Fact]
    public void Foreign_patient_with_aesthetic_lines_is_rejected()
    {
        // (c)-4: 334 istisnası estetik (KDV %20) kalemle birleşemez.
        var decision = EDocumentTypeResolver.Resolve(
            ForeignPatient() with { HasAestheticLines = true });

        Assert.False(decision.IsValid);
        Assert.Contains(decision.Errors, e => e.Contains("estetik"));
        Assert.Null(decision.ExemptionCode);
    }

    [Fact]
    public void Foreign_patient_without_tenant_authorization_is_rejected()
    {
        // (c)-4: sağlık turizmi yetki belgesi olmayan tesis 334 uygulayamaz.
        var decision = EDocumentTypeResolver.Resolve(
            ForeignPatient() with { TenantHasHealthTourismAuthorization = false });

        Assert.False(decision.IsValid);
        Assert.Contains(decision.Errors, e => e.Contains("yetki belgesi"));
    }

    [Fact]
    public void Foreign_patient_without_passport_is_rejected()
    {
        var decision = EDocumentTypeResolver.Resolve(
            ForeignPatient() with { BuyerPassportNumber = null });

        Assert.False(decision.IsValid);
        Assert.Contains(decision.Errors, e => e.Contains("pasaport"));
    }

    [Fact]
    public void Foreign_patient_without_nationality_is_rejected()
    {
        var decision = EDocumentTypeResolver.Resolve(
            ForeignPatient() with { BuyerNationality = " " });

        Assert.False(decision.IsValid);
        Assert.Contains(decision.Errors, e => e.Contains("uyruk"));
    }

    [Fact]
    public void Foreign_patient_without_last_entry_date_is_rejected()
    {
        var decision = EDocumentTypeResolver.Resolve(
            ForeignPatient() with { BuyerLastEntryDate = null });

        Assert.False(decision.IsValid);
        Assert.Contains(decision.Errors, e => e.Contains("son giriş"));
    }

    [Fact]
    public void Refund_with_source_document_produces_iade()
    {
        var decision = EDocumentTypeResolver.Resolve(Company() with
        {
            IsRefund = true,
            SourceInvoiceNumber = "DIS2026000000001",
            SourceIssueDate = new DateOnly(2026, 7, 1),
        });

        Assert.True(decision.IsValid);
        Assert.Equal("IADE", decision.TypeCode);
    }

    [Fact]
    public void Refund_without_source_document_is_rejected()
    {
        var decision = EDocumentTypeResolver.Resolve(Company() with { IsRefund = true });

        Assert.False(decision.IsValid);
        Assert.Contains(decision.Errors, e => e.Contains("kaynak belge"));
    }

    [Fact]
    public void Refund_on_efatura_downgrades_profile_to_temelfatura()
    {
        // IADE senaryosu yalnız TEMELFATURA/EARSIVFATURA olabilir.
        var decision = EDocumentTypeResolver.Resolve(Company() with
        {
            BuyerIsGibEInvoiceUser = true,
            IsRefund = true,
            SourceInvoiceNumber = "DIS2026000000001",
            SourceIssueDate = new DateOnly(2026, 7, 1),
        });

        Assert.Equal(DocumentKind.EFatura, decision.DocumentKind);
        Assert.Equal("TEMELFATURA", decision.ProfileId);
    }

    [Fact]
    public void Domestic_individual_patient_defaults_to_satis()
    {
        var decision = EDocumentTypeResolver.Resolve(Company());

        Assert.True(decision.IsValid);
        Assert.Equal("SATIS", decision.TypeCode);
        Assert.Null(decision.VatRateOverride);
        Assert.Null(decision.ExemptionCode);
        Assert.Null(decision.WithholdingCode);
    }
}
