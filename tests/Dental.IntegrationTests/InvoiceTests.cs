using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// F aşaması "bitti" kriterleri: belge tipi karar doğruluğu (e-Arşiv SATIS / ISTISNA 334 /
/// TEVKIFAT 616 / yetki belgesi ve estetik engelleri), Draft→UblGenerated'da numara+ETTN ataması,
/// UBL'in MediaFile'a yazılması, fake sürücüyle uçtan uca gönderim, numara atomikliği
/// (paralel üretimde ardışık ve benzersiz) ve kiracı izolasyonu.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class InvoiceTests(ApiFixture fx)
{
    private const string DemoEmail = "demo@dental.local";
    private const string DemoPassword = "Demo!2026";

    // ---- Yardımcılar ----

    private async Task<string> LoginAsync(string email, string password)
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private Task<string> LoginDemoAsync() => LoginAsync(DemoEmail, DemoPassword);

    private static HttpRequestMessage Req(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<JsonElement> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await fx.Client.SendAsync(Req(method, url, token, body));
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"{method} {url} → {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    private async Task<long> CreatePatientAsync(string token, object request)
    {
        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/patients", token, request);
        return dto.GetProperty("id").GetInt64();
    }

    private async Task<long> FindDefinitionAsync(string token, string code)
    {
        var page = await SendOkAsync(HttpMethod.Get, $"/api/v1/treatment-catalog?search={code}", token);
        return page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == code)
            .GetProperty("id").GetInt64();
    }

    /// <summary>Faturalanabilmesi için tedaviyi doğrudan 'Yapıldı' durumunda ekler.</summary>
    private async Task<long> AddDoneTreatmentAsync(
        string token, long patientId, long definitionId, decimal price, string? tooth = "16")
    {
        var added = await SendOkAsync(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[]
            {
                new
                {
                    treatmentDefinitionId = definitionId,
                    toothNumber = tooth,
                    status = (byte)TreatmentRecordStatus.Done,
                    price,
                },
            },
        });
        return added[0].GetProperty("id").GetInt64();
    }

    private async Task<long> CreateCompanyAsync(string token, string name, string vkn, bool eInvoiceUser)
    {
        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/companies", token, new
        {
            name,
            vkn,
            taxOffice = "Kadıköy",
            address = "Merkez Mah. No:1",
            email = "muhasebe@example.com",
            isEInvoiceUser = eInvoiceUser,
        });
        return dto.GetProperty("id").GetInt64();
    }

    // ---- 1) Karar motoru: şirket kiracı + bireysel hasta → e-Arşiv SATIS ----

    [Fact]
    public async Task Preview_CompanyTenantIndividualPatient_ResolvesToEArchiveSale()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, new
        {
            firstName = "Ayşe",
            lastName = "Yerli",
            tckn = "10000000078",
            email = "ayse.yerli@example.com",
            city = "İstanbul",
            district = "Kadıköy",
        });
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 2000m);

        var preview = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices/preview", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
        });

        Assert.Equal((int)InvoiceDocumentKind.EArsiv, preview.GetProperty("documentKind").GetInt32());
        Assert.Equal("EARSIVFATURA", preview.GetProperty("profileId").GetString());
        Assert.Equal("SATIS", preview.GetProperty("typeCode").GetString());
        Assert.True(preview.GetProperty("canCreate").GetBoolean());
        Assert.Empty(preview.GetProperty("errors").EnumerateArray());
        Assert.Contains("e-Arşiv", preview.GetProperty("rationale").GetString());

        // Sağlık hizmeti KDV %10 → 2000 + 200 = 2200.
        var totals = preview.GetProperty("totals");
        Assert.Equal(2000m, totals.GetProperty("subTotal").GetDecimal());
        Assert.Equal(200m, totals.GetProperty("vatTotal").GetDecimal());
        Assert.Equal(2200m, totals.GetProperty("payableAmount").GetDecimal());
        Assert.Equal(10m, preview.GetProperty("lines")[0].GetProperty("vatRate").GetDecimal());
    }

    // ---- 2) Yabancı hasta + yetki belgesi → ISTISNA 334 ----

    [Fact]
    public async Task Preview_ForeignPatientWithAuthorization_ResolvesToExemption334()
    {
        var token = await LoginDemoAsync();
        await SetHealthTourismAuthorizationAsync(true);

        var patientId = await CreateForeignPatientAsync(token, "Hans", "Muller");
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 5000m);

        var preview = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices/preview", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
            isForeignPatient = true,
        });

        Assert.Equal("ISTISNA", preview.GetProperty("typeCode").GetString());
        Assert.Equal("334", preview.GetProperty("exemptionCode").GetString());
        Assert.True(preview.GetProperty("canCreate").GetBoolean());
        // İstisnada KDV %0 → ödenecek tutar brüt tutara eşit.
        var totals = preview.GetProperty("totals");
        Assert.Equal(0m, totals.GetProperty("vatTotal").GetDecimal());
        Assert.Equal(5000m, totals.GetProperty("payableAmount").GetDecimal());
        // GİB: TCKN'si olmayan yabancı GERÇEK KİŞİDE 11 adet 1 (schemeID="TCKN");
        // 10 haneli "2222222222" yabancı tüzel kişi içindir.
        Assert.Equal("11111111111", preview.GetProperty("buyerTcknVkn").GetString());
        // Snapshot SKRS alfa-3 tutar; alpha-2'ye çevrim UBL üretiminde yapılır.
        Assert.Equal("DEU", preview.GetProperty("buyerNationality").GetString());
    }

    // ---- 2b) 334 belgesinin UBL çıktısı: kimlik, uyruk, pasaport, istisna kodu ----

    [Fact]
    public async Task Exemption334_UblCarriesForeignIdentityNationalityAndExemption()
    {
        var token = await LoginDemoAsync();
        await SetHealthTourismAuthorizationAsync(true);

        var patientId = await CreateForeignPatientAsync(token, "Giulia", "Rossi", "ITA");
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 7000m);

        var created = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
            isForeignPatient = true,
        });
        var invoiceId = created.GetProperty("id").GetInt64();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/generate-ubl", token);

        var ublResponse = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}/ubl", token));
        var xml = XDocument.Parse(await ublResponse.Content.ReadAsStringAsync());
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        Assert.Equal("ISTISNA", xml.Root!.Element(cbc + "InvoiceTypeCode")!.Value);

        var party = xml.Root.Element(cac + "AccountingCustomerParty")!.Element(cac + "Party")!;
        var identifications = party.Elements(cac + "PartyIdentification").Select(p => p.Element(cbc + "ID")!).ToList();
        // Schematron: schemeID='TCKN' ⇒ 11 hane. Yabancı gerçek kişide 11 adet 1.
        Assert.Contains(identifications, i => i.Attribute("schemeID")!.Value == "TCKN" && i.Value == "11111111111");
        Assert.Contains(identifications, i => i.Attribute("schemeID")!.Value == "PASAPORTNO");

        var person = party.Element(cac + "Person")!;
        // Hasta kartındaki SKRS alfa-3 (ITA) UBL'de ISO alpha-2'ye (IT) çevrilmelidir.
        Assert.Equal("IT", person.Element(cbc + "NationalityID")!.Value);
        Assert.NotNull(person.Element(cac + "IdentityDocumentReference")?.Element(cbc + "ID")?.Value);

        // İstisna kodu + gerekçe metni TaxCategory altında, KDV 0.
        var category = xml.Root.Element(cac + "TaxTotal")!.Element(cac + "TaxSubtotal")!.Element(cac + "TaxCategory")!;
        Assert.Equal("334", category.Element(cbc + "TaxExemptionReasonCode")!.Value);
        Assert.False(string.IsNullOrWhiteSpace(category.Element(cbc + "TaxExemptionReason")!.Value));
        // Türkiye'ye son giriş tarihi UBL'de tanımlı alanı olmadığı için Note'ta taşınır.
        Assert.Contains(xml.Root.Elements(cbc + "Note"), n => n.Value.Contains("son giriş"));
    }

    // ---- 3) Yetki belgesi kapalıyken 334 reddedilir ----

    [Fact]
    public async Task Preview_ForeignPatientWithoutAuthorization_IsRejected()
    {
        var token = await LoginDemoAsync();
        await SetHealthTourismAuthorizationAsync(false);
        try
        {
            var patientId = await CreateForeignPatientAsync(token, "Marie", "Dubois", "FRA");
            var definitionId = await FindDefinitionAsync(token, "DIS-101");
            var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 3000m);

            var preview = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices/preview", token, new
            {
                patientId,
                treatmentRecordIds = new[] { recordId },
                isForeignPatient = true,
            });

            Assert.False(preview.GetProperty("canCreate").GetBoolean());
            Assert.Contains(preview.GetProperty("errors").EnumerateArray().Select(e => e.GetString()),
                e => e!.Contains("yetki belgesi"));

            // Oluşturma da reddedilmeli.
            var create = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/invoices", token, new
            {
                patientId,
                treatmentRecordIds = new[] { recordId },
                isForeignPatient = true,
            }));
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        }
        finally
        {
            await SetHealthTourismAuthorizationAsync(true);
        }
    }

    // ---- 4) Estetik kalem + 334 birleşemez ----

    [Fact]
    public async Task Preview_ForeignPatientWithAestheticLine_IsRejected()
    {
        var token = await LoginDemoAsync();
        await SetHealthTourismAuthorizationAsync(true);

        var patientId = await CreateForeignPatientAsync(token, "Olga", "Ivanova", "RUS");
        // DIS-164 "Estetik temizlik paketi" — Beyazlatma ve Estetik kategorisi (KDV %20).
        var aestheticId = await FindDefinitionAsync(token, "DIS-164");
        var recordId = await AddDoneTreatmentAsync(token, patientId, aestheticId, 4500m, tooth: null);

        var preview = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices/preview", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
            isForeignPatient = true,
        });

        Assert.False(preview.GetProperty("canCreate").GetBoolean());
        Assert.Contains(preview.GetProperty("errors").EnumerateArray().Select(e => e.GetString()),
            e => e!.Contains("estetik", StringComparison.OrdinalIgnoreCase));
        Assert.True(preview.GetProperty("lines")[0].GetProperty("isAesthetic").GetBoolean());
    }

    // ---- 5) Kamu kurumu → TEVKIFAT 616 (5/10) ----

    [Fact]
    public async Task Preview_GovernmentCompanyBuyer_ResolvesToWithholding616()
    {
        var token = await LoginDemoAsync();
        var companyId = await CreateCompanyAsync(token, $"Kamu Hastanesi {Guid.NewGuid():N}"[..30], "5555555550", false);
        var patientId = await CreatePatientAsync(token, new
        {
            firstName = "Kurum",
            lastName = "Hastası",
            companyId,
            city = "Ankara",
            district = "Çankaya",
        });
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 1000m);

        var preview = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices/preview", token, new
        {
            companyId,
            treatmentRecordIds = new[] { recordId },
            isGovernmentBuyer = true,
        });

        Assert.Equal("TEVKIFAT", preview.GetProperty("typeCode").GetString());
        Assert.Equal("616", preview.GetProperty("withholdingCode").GetString());
        Assert.Equal(50m, preview.GetProperty("withholdingPercent").GetDecimal());

        // 1000 + %10 KDV = 1100; tevkifat 5/10 → 50 TL alıcıda kalır → ödenecek 1050.
        var totals = preview.GetProperty("totals");
        Assert.Equal(100m, totals.GetProperty("vatTotal").GetDecimal());
        Assert.Equal(50m, totals.GetProperty("withholdingTotal").GetDecimal());
        Assert.Equal(1050m, totals.GetProperty("payableAmount").GetDecimal());
    }

    // ---- 6) Uçtan uca: create → generate-ubl → send (fake) → Succeeded ----

    [Fact]
    public async Task FullFlow_CreateGenerateSend_ReachesSucceededAndArchivesUbl()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, new
        {
            firstName = "Uçtan",
            lastName = "Uca",
            tckn = "10000000214",
            email = "uctan.uca@example.com",
            city = "İstanbul",
            district = "Üsküdar",
            address = "Test Mah. 1. Sk. No:2",
        });
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 1500m);

        var created = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
        });
        var invoiceId = created.GetProperty("id").GetInt64();
        Assert.Equal((int)InvoiceStatus.Draft, created.GetProperty("status").GetInt32());
        Assert.True(created.GetProperty("invoiceNumber").ValueKind == JsonValueKind.Null);
        Assert.True(created.GetProperty("ettn").ValueKind == JsonValueKind.Null);

        var generated = await SendOkAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/generate-ubl", token);
        Assert.Equal((int)InvoiceStatus.UblGenerated, generated.GetProperty("status").GetInt32());
        var number = generated.GetProperty("invoiceNumber").GetString()!;
        Assert.Equal(16, number.Length);
        Assert.StartsWith($"DIS{DateTime.UtcNow.Year}", number);
        Assert.NotEqual(Guid.Empty, generated.GetProperty("ettn").GetGuid());
        Assert.True(generated.GetProperty("ublFileId").GetInt64() > 0);

        // UBL akışı gerçekten arşivlenmiş ve UBL-TR başlıkları doğru.
        var ublResponse = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}/ubl", token));
        Assert.Equal(HttpStatusCode.OK, ublResponse.StatusCode);
        var xml = XDocument.Parse(await ublResponse.Content.ReadAsStringAsync());
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        Assert.Equal("Invoice", xml.Root!.Name.LocalName);
        Assert.Equal("TR1.2", xml.Root.Element(cbc + "CustomizationID")!.Value);
        Assert.Equal(number, xml.Root.Element(cbc + "ID")!.Value);
        Assert.Equal(generated.GetProperty("ettn").GetGuid().ToString("D"), xml.Root.Element(cbc + "UUID")!.Value);
        Assert.Equal("EARSIVFATURA", xml.Root.Element(cbc + "ProfileID")!.Value);

        var sent = await SendOkAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/send", token);
        // Fake sürücü belgeyi kabul eder → SentToIntegrator.
        Assert.Equal((int)InvoiceStatus.SentToIntegrator, sent.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(sent.GetProperty("integratorRefId").GetString()));

        // Durum yoklama job'ının yaptığı işi doğrudan çağır: fake sürücü Succeeded döner.
        await PollStatusesAsync();

        var final = await SendOkAsync(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}", token);
        Assert.Equal((int)InvoiceStatus.Succeeded, final.GetProperty("status").GetInt32());

        // Durum geçmişi tam zinciri içermeli.
        var statuses = final.GetProperty("statusLogs").EnumerateArray()
            .Select(l => l.GetProperty("toStatus").GetInt32()).ToList();
        Assert.Equal(
            new[]
            {
                (int)InvoiceStatus.Draft, (int)InvoiceStatus.UblGenerated, (int)InvoiceStatus.Queued,
                (int)InvoiceStatus.SentToIntegrator, (int)InvoiceStatus.Succeeded,
            },
            statuses);
    }

    // ---- 7) Numara atomikliği: 5 paralel generate-ubl → ardışık ve benzersiz ----

    [Fact]
    public async Task GenerateUbl_InParallel_ProducesUniqueSequentialNumbers()
    {
        var token = await LoginDemoAsync();
        var definitionId = await FindDefinitionAsync(token, "DIS-101");

        var invoiceIds = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var patientId = await CreatePatientAsync(token, new
            {
                firstName = "Paralel",
                lastName = $"Numara{i}",
                city = "İstanbul",
                district = "Kadıköy",
            });
            var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 100m + i);
            var created = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices", token, new
            {
                patientId,
                treatmentRecordIds = new[] { recordId },
            });
            invoiceIds.Add(created.GetProperty("id").GetInt64());
        }

        var results = await Task.WhenAll(invoiceIds.Select(id =>
            fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/invoices/{id}/generate-ubl", token))));

        var numbers = new List<string>();
        foreach (var response in results)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"generate-ubl → {(int)response.StatusCode}: {content}");
            numbers.Add(JsonDocument.Parse(content).RootElement.GetProperty("invoiceNumber").GetString()!);
        }

        Assert.Equal(5, numbers.Distinct().Count());
        // Sıra numaraları (son 9 hane) ardışık olmalı — atomik UPDATE...OUTPUT boşluk bırakmaz.
        var sequence = numbers.Select(n => long.Parse(n[^9..])).OrderBy(n => n).ToList();
        Assert.Equal(sequence[0] + 4, sequence[^1]);
    }

    // ---- 8) Kiracı izolasyonu ----

    [Fact]
    public async Task Invoice_IsNotVisibleToAnotherTenant()
    {
        var demoToken = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(demoToken, new
        {
            firstName = "İzolasyon",
            lastName = "Testi",
            city = "İstanbul",
            district = "Kadıköy",
        });
        var definitionId = await FindDefinitionAsync(demoToken, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(demoToken, patientId, definitionId, 750m);
        var created = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices", demoToken, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
        });
        var invoiceId = created.GetProperty("id").GetInt64();

        var (otherEmail, otherPassword) = await EnsureSecondTenantAsync();
        var otherToken = await LoginAsync(otherEmail, otherPassword);

        var read = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var list = await SendOkAsync(HttpMethod.Get, "/api/v1/invoices?pageSize=100", otherToken);
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("id").GetInt64() == invoiceId);
    }

    // ---- 9) e-SMM: şahıs hekim kiracısı UBL CreditNote üretir ----

    [Fact]
    public async Task SoleProprietorTenant_ProducesEsmmCreditNote()
    {
        var (email, password) = await EnsureSoleProprietorTenantAsync();
        var token = await LoginAsync(email, password);

        var patientId = await CreatePatientAsync(token, new
        {
            firstName = "Serbest",
            lastName = "Meslek",
            tckn = "10000000146",
            city = "İstanbul",
            district = "Beşiktaş",
        });
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 1200m);

        var preview = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices/preview", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
        });
        Assert.Equal((int)InvoiceDocumentKind.ESmm, preview.GetProperty("documentKind").GetInt32());
        // (c)-3: bireysel hastaya GV stopajı kesilmez.
        Assert.Equal(0m, preview.GetProperty("totals").GetProperty("gvStopajTotal").GetDecimal());

        var created = await SendOkAsync(HttpMethod.Post, "/api/v1/invoices", token, new
        {
            patientId,
            treatmentRecordIds = new[] { recordId },
        });
        var invoiceId = created.GetProperty("id").GetInt64();
        var generated = await SendOkAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/generate-ubl", token);
        Assert.StartsWith("SMM", generated.GetProperty("invoiceNumber").GetString());

        var ublResponse = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}/ubl", token));
        var xml = XDocument.Parse(await ublResponse.Content.ReadAsStringAsync());
        // e-SMM UBL Invoice DEĞİL CreditNote'tur — sık yapılan hata burada yakalanır.
        Assert.Equal("CreditNote", xml.Root!.Name.LocalName);
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        Assert.Null(xml.Root.Element(cbc + "InvoiceTypeCode"));
        Assert.Null(xml.Root.Element(cbc + "CreditNoteTypeCode"));

        var sent = await SendOkAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/send", token);
        Assert.Equal((int)InvoiceStatus.SentToIntegrator, sent.GetProperty("status").GetInt32());
    }

    // ---- 10) Aynı tedavi iki kez faturalanamaz ----

    [Fact]
    public async Task Create_SameTreatmentTwice_IsRejected()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, new
        {
            firstName = "Mükerrer",
            lastName = "Fatura",
            city = "İstanbul",
            district = "Kadıköy",
        });
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        var recordId = await AddDoneTreatmentAsync(token, patientId, definitionId, 900m);

        await SendOkAsync(HttpMethod.Post, "/api/v1/invoices", token,
            new { patientId, treatmentRecordIds = new[] { recordId } });

        var second = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/invoices", token,
            new { patientId, treatmentRecordIds = new[] { recordId } }));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // ---- 11) GİB mükellef aynası sorgusu ----

    [Fact]
    public async Task GibTaxpayerLookup_ReturnsSeededEntryAndUnknown()
    {
        var token = await LoginDemoAsync();

        var known = await SendOkAsync(HttpMethod.Get, "/api/v1/gib-taxpayers/9876543210", token);
        Assert.True(known.GetProperty("isEInvoiceUser").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(known.GetProperty("alias").GetString()));

        var unknown = await SendOkAsync(HttpMethod.Get, "/api/v1/gib-taxpayers/0000000000", token);
        Assert.False(unknown.GetProperty("isEInvoiceUser").GetBoolean());
    }

    // ---- 12) GİB mükellef senkron job'ı fake sürücüden liste çeker ----

    [Fact]
    public async Task GibTaxpayerSyncJob_UpsertsFromProviderList()
    {
        using var jobScope = fx.Services.CreateScope();
        var jobs = jobScope.ServiceProvider.GetRequiredService<Dental.Jobs.EDocumentJobs>();
        await jobs.SyncGibTaxpayersAsync();

        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Fake sürücünün örnek listesindeki kamu kaydı ayna tablosuna işlenmiş olmalı.
        Assert.True(await db.GibTaxpayers.AnyAsync(g => g.Vkn == "5555555555"));
    }

    // ---- Ortam yardımcıları ----

    private async Task<long> CreateForeignPatientAsync(
        string token, string firstName, string lastName, string nationality = "DEU")
    {
        return await CreatePatientAsync(token, new
        {
            firstName,
            lastName,
            identityType = (byte)IdentityType.Passport,
            passportNo = $"P{Random.Shared.Next(1000000, 9999999)}",
            nationalityCode = nationality,
            lastEntryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            email = $"{firstName.ToLowerInvariant()}@example.com",
            city = "İstanbul",
            district = "Şişli",
        });
    }

    /// <summary>Demo kiracının sağlık turizmi yetki belgesi bayrağını doğrudan veritabanında ayarlar.</summary>
    private async Task SetHealthTourismAuthorizationAsync(bool value)
    {
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await DemoTenantIdAsync(db);
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId);
        tenant.HasHealthTourismAuthorization = value;
        await db.SaveChangesAsync();
    }

    private static async Task<long> DemoTenantIdAsync(AppDbContext db) =>
        await db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == DemoEmail.ToUpperInvariant())
            .Select(u => u.TenantId!.Value)
            .FirstAsync();

    private async Task PollStatusesAsync()
    {
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await DemoTenantIdAsync(db);

        var scopes = fx.Services.GetRequiredService<ITenantScopeFactory>();
        using var tenantScope = scopes.CreateScope(tenantId);
        var dispatcher = tenantScope.ServiceProvider
            .GetRequiredService<Dental.Application.Invoices.IEDocumentDispatcher>();
        await dispatcher.PollStatusesAsync();
    }

    private async Task<(string Email, string Password)> EnsureSoleProprietorTenantAsync()
    {
        using var scope = fx.Services.CreateScope();
        var setter = (ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        setter.Set(null, null, null, isSuperAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("InvoiceTests");
        await Dental.Infrastructure.Seed.EInvoiceSeed.EnsureSoleProprietorTenantAsync(
            scope.ServiceProvider, db, logger);
        return ("hekim@dental.local", DemoPassword);
    }

    private async Task<(string Email, string Password)> EnsureSecondTenantAsync()
    {
        const string email = "izole@dental.local";
        using var scope = fx.Services.CreateScope();
        var setter = (ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        setter.Set(null, null, null, isSuperAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant()))
        {
            var provisioning = scope.ServiceProvider
                .GetRequiredService<Dental.Application.Tenants.ITenantProvisioningService>();
            await provisioning.CreateAsync(new Dental.Application.Tenants.CreateTenantRequest(
                ClinicName: "İzole Klinik",
                LegalType: TenantLegalType.Company,
                AdminEmail: email,
                AdminFirstName: "İzole",
                AdminLastName: "Sahip",
                AdminPassword: DemoPassword,
                TaxNumber: "2222222220"));
        }

        return (email, DemoPassword);
    }
}
