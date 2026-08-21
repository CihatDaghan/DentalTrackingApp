namespace Dental.Integrations.Enabiz.PacketBuilders;

/// <summary>
/// Paket üreticilerinin girdisi — saf veri, EF/domain tipi taşımaz.
/// Böylece üreticiler birim testlerinde veritabanı olmadan koşturulabilir.
/// </summary>
public sealed record EnabizPacketContext
{
    /// <summary>ÇKYS tesis kodu — 101'de HIZMET_SUNUCU ve KAYIT_YERI olarak yazılır.</summary>
    public string? FacilityCode { get; init; }

    /// <summary>Tesis adı (kod sisteminin <c>value</c> alanı).</summary>
    public string? FacilityName { get; init; }

    /// <summary>KTS'de kayıtlı yazılım firmasının kodu (<c>firmaKodu</c>).</summary>
    public string? SoftwareCompanyCode { get; init; }

    /// <summary>Ziyaretin protokol numarası.</summary>
    public required string ProtocolNo { get; init; }

    /// <summary>
    /// Tesisin kendi tekil başvuru referansı (HASTANE_REFERANS_NUMARASI). 402 ile geri sorgulanır.
    /// </summary>
    public string? FacilityReferenceNo { get; init; }

    /// <summary>101'den dönen sistem takip numarası; bağımlı paketlerde ZORUNLUDUR.</summary>
    public string? SysTakipNo { get; init; }

    /// <summary>Yerel (TR) paket üretim zamanı.</summary>
    public DateTime LocalTimestamp { get; init; }

    /// <summary>Başvuru kabul zamanı (KABUL_ZAMANI).</summary>
    public DateTime AdmissionAtLocal { get; init; }

    public required EnabizPatient Patient { get; init; }
    public required EnabizPhysician Physician { get; init; }

    public DateOnly VisitDate { get; init; }

    /// <summary>SKRS klinik kodu; ağız-diş sağlığı için tenant ayarından gelir.</summary>
    public string ClinicCode { get; init; } = EnabizCodeSystems.DefaultDentalClinicCode;

    /// <summary>SKRS hasta tipi kodu (ayaktan/yatan). Ayaktan hasta varsayılan.</summary>
    public string PatientTypeCode { get; init; } = "1";

    /// <summary>SKRS sosyal güvence durumu kodu. Özel klinikte ücretli/kendi ödeyen varsayılan.</summary>
    public string SocialSecurityCode { get; init; } = "0";

    /// <summary>SKRS vaka türü kodu (normal vaka varsayılan).</summary>
    public string CaseTypeCode { get; init; } = "1";

    public IReadOnlyList<EnabizProcedure> Procedures { get; init; } = [];
    public IReadOnlyList<EnabizDiagnosis> Diagnoses { get; init; } = [];
    public IReadOnlyList<EnabizPrescription> Prescriptions { get; init; } = [];
}

/// <param name="Tckn">TC Kimlik No (HASTA_KIMLIK_NUMARASI).</param>
/// <param name="ForeignPatientId">
/// Yabancı hasta kimlik numarası (Bakanlıkça verilen 99 ile başlayan numara) ya da YUPASS numarası.
/// Resmi 101 tanımında <c>HASTA_KIMLIK_NUMARASI</c> ZORUNLUDUR; yabancı hastada bu alanı pasaport
/// değil, bu numara karşılar. Pasaport ayrıca <c>PASAPORT_NO</c> olarak yazılır.
/// </param>
/// <param name="Gender">SKRS cinsiyet kodu.</param>
/// <param name="NationalityCode">SKRS uyruk kodu.</param>
public sealed record EnabizPatient(
    string? Tckn,
    string? PassportNo,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string Gender,
    string NationalityCode = "1",
    string? Address = null,
    string? District = null,
    string? Phone = null,
    string? Email = null,
    string? ForeignPatientId = null);

/// <param name="Tckn">Hekimin TC kimlik numarası — USS'de hekim bu numarayla tanımlıdır.</param>
public sealed record EnabizPhysician(string? Tckn, string FullName, string? DiplomaNo = null);

