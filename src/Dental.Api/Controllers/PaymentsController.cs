using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Finance;
using Dental.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>Cari ekstre + tahsilat + indirim + kasa uçları.</summary>
[ApiController]
[Route("api/v1")]
public sealed class PaymentsController(
    ILedgerService ledger,
    IPaymentService payments,
    ICashRegisterService cashRegister,
    IValidator<ManualLedgerEntryRequest> manualEntryValidator) : ControllerBase
{
    /// <summary>Hasta cari ekstresi: tarih sıralı hareketler + koşan bakiye + özet.</summary>
    [HttpGet("patients/{patientId:long}/ledger")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<LedgerStatementDto>> GetPatientLedger(
        long patientId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await ledger.GetStatementAsync(
            new LedgerStatementQuery(LedgerAccountType.Patient, patientId, from, to), ct));

    /// <summary>Elle cari kaydı (açılış bakiyesi / düzeltme / iade).</summary>
    [HttpPost("patients/{patientId:long}/ledger")]
    [HasPermission("payment.create")]
    public async Task<ActionResult> AddPatientLedgerEntry(
        long patientId, ManualLedgerEntryRequest request, CancellationToken ct)
    {
        // İnceleme bulgusu: validator tanımlıydı ama hiç çalıştırılmıyordu.
        await manualEntryValidator.ValidateAndThrowAsync(request, ct);
        var id = await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
            LedgerAccountType.Patient, patientId, CompanyId: null,
            request.EntryType, request.Debit, request.Credit,
            request.Description, request.EntryDate, request.ClinicId), ct);
        return Ok(new { id });
    }

    [HttpGet("payments")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<PagedResult<PaymentDto>>> List(
        [FromQuery] long? patientId, [FromQuery] long? companyId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] PaymentMethod? method,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await payments.ListAsync(new PaymentListQuery(patientId, companyId, from, to, method, page, pageSize), ct));

    [HttpGet("payments/{id:long}")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<PaymentDto>> Get(long id, CancellationToken ct)
        => Ok(await payments.GetAsync(id, ct));

    /// <summary>Tahsilat: PaymentIn ledger kaydı + bakiye + açık taksitlere FIFO dağıtım.</summary>
    [HttpPost("payments")]
    [HasPermission("payment.create")]
    public async Task<ActionResult<PaymentDto>> Create(PaymentCreateRequest request, CancellationToken ct)
    {
        var dto = await payments.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Tahsilat silme: soft-delete + ters (Correction) ledger kaydıyla bakiye geri alınır.</summary>
    [HttpDelete("payments/{id:long}")]
    [HasPermission("payment.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await payments.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>İndirim: hasta/kurum carisine Discount (Credit) kaydı.</summary>
    [HttpPost("payments/discount")]
    [HasPermission("payment.discount")]
    public async Task<ActionResult> ApplyDiscount(DiscountRequest request, CancellationToken ct)
    {
        var id = await payments.ApplyDiscountAsync(request, ct);
        return Ok(new { ledgerEntryId = id });
    }

    /// <summary>Kasa günlük özeti: yöntem bazında tahsilat, gider, net + gün hareketleri.</summary>
    [HttpGet("cash-register")]
    [HasPermission("payment.read")]
    public async Task<ActionResult<CashRegisterDailySummaryDto>> CashRegister(
        [FromQuery] DateOnly? date, [FromQuery] long? clinicId, CancellationToken ct)
        => Ok(await cashRegister.GetDailySummaryAsync(
            date ?? Dental.Domain.Common.TrTime.ToLocalDate(DateTime.UtcNow), clinicId, ct));
}
