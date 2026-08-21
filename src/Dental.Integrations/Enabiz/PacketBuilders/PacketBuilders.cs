using System.Globalization;
using System.Xml.Linq;
using Dental.Domain.Enums;
using Dental.Integrations.Enabiz.Schemas;

namespace Dental.Integrations.Enabiz.PacketBuilders;

/// <summary>
/// Tüm paket üreticilerinin ortak sözleşmesi. Üretilen XML, gönderilmeden önce
/// <see cref="PacketSchemaValidator"/> ile Bakanlığın resmi alan tanımına göre doğrulanır.
/// </summary>
public interface IEnabizPacketBuilder
{
    EnabizPacketType PacketType { get; }
    XElement Build(EnabizPacketContext context);
}

/// <summary>Üreticiler için ortak temel: üret → resmi alan tanımıyla doğrula.</summary>
public abstract class EnabizPacketBuilderBase : IEnabizPacketBuilder
{
    public abstract EnabizPacketType PacketType { get; }
    protected abstract string PacketName { get; }
    protected abstract IEnumerable<XElement> BuildDataSets(EnabizPacketContext context);

    public XElement Build(EnabizPacketContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = EnabizPacketXml.SysMessage(
            (short)PacketType, PacketName, context, [.. BuildDataSets(context)]);

        PacketSchemaValidator.Validate((short)PacketType, message);
        return message;
    }
}

/// <summary>
/// 101 — Hasta Kayıt. Her başvuruda İLK gönderilen pakettir; USS bu pakete SysTakipNo atar,
/// ziyaretin diğer paketleri (102/103/203) o numaraya bağlanır.
/// </summary>
public sealed class Packet101Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.HastaKayit101;
    protected override string PacketName => "Hasta Kayit";

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        var patient = context.Patient;

        // Resmi tanımda HASTA_KIMLIK_NUMARASI zorunludur — yabancı hastada da. Pasaport bu alanı
        // KARŞILAMAZ; yabancı hasta için Bakanlıkça verilen kimlik/YUPASS numarası gerekir.
        var identityNo = !string.IsNullOrWhiteSpace(patient.Tckn)
            ? patient.Tckn
            : patient.ForeignPatientId;

        if (string.IsNullOrWhiteSpace(identityNo))
        {
            throw new EnabizPacketException(
                "101 paketi hasta kimlik numarası olmadan üretilemez. Yabancı hastada pasaport tek başına " +
                "yetmez; Bakanlıkça verilen yabancı hasta kimlik numarası (99...) veya YUPASS numarası gerekir.");
        }

        var identity = new XElement("HASTA_KIMLIK_BILGILERI");
        identity.Add(EnabizPacketXml.Value("HASTA_KIMLIK_NUMARASI", identityNo));
        if (string.IsNullOrWhiteSpace(patient.Tckn))
        {
            identity.Add(EnabizPacketXml.Optional("YABANCI_HASTA_KIMLIK_NUMARASI", patient.ForeignPatientId));
            identity.Add(EnabizPacketXml.Optional("PASAPORT_NO", patient.PassportNo));
        }

        identity.Add(EnabizPacketXml.Value("AD", patient.FirstName));
        identity.Add(EnabizPacketXml.Value("SOYAD", patient.LastName));
        if (patient.BirthDate is { } birthDate)
            identity.Add(EnabizPacketXml.Value("DOGUM_TARIHI", EnabizPacketXml.Format(birthDate)));
        identity.Add(EnabizPacketXml.Coded("CINSIYET", EnabizCodeSystems.Gender, patient.Gender));
        identity.Add(EnabizPacketXml.Coded("UYRUK", EnabizCodeSystems.Nationality, patient.NationalityCode));
        identity.Add(EnabizPacketXml.Coded("HASTA_TIPI", EnabizCodeSystems.PatientType, context.PatientTypeCode));
        identity.Add(EnabizPacketXml.Optional("TELEFON_NUMARASI", patient.Phone));
        identity.Add(EnabizPacketXml.Optional("EPOSTA_ADRESI", patient.Email));

        // ADRES_BILGISI zorunlu bir gruptur; adres yoksa bile grup yazılır (içi opsiyoneldir).
        identity.Add(new XElement("ADRES_BILGISI",
            EnabizPacketXml.Optional("ACIK_ADRES", patient.Address),
            EnabizPacketXml.Optional("ACIK_ADRES_ILCE", patient.District)));

        yield return identity;

        yield return new XElement("HASTA_BASVURU_BILGILERI",
            EnabizPacketXml.Coded("HIZMET_SUNUCU", EnabizCodeSystems.HealthcareProvider,
                context.FacilityCode ?? "", context.FacilityName),
            EnabizPacketXml.Coded("KAYIT_YERI", EnabizCodeSystems.HealthcareProvider,
                context.FacilityCode ?? "", context.FacilityName),
            EnabizPacketXml.Value("PROTOKOL_NUMARASI", context.ProtocolNo),
            EnabizPacketXml.Value("HASTANE_REFERANS_NUMARASI",
                context.FacilityReferenceNo ?? context.ProtocolNo),
            EnabizPacketXml.Value("KABUL_ZAMANI", EnabizPacketXml.Format(context.AdmissionAtLocal)),
            EnabizPacketXml.Coded("KLINIK_KODU", EnabizCodeSystems.ClinicCode, context.ClinicCode),
            EnabizPacketXml.Coded("SOSYAL_GUVENCE_DURUMU", EnabizCodeSystems.SocialSecurity,
                context.SocialSecurityCode),
            EnabizPacketXml.Coded("VAKA_TURU", EnabizCodeSystems.CaseType, context.CaseTypeCode),
            EnabizPacketXml.Optional("HEKIM_KIMLIK_NUMARASI", context.Physician.Tckn),
            // YATIS_BILGISI zorunlu grup; ayaktan hastada içi boş kalır.
            new XElement("YATIS_BILGISI"));
    }
}

