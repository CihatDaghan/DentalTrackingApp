using System.Xml.Linq;
using Dental.Integrations.Enabiz;
using Dental.Integrations.Enabiz.PacketBuilders;
using Dental.Integrations.Enabiz.Schemas;

namespace Dental.UnitTests.Integrations;

/// <summary>
/// Paket üreticilerinin alan/biçim doğrulaması.
///
/// Beklentiler Bakanlığın <b>resmi</b> paket tanımından alınmıştır
/// (rehber.enabiz.gov.tr/Home/PaketDetay → Veri + OrnekXML sekmeleri): veri seti adları,
/// zorunlu alanlar, SKRS kod sistemi GUID'leri ve <c>value</c> niteliğiyle yazım biçimi.
/// </summary>
public sealed class EnabizPacketBuilderTests
{
    private static EnabizPacketContext BaseContext() => new()
    {
        FacilityCode = "123456",
        FacilityName = "TEST ADSM",
        SoftwareCompanyCode = "FIRMA12345",
        ProtocolNo = "2026-000042",
        FacilityReferenceNo = "2026-000042",
        LocalTimestamp = new DateTime(2026, 8, 20, 14, 30, 0),
        AdmissionAtLocal = new DateTime(2026, 8, 20, 14, 0, 0),
        VisitDate = new DateOnly(2026, 8, 20),
        Patient = new EnabizPatient(
            Tckn: "10000000146",
            PassportNo: null,
            FirstName: "AYŞE",
            LastName: "YILMAZ",
            BirthDate: new DateOnly(1988, 3, 9),
            Gender: "2"),
        Physician = new EnabizPhysician("20000000232", "Dr. Mehmet Demir"),
    };

    private static EnabizPacketContext WithProcedure(
        string? tooth = "36", string? sut = "404010", string? icd = "K02.1") =>
        BaseContext() with
        {
            SysTakipNo = "SYS123456789",
            Procedures =
            [
                new EnabizProcedure(
                    ToothNumber: tooth,
                    SutCode: sut,
                    Name: "Kompozit dolgu",
                    PerformedAtLocal: new DateTime(2026, 8, 20, 14, 10, 0),
                    ReferenceNo: "9001",
                    EndsAtLocal: new DateTime(2026, 8, 20, 14, 25, 0),
                    DiagnosisIcdCode: icd),
            ],
            Diagnoses = icd is null ? [] : [new EnabizDiagnosis(icd)],
        };

    // ---- Zarf ----

    [Fact]
    public void SysMessage_UsesOfficialEnvelopeShape()
    {
        var packet = new Packet101Builder().Build(BaseContext());

        Assert.Equal("SYSMessage", packet.Name.LocalName);
        // Paket XML'i ad alanı KULLANMAZ (resmi örneklerde bildirim yoktur).
        Assert.Equal("", packet.Name.NamespaceName);

        var messageType = packet.Element("messageType")!;
        Assert.Equal("101", messageType.Attribute("code")!.Value);
        Assert.Equal(EnabizCodeSystems.MessageType, messageType.Attribute("codeSystemGuid")!.Value);
        Assert.Equal("1", messageType.Attribute("version")!.Value);

        Assert.True(Guid.TryParse(packet.Element("messageGuid")!.Attribute("value")!.Value, out _));
        Assert.Equal("FIRMA12345", packet.Element("firmaKodu")!.Attribute("value")!.Value);

        var provider = packet.Element("author")!.Element("healthcareProvider")!;
        Assert.Equal("123456", provider.Attribute("code")!.Value);
        Assert.Equal(EnabizCodeSystems.HealthcareProvider, provider.Attribute("codeSystemGuid")!.Value);
    }

    [Fact]
    public void DocumentGenerationTime_UsesUsvsMinuteFormat()
    {
        var packet = new Packet101Builder().Build(BaseContext());

        // USVS biçimi yyyyMMddHHmm — resmi örnek: 201106240304.
        Assert.Equal("202608201430", packet.Element("documentGenerationTime")!.Attribute("value")!.Value);
    }

