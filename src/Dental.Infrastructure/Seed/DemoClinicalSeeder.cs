using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Demo kiracıya E2 örnek verisi: 1 laboratuvar firması, 2 lab vakası (biri gecikmiş),
/// 3 stok kartı + hareketler ve 1 (kontrole tabi olmayan) reçete. Idempotent: demo kiracıda
/// Laboratory varsa hiçbir şey yapmaz. DemoDataSeeder'ın hekim/hasta verisini gerektirir.
/// </summary>
public static class DemoClinicalSeeder
{
    public static async Task ApplyAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var demoTenantId = await db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == DbSeeder.DemoEmail.ToUpperInvariant())
            .Select(u => u.TenantId)
            .FirstOrDefaultAsync(ct);
        if (demoTenantId is not { } tenantId) return;

        if (await db.Laboratories.IgnoreQueryFilters().AnyAsync(l => l.TenantId == tenantId && !l.IsDeleted, ct))
            return;

        var clinicId = await db.Clinics.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => (long?)c.Id).FirstOrDefaultAsync(ct);
        var dentist = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.UserType == UserType.Dentist && u.IsActive)
            .OrderBy(u => u.Id)
            .Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);
        var patientIds = await db.Patients.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .OrderBy(p => p.Id)
            .Select(p => p.Id).Take(2).ToListAsync(ct);
        if (clinicId is not { } demoClinicId || dentist is not { } dentistId || patientIds.Count < 2)
            return; // DemoDataSeeder çalışmamış — örnek klinik verisi olmadan E2 demo verisi anlamsız

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        // ---- 1 laboratuvar + 2 vaka (biri gecikmiş: DueDate geçmişte, Status < Received) ----
        var lab = new Laboratory
        {
            TenantId = tenantId,
            Name = "Ankara Dental Lab",
            Phone = "+903121110099",
            Email = "info@ankaradentallab.local",
            Address = "Çankaya, Ankara",
            ContactPerson = "Hakan Usta",
        };
        db.Laboratories.Add(lab);
        await db.SaveChangesAsync(ct);

        var case1 = new LabCase
        {
            TenantId = tenantId,
            ClinicId = demoClinicId,
            PatientId = patientIds[0],
            DoctorUserId = dentistId,
            LaboratoryId = lab.Id,
            CaseNo = $"LAB-{now.Year}-0001",
            WorkType = "Zirkonyum Kron",
            TeethCsv = "11,21",
            Shade = "A2",
            Material = "Zirkonyum",
            Status = LabCaseStatus.InLab,
            SentDate = today.AddDays(-10),
            DueDate = today.AddDays(-3), // gecikmiş
            Price = 2400m,
            Note = "Ön bölge — renk uyumuna dikkat.",
        };
        var case2 = new LabCase
        {
            TenantId = tenantId,
            ClinicId = demoClinicId,
            PatientId = patientIds[1],
            DoctorUserId = dentistId,
            LaboratoryId = lab.Id,
            CaseNo = $"LAB-{now.Year}-0002",
            WorkType = "Total Protez",
            Shade = "B1",
            Material = "Akrilik",
            Status = LabCaseStatus.Sent,
            SentDate = today.AddDays(-2),
            DueDate = today.AddDays(12),
            Price = 5200m,
        };
        db.LabCases.AddRange(case1, case2);
        await db.SaveChangesAsync(ct);

        db.LabCaseStatusHistories.AddRange(
            NewHistory(tenantId, case1.Id, LabCaseStatus.Draft, now.AddDays(-11), "Vaka oluşturuldu."),
            NewHistory(tenantId, case1.Id, LabCaseStatus.Sent, now.AddDays(-10), null),
            NewHistory(tenantId, case1.Id, LabCaseStatus.InLab, now.AddDays(-9), "Lab teslim aldı."),
            NewHistory(tenantId, case2.Id, LabCaseStatus.Draft, now.AddDays(-3), "Vaka oluşturuldu."),
            NewHistory(tenantId, case2.Id, LabCaseStatus.Sent, now.AddDays(-2), null));

        // ---- 3 stok kartı + hareketler ----
        var category = new StockCategory { TenantId = tenantId, Name = "Sarf Malzeme" };
        db.StockCategories.Add(category);
        await db.SaveChangesAsync(ct);

        var eldiven = NewItem(tenantId, demoClinicId, category.Id, "Lateks Eldiven (M) Kutu", "kutu", 20m, 145m);
        var anestezik = NewItem(tenantId, demoClinicId, category.Id, "Artikain %4 Ampul", "adet", 50m, 18.5m);
        var kompozit = NewItem(tenantId, demoClinicId, category.Id, "Kompozit Dolgu A2 Şırınga", "adet", 5m, 950m);
        db.StockItems.AddRange(eldiven, anestezik, kompozit);
        await db.SaveChangesAsync(ct);

        AddMovements(db, tenantId, demoClinicId, eldiven, dentistId, now, purchase: 40m, use: 12m);
        AddMovements(db, tenantId, demoClinicId, anestezik, dentistId, now, purchase: 100m, use: 62m);
        AddMovements(db, tenantId, demoClinicId, kompozit, dentistId, now, purchase: 6m, use: 2m); // 4 kaldı → düşük stok
        await db.SaveChangesAsync(ct);

        // ---- 1 kontrole tabi olmayan reçete (Çekim Sonrası kalemleri) ----
        var drugIds = await db.Drugs
            .Where(d => d.TenantId == null && !d.IsControlled)
            .OrderBy(d => d.Id)
            .Where(d => d.Barcode == "8690000000029" || d.Barcode == "8690000000272" || d.Barcode == "8690000000494")
            .Select(d => d.Id)
            .ToListAsync(ct);
        if (drugIds.Count == 3)
        {
            var prescription = new Prescription
            {
                TenantId = tenantId,
                ClinicId = demoClinicId,
                PatientId = patientIds[0],
                DoctorUserId = dentistId,
                PrescriptionNo = $"RX-{now.Year}-000001",
                Status = PrescriptionStatus.Draft,
            };
            foreach (var drugId in drugIds)
            {
                prescription.Items.Add(new PrescriptionItem
                {
                    TenantId = tenantId,
                    DrugId = drugId,
                    BoxCount = 1,
                });
            }
            db.Prescriptions.Add(prescription);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Demo E2 verisi yüklendi: 1 laboratuvar, 2 lab vakası, 3 stok kartı, 1 reçete (TenantId={TenantId})",
            tenantId);
    }

    private static LabCaseStatusHistory NewHistory(
        long tenantId, long labCaseId, LabCaseStatus status, DateTime atUtc, string? note) => new()
    {
        TenantId = tenantId,
        LabCaseId = labCaseId,
        Status = status,
        ChangedAtUtc = atUtc,
        Note = note,
    };

    private static StockItem NewItem(
        long tenantId, long clinicId, long categoryId, string name, string unit, decimal minQty, decimal price) => new()
    {
        TenantId = tenantId,
        ClinicId = clinicId,
        CategoryId = categoryId,
        Name = name,
        Unit = unit,
        MinQty = minQty,
        LastPurchasePrice = price,
        IsActive = true,
    };

    private static void AddMovements(
        AppDbContext db, long tenantId, long clinicId, StockItem item, long userId,
        DateTime now, decimal purchase, decimal use)
    {
        db.StockMovements.AddRange(
            new StockMovement
            {
                TenantId = tenantId,
                ClinicId = clinicId,
                StockItemId = item.Id,
                Direction = StockMovementDirection.In,
                Qty = purchase,
                UnitCost = item.LastPurchasePrice,
                RefType = StockMovementRefType.Purchase,
                MovedAtUtc = now.AddDays(-7),
                UserId = userId,
            },
            new StockMovement
            {
                TenantId = tenantId,
                ClinicId = clinicId,
                StockItemId = item.Id,
                Direction = StockMovementDirection.Out,
                Qty = use,
                RefType = StockMovementRefType.TreatmentUse,
                MovedAtUtc = now.AddDays(-1),
                UserId = userId,
            });
        item.CurrentQty = purchase - use; // seed: denormalize alanı hareketlerle tutarlı yaz
    }
}