/// <summary>
/// 102 — Hizmet/İlaç/Malzeme Kayıt. Yapılan işlemler SUT/SKRS kodlarıyla taşınır.
/// SUT kodu olmayan işlem gönderilmez (USS kodsuz hizmeti reddeder).
/// </summary>
public sealed class Packet102Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.HizmetKayit102;
    protected override string PacketName => "Hizmet Ilac Malzeme Bilgisi Kayit";

    /// <summary>SKRS işlem türü: normal (ameliyat dışı) işlem.</summary>
    private const string ProcedureKindCode = "1";

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        yield return EnabizPacketXml.TakipBilgisi(context);

        var services = new XElement("HASTA_ISLEM_BILGILERI");
        foreach (var procedure in context.Procedures)
        {
            if (string.IsNullOrWhiteSpace(procedure.SutCode)) continue;

            services.Add(new XElement("ISLEM_BILGISI",
                EnabizPacketXml.Coded("KLINIK_KODU", EnabizCodeSystems.ClinicCode, context.ClinicCode),
                EnabizPacketXml.Coded("ISLEM_TURU", EnabizCodeSystems.ProcedureKind, ProcedureKindCode),
                EnabizPacketXml.Value("ISLEM_KODU", procedure.SutCode.Trim()),
                EnabizPacketXml.Value("ISLEM_ADI", procedure.Name),
                EnabizPacketXml.Value("ISLEM_ZAMANI", EnabizPacketXml.Format(procedure.PerformedAtLocal)),
                EnabizPacketXml.Value("ADET", procedure.Quantity.ToString(CultureInfo.InvariantCulture)),
                EnabizPacketXml.Value("ISLEM_REFERANS_NUMARASI", procedure.ReferenceNo),
                new XElement("ISLEM_HEKIM_BILGISI",
                    EnabizPacketXml.Optional("HEKIM_KIMLIK_NUMARASI", context.Physician.Tckn))));
        }

        if (!services.HasElements)
            throw new EnabizPacketException("102 paketi için SUT/SKRS işlem kodu olan hizmet yok.");

        yield return services;
    }
}