    [Fact]
    public void Values_AreWrittenAsAttributes_NotElementText()
    {
        var packet = new Packet101Builder().Build(BaseContext());
        var name = packet.Descendants("AD").Single();

        Assert.Equal("AYŞE", name.Attribute("value")!.Value);
        // Değer öğe metninde OLMAMALIDIR; en sık yapılan biçim hatası budur.
        Assert.Equal("", name.Value);
    }

    // ---- 101 ----

    [Fact]
    public void Packet101_WritesMandatoryDataSets()
    {
        var packet = new Packet101Builder().Build(BaseContext());
        var record = packet.Element("recordData")!;

        Assert.NotNull(record.Element("HASTA_KIMLIK_BILGILERI"));
        Assert.NotNull(record.Element("HASTA_BASVURU_BILGILERI"));

        var identity = record.Element("HASTA_KIMLIK_BILGILERI")!;
        Assert.Equal("10000000146", identity.Element("HASTA_KIMLIK_NUMARASI")!.Attribute("value")!.Value);
        Assert.Equal("19880309", identity.Element("DOGUM_TARIHI")!.Attribute("value")!.Value);
        Assert.Equal(EnabizCodeSystems.Gender, identity.Element("CINSIYET")!.Attribute("codeSystemGuid")!.Value);
        Assert.NotNull(identity.Element("ADRES_BILGISI"));

        var admission = record.Element("HASTA_BASVURU_BILGILERI")!;
        Assert.Equal("2026-000042", admission.Element("PROTOKOL_NUMARASI")!.Attribute("value")!.Value);
        Assert.Equal("202608201400", admission.Element("KABUL_ZAMANI")!.Attribute("value")!.Value);
        Assert.NotNull(admission.Element("YATIS_BILGISI"));
    }

    [Fact]
    public void Packet101_ForeignPatient_UsesForeignIdAsIdentityAndKeepsPassport()
    {
        // Resmi tanımda HASTA_KIMLIK_NUMARASI yabancı hastada da zorunludur; onu pasaport değil,
        // Bakanlıkça verilen yabancı hasta kimlik numarası karşılar.
        var context = BaseContext() with
        {
            Patient = new EnabizPatient(
                Tckn: null, PassportNo: "U1234567", FirstName: "JOHN", LastName: "SMITH",
                BirthDate: new DateOnly(1975, 1, 2), Gender: "1", NationalityCode: "GBR",
                ForeignPatientId: "99123456789"),
        };

        var identity = new Packet101Builder().Build(context).Descendants("HASTA_KIMLIK_BILGILERI").Single();

        Assert.Equal("99123456789", identity.Element("HASTA_KIMLIK_NUMARASI")!.Attribute("value")!.Value);
        Assert.Equal("99123456789",
            identity.Element("YABANCI_HASTA_KIMLIK_NUMARASI")!.Attribute("value")!.Value);
        Assert.Equal("U1234567", identity.Element("PASAPORT_NO")!.Attribute("value")!.Value);
    }

