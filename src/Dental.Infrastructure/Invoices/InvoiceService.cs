using System.Text;
using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Invoices;
using Dental.Application.Media;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.EDocument.Ubl;
using Dental.EDocument.Ubl.Builders;
using Dental.EDocument.Ubl.Models;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Invoices;

/// <summary>
/// e-Belge iş akışının tek sahibi. Durum makinesi geçişleri ve numara/ETTN ataması
/// yalnız burada yapılır; her geçiş <see cref="InvoiceStatusLog"/>'a yazılır.
/// Gerçek gönderim <see cref="IEDocumentDispatcher"/>'a devredilir (job ile ortak yol).
/// </summary>
public sealed class InvoiceService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IMediaService media,
    INumberSequenceService numbers,
    ITaxConfigService taxConfig,
    ITcknProtector tcknProtector,
    IEDocumentDispatcher dispatcher,
    IIntegrationProviderFactory providerFactory,
    IValidator<InvoiceDraftRequest> draftValidator,
    IValidator<InvoiceCancelRequest> cancelValidator,
    ILogger<InvoiceService> logger) : IInvoiceService
{
    /// <summary>Fatura/e-Arşiv seri kodu (16 hanenin ilk 3'ü).</summary>
    public const string InvoiceSerial = "DIS";

    /// <summary>e-SMM seri kodu.</summary>
    public const string SmmSerial = "SMM";

    // ---- Önizleme ----

    public async Task<InvoicePreviewDto> PreviewAsync(InvoiceDraftRequest request, CancellationToken ct = default)
    {
        var draft = await BuildDraftAsync(request, ct);
        return ToPreview(draft);
    }

    // ---- Oluşturma ----

    public async Task<InvoiceDto> CreateAsync(InvoiceDraftRequest request, CancellationToken ct = default)
    {
        var draft = await BuildDraftAsync(request, ct);
        if (draft.Errors.Count > 0)
            throw new InvalidOperationException("Fatura oluşturulamaz: " + string.Join(" ", draft.Errors));

        var now = clock.UtcNow;
        var issueLocal = TrLocal(now);
        var decision = draft.Decision;

        var invoice = new Invoice
        {
            ClinicId = draft.ClinicId,
            DocumentKind = MapKind(decision.DocumentKind),
            ProfileId = decision.ProfileId,
            TypeCode = decision.TypeCode,
            Status = InvoiceStatus.Draft,
            IssueDate = DateOnly.FromDateTime(issueLocal),
            IssueTime = TimeOnly.FromDateTime(issueLocal),
            CustomerType = draft.CustomerType,
            PatientId = draft.Patient?.Id,
            CompanyId = draft.Company?.Id,
            BuyerName = draft.BuyerName,
            BuyerTcknVkn = draft.BuyerTcknVkn,
            BuyerPassportNo = draft.BuyerPassportNo,
            BuyerNationality = draft.BuyerNationality,
            BuyerLastEntryDate = draft.BuyerLastEntryDate,
            BuyerAddress = draft.BuyerAddress,
            BuyerCity = draft.BuyerCity,
            BuyerDistrict = draft.BuyerDistrict,
            BuyerEmail = draft.BuyerEmail,
            BuyerTaxOffice = draft.BuyerTaxOffice,
            BuyerAlias = draft.BuyerAlias,
            SubTotal = draft.Totals.SubTotal,
            DiscountTotal = draft.Totals.DiscountTotal,
            VatTotal = draft.Totals.VatTotal,
            WithholdingTotal = draft.Totals.WithholdingTotal,
            GvStopajTotal = draft.Totals.GvStopajTotal,
            PayableAmount = draft.Totals.PayableAmount,
            ExemptionCode = decision.ExemptionCode,
            ExemptionReason = decision.ExemptionReason,
            WithholdingCode = decision.WithholdingCode,
            SourceInvoiceId = draft.SourceInvoice?.Id,
        };

        var seq = 0;
        foreach (var line in draft.Lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                SeqNo = ++seq,
                TreatmentRecordId = line.TreatmentRecordId,
                ItemName = line.ItemName,
                Quantity = line.Quantity,
                UnitCode = line.UnitCode,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount,
                VatRate = line.VatRate,
                VatAmount = line.VatAmount,
                LineTotal = line.LineTotal,
                IsAesthetic = line.IsAesthetic,
            });
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        // Tedavi kayıtları faturaya bağlanır (aynı kalem iki kez faturalanmasın).
        var recordIds = invoice.Lines.Where(l => l.TreatmentRecordId is not null)
            .ToDictionary(l => l.TreatmentRecordId!.Value, l => l.Id);
        if (recordIds.Count > 0)
        {
            var records = await db.TreatmentRecords.Where(r => recordIds.Keys.Contains(r.Id)).ToListAsync(ct);
            foreach (var record in records)
                record.InvoiceLineId = recordIds[record.Id];
        }

        // Alıcı kurumun GİB mükellefiyet bayrağı aynadan tazelenmişse kalıcı yaz.
        if (draft.Company is { } company && draft.RefreshedCompanyEInvoiceUser is { } refreshed)
        {
            var tracked = await db.Companies.FirstAsync(c => c.Id == company.Id, ct);
            tracked.IsEInvoiceUser = refreshed;
            if (draft.BuyerAlias is not null) tracked.EInvoiceAlias = draft.BuyerAlias;
        }

        AddStatusLog(invoice, from: null, to: InvoiceStatus.Draft, detail: draft.Rationale);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Fatura taslağı oluşturuldu. Id={Id} Tip={Kind}/{TypeCode} Tutar={Amount}",
            invoice.Id, invoice.DocumentKind, invoice.TypeCode, invoice.PayableAmount);
        return await GetAsync(invoice.Id, ct);
    }

    // ---- Draft → UblGenerated ----

    public async Task<InvoiceDto> GenerateUblAsync(long id, CancellationToken ct = default)
    {
        var invoice = await LoadWithLinesAsync(id, ct);
        if (invoice.Status != InvoiceStatus.Draft)
            throw new InvalidOperationException(
                $"UBL yalnız Draft durumundaki belgeden üretilir (mevcut durum: {invoice.Status}).");

        var tenantId = RequireTenantId();
        var year = invoice.IssueDate.Year;
        var serial = invoice.DocumentKind == InvoiceDocumentKind.ESmm ? SmmSerial : InvoiceSerial;
        var sequenceType = invoice.DocumentKind switch
        {
            InvoiceDocumentKind.EFatura => NumberSequenceType.InvoiceEFatura,
            InvoiceDocumentKind.EArsiv => NumberSequenceType.InvoiceEArsiv,
            _ => NumberSequenceType.ESmm,
        };

        // (c)-1: numara + ETTN tam bu geçişte atanır ve bir daha değişmez.
        var next = await numbers.NextAsync(tenantId, sequenceType, serial, year, ct);
        invoice.Serial = serial;
        invoice.InvoiceNumber = $"{serial}{year}{next:D9}";
        invoice.Ettn = Guid.NewGuid();

        var model = await BuildModelAsync(invoice, ct);
        var builder = SelectBuilder(model.Kind);
        var xml = builder.BuildXmlString(model);

        var file = await media.SaveGeneratedAsync(new GeneratedFileRequest(
            invoice.ClinicId,
            invoice.PatientId,
            MediaCategory.InvoiceUbl,
            $"{invoice.InvoiceNumber}.xml",
            "application/xml",
            Encoding.UTF8.GetBytes(xml),
            Description: $"{invoice.DocumentKind} {invoice.TypeCode} UBL — {invoice.InvoiceNumber}"), ct);

        invoice.UblFileId = file.Id;
        Transition(invoice, InvoiceStatus.UblGenerated,
            $"Belge no {invoice.InvoiceNumber}, ETTN {invoice.Ettn}. UBL MediaFile #{file.Id} (SHA-256 {file.Sha256}).");
        await db.SaveChangesAsync(ct);

        logger.LogInformation("UBL üretildi. Id={Id} No={No} Ettn={Ettn}", invoice.Id, invoice.InvoiceNumber, invoice.Ettn);
        return await GetAsync(invoice.Id, ct);
    }

    // ---- UblGenerated → Queued (→ gönderim) ----

    public async Task<InvoiceDto> SendAsync(long id, bool sendNow = true, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        if (invoice.Status is not (InvoiceStatus.UblGenerated or InvoiceStatus.Error or InvoiceStatus.ManualReview))
            throw new InvalidOperationException(
                $"Gönderim yalnız UblGenerated/Error/ManualReview durumundan başlatılır (mevcut durum: {invoice.Status}).");

        Transition(invoice, InvoiceStatus.Queued, "Gönderim kuyruğuna alındı.");
        invoice.NextAttemptAtUtc = clock.UtcNow;
        invoice.ErrorMessage = null;
        await db.SaveChangesAsync(ct);

        if (sendNow)
            await dispatcher.DispatchAsync(invoice.Id, ct);

        return await GetAsync(id, ct);
    }

    // ---- İptal ----

    public async Task<InvoiceDto> CancelAsync(long id, InvoiceCancelRequest request, CancellationToken ct = default)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);
        var invoice = await LoadAsync(id, ct);

        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Belge zaten iptal edilmiş.");

        // e-Faturada iptal yoktur: karşı taraf red/iade eder ya da IADE belgesi kesilir.
        if (invoice.DocumentKind == InvoiceDocumentKind.EFatura && invoice.Status == InvoiceStatus.Succeeded)
            throw new InvalidOperationException(
                "GİB'e ulaşmış e-Fatura iptal edilemez; iade faturası (IADE) kesilmelidir.");

        // Entegratöre ulaşmış belgelerde iptal bildirimi gönderilir; ulaşmamışsa yalnız yerel iptal.
        if (invoice.IntegratorRefId is { } reference)
        {
            var tenantId = RequireTenantId();
            var provider = await providerFactory.ResolveAsync<IEInvoiceProvider>(tenantId, ct);
            try
            {
                await provider.Instance.CancelEArchiveAsync(reference, request.Reason, ct);
            }
            catch (Exception ex) when (ex is EInvoiceProviderException or HttpRequestException)
            {
                logger.LogError(ex, "e-Arşiv iptal bildirimi başarısız. Id={Id} Ref={Ref}", invoice.Id, reference);
                throw new InvalidOperationException($"Entegratör iptali reddetti: {ex.Message}", ex);
            }
        }

        Transition(invoice, InvoiceStatus.Cancelled, $"İptal gerekçesi: {request.Reason}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---- Sorgular ----

    public async Task<PagedResult<InvoiceListItemDto>> ListAsync(
        InvoiceStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.Invoices.AsNoTracking().AsQueryable();

        if (status is { } s) query = query.Where(i => i.Status == s);
        if (from is { } f) query = query.Where(i => i.IssueDate >= f);
        if (to is { } t) query = query.Where(i => i.IssueDate <= t);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id)
            .Skip(request.Skip).Take(request.EffectivePageSize)
            .Select(i => new InvoiceListItemDto(
                i.Id, i.InvoiceNumber, i.DocumentKind, i.TypeCode, i.BuyerName,
                i.PayableAmount, i.CurrencyCode, i.Status, i.ErrorMessage, i.IssueDate, i.Ettn))
            .ToListAsync(ct);

        return new PagedResult<InvoiceListItemDto>(items, Math.Max(page, 1), request.EffectivePageSize, total);
    }

    public async Task<InvoiceDto> GetAsync(long id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
                .Include(i => i.Lines.OrderBy(l => l.SeqNo))
                .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new KeyNotFoundException("Fatura bulunamadı.");

        var logs = await db.InvoiceStatusLogs.AsNoTracking()
            .Where(l => l.InvoiceId == id)
            .OrderBy(l => l.AtUtc).ThenBy(l => l.Id)
            .Select(l => new InvoiceStatusLogDto(
                l.Id, l.FromStatus, l.ToStatus, l.AtUtc, l.ActorUserId, l.IntegratorRawResponse))
            .ToListAsync(ct);

        return ToDto(invoice, logs);
    }

    public async Task<MediaDownload> OpenUblAsync(long id, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        if (invoice.UblFileId is not { } fileId)
            throw new InvalidOperationException("Belgenin UBL dosyası henüz üretilmedi.");
        return await media.OpenDownloadAsync(fileId, ct);
    }

    public async Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);

        if (invoice.PdfFileId is null)
        {
            if (invoice.IntegratorRefId is not { } reference)
                throw new InvalidOperationException("Belge entegratöre gönderilmeden PDF alınamaz.");

            var tenantId = RequireTenantId();
            var provider = await providerFactory.ResolveAsync<IEInvoiceProvider>(tenantId, ct);
            var bytes = await provider.Instance.GetPdfAsync(reference, MapDocType(invoice.DocumentKind), ct);

            var file = await media.SaveGeneratedAsync(new GeneratedFileRequest(
                invoice.ClinicId, invoice.PatientId, MediaCategory.InvoicePdf,
                $"{invoice.InvoiceNumber ?? invoice.Id.ToString()}.pdf", "application/pdf", bytes,
                Description: $"{invoice.DocumentKind} PDF — {invoice.InvoiceNumber}"), ct);

            invoice.PdfFileId = file.Id;
            await db.SaveChangesAsync(ct);
        }

        return await media.OpenDownloadAsync(invoice.PdfFileId!.Value, ct);
    }

    public async Task<GibTaxpayerDto> GetTaxpayerAsync(string vkn, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vkn);
        var normalized = vkn.Trim();

        var taxpayer = await db.GibTaxpayers.AsNoTracking().FirstOrDefaultAsync(g => g.Vkn == normalized, ct);
        return taxpayer is null
            ? new GibTaxpayerDto(normalized, null, null, null, IsEInvoiceUser: false, null)
            : new GibTaxpayerDto(taxpayer.Vkn, taxpayer.Title, taxpayer.Alias, taxpayer.AccountType,
                IsEInvoiceUser: true, taxpayer.LastSyncUtc);
    }

    // ---- Taslak hesaplama (Preview + Create ortak yolu) ----

    private async Task<Draft> BuildDraftAsync(InvoiceDraftRequest request, CancellationToken ct)
    {
        await draftValidator.ValidateAndThrowAsync(request, ct);
        var tenantId = RequireTenantId();

        var tenantEntity = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Kiracı bulunamadı.");

        var warnings = new List<string>();
        var errors = new List<string>();
        var today = DateOnly.FromDateTime(TrLocal(clock.UtcNow));
        var tax = await taxConfig.GetAsync(today, ct);

        // ---- Alıcı ----
        Patient? patient = null;
        Company? company = null;
        long clinicId;
        string buyerName;
        string? buyerTaxId = null, buyerPassport = null, buyerNationality = null;
        string? buyerAddress = null, buyerCity = null, buyerDistrict = null, buyerEmail = null;
        string? buyerTaxOffice = null, buyerAlias = null;
        DateOnly? buyerLastEntry = null;
        bool isForeign = false, buyerIsEInvoiceUser = false;
        bool? refreshedCompanyFlag = null;
        var customerType = InvoiceCustomerType.Patient;

        if (request.PatientId is { } patientId)
        {
            patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct)
                ?? throw new KeyNotFoundException("Hasta bulunamadı.");
            clinicId = patient.ClinicId;
            buyerName = patient.FullName;
            buyerAddress = patient.Address;
            buyerCity = patient.City;
            buyerDistrict = patient.District;
            buyerEmail = patient.Email;

            isForeign = request.IsForeignPatient
                || patient.IdentityType == IdentityType.Passport
                || !string.Equals(patient.NationalityCode, "TUR", StringComparison.OrdinalIgnoreCase);

            if (isForeign)
            {
                buyerPassport = patient.PassportNo;
                buyerNationality = patient.NationalityCode;
                buyerLastEntry = patient.LastEntryDate;
                // GİB: TCKN'si olmayan yabancı alıcıda sabit kimlik numarası kullanılır.
                buyerTaxId = EDocumentMapper.ForeignBuyerTaxId;
            }
            else if (patient.TcknEncrypted is { } encrypted)
            {
                buyerTaxId = tcknProtector.Unprotect(encrypted);
                if (buyerTaxId is null)
                    warnings.Add("Hastanın TCKN bilgisi çözülemedi (şifreleme anahtarı değişmiş olabilir); belgeyi göndermeden önce hasta kartından TCKN'yi yeniden girin.");
            }
            else
            {
                warnings.Add("Hastanın TCKN bilgisi yok; e-Arşiv belgesinde alıcı kimlik numarası boş kalacak.");
            }
        }
        else
        {
            var companyId = request.CompanyId!.Value;
            company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct)
                ?? throw new KeyNotFoundException("Kurum bulunamadı.");
            customerType = InvoiceCustomerType.Company;
            buyerName = company.Name;
            buyerTaxId = company.Vkn;
            buyerAddress = company.Address;
            buyerEmail = company.Email;
            buyerTaxOffice = company.TaxOffice;
            buyerAlias = company.EInvoiceAlias;
            clinicId = tenant.ClinicId ?? await FirstClinicIdAsync(ct);

            if (string.IsNullOrWhiteSpace(company.Vkn))
            {
                errors.Add("Kurumun VKN bilgisi yok; kuruma fatura kesilemez.");
            }
            else
            {
                // e-Fatura mı e-Arşiv mi kararı LOKAL mükellef aynasından verilir (günlük job tazeler).
                var taxpayer = await db.GibTaxpayers.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Vkn == company.Vkn, ct);
                buyerIsEInvoiceUser = taxpayer is not null;
                if (taxpayer is not null)
                {
                    buyerAlias = taxpayer.Alias ?? buyerAlias;
                    buyerTaxOffice ??= taxpayer.Title;
                }

                if (buyerIsEInvoiceUser != company.IsEInvoiceUser)
                {
                    refreshedCompanyFlag = buyerIsEInvoiceUser;
                    warnings.Add(buyerIsEInvoiceUser
                        ? "Kurum GİB mükellef aynasında bulundu; belge e-Fatura olarak kesilecek."
                        : "Kurum GİB mükellef aynasında yok; belge e-Arşiv olarak kesilecek.");
                }
            }
        }

        // ---- Satırlar ----
        var lines = await BuildLinesAsync(request, patient, company, tax, errors, warnings, ct);
        if (lines.Count == 0 && errors.Count == 0)
            errors.Add("Faturalanabilir kalem bulunamadı.");

        // ---- Kaynak belge (IADE) ----
        Invoice? sourceInvoice = null;
        if (request.SourceInvoiceId is { } sourceId)
        {
            sourceInvoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == sourceId, ct)
                ?? throw new KeyNotFoundException("Kaynak fatura bulunamadı.");
            if (sourceInvoice.InvoiceNumber is null)
                errors.Add("Kaynak faturanın belge numarası yok; iade belgesi kesilemez.");
        }

        // ---- Karar motoru ----
        var decision = EDocumentTypeResolver.Resolve(new EDocumentResolutionRequest
        {
            SellerLegalType = EDocumentMapper.MapLegalType(tenantEntity.LegalType),
            TenantHasHealthTourismAuthorization = tenantEntity.HasHealthTourismAuthorization,
            BuyerIsGibEInvoiceUser = buyerIsEInvoiceUser,
            BuyerIsGovernment = request.IsGovernmentBuyer,
            BuyerIsForeign = isForeign,
            BuyerIsVatRegistered = customerType == InvoiceCustomerType.Company,
            BuyerPassportNumber = buyerPassport,
            BuyerNationality = buyerNationality,
            BuyerLastEntryDate = buyerLastEntry,
            HasAestheticLines = lines.Any(l => l.IsAesthetic),
            IsRefund = request.IsRefund,
            SourceInvoiceNumber = sourceInvoice?.InvoiceNumber,
            SourceIssueDate = sourceInvoice?.IssueDate,
        });
        errors.AddRange(decision.Errors);

        // Kod ve açıklama metinleri TaxConfig'ten gelir; karar motorundaki varsayılan metin ezilir.
        if (decision.ExemptionCode == tax.HealthTourismExemptionCode)
            decision = decision with { ExemptionReason = tax.HealthTourismExemptionReason };
        if (decision.WithholdingCode == tax.PublicWithholdingCode)
            decision = decision with { WithholdingPercent = tax.PublicWithholdingPercent };

        // İstisnada satır KDV oranları ezilir (%0).
        if (decision.VatRateOverride is { } overrideRate)
            lines = [.. lines.Select(l => l with { VatRate = overrideRate, VatAmount = Round(l.LineTotal * overrideRate / 100m) })];

        // ---- Toplamlar ----
        var subTotal = Round(lines.Sum(l => l.LineTotal));
        var discountTotal = Round(lines.Sum(l => l.DiscountAmount));
        var vatTotal = Round(lines.Sum(l => l.VatAmount));
        var withholdingTotal = decision.WithholdingPercent is { } withholdingPercent
            ? Round(vatTotal * withholdingPercent / 100m)
            : 0m;
        var gvStopajTotal = decision.AppliesGvStopaj ? Round(subTotal * tax.GvStopajPercent / 100m) : 0m;
        var totals = new InvoiceTotalsDto(
            subTotal, discountTotal, vatTotal, withholdingTotal, gvStopajTotal,
            Round(subTotal + vatTotal - withholdingTotal - gvStopajTotal));

        // ---- Satıcı/alıcı eksikleri ----
        if (string.IsNullOrWhiteSpace(tenantEntity.TaxNumber))
            errors.Add("Kiracının VKN/TCKN bilgisi tanımlı değil; e-belge kesilemez.");
        if (string.IsNullOrWhiteSpace(tenantEntity.TaxOffice))
            warnings.Add("Kiracının vergi dairesi tanımlı değil.");
        if (string.IsNullOrWhiteSpace(buyerEmail) && decision.DocumentKind != DocumentKind.EFatura)
            warnings.Add("Alıcının e-posta adresi yok; belge elektronik olarak iletilemez.");
        if (string.IsNullOrWhiteSpace(buyerCity))
            warnings.Add("Alıcının il/ilçe bilgisi eksik; adres alanları eksik gönderilecek.");
        if (decision.DocumentKind == DocumentKind.EFatura && string.IsNullOrWhiteSpace(buyerAlias))
            warnings.Add("Alıcının posta kutusu etiketi (alias) bilinmiyor; entegratör varsayılan kutuyu seçecek.");
        if (isForeign && buyerNationality is not null && NationalityCodes.ToAlpha2(buyerNationality) is null)
            warnings.Add($"Uyruk kodu '{buyerNationality}' ISO ülke koduna çevrilemedi; " +
                         "belgede uyruk alanı boş kalacak (GİB geçerli iki harfli kod bekler).");

        return new Draft(
            tenantEntity, clinicId, patient, company, customerType, buyerName, buyerTaxId, buyerPassport,
            buyerNationality, buyerLastEntry, buyerAddress, buyerCity, buyerDistrict, buyerEmail,
            buyerTaxOffice, buyerAlias, refreshedCompanyFlag, decision, lines, totals, warnings, errors,
            tax, sourceInvoice, BuildRationale(tenantEntity, decision, buyerIsEInvoiceUser, isForeign, request));
    }

    private async Task<List<DraftLine>> BuildLinesAsync(
        InvoiceDraftRequest request, Patient? patient, Company? company,
        TaxConfigSet tax, List<string> errors, List<string> warnings, CancellationToken ct)
    {
        var lines = new List<DraftLine>();
        var ids = request.TreatmentRecordIds.Distinct().ToList();

        if (ids.Count > 0)
        {
            var records = await db.TreatmentRecords.AsNoTracking()
                .Include(r => r.TreatmentDefinition).ThenInclude(d => d!.Category)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync(ct);

            if (records.Count != ids.Count)
                throw new KeyNotFoundException("Bir veya birden fazla tedavi kaydı bulunamadı.");

            // Alıcı doğrulaması: hasta faturasında kayıtlar o hastaya, kurum faturasında
            // kurumun anlaşmalı hastalarına ait olmalıdır.
            if (patient is not null && records.Any(r => r.PatientId != patient.Id))
                errors.Add("Seçilen tedavi kayıtlarının bir kısmı bu hastaya ait değil.");
            if (company is not null)
            {
                // Başka bir kuruma bağlı hastanın tedavisi bu kuruma faturalanamaz (kesin hata).
                // Hiçbir kuruma bağlı olmayan hasta ise engel değildir: kurumu alıcı olarak seçen
                // kullanıcı bilinçli davranmıştır; yalnız uyarılır.
                var patientIds = records.Select(r => r.PatientId).Distinct().ToList();
                var otherCompany = await db.Patients.AsNoTracking()
                    .AnyAsync(p => patientIds.Contains(p.Id) && p.CompanyId != null && p.CompanyId != company.Id, ct);
                if (otherCompany)
                    errors.Add("Seçilen tedavi kayıtlarının bir kısmı BAŞKA bir kuruma bağlı hastalara ait.");

                var unlinked = await db.Patients.AsNoTracking()
                    .AnyAsync(p => patientIds.Contains(p.Id) && p.CompanyId == null, ct);
                if (unlinked)
                    warnings.Add("Seçilen hastalardan bazıları bu kuruma bağlı değil; fatura yine de kuruma kesilecek.");
            }

            if (records.Any(r => r.Status != TreatmentRecordStatus.Done))
                errors.Add("Yalnız 'Yapıldı' durumundaki tedavi kayıtları faturalanabilir.");

            var alreadyInvoiced = await db.InvoiceLines.AsNoTracking()
                .Where(l => l.TreatmentRecordId != null && ids.Contains(l.TreatmentRecordId!.Value))
                .Join(db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Cancelled),
                    l => l.InvoiceId, i => i.Id, (l, _) => l.TreatmentRecordId!.Value)
                .Distinct()
                .ToListAsync(ct);
            if (alreadyInvoiced.Count > 0)
                errors.Add($"Şu tedavi kayıtları zaten faturalandı: {string.Join(", ", alreadyInvoiced)}.");

            foreach (var record in records.OrderBy(r => r.Id))
            {
                var definition = record.TreatmentDefinition;
                var isAesthetic = IsAesthetic(definition);
                var vatRate = isAesthetic ? tax.AestheticVatRate : tax.HealthVatRate;
                var gross = record.Price;
                var lineTotal = Round(gross - record.DiscountAmount);
                lines.Add(new DraftLine(
                    record.Id,
                    BuildItemName(definition?.Name ?? "Tedavi", record.ToothNumber),
                    1m,
                    tax.ServiceUnitCode,
                    Round(gross),
                    Round(record.DiscountAmount),
                    vatRate,
                    Round(lineTotal * vatRate / 100m),
                    lineTotal,
                    isAesthetic));
            }
        }

        foreach (var manual in request.ManualLines ?? [])
        {
            var vatRate = manual.VatRate ?? (manual.IsAesthetic ? tax.AestheticVatRate : tax.HealthVatRate);
            var lineTotal = Round(manual.Quantity * manual.UnitPrice - manual.DiscountAmount);
            lines.Add(new DraftLine(
                null, manual.ItemName.Trim(), manual.Quantity, tax.ServiceUnitCode,
                Round(manual.UnitPrice), Round(manual.DiscountAmount), vatRate,
                Round(lineTotal * vatRate / 100m), lineTotal, manual.IsAesthetic));
        }

        return lines;
    }

    /// <summary>
    /// Estetik işaretini KATEGORİ belirler (Beyazlatma ve Estetik) — (c)-4 gereği 334 istisnasıyla
    /// birleşemeyen kalemler bunlardır. Kategori adı okunamıyorsa tanımın KDV oranına düşülür.
    /// </summary>
    internal static bool IsAesthetic(TreatmentDefinition? definition)
    {
        if (definition is null) return false;
        var categoryName = definition.Category?.Name;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            if (categoryName.Contains("Estetik", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("Beyazlatma", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return definition.VatRate >= TaxConfigService.DefaultAestheticVatRate;
    }

    private static string BuildItemName(string name, string? toothNumber) =>
        string.IsNullOrWhiteSpace(toothNumber) ? name : $"{name} (Diş {toothNumber})";

    private static string BuildRationale(
        Tenant tenantEntity, EDocumentDecision decision, bool buyerIsEInvoiceUser,
        bool isForeign, InvoiceDraftRequest request)
    {
        var parts = new List<string>
        {
            decision.DocumentKind switch
            {
                DocumentKind.ESmm =>
                    "Kiracı şahıs hekim (serbest meslek erbabı) → e-Serbest Meslek Makbuzu (UBL CreditNote).",
                DocumentKind.EFatura =>
                    "Kiracı şirket + alıcı GİB mükellef aynasında kayıtlı → e-Fatura.",
                _ => "Kiracı şirket + alıcı GİB mükellef aynasında yok → e-Arşiv fatura.",
            },
        };

        parts.Add(decision.TypeCode switch
        {
            UblTypeCodes.Istisna =>
                $"Yabancı uyruklu hasta + sağlık turizmi yetki belgesi → ISTISNA {decision.ExemptionCode} (KDV %0).",
            UblTypeCodes.Tevkifat =>
                $"Kamu idaresi alıcı → TEVKIFAT {decision.WithholdingCode} ({decision.WithholdingPercent}% KDV tevkifatı).",
            UblTypeCodes.Iade => "İade belgesi → IADE (kaynak belge BillingReference ile bağlanır).",
            _ => "Standart satış → SATIS.",
        });

        if (decision.AppliesGvStopaj)
            parts.Add("Alıcı vergi mükellefi olduğu için e-SMM'de %20 gelir vergisi stopajı uygulanır.");
        if (isForeign && !tenantEntity.HasHealthTourismAuthorization)
            parts.Add("Kiracıda sağlık turizmi yetki belgesi bayrağı kapalı.");
        if (request.IsRefund)
            parts.Add("İade senaryosu talep edildi.");

        return string.Join(" ", parts);
    }

    // ---- UBL modeli ----

    private async Task<EDocumentModel> BuildModelAsync(Invoice invoice, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var tenantEntity = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
        var clinic = await db.Clinics.AsNoTracking().FirstAsync(c => c.Id == invoice.ClinicId, ct);
        var tax = await taxConfig.GetAsync(invoice.IssueDate, ct);

        string? sellerFirst = null, sellerLast = null, sellerEmail = null;
        if (tenantEntity.LegalType == TenantLegalType.SoleProprietor)
        {
            // Şahıs hekimde cac:Person bloğu kiracının sahibi (Owner) kullanıcısından doldurulur.
            var owner = await db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.UserType == UserType.Owner)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(ct);
            sellerFirst = owner?.FirstName;
            sellerLast = owner?.LastName;
            sellerEmail = owner?.Email;
        }

        Invoice? source = null;
        if (invoice.SourceInvoiceId is { } sourceId)
            source = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == sourceId, ct);

        return EDocumentMapper.ToModel(invoice, new EDocumentMappingContext(
            tenantEntity, clinic, sellerFirst, sellerLast, sellerEmail,
            tax.PublicWithholdingPercent, tax.GvStopajPercent,
            source?.InvoiceNumber, source?.IssueDate));
    }

    private static IUblDocumentBuilder SelectBuilder(DocumentKind kind) =>
        kind == DocumentKind.ESmm ? new CreditNoteUblBuilder() : new InvoiceUblBuilder();

    // ---- Yardımcılar ----

    private long RequireTenantId() => tenant.TenantId
        ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan fatura işlemi yapılamaz.");

    private async Task<long> FirstClinicIdAsync(CancellationToken ct)
    {
        var id = await db.Clinics.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync(ct);
        return id > 0 ? id : throw new InvalidOperationException("Kiracının kliniği tanımlı değil.");
    }

    private async Task<Invoice> LoadAsync(long id, CancellationToken ct) =>
        await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new KeyNotFoundException("Fatura bulunamadı.");

    private async Task<Invoice> LoadWithLinesAsync(long id, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines.OrderBy(l => l.SeqNo)).FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new KeyNotFoundException("Fatura bulunamadı.");

    private void Transition(Invoice invoice, InvoiceStatus to, string? detail)
    {
        AddStatusLog(invoice, invoice.Status, to, detail);
        invoice.Status = to;
    }

    /// <summary>Kayıt her zaman kalıcı Id üzerinden bağlanır; çağrı öncesi fatura kaydedilmiş olmalıdır.</summary>
    private void AddStatusLog(Invoice invoice, InvoiceStatus? from, InvoiceStatus to, string? detail) =>
        db.InvoiceStatusLogs.Add(new InvoiceStatusLog
        {
            InvoiceId = invoice.Id,
            FromStatus = from,
            ToStatus = to,
            AtUtc = clock.UtcNow,
            ActorUserId = tenant.UserId,
            IntegratorRawResponse = detail,
        });

    internal static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    internal static DateTime TrLocal(DateTime utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(3);
        }
    }

    internal static InvoiceDocumentKind MapKind(DocumentKind kind) => kind switch
    {
        DocumentKind.EFatura => InvoiceDocumentKind.EFatura,
        DocumentKind.EArsiv => InvoiceDocumentKind.EArsiv,
        _ => InvoiceDocumentKind.ESmm,
    };

    internal static EDocType MapDocType(InvoiceDocumentKind kind) => kind switch
    {
        InvoiceDocumentKind.EFatura => EDocType.EInvoice,
        InvoiceDocumentKind.EArsiv => EDocType.EArchive,
        _ => EDocType.ESmm,
    };

    private static InvoicePreviewDto ToPreview(Draft draft) => new(
        MapKind(draft.Decision.DocumentKind),
        draft.Decision.ProfileId,
        draft.Decision.TypeCode,
        draft.Rationale,
        draft.Decision.ExemptionCode,
        draft.Decision.ExemptionReason,
        draft.Decision.WithholdingCode,
        draft.Decision.WithholdingPercent,
        draft.CustomerType,
        draft.Patient?.Id,
        draft.Company?.Id,
        draft.BuyerName,
        draft.BuyerTcknVkn,
        draft.BuyerPassportNo,
        draft.BuyerNationality,
        draft.BuyerLastEntryDate,
        "TRY",
        [.. draft.Lines.Select((l, i) => new InvoiceLineDto(
            0, i + 1, l.TreatmentRecordId, l.ItemName, l.Quantity, l.UnitCode, l.UnitPrice,
            l.DiscountAmount, l.VatRate, l.VatAmount, l.LineTotal, l.IsAesthetic))],
        draft.Totals,
        draft.Warnings,
        draft.Errors,
        draft.Errors.Count == 0);

    private static InvoiceDto ToDto(Invoice i, IReadOnlyList<InvoiceStatusLogDto> logs) => new(
        i.Id, i.ClinicId, i.DocumentKind, i.ProfileId, i.TypeCode, i.Status, i.InvoiceNumber, i.Serial, i.Ettn,
        i.IssueDate, i.IssueTime, i.CustomerType, i.PatientId, i.CompanyId, i.BuyerName, i.BuyerTcknVkn,
        i.BuyerPassportNo, i.BuyerNationality, i.BuyerLastEntryDate, i.BuyerAddress, i.BuyerEmail,
        i.CurrencyCode, i.ExchangeRate,
        new InvoiceTotalsDto(i.SubTotal, i.DiscountTotal, i.VatTotal, i.WithholdingTotal, i.GvStopajTotal, i.PayableAmount),
        i.ExemptionCode, i.ExemptionReason, i.WithholdingCode, i.IntegratorProvider, i.IntegratorRefId,
        i.LastStatusCheckUtc, i.AttemptCount, i.NextAttemptAtUtc, i.ErrorMessage, i.UblFileId, i.PdfFileId,
        i.SourceInvoiceId,
        [.. i.Lines.OrderBy(l => l.SeqNo).Select(l => new InvoiceLineDto(
            l.Id, l.SeqNo, l.TreatmentRecordId, l.ItemName, l.Quantity, l.UnitCode, l.UnitPrice,
            l.DiscountAmount, l.VatRate, l.VatAmount, l.LineTotal, l.IsAesthetic))],
        logs,
        i.CreatedAtUtc);

    private sealed record DraftLine(
        long? TreatmentRecordId, string ItemName, decimal Quantity, string UnitCode, decimal UnitPrice,
        decimal DiscountAmount, decimal VatRate, decimal VatAmount, decimal LineTotal, bool IsAesthetic);

    private sealed record Draft(
        Tenant Tenant, long ClinicId, Patient? Patient, Company? Company, InvoiceCustomerType CustomerType,
        string BuyerName, string? BuyerTcknVkn, string? BuyerPassportNo, string? BuyerNationality,
        DateOnly? BuyerLastEntryDate, string? BuyerAddress, string? BuyerCity, string? BuyerDistrict,
        string? BuyerEmail, string? BuyerTaxOffice, string? BuyerAlias, bool? RefreshedCompanyEInvoiceUser,
        EDocumentDecision Decision, List<DraftLine> Lines, InvoiceTotalsDto Totals,
        List<string> Warnings, List<string> Errors, TaxConfigSet Tax, Invoice? SourceInvoice, string Rationale);
}
