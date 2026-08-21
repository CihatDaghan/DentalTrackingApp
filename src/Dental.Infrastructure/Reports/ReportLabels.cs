using Dental.Domain.Enums;

namespace Dental.Infrastructure.Reports;

/// <summary>
/// Yalnız ÜRETİLEN BELGELERDE (Excel/PDF) kullanılan Türkçe etiketler.
/// API sözleşmesinde yerelleştirilmiş metin taşınmaz — JSON yanıtlar enum döner,
/// etiketi istemci kendi diline göre üretir (D aşaması kararı).
/// </summary>
public static class ReportLabels
{
    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.CreditCardPos => "Kredi kartı",
        PaymentMethod.BankTransfer => "Havale/EFT",
        PaymentMethod.OnlineLink => "Ödeme linki",
        PaymentMethod.Check => "Çek",
        _ => method.ToString(),
    };

    public static string AppointmentStatus(Domain.Enums.AppointmentStatus status) => status switch
    {
        Domain.Enums.AppointmentStatus.Scheduled => "Planlandı",
        Domain.Enums.AppointmentStatus.Confirmed => "Onaylandı",
        Domain.Enums.AppointmentStatus.Arrived => "Geldi",
        Domain.Enums.AppointmentStatus.InChair => "Koltukta",
        Domain.Enums.AppointmentStatus.Completed => "Tamamlandı",
        Domain.Enums.AppointmentStatus.Cancelled => "İptal",
        Domain.Enums.AppointmentStatus.NoShow => "Gelmedi",
        _ => status.ToString(),
    };

    public static string TreatmentStatus(TreatmentRecordStatus status) => status switch
    {
        TreatmentRecordStatus.Diagnosis => "Tanı",
        TreatmentRecordStatus.Planned => "Planlandı",
        TreatmentRecordStatus.Done => "Yapıldı",
        TreatmentRecordStatus.Cancelled => "İptal",
        _ => status.ToString(),
    };

    public static string ToothCondition(Domain.Enums.ToothCondition condition) => condition switch
    {
        Domain.Enums.ToothCondition.Present => "Mevcut",
        Domain.Enums.ToothCondition.Missing => "Eksik",
        Domain.Enums.ToothCondition.Extracted => "Çekilmiş",
        Domain.Enums.ToothCondition.Implant => "İmplant",
        Domain.Enums.ToothCondition.Crown => "Kron",
        Domain.Enums.ToothCondition.Bridge => "Köprü",
        Domain.Enums.ToothCondition.RootCanalTreated => "Kanal tedavili",
        Domain.Enums.ToothCondition.Unerupted => "Sürmemiş",
        _ => condition.ToString(),
    };

    public static string Gender(Domain.Enums.Gender gender) => gender switch
    {
        Domain.Enums.Gender.Male => "Erkek",
        Domain.Enums.Gender.Female => "Kadın",
        _ => "Belirtilmemiş",
    };
}
