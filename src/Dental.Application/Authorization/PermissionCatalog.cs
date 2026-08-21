namespace Dental.Application.Authorization;

/// <summary>
/// Sabit izin kataloğu. Kod = "modul.eylem". Seed bu listeden üretilir; yeni izinler buraya eklenir.
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyDictionary<string, string[]> ByModule = new Dictionary<string, string[]>
    {
        ["patient"] = ["read", "create", "update", "delete", "export"],
        ["appointment"] = ["read", "create", "update", "delete", "cancel"],
        ["treatment"] = ["read", "create", "update", "delete", "edit-price"],
        ["payment"] = ["read", "create", "delete", "discount"],
        ["invoice"] = ["read", "create", "send", "cancel"],
        ["prescription"] = ["read", "create"],
        ["consent"] = ["read", "create", "send"],
        ["media"] = ["read", "upload", "delete"],
        ["anamnesis"] = ["read", "create"],
        ["note"] = ["read", "create", "update", "delete"],
        ["lab"] = ["read", "create", "update", "delete"],
        ["stock"] = ["read", "create", "update", "delete"],
        ["report"] = ["view", "export"],
        ["messaging"] = ["read", "send", "bulk", "templates"],
        ["enabiz"] = ["read", "send", "manage"],
        ["settings"] = ["view", "update", "staff", "integrations"],
        ["expense"] = ["read", "create", "update", "delete"],
        ["recall"] = ["read", "create", "update"],
        ["epicrisis"] = ["read", "create"],
    };

    public static IEnumerable<(string Code, string Module)> All =>
        ByModule.SelectMany(kv => kv.Value.Select(a => ($"{kv.Key}.{a}", kv.Key)));

    /// <summary>Sistem rolleri ve varsayılan izin setleri (design-1 §5 rol matrisi).</summary>
    public static readonly IReadOnlyDictionary<string, Func<string, bool>> SystemRoles = new Dictionary<string, Func<string, bool>>
    {
        // Owner: her şey
        ["Owner"] = _ => true,
        // Dentist: silme/ayar/mesajlaşma-toplu hariç klinik işlemleri
        ["Dentist"] = c => c is not ("payment.delete" or "invoice.cancel" or "settings.staff" or "settings.integrations" or "messaging.bulk" or "patient.delete"),
        // Assistant: okuma + klinik veri girişi; finans/ayar yok
        ["Assistant"] = c => c.EndsWith(".read") || c == "report.view" ||
                             c is "appointment.create" or "appointment.update" or "treatment.create" or "treatment.update"
                                or "anamnesis.create" or "note.create" or "media.upload" or "lab.create" or "lab.update"
                                or "stock.create" or "stock.update" or "recall.create" or "recall.update",
        // Secretary: hasta/randevu/tahsilat/fatura kesme/mesajlaşma; klinik kayıtlar salt okunur
        ["Secretary"] = c => c.EndsWith(".read") ||
                             c is "patient.create" or "patient.update"
                                or "appointment.create" or "appointment.update" or "appointment.cancel"
                                or "payment.create" or "invoice.create" or "invoice.send"
                                or "messaging.send" or "messaging.bulk" or "consent.send" or "recall.create",
        // Accountant: finans + raporlar
        ["Accountant"] = c => c.StartsWith("payment.") || c.StartsWith("invoice.") || c.StartsWith("expense.") ||
                              c.StartsWith("report.") || c.EndsWith(".read"),
    };
}
