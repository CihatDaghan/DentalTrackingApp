namespace Dental.Domain.Enums;

/// <summary>Cari defter hesap türü: hasta carisi / kurum (sigorta) carisi.</summary>
public enum LedgerAccountType : byte
{
    Patient = 1,
    Company = 2,
}

/// <summary>Cari defter kayıt türü. Bakiye = SUM(Debit - Credit); borç (+) / alacak (-).</summary>
public enum LedgerEntryType : byte
{
    /// <summary>Done tedaviden doğan borçlandırma (Debit).</summary>
    TreatmentCharge = 1,
    /// <summary>Tahsilat (Credit).</summary>
    PaymentIn = 2,
    /// <summary>İade (Debit).</summary>
    Refund = 3,
    /// <summary>İndirim (Credit).</summary>
    Discount = 4,
    /// <summary>Açılış bakiyesi (devir).</summary>
    OpeningBalance = 5,
    /// <summary>Kurumun üstlendiği pay aktarımı.</summary>
    CompanyTransfer = 6,
    /// <summary>Düzeltme / ters kayıt (tedavi iptali, tahsilat silme).</summary>
    Correction = 7,
}

/// <summary>Tahsilat/ödeme yöntemi (Payment ve Expense ortak).</summary>
public enum PaymentMethod : byte
{
    Cash = 1,
    CreditCardPos = 2,
    BankTransfer = 3,
    OnlineLink = 4,
    Check = 5,
}

/// <summary>
/// Taksit durumu. Overdue kalıcı yazılmaz; DueDate &lt; bugün &amp;&amp; Pending/Partial
/// sorgu bazlı Overdue olarak sunulur (ödeme hatırlatma job'ı da bu görünümden çalışır).
/// </summary>
public enum InstallmentStatus : byte
{
    Pending = 1,
    Partial = 2,
    Paid = 3,
    Overdue = 4,
}