    [Fact]
    public void Packet101_ForeignPatientWithPassportOnly_IsRejectedWithActionableMessage()
    {
        // Pasaport tek başına USS için yetmez; sessizce eksik paket göndermek yerine durdurulur.
        var context = BaseContext() with
        {
            Patient = new EnabizPatient(
                Tckn: null, PassportNo: "U1234567", FirstName: "JOHN", LastName: "SMITH",
                BirthDate: new DateOnly(1975, 1, 2), Gender: "1", NationalityCode: "GBR"),
        };

        var ex = Assert.Throws<EnabizPacketException>(() => new Packet101Builder().Build(context));
        Assert.Contains("yabancı hasta kimlik numarası", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- 203 (ürünün asıl paketi) ----

    [Fact]
    public void Packet203_WritesToothProcedureWithOfficialCodeSystems()
    {
        var packet = new Packet203Builder().Build(WithProcedure());
        var item = packet.Descendants("DIS_MUDAHALE_BILGISI").Single();

        var tooth = item.Element("TEDAVI_EDILEN_DISIN_KODU")!;
        Assert.Equal("36", tooth.Attribute("code")!.Value);
        Assert.Equal(EnabizCodeSystems.ToothCode, tooth.Attribute("codeSystemGuid")!.Value);

        var procedure = item.Element("MUDAHALE")!;
        Assert.Equal("404010", procedure.Attribute("code")!.Value);
        Assert.Equal(EnabizCodeSystems.Sut, procedure.Attribute("codeSystemGuid")!.Value);

        Assert.Equal("202608201410", item.Element("MUDAHALE_BASLANGIC_ZAMANI")!.Attribute("value")!.Value);
        Assert.Equal("202608201425", item.Element("MUDAHALE_BITIS_ZAMANI")!.Attribute("value")!.Value);
        Assert.Equal("9001", item.Element("ISLEM_REFERANS_NUMARASI")!.Attribute("value")!.Value);
    }

    [Fact]
    public void Packet203_RequiresSysTakipNo()
    {
        var context = WithProcedure() with { SysTakipNo = null };

        var ex = Assert.Throws<EnabizPacketException>(() => new Packet203Builder().Build(context));
        Assert.Contains("SysTakipNo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Packet203_WithoutSutCode_IsRejected()
    {
        // MUDAHALE resmi tanımda zorunludur; kodsuz işlem paket üretemez.
        var context = WithProcedure(sut: null);

        Assert.Throws<EnabizPacketException>(() => new Packet203Builder().Build(context));
    }

    [Theory]
    [InlineData("99")]
    [InlineData("00")]
    [InlineData("9")]
    [InlineData("abc")]
    public void Packet203_InvalidFdiToothNumber_IsRejected(string tooth)
    {
        var context = WithProcedure(tooth: tooth);

        var ex = Assert.Throws<EnabizPacketException>(() => new Packet203Builder().Build(context));
        Assert.Contains("FDI", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("11")]
    [InlineData("48")]
    [InlineData("55")]
    [InlineData("85")]
    public void Packet203_ValidFdiToothNumbers_AreAccepted(string tooth)
    {
        var packet = new Packet203Builder().Build(WithProcedure(tooth: tooth));

        Assert.Equal(tooth, packet.Descendants("TEDAVI_EDILEN_DISIN_KODU").Single().Attribute("code")!.Value);
    }

    [Fact]
    public void Packet203_MouthWideProcedure_IsRejected()
    {
        // Diş numarası olmayan işlem 203'e değil 102'ye gider.
        var context = WithProcedure(tooth: null);

        Assert.Throws<EnabizPacketException>(() => new Packet203Builder().Build(context));
    }

    // ---- 103 + ICD-10 ----

    [Fact]
    public void Packet103_WritesIcd10WithOfficialCodeSystem()
    {
        var packet = new Packet103Builder().Build(WithProcedure());
        var diagnosis = packet.Descendants("TANI_BILGISI").Single();

        var icd = diagnosis.Element("ICD10")!;
        Assert.Equal("K02.1", icd.Attribute("code")!.Value);
        Assert.Equal(EnabizCodeSystems.Icd10, icd.Attribute("codeSystemGuid")!.Value);
        Assert.Equal(EnabizCodeSystems.DiagnosisKind,
            diagnosis.Element("TANI_TURU")!.Attribute("codeSystemGuid")!.Value);
    }

    [Fact]
    public void Packet103_WithoutDiagnosis_IsRejected()
    {
        var context = WithProcedure(icd: null);

        Assert.Throws<EnabizPacketException>(() => new Packet103Builder().Build(context));
    }

    [Theory]
    [InlineData("K02.1", true)]
    [InlineData("J98.9", true)]
    [InlineData("M63.39", true)]
    [InlineData("K00", true)]
    [InlineData("U07.1", false)]  // 'U' bölümü ICD-10 ana kodlamasında kullanılmaz.
    [InlineData("K2.1", false)]
    [InlineData("102", false)]
    [InlineData("", false)]
    public void Icd10Validation_MatchesExpectedFormat(string code, bool valid)
    {
        Assert.Equal(valid, EnabizPacketXml.IsValidIcd10(code));
    }

    // ---- 102 / 405 ----

    [Fact]
    public void Packet102_WritesServiceWithSutCode()
    {
        var packet = new Packet102Builder().Build(WithProcedure(tooth: null));
        var service = packet.Descendants("ISLEM_BILGISI").Single();

        Assert.Equal("404010", service.Element("ISLEM_KODU")!.Attribute("value")!.Value);
        Assert.Equal("202608201410", service.Element("ISLEM_ZAMANI")!.Attribute("value")!.Value);
        Assert.Equal("9001", service.Element("ISLEM_REFERANS_NUMARASI")!.Attribute("value")!.Value);
    }

    [Fact]
    public void Packet405_WritesQueryDate()
    {
        var packet = new Packet405Builder().Build(BaseContext());

        Assert.Equal("20260820",
            packet.Descendants("SORGU_TARIHI").Single().Attribute("value")!.Value);
    }

    // ---- Şema doğrulayıcı ----

    [Theory]
    [InlineData((short)101)]
    [InlineData((short)102)]
    [InlineData((short)103)]
    [InlineData((short)203)]
    [InlineData((short)200)]
    [InlineData((short)402)]
    [InlineData((short)405)]
    public void OfficialFieldDefinitions_AreEmbeddedForEveryPacket(short packetType)
    {
        Assert.True(PacketSchemaValidator.HasSchema(packetType),
            $"{packetType} paketinin resmi alan tanımı gömülü kaynaklarda yok.");
    }

    [Fact]
    public void Validator_RejectsUnknownElement()
    {
        var message = new XElement("SYSMessage",
            new XElement("recordData",
                new XElement("HASTA_TAKIP_BILGISI",
                    EnabizPacketXml.Value("SYSTakipNo", "X1"),
                    EnabizPacketXml.Value("UYDURMA_ALAN", "Y"))));

        var ex = Assert.Throws<EnabizPacketValidationException>(
            () => PacketSchemaValidator.Validate(402, message));
        Assert.Contains("UYDURMA_ALAN", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RejectsMissingMandatoryField()
    {
        // 405'te SORGU_TARIHI zorunludur.
        var message = new XElement("SYSMessage",
            new XElement("recordData", new XElement("GUNLUK_VERI_SORGULAMA")));

        var ex = Assert.Throws<EnabizPacketValidationException>(
            () => PacketSchemaValidator.Validate(405, message));
        Assert.Contains("SORGU_TARIHI", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RejectsCodedElementWithWrongCodeSystem()
    {
        var message = new XElement("SYSMessage",
            new XElement("recordData",
                new XElement("HASTA_TAKIP_BILGISI", EnabizPacketXml.Value("SYSTakipNo", "X1")),
                new XElement("AGIZ_DIS_SAGLISI",
                    new XElement("DIS_MUDAHALE_BILGISI",
                        EnabizPacketXml.Coded("MUDAHALE", "00000000-0000-0000-0000-000000000000", "404010"),
                        EnabizPacketXml.Value("MUDAHALE_BASLANGIC_ZAMANI", "202608201410"),
                        EnabizPacketXml.Value("MUDAHALE_BITIS_ZAMANI", "202608201425"),
                        EnabizPacketXml.Coded("TEDAVI_EDILEN_DISIN_KODU", EnabizCodeSystems.ToothCode, "36"),
                        EnabizPacketXml.Value("ISLEM_REFERANS_NUMARASI", "9001")))));

        var ex = Assert.Throws<EnabizPacketValidationException>(
            () => PacketSchemaValidator.Validate(203, message));
        Assert.Contains("codeSystemGuid", ex.Message, StringComparison.Ordinal);
    }
}