/// <summary>103 — Muayene Bilgisi Kayıt. ICD-10 tanıları ve (varsa) reçete bilgisini taşır.</summary>
public sealed class Packet103Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.Muayene103;
    protected override string PacketName => "Muayene Bilgisi Kayit";

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        yield return EnabizPacketXml.TakipBilgisi(context);

        var examination = new XElement("MUAYENE_BILGILERI",
            EnabizPacketXml.Value("MUAYENE_BASLANGIC_TARIHI", EnabizPacketXml.Format(context.AdmissionAtLocal)),
            EnabizPacketXml.Value("MUAYENE_BITIS_TARIHI", EnabizPacketXml.Format(context.LocalTimestamp)));

        var hasDiagnosis = false;
        foreach (var diagnosis in context.Diagnoses)
        {
            examination.Add(new XElement("TANI_BILGISI",
                EnabizPacketXml.Coded("TANI_TURU", EnabizCodeSystems.DiagnosisKind, diagnosis.KindCode),
                EnabizPacketXml.Coded("ICD10", EnabizCodeSystems.Icd10,
                    EnabizPacketXml.RequireIcd10(diagnosis.IcdCode), diagnosis.Name)));
            hasDiagnosis = true;
        }

        if (!hasDiagnosis)
            throw new EnabizPacketException("103 paketi için en az bir ICD-10 tanısı gerekir.");

        yield return examination;

        if (context.Prescriptions.Count == 0) yield break;

        var prescriptions = new XElement("HASTA_RECETE_BILGILERI");
        foreach (var prescription in context.Prescriptions)
        {
            var element = new XElement("RECETE_BILGISI",
                EnabizPacketXml.Value("RECETE_TARIHI", EnabizPacketXml.Format(prescription.IssuedAtLocal)),
                EnabizPacketXml.Value("RECETE_NUMARASI", prescription.PrescriptionNo),
                EnabizPacketXml.Coded("RECETE_TURU", EnabizCodeSystems.PrescriptionKind, prescription.TypeCode),
                EnabizPacketXml.Optional("HEKIM_KIMLIK_NUMARASI",
                    prescription.PhysicianTckn ?? context.Physician.Tckn));

            foreach (var drug in prescription.Drugs)
            {
                element.Add(new XElement("ILAC_BILGISI",
                    EnabizPacketXml.Optional("ILAC_BARKODU", drug.Barcode),
                    EnabizPacketXml.Value("ILAC_ADI", drug.Name),
                    EnabizPacketXml.Value("KUTU_ADETI", drug.BoxCount.ToString(CultureInfo.InvariantCulture)),
                    EnabizPacketXml.OptionalCoded("ILAC_KULLANIM_SEKLI",
                        EnabizCodeSystems.DrugUsageForm, drug.UsageFormCode),
                    EnabizPacketXml.Optional("ILAC_KULLANIM_DOZU", drug.Dose),
                    EnabizPacketXml.Optional("ILAC_ACIKLAMA", drug.Description)));
            }

            prescriptions.Add(element);
        }

        yield return prescriptions;
    }
}