/// <param name="ToothNumber">FDI diş numarası (iki hane); ağız geneli işlemde null.</param>
/// <param name="SutCode">SUT/SKRS işlem (MUDAHALE) kodu.</param>
/// <param name="ReferenceNo">İşlemin tesis içi tekil referansı (ISLEM_REFERANS_NUMARASI) — zorunlu.</param>
public sealed record EnabizProcedure(
    string? ToothNumber,
    string? SutCode,
    string Name,
    DateTime PerformedAtLocal,
    string ReferenceNo,
    DateTime? EndsAtLocal = null,
    int Quantity = 1,
    string? DiagnosisIcdCode = null,
    string? Surfaces = null,
    byte? RootCanalCount = null);

/// <param name="IcdCode">ICD-10 tanı kodu.</param>
/// <param name="KindCode">SKRS tanı türü kodu (kesin/ön tanı).</param>
public sealed record EnabizDiagnosis(string IcdCode, string? Name = null, string KindCode = "1");

public sealed record EnabizPrescription(
    string PrescriptionNo,
    DateTime IssuedAtLocal,
    string? PhysicianTckn,
    string TypeCode,
    IReadOnlyList<EnabizPrescribedDrug> Drugs);

public sealed record EnabizPrescribedDrug(
    string? Barcode,
    string Name,
    int BoxCount,
    string? Dose = null,
    string? UsageFormCode = null,
    string? Description = null);

/// <summary>
/// Paketlerde kullanılan SKRS kod sistemi GUID'leri — Bakanlığın resmi paket detayından alınmıştır
/// (rehber.enabiz.gov.tr/Home/PaketDetay). Kod DEĞERLERİ SKRS'den senkronlanır; buradakiler
/// yalnız hangi kod listesine bağlı olduğunu söyleyen sistem kimlikleridir.
/// </summary>
public static class EnabizCodeSystems
{
    public const string MessageType = "0a9ba485-e7e0-4abb-9c86-0a14fd364bb8";
    /// <summary>Sağlık kurumları (ÇKYS kurum kodu) — HIZMET_SUNUCU / KAYIT_YERI / healthcareProvider.</summary>
    public const string HealthcareProvider = "c3eade04-4f91-5dab-e043-14031b0ac9f9";
    public const string Gender = "784d0f4f-0603-4425-937f-1a3941fc3a1f";
    public const string Nationality = "d650777a-3d4d-a259-e040-7c0a01167a83";
    public const string PatientType = "4f4fd85e-6f52-4c38-a302-6d5e3d6dc1c4";
    public const string AddressLevel = "aa0e83ba-e9db-4817-80da-577fd6a17373";
    public const string ClinicCode = "c04bee57-c5d4-443d-e040-7b0a6f146a3d";
    public const string SocialSecurity = "530da738-2be0-4adc-a7c1-aca18c66a3f8";
    public const string CaseType = "46380e82-d8b1-407d-9554-255d95a9f959";
    public const string DiagnosisKind = "55894edb-1a8c-4f7f-a447-0119e61c14f1";
    /// <summary>ICD-10 — 103 paketindeki resmi kod sistemi.</summary>
    public const string Icd10 = "c3eaabad-8c4c-56ee-e043-14031b0a5530";
    /// <summary>SUT/işlem kodu — 203'te MUDAHALE olarak kullanılır.</summary>
    public const string Sut = "c3eb10bb-27b9-6344-e043-14031b0a5679";
    /// <summary>Diş kodu (FDI) — TEDAVI_EDILEN_DISIN_KODU ve MEVCUT_DIS_KODU ortak kod sistemi.</summary>
    public const string ToothCode = "d5743829-cf07-4dda-bfb5-69439599628a";
    public const string ToothStatus = "633f6442-19f4-419c-a7c9-9b2e0bd16a00";
    public const string ProcedureKind = "d03e562d-252e-451f-9a80-98d48b47c2f2";
    public const string PrescriptionKind = "c2fbe9bb-f6b3-4cb5-8670-47890ed7ed4b";
    public const string DrugUsageForm = "32d57611-4928-46da-afac-624aaaa388d8";

    /// <summary>Ağız ve diş sağlığı kliniği — SKRS klinik kod listesinden.</summary>
    public const string DefaultDentalClinicCode = "5300";
}
