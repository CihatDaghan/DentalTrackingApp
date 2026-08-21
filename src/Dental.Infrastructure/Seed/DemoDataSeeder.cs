using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Demo kiracıya gerçekçi örnek veri: 2 hekim + 1 sekreter, 8 hasta, bu haftanın randevuları, 1 recall.
/// Idempotent: demo kiracıda hekim varsa hiçbir şey yapmaz. Yalnız dev seed zincirinden çağrılır.
/// </summary>
public static class DemoDataSeeder
{
    private static readonly TimeSpan TrOffset = TimeSpan.FromHours(3); // Europe/Istanbul (sabit UTC+3)

    public static async Task ApplyAsync(IServiceProvider sp, AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var demoTenantId = await db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == DbSeeder.DemoEmail.ToUpperInvariant())
            .Select(u => u.TenantId)
            .FirstOrDefaultAsync(ct);
        if (demoTenantId is not { } tenantId) return;

        var hasDentist = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenantId && u.UserType == UserType.Dentist, ct);
        if (hasDentist) return;

        var clinicId = await db.Clinics.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => c.Id)
            .FirstAsync(ct);

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var roles = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);

        async Task<AppUser> CreateUser(string email, string first, string last, UserType type, string role, string? color, string? branch)
        {
            var user = new AppUser
            {
                TenantId = tenantId,
                UserName = email,
                Email = email,
                FirstName = first,
                LastName = last,
                UserType = type,
                EmailConfirmed = true,
                Color = color,
                Branch = branch,
            };
            var result = await userManager.CreateAsync(user, "Demo!2026");
            if (!result.Succeeded)
                throw new InvalidOperationException($"Demo kullanıcı seed başarısız ({email}): " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            db.UserClinics.Add(new UserClinic { UserId = user.Id, ClinicId = clinicId, IsDefault = true });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[role] });
            return user;
        }

        var hekim1 = await CreateUser("elif.kaya@dental.local", "Elif", "Kaya", UserType.Dentist, "Dentist", "#3b82f6", "Genel Diş Hekimliği");
        var hekim2 = await CreateUser("mert.demir@dental.local", "Mert", "Demir", UserType.Dentist, "Dentist", "#10b981", "Ortodonti");
        await CreateUser("zeynep.aydin@dental.local", "Zeynep", "Aydın", UserType.Secretary, "Secretary", null, null);

        var patients = new (string First, string Last, string Phone, int BirthYear, Gender Gender)[]
        {
            ("Ahmet", "Yılmaz", "+905321110001", 1985, Gender.Male),
            ("Ayşe", "Demirtaş", "+905321110002", 1992, Gender.Female),
            ("Mehmet", "Çelik", "+905321110003", 1978, Gender.Male),
            ("Fatma", "Şahin", "+905321110004", 2001, Gender.Female),
            ("Emre", "Koç", "+905321110005", 1995, Gender.Male),
            ("Selin", "Arslan", "+905321110006", 1988, Gender.Female),
            ("Can", "Öztürk", "+905321110007", 2015, Gender.Male),
            ("Elena", "Petrova", "+905321110008", 1990, Gender.Female),
        };
        var patientEntities = new List<Patient>();
        for (var i = 0; i < patients.Length; i++)
        {
            var p = patients[i];
            var patient = new Patient
            {
                TenantId = tenantId,
                ClinicId = clinicId,
                FileNo = (i + 100).ToString(),
                FirstName = p.First,
                LastName = p.Last,
                Phone = p.Phone,
                BirthDate = new DateOnly(p.BirthYear, 3 + (i % 9), 1 + i * 3),
                Gender = p.Gender,
                IdentityType = p.Last == "Petrova" ? IdentityType.Passport : IdentityType.Tckn,
                PassportNo = p.Last == "Petrova" ? "P1234567" : null,
                NationalityCode = p.Last == "Petrova" ? "RUS" : "TUR",
                City = "Ankara",
            };
            db.Patients.Add(patient);
            patientEntities.Add(patient);
        }
        await db.SaveChangesAsync(ct);

        // Bu haftanın randevuları: bugünden itibaren 5 iş günü, iki hekime dağıtılmış.
        var todayTr = DateOnly.FromDateTime(DateTime.UtcNow + TrOffset);
        var slotHours = new[] { 9, 10, 11, 14, 15, 16 };
        var idx = 0;
        for (var day = 0; day < 5; day++)
        {
            var date = todayTr.AddDays(day);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            foreach (var hour in slotHours.Take(day == 0 ? 6 : 3))
            {
                var patient = patientEntities[idx % patientEntities.Count];
                var doctor = idx % 2 == 0 ? hekim1 : hekim2;
                var startTr = date.ToDateTime(new TimeOnly(hour, 0));
                db.Appointments.Add(new Appointment
                {
                    TenantId = tenantId,
                    ClinicId = clinicId,
                    PatientId = patient.Id,
                    DoctorUserId = doctor.Id,
                    StartUtc = startTr - TrOffset,
                    EndUtc = startTr.AddMinutes(45) - TrOffset,
                    Type = idx % 5 == 4 ? AppointmentType.Control : AppointmentType.Normal,
                    Status = day == 0 && hour < 11 ? AppointmentStatus.Completed : AppointmentStatus.Scheduled,
                    Note = idx % 3 == 0 ? "Kontrol + temizlik" : null,
                });
                idx++;
            }
        }

        db.RecallPlans.Add(new RecallPlan
        {
            TenantId = tenantId,
            PatientId = patientEntities[0].Id,
            DoctorUserId = hekim1.Id,
            SuggestedDate = todayTr.AddDays(30),
            Reason = "Dolgu kontrolü",
            Status = RecallStatus.Planned,
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo örnek verisi yüklendi: 3 kullanıcı, {PatientCount} hasta, {AppointmentCount} randevu",
            patientEntities.Count, idx);
    }
}