/// <summary>
/// 203 — Ağız ve Diş Sağlığı Veri Seti. <b>Ürünün asıl paketi.</b>
///
/// <para>Resmi alan tanımı (rehber.enabiz.gov.tr): her <c>DIS_MUDAHALE_BILGISI</c> için
/// MUDAHALE (SUT kodu), MUDAHALE_BASLANGIC_ZAMANI, MUDAHALE_BITIS_ZAMANI,
/// TEDAVI_EDILEN_DISIN_KODU (FDI) ve ISLEM_REFERANS_NUMARASI ZORUNLUDUR.
/// Ayrıca opsiyonel <c>MEVCUT_DIS_BILGISI</c> ile ağızdaki mevcut diş durumu bildirilir.</para>
/// </summary>
public sealed class Packet203Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.AgizDisSagligi203;
    protected override string PacketName => "Agiz ve Dis Sagligi Veri Seti";

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        yield return EnabizPacketXml.TakipBilgisi(context);

        var dental = new XElement("AGIZ_DIS_SAGLISI");
        var any = false;

        foreach (var procedure in context.Procedures)
        {
            // 203 diş bazlı settir; ağız geneli işlem (diş no yok) 102'ye gider.
            if (string.IsNullOrWhiteSpace(procedure.ToothNumber)) continue;

            if (string.IsNullOrWhiteSpace(procedure.SutCode))
            {
                throw new EnabizPacketException(
                    $"203 paketinde MUDAHALE zorunludur; '{procedure.Name}' işleminin SUT/SKRS kodu yok.");
            }

            dental.Add(new XElement("DIS_MUDAHALE_BILGISI",
                EnabizPacketXml.Coded("MUDAHALE", EnabizCodeSystems.Sut,
                    procedure.SutCode.Trim(), procedure.Name),
                EnabizPacketXml.Value("MUDAHALE_BASLANGIC_ZAMANI",
                    EnabizPacketXml.Format(procedure.PerformedAtLocal)),
                EnabizPacketXml.Value("MUDAHALE_BITIS_ZAMANI",
                    EnabizPacketXml.Format(procedure.EndsAtLocal ?? procedure.PerformedAtLocal)),
                EnabizPacketXml.Coded("TEDAVI_EDILEN_DISIN_KODU", EnabizCodeSystems.ToothCode,
                    EnabizPacketXml.RequireFdiTooth(procedure.ToothNumber)),
                EnabizPacketXml.Value("ISLEM_REFERANS_NUMARASI", procedure.ReferenceNo)));
            any = true;
        }

        if (!any)
            throw new EnabizPacketException("203 paketi için diş numarası olan en az bir işlem gerekir.");

        yield return dental;
    }
}

/// <summary>
/// 200 — Veri Paketi Silme. Reddedilen ya da hatalı gönderilmiş bir paketi geri alır;
/// düzeltilmiş paket ardından yeniden gönderilir.
/// </summary>
public sealed class Packet200Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.Silme200;
    protected override string PacketName => "Veri Paketi Silme";

    /// <summary>Silinecek paketin tipi; <see cref="BuildDelete"/> tarafından doldurulur.</summary>
    private EnabizPacketType _target = EnabizPacketType.HastaKayit101;

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        yield return new XElement("VERI_PAKETI_SILME",
            EnabizPacketXml.Value("SILINECEK_VERI_PAKETI",
                ((short)_target).ToString(CultureInfo.InvariantCulture)));
        yield return EnabizPacketXml.TakipBilgisi(context);
    }

    /// <summary>Silme paketi üretir: hangi paket tipinin hangi takip numarasıyla geri alınacağı.</summary>
    public XElement BuildDelete(EnabizPacketContext context, EnabizPacketType targetPacketType)
    {
        _target = targetPacketType;
        return Build(context);
    }
}

/// <summary>402 — SYS Takip No Sorgulama.</summary>
public sealed class Packet402Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.TakipNoSorgu402;
    protected override string PacketName => "SYS Takip No Sorgulama";

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        yield return EnabizPacketXml.TakipBilgisi(context);
    }
}

/// <summary>
/// 405 — Günlük Veri Sorgulama. Mutabakat için: USS'nin o gün gördüğü kayıtlar döner;
/// bizde Accepted görünüp USS'de olmayanlar yeniden kuyruğa alınır.
/// </summary>
public sealed class Packet405Builder : EnabizPacketBuilderBase
{
    public override EnabizPacketType PacketType => EnabizPacketType.GunlukVeriSorgu405;
    protected override string PacketName => "Gunluk Veri Sorgulama";

    protected override IEnumerable<XElement> BuildDataSets(EnabizPacketContext context)
    {
        yield return new XElement("GUNLUK_VERI_SORGULAMA",
            EnabizPacketXml.Value("SORGU_TARIHI", EnabizPacketXml.Format(context.VisitDate)));
    }
}
