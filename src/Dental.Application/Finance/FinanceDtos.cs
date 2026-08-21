using Dental.Domain.Enums;

namespace Dental.Application.Finance;

// ---- Cari defter ----

/// <summary>
/// Cari defter kaydı ekleme isteği (servisler arası çekirdek sözleşme).
/// AccountType'a göre PatientId veya CompanyId'den tam biri dolu olmalıdır.
/// </summary>
public sealed record LedgerEntryCreateRequest(
    LedgerAccountType AccountType,
    long? PatientId,
    long? CompanyId,
    LedgerEntryType EntryType,
    decimal Debit,
    decimal Credit,
    string? Description = null,
    /// <summary>NULL ise bugün (UTC).</summary>
    DateOnly? EntryDate = null,
    long? ClinicId = null,
    string? RefType = null,
    long? RefId = null,
    string CurrencyCode = "TRY",
    decimal ExchangeRate = 1m);

/// <summary>Elle cari kaydı (açılış bakiyesi / düzeltme) API isteği.</summary>
public sealed record ManualLedgerEntryRequest(
    LedgerEntryType EntryType,
    decimal Debit,
    decimal Credit,
    string? Description = null,
    DateOnly? EntryDate = null,
    long? ClinicId = null);

public sealed record LedgerStatementQuery(
    LedgerAccountType AccountType,
    long AccountId,
    DateOnly? From = null,
    DateOnly? To = null);

/// <summary>Ekstre satırı; RunningBalance açılış bakiyesinden koşan toplamdır.</summary>
public sealed record LedgerLineDto(
    long Id,
    DateOnly EntryDate,
    LedgerEntryType EntryType,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? RefType,
    long? RefId,
    DateTime CreatedAtUtc);

/// <summary>Ekstre özeti. Balance hesabın güncel (denormalize) bakiyesidir.</summary>
public sealed record LedgerSummaryDto(
    decimal TotalTreatment,
    decimal TotalPayment,
    decimal TotalDiscount,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance);

/// <summary>Tarih sıralı ekstre + koşan bakiye + özet.</summary>
public sealed record LedgerStatementDto(
    LedgerAccountType AccountType,
    long AccountId,
    string AccountName,
    DateOnly? From,
    DateOnly? To,
    decimal OpeningBalance,
    IReadOnlyList<LedgerLineDto> Lines,
    LedgerSummaryDto Summary);

// ---- Tahsilat ----

public sealed record PaymentCreateRequest(
    decimal Amount,
    PaymentMethod Method,
    long? PatientId = null,
    long? CompanyId = null,
    long? ClinicId = null,
    byte? InstallmentCount = null,
    string? Note = null,
    /// <summary>NULL ise şimdi (UTC).</summary>
    DateTime? ReceivedAtUtc = null,
    string CurrencyCode = "TRY",
    decimal ExchangeRate = 1m);

public sealed record PaymentDto(
    long Id,
    long ClinicId,
    long? PatientId,
    string? PatientName,
    long? CompanyId,
    string? CompanyName,
    decimal Amount,
    string CurrencyCode,
    PaymentMethod Method,
    byte? InstallmentCount,
    DateTime ReceivedAtUtc,
    long ReceivedByUserId,
    string ReceivedByName,
    long LedgerEntryId,
    string? Note);

public sealed record PaymentListQuery(
    long? PatientId = null,
    long? CompanyId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    PaymentMethod? Method = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>İndirim: hasta/kurum carisine Discount (Credit) kaydı — payment.discount izni.</summary>
public sealed record DiscountRequest(
    decimal Amount,
    long? PatientId = null,
    long? CompanyId = null,
    string? Description = null,
    long? ClinicId = null);

// ---- Taksit planı ----

public sealed record PaymentPlanCreateRequest(
    long PatientId,
    decimal TotalAmount,
    int InstallmentCount,
    DateOnly StartDate,
    string? Note = null);

public sealed record InstallmentDto(
    long Id,
    int SeqNo,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    /// <summary>Sorgu bazlı durum: DueDate geçmiş Pending/Partial → Overdue.</summary>
    InstallmentStatus Status);

public sealed record PaymentPlanDto(
    long Id,
    long PatientId,
    string PatientName,
    decimal TotalAmount,
    decimal PaidAmount,
    DateOnly StartDate,
    string? Note,
    IReadOnlyList<InstallmentDto> Installments);

// ---- Kurum ----

public sealed record CompanyUpsertRequest(
    string Name,
    string? TaxOffice = null,
    string? Vkn = null,
    string? Address = null,
    string? Email = null,
    string? Phone = null,
    long? PriceListId = null,
    bool IsEInvoiceUser = false,
    string? EInvoiceAlias = null);

public sealed record CompanyDto(
    long Id,
    string Name,
    string? TaxOffice,
    string? Vkn,
    string? Address,
    string? Email,
    string? Phone,
    long? PriceListId,
    bool IsEInvoiceUser,
    string? EInvoiceAlias,
    decimal Balance,
    DateTime CreatedAtUtc);

// ---- Gider ----

public sealed record ExpenseCategoryUpsertRequest(string Name);

public sealed record ExpenseCategoryDto(long Id, string Name);

public sealed record ExpenseUpsertRequest(
    long CategoryId,
    decimal Amount,
    DateOnly ExpenseDate,
    PaymentMethod Method = PaymentMethod.Cash,
    string? Description = null,
    long? ClinicId = null,
    long? PaidByUserId = null);

public sealed record ExpenseDto(
    long Id,
    long ClinicId,
    long CategoryId,
    string CategoryName,
    decimal Amount,
    DateOnly ExpenseDate,
    PaymentMethod Method,
    string? Description,
    long? PaidByUserId,
    string? PaidByName);

public sealed record ExpenseListQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    long? CategoryId = null,
    long? ClinicId = null,
    int Page = 1,
    int PageSize = 25);

// ---- Kasa ----

public sealed record CashMethodTotalDto(PaymentMethod Method, decimal Total, int Count);

/// <summary>Gün hareketi: tahsilat veya gider satırı.</summary>
/// <summary>Kasa hareketinin türü. API sözleşmesinde yerelleştirilmiş metin taşınmaz;
/// görünen etiketi istemci kendi diline göre üretir (inceleme bulgusu).</summary>
public enum CashMovementKind : byte
{
    Payment = 1,
    Expense = 2,
}

public sealed record CashMovementDto(
    DateTime AtUtc,
    CashMovementKind Kind,
    string? AccountName,
    PaymentMethod Method,
    decimal Amount,
    string? UserName,
    string? Description);

public sealed record CashRegisterDailySummaryDto(
    DateOnly Date,
    long? ClinicId,
    IReadOnlyList<CashMethodTotalDto> CollectionsByMethod,
    decimal TotalCollections,
    decimal TotalExpenses,
    decimal Net,
    IReadOnlyList<CashMovementDto> Movements);
