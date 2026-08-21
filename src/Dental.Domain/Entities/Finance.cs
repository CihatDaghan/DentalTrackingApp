using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Kurum/sigorta carisi. Anlaşmalı kurum hastaları Patient.CompanyId ile bağlanır;
/// kurum anlaşma fiyatı PriceListId üzerinden çözülür. Balance denormalizedir ve
/// yalnız LedgerService üzerinden (ledger kaydıyla aynı transaction'da) güncellenir.
/// </summary>
public class Company : TenantEntity
{
    public required string Name { get; set; }
    public string? TaxOffice { get; set; }
    /// <summary>Vergi kimlik no (10 hane).</summary>
    public string? Vkn { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Kurum anlaşma tarifesi.</summary>
    public long? PriceListId { get; set; }
    /// <summary>GİB mükellef listesinden güncellenir (e-Fatura mı e-Arşiv mi kararı).</summary>
    public bool IsEInvoiceUser { get; set; }
    public string? EInvoiceAlias { get; set; }
    /// <summary>Denormalize cari bakiye = SUM(Debit - Credit); borç (+) / alacak (-).</summary>
    public decimal Balance { get; set; }
}

/// <summary>
/// Tek cari defter (hasta + kurum). Bakiye = SUM(Debit - Credit).
/// Patient.Balance / Company.Balance güncellemesi HER ZAMAN kayıt ekleme ile aynı
/// transaction'da atomik UPDATE (satır kilidi) ile yapılır — denetim (c)-10 düzeltmesi.
/// </summary>
public class LedgerEntry : TenantEntity
{
    public long ClinicId { get; set; }
    public LedgerAccountType AccountType { get; set; }
    public long? PatientId { get; set; }
    public long? CompanyId { get; set; }
    public LedgerEntryType EntryType { get; set; }
    /// <summary>Borç (+): tedavi ücreti, iade, açılış borcu...</summary>
    public decimal Debit { get; set; }
    /// <summary>Alacak (-): tahsilat, indirim, ters kayıt...</summary>
    public decimal Credit { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public decimal ExchangeRate { get; set; } = 1m;
    public DateOnly EntryDate { get; set; }
    public string? Description { get; set; }
    /// <summary>Kaynak tablo adı (TreatmentRecord, Payment, Invoice...).</summary>
    public string? RefType { get; set; }
    public long? RefId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Tahsilat. Kaydı her zaman bir PaymentIn ledger kaydı (LedgerEntryId) eşlik eder;
/// silme soft-delete + ters (Correction) ledger kaydıdır — payment.delete iznine bağlı.
/// </summary>
public class Payment : TenantEntity
{
    public long ClinicId { get; set; }
    public long? PatientId { get; set; }
    public long? CompanyId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public decimal ExchangeRate { get; set; } = 1m;
    public PaymentMethod Method { get; set; }
    /// <summary>Kredi kartı taksit sayısı (POS).</summary>
    public byte? InstallmentCount { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public long ReceivedByUserId { get; set; }
    /// <summary>Online tahsilat bağlantısı — G aşamasında (PaymentIntent) bağlanacak; şimdilik düz kolon.</summary>
    public long? PaymentIntentId { get; set; }
    /// <summary>Tahsilatın PaymentIn ledger kaydı.</summary>
    public long LedgerEntryId { get; set; }
    public string? Note { get; set; }
}

/// <summary>Taksit planı (eşit aylık taksitler CreateAsync'te üretilir).</summary>
public class PaymentPlan : TenantEntity
{
    public long PatientId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly StartDate { get; set; }
    public string? Note { get; set; }

    public ICollection<PaymentPlanInstallment> Installments { get; set; } = [];
}

/// <summary>
/// Taksit. PaidAmount tahsilatların FIFO (vade sırası) dağıtımıyla güncellenir;
/// Overdue kalıcı yazılmaz, sorgu bazlı sunulur (DueDate &lt; bugün &amp;&amp; Pending/Partial).
/// </summary>
public class PaymentPlanInstallment : TenantEntity
{
    public long PlanId { get; set; }
    public int SeqNo { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;

    public PaymentPlan? Plan { get; set; }
}

/// <summary>Gider kategorisi (seed şablonu: Kira, Maaş, Malzeme, Laboratuvar, Fatura/Aidat, Vergi, Diğer).</summary>
public class ExpenseCategory : TenantEntity
{
    public required string Name { get; set; }
}

/// <summary>Klinik gideri. Kasa günlük özetinde tahsilatlardan düşülür (net).</summary>
public class Expense : TenantEntity
{
    public long ClinicId { get; set; }
    public long CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
    public long? PaidByUserId { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public ExpenseCategory? Category { get; set; }
}
