using System.Linq.Expressions;
using Dental.Application.Abstractions;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : IdentityUserContext<AppUser, long>(options)
{
    private readonly ITenantContext _tenant = tenant;

    // Erişim, global query filter ifadelerinde her sorguda değerlendirilir.
    public long CurrentTenantId => _tenant.TenantId ?? -1;
    public bool BypassTenantFilter => _tenant.IsSuperAdmin;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<ClinicWorkingHour> ClinicWorkingHours => Set<ClinicWorkingHour>();
    public DbSet<UserClinic> UserClinics => Set<UserClinic>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TenantIntegrationSetting> TenantIntegrationSettings => Set<TenantIntegrationSetting>();
    public DbSet<IntegrationCallLog> IntegrationCallLogs => Set<IntegrationCallLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<CommunicationConsent> CommunicationConsents => Set<CommunicationConsent>();
    public DbSet<PatientTag> PatientTags => Set<PatientTag>();
    public DbSet<PatientTagAssignment> PatientTagAssignments => Set<PatientTagAssignment>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<DoctorWorkingHour> DoctorWorkingHours => Set<DoctorWorkingHour>();
    public DbSet<RecallPlan> RecallPlans => Set<RecallPlan>();
    public DbSet<TreatmentCategory> TreatmentCategories => Set<TreatmentCategory>();
    public DbSet<TreatmentDefinition> TreatmentDefinitions => Set<TreatmentDefinition>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<ToothStatus> ToothStatuses => Set<ToothStatus>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<TreatmentRecord> TreatmentRecords => Set<TreatmentRecord>();
    public DbSet<IcdCode> IcdCodes => Set<IcdCode>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentPlan> PaymentPlans => Set<PaymentPlan>();
    public DbSet<PaymentPlanInstallment> PaymentPlanInstallments => Set<PaymentPlanInstallment>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AnamnesisTemplate> AnamnesisTemplates => Set<AnamnesisTemplate>();
    public DbSet<AnamnesisQuestion> AnamnesisQuestions => Set<AnamnesisQuestion>();
    public DbSet<AnamnesisResponse> AnamnesisResponses => Set<AnamnesisResponse>();
    public DbSet<AnamnesisAnswer> AnamnesisAnswers => Set<AnamnesisAnswer>();
    public DbSet<PatientNote> PatientNotes => Set<PatientNote>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<ConsentTemplate> ConsentTemplates => Set<ConsentTemplate>();
    public DbSet<ConsentForm> ConsentForms => Set<ConsentForm>();
    public DbSet<Drug> Drugs => Set<Drug>();
    public DbSet<PrescriptionTemplate> PrescriptionTemplates => Set<PrescriptionTemplate>();
    public DbSet<PrescriptionTemplateItem> PrescriptionTemplateItems => Set<PrescriptionTemplateItem>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Laboratory> Laboratories => Set<Laboratory>();
    public DbSet<LabCase> LabCases => Set<LabCase>();
    public DbSet<LabCaseStatusHistory> LabCaseStatusHistories => Set<LabCaseStatusHistory>();
    public DbSet<StockCategory> StockCategories => Set<StockCategory>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<EpicrisisDocument> EpicrisisDocuments => Set<EpicrisisDocument>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceStatusLog> InvoiceStatusLogs => Set<InvoiceStatusLog>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<GibTaxpayer> GibTaxpayers => Set<GibTaxpayer>();
    public DbSet<TaxConfig> TaxConfigs => Set<TaxConfig>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<EnabizSubmission> EnabizSubmissions => Set<EnabizSubmission>();
    public DbSet<SkrsCodeSystem> SkrsCodeSystems => Set<SkrsCodeSystem>();
    public DbSet<SkrsCode> SkrsCodes => Set<SkrsCode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(b =>
        {
            b.ToTable("Users");
            b.Property(u => u.FirstName).HasMaxLength(100);
            b.Property(u => u.LastName).HasMaxLength(100);
            b.Property(u => u.TcknHash).HasMaxLength(64);
            b.Property(u => u.Color).HasMaxLength(7);
            b.Property(u => u.Branch).HasMaxLength(100);
            b.Property(u => u.Locale).HasMaxLength(5);
            b.HasIndex(u => u.NormalizedEmail).IsUnique();
            b.HasIndex(u => new { u.TenantId, u.UserType });
            b.HasOne(u => u.Tenant).WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Tenant>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(200);
            b.Property(t => t.TaxNumber).HasMaxLength(11);
            b.Property(t => t.TaxOffice).HasMaxLength(100);
            b.Property(t => t.DefaultLocale).HasMaxLength(5);
            b.Property(t => t.PlanCode).HasMaxLength(30);
            b.HasIndex(t => t.PlanCode);
            // Tenant ITenantOwned değildir; convention filtresi uygulanmaz. Silinmiş kiracı
            // job taramalarına ve normal sorgulara girmesin diye soft-delete filtresi elle konur
            // (süper admin panelinde IgnoreQueryFilters ile görülebilir).
            b.HasQueryFilter(t => !t.IsDeleted);
        });

        builder.Entity<Clinic>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(200);
            b.Property(c => c.CkysCode).HasMaxLength(20);
            b.Property(c => c.TimeZone).HasMaxLength(50);
            b.HasIndex(c => c.TenantId);
            b.HasOne(c => c.Tenant).WithMany(t => t.Clinics).HasForeignKey(c => c.TenantId);
        });

        builder.Entity<ClinicWorkingHour>(b =>
        {
            b.HasIndex(w => new { w.TenantId, w.ClinicId });
            b.HasOne(w => w.Clinic).WithMany(c => c.WorkingHours).HasForeignKey(w => w.ClinicId);
        });

        builder.Entity<UserClinic>(b =>
        {
            b.HasKey(uc => new { uc.UserId, uc.ClinicId });
            b.HasOne(uc => uc.User).WithMany(u => u.Clinics).HasForeignKey(uc => uc.UserId);
            b.HasOne(uc => uc.Clinic).WithMany().HasForeignKey(uc => uc.ClinicId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Role>(b =>
        {
            b.Property(r => r.Name).HasMaxLength(100);
            b.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
        });

        builder.Entity<Permission>(b =>
        {
            b.Property(p => p.Code).HasMaxLength(60);
            b.Property(p => p.Module).HasMaxLength(30);
            b.HasIndex(p => p.Code).IsUnique();
        });

        builder.Entity<RolePermission>(b =>
        {
            b.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            b.HasOne(rp => rp.Role).WithMany(r => r.Permissions).HasForeignKey(rp => rp.RoleId);
            b.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId);
        });

        builder.Entity<UserRole>(b =>
        {
            b.HasKey(ur => new { ur.UserId, ur.RoleId });
            b.HasOne(ur => ur.User).WithMany(u => u.Roles).HasForeignKey(ur => ur.UserId);
            b.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.Property(t => t.TokenHash).HasMaxLength(64);
            b.HasIndex(t => t.TokenHash);
            b.HasIndex(t => t.UserId);
            b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId);
        });

        builder.Entity<AuditLog>(b =>
        {
            b.Property(a => a.EntityName).HasMaxLength(100);
            b.HasIndex(a => new { a.TenantId, a.EntityName, a.EntityId });
            b.HasIndex(a => new { a.TenantId, a.AtUtc });
        });

        builder.Entity<TenantIntegrationSetting>(b =>
        {
            b.Property(s => s.IntegrationKey).HasMaxLength(30);
            b.Property(s => s.ProviderKey).HasMaxLength(30);
            b.Property(s => s.Environment).HasMaxLength(10);
            b.HasIndex(s => new { s.TenantId, s.IntegrationKey }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<IntegrationCallLog>(b =>
        {
            b.Property(l => l.IntegrationKey).HasMaxLength(30);
            b.Property(l => l.ProviderKey).HasMaxLength(30);
            b.Property(l => l.Operation).HasMaxLength(60);
            b.Property(l => l.RequestSummary).HasMaxLength(1000);
            b.Property(l => l.ResponseSummary).HasMaxLength(1000);
            b.HasIndex(l => new { l.TenantId, l.CreatedAtUtc });
        });

        builder.Entity<Notification>(b =>
        {
            b.Property(n => n.EventType).HasMaxLength(50);
            b.Property(n => n.Title).HasMaxLength(200);
            b.Property(n => n.Body).HasMaxLength(1000);
            b.Property(n => n.LinkPath).HasMaxLength(300);
            b.HasIndex(n => new { n.TenantId, n.UserId, n.ReadAtUtc });
            // Zil listesi: kiracı + tarihe göre sıralı sayfalama.
            b.HasIndex(n => new { n.TenantId, n.CreatedAtUtc });
        });

        // I: plan + duyuru GLOBAL'dir (kiracıya ait değil) — süper admin yönetir.
        builder.Entity<Plan>(b =>
        {
            b.Property(p => p.Code).HasMaxLength(30).IsUnicode(false);
            b.Property(p => p.Name).HasMaxLength(100);
            b.HasIndex(p => p.Code).IsUnique();
        });

        builder.Entity<Announcement>(b =>
        {
            b.Property(a => a.Title).HasMaxLength(200);
            b.Property(a => a.Body).HasMaxLength(2000);
            // Banner sorgusu: aktif + pencere içi + (hedefsiz | bu kiracı).
            b.HasIndex(a => new { a.IsActive, a.StartsAtUtc, a.EndsAtUtc });
            b.HasIndex(a => a.TargetTenantId);
            b.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TargetTenantId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Patient>(b =>
        {
            b.Property(p => p.FileNo).HasMaxLength(20);
            b.Property(p => p.FirstName).HasMaxLength(100);
            b.Property(p => p.LastName).HasMaxLength(100);
            // Arama kolonu: persisted computed — LIKE aramaları tek kolondan.
            b.Property(p => p.SearchName).HasMaxLength(201)
                .HasComputedColumnSql("UPPER([FirstName] + N' ' + [LastName])", stored: true);
            b.Property(p => p.TcknHash).HasMaxLength(64);
            b.Property(p => p.PassportNo).HasMaxLength(20);
            b.Property(p => p.NationalityCode).HasColumnType("char(3)").HasDefaultValue("TUR");
            b.Property(p => p.Phone).HasMaxLength(20);
            b.Property(p => p.Phone2).HasMaxLength(20);
            b.Property(p => p.Email).HasMaxLength(200);
            b.Property(p => p.City).HasMaxLength(100);
            b.Property(p => p.District).HasMaxLength(100);
            b.Property(p => p.ReferralSource).HasMaxLength(100);
            b.Property(p => p.Profession).HasMaxLength(100);
            b.HasIndex(p => new { p.TenantId, p.FileNo }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(p => new { p.TenantId, p.TcknHash }).IsUnique()
                .HasFilter("[TcknHash] IS NOT NULL AND [IsDeleted] = 0");
            b.HasIndex(p => new { p.TenantId, p.SearchName });
            b.HasIndex(p => new { p.TenantId, p.Phone });
            // I: borçlu hastalar raporu + yaşlandırma bakiyeye göre süzer.
            b.HasIndex(p => new { p.TenantId, p.Balance });
            b.HasOne<Clinic>().WithMany().HasForeignKey(p => p.ClinicId).OnDelete(DeleteBehavior.NoAction);
            // D aşaması: anlaşmalı kurum artık gerçek FK.
            b.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<CommunicationConsent>(b =>
        {
            b.HasIndex(c => new { c.TenantId, c.PatientId, c.ConsentType }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasOne(c => c.Patient).WithMany(p => p.Consents).HasForeignKey(c => c.PatientId);
        });

        builder.Entity<PatientTag>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(100);
            b.Property(t => t.ColorHex).HasMaxLength(7);
            b.HasIndex(t => new { t.TenantId, t.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<PatientTagAssignment>(b =>
        {
            b.HasKey(a => new { a.PatientId, a.PatientTagId });
            b.HasOne(a => a.Patient).WithMany(p => p.Tags).HasForeignKey(a => a.PatientId);
            b.HasOne(a => a.Tag).WithMany().HasForeignKey(a => a.PatientTagId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Appointment>(b =>
        {
            b.Property(a => a.Title).HasMaxLength(200);
            b.Property(a => a.Note).HasMaxLength(1000);
            b.Property(a => a.Color).HasMaxLength(7);
            b.Property(a => a.CancelReason).HasMaxLength(500);
            b.Property(a => a.RowVersion).IsRowVersion();
            b.HasIndex(a => new { a.TenantId, a.ClinicId, a.StartUtc });
            b.HasIndex(a => new { a.TenantId, a.DoctorUserId, a.StartUtc });
            b.HasIndex(a => new { a.TenantId, a.PatientId });
            // I: randevu raporu (doluluk / no-show trendi) durum kırılımıyla tarih tarar.
            b.HasIndex(a => new { a.TenantId, a.StartUtc, a.Status });
            b.HasOne<Clinic>().WithMany().HasForeignKey(a => a.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(a => a.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<DoctorWorkingHour>(b =>
        {
            b.HasIndex(w => new { w.TenantId, w.DoctorUserId, w.ClinicId });
            b.HasOne<AppUser>().WithMany().HasForeignKey(w => w.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<RecallPlan>(b =>
        {
            b.Property(r => r.Reason).HasMaxLength(300);
            b.HasIndex(r => new { r.TenantId, r.Status, r.SuggestedDate });
            b.HasOne<Patient>().WithMany().HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Appointment>().WithMany().HasForeignKey(r => r.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<TreatmentCategory>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100);
            b.Property(c => c.NameEn).HasMaxLength(100);
            b.Property(c => c.ColorHex).HasMaxLength(7);
            b.HasIndex(c => new { c.TenantId, c.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<TreatmentDefinition>(b =>
        {
            b.Property(d => d.Code).HasMaxLength(20);
            b.Property(d => d.Name).HasMaxLength(200);
            b.Property(d => d.NameEn).HasMaxLength(200);
            b.Property(d => d.SutCode).HasMaxLength(20);
            b.Property(d => d.VatRate).HasColumnType("decimal(5,2)");
            b.HasIndex(d => new { d.TenantId, d.CategoryId });
            b.HasIndex(d => new { d.TenantId, d.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasOne(d => d.Category).WithMany().HasForeignKey(d => d.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PriceList>(b =>
        {
            b.Property(p => p.Name).HasMaxLength(100);
            b.Property(p => p.CurrencyCode).HasColumnType("char(3)").HasDefaultValue("TRY");
            b.HasIndex(p => new { p.TenantId, p.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<PriceListItem>(b =>
        {
            b.HasIndex(i => new { i.TenantId, i.PriceListId, i.TreatmentDefinitionId })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasOne(i => i.PriceList).WithMany(p => p.Items).HasForeignKey(i => i.PriceListId);
            b.HasOne(i => i.TreatmentDefinition).WithMany().HasForeignKey(i => i.TreatmentDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ToothStatus>(b =>
        {
            b.Property(t => t.ToothNumber).HasColumnType("char(2)");
            b.HasIndex(t => new { t.TenantId, t.PatientId, t.ToothNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasOne<Patient>().WithMany().HasForeignKey(t => t.PatientId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Visit>(b =>
        {
            b.Property(v => v.ProtocolNo).HasMaxLength(20);
            b.Property(v => v.SysTakipNo).HasMaxLength(30);
            b.HasIndex(v => new { v.TenantId, v.ProtocolNo }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(v => new { v.TenantId, v.PatientId, v.VisitDate });
            b.HasOne<Clinic>().WithMany().HasForeignKey(v => v.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(v => v.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(v => v.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<TreatmentRecord>(b =>
        {
            b.Property(t => t.ToothNumber).HasColumnType("char(2)");
            b.Property(t => t.DiagnosisIcdCode).HasMaxLength(10).IsUnicode(false);
            b.Property(t => t.VatRate).HasColumnType("decimal(5,2)");
            b.Property(t => t.CurrencyCode).HasColumnType("char(3)").HasDefaultValue("TRY");
            b.Property(t => t.Description).HasMaxLength(1000);
            b.HasIndex(t => new { t.TenantId, t.PatientId, t.Status });
            b.HasIndex(t => new { t.TenantId, t.DoctorUserId, t.PerformedAtUtc });
            // I: tedavi/ciro raporu Done kayıtları tarih aralığında tarar (hekim süzmesi opsiyonel).
            b.HasIndex(t => new { t.TenantId, t.Status, t.PerformedAtUtc });
            b.HasOne<Clinic>().WithMany().HasForeignKey(t => t.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(t => t.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(t => t.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(t => t.TreatmentDefinition).WithMany().HasForeignKey(t => t.TreatmentDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Visit>().WithMany().HasForeignKey(t => t.VisitId).OnDelete(DeleteBehavior.NoAction);
            // D aşaması: Done geçişinde oluşan borç kaydı artık gerçek FK.
            b.HasOne<LedgerEntry>().WithMany().HasForeignKey(t => t.LedgerEntryId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<IcdCode>(b =>
        {
            b.Property(c => c.Code).HasMaxLength(10).IsUnicode(false);
            b.Property(c => c.Name).HasMaxLength(300);
            b.Property(c => c.NameEn).HasMaxLength(300);
            b.HasIndex(c => c.Code).IsUnique();
        });

        builder.Entity<Company>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(200);
            b.Property(c => c.TaxOffice).HasMaxLength(100);
            b.Property(c => c.Vkn).HasMaxLength(10).IsUnicode(false);
            b.Property(c => c.Address).HasMaxLength(300);
            b.Property(c => c.Email).HasMaxLength(200);
            b.Property(c => c.Phone).HasMaxLength(20);
            b.Property(c => c.EInvoiceAlias).HasMaxLength(150);
            b.HasIndex(c => new { c.TenantId, c.Name });
            b.HasOne<PriceList>().WithMany().HasForeignKey(c => c.PriceListId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<LedgerEntry>(b =>
        {
            b.Property(e => e.CurrencyCode).HasColumnType("char(3)").HasDefaultValue("TRY");
            b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m);
            b.Property(e => e.Description).HasMaxLength(500);
            b.Property(e => e.RefType).HasMaxLength(30);
            b.Property(e => e.RowVersion).IsRowVersion();
            b.HasIndex(e => new { e.TenantId, e.PatientId, e.EntryDate });
            b.HasIndex(e => new { e.TenantId, e.CompanyId, e.EntryDate });
            b.HasOne<Clinic>().WithMany().HasForeignKey(e => e.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Payment>(b =>
        {
            b.Property(p => p.CurrencyCode).HasColumnType("char(3)").HasDefaultValue("TRY");
            b.Property(p => p.ExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m);
            b.Property(p => p.Note).HasMaxLength(500);
            b.HasIndex(p => new { p.TenantId, p.PatientId });
            b.HasIndex(p => new { p.TenantId, p.ClinicId, p.ReceivedAtUtc });
            // I: ciro/tahsilat raporları klinik süzmeden tüm kiracıyı tarar.
            b.HasIndex(p => new { p.TenantId, p.ReceivedAtUtc });
            b.HasOne<Clinic>().WithMany().HasForeignKey(p => p.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(p => p.ReceivedByUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<LedgerEntry>().WithMany().HasForeignKey(p => p.LedgerEntryId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PaymentPlan>(b =>
        {
            b.Property(p => p.Note).HasMaxLength(500);
            b.HasIndex(p => new { p.TenantId, p.PatientId });
            b.HasOne<Patient>().WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PaymentPlanInstallment>(b =>
        {
            b.HasIndex(i => new { i.TenantId, i.PlanId, i.SeqNo });
            b.HasIndex(i => new { i.TenantId, i.Status, i.DueDate }); // gecikmiş taksit sorguları/hatırlatma job'ı
            b.HasOne(i => i.Plan).WithMany(p => p.Installments).HasForeignKey(i => i.PlanId);
        });

        builder.Entity<ExpenseCategory>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100);
            b.HasIndex(c => new { c.TenantId, c.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<Expense>(b =>
        {
            b.Property(e => e.Description).HasMaxLength(500);
            b.HasIndex(e => new { e.TenantId, e.ClinicId, e.ExpenseDate });
            b.HasIndex(e => new { e.TenantId, e.CategoryId });
            // I: gelir-gider raporu klinik süzmeden aylık toplar.
            b.HasIndex(e => new { e.TenantId, e.ExpenseDate });
            b.HasOne<Clinic>().WithMany().HasForeignKey(e => e.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(e => e.PaidByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<AnamnesisTemplate>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(150);
            b.HasIndex(t => new { t.TenantId, t.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<AnamnesisQuestion>(b =>
        {
            b.Property(q => q.QuestionText).HasMaxLength(500);
            b.Property(q => q.QuestionTextEn).HasMaxLength(500);
            b.Property(q => q.OptionsJson).HasMaxLength(2000);
            b.HasIndex(q => new { q.TenantId, q.TemplateId, q.SortOrder });
            b.HasOne(q => q.Template).WithMany(t => t.Questions).HasForeignKey(q => q.TemplateId);
        });

        builder.Entity<AnamnesisResponse>(b =>
        {
            b.HasIndex(r => new { r.TenantId, r.PatientId, r.FilledAtUtc });
            b.HasOne<Patient>().WithMany().HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AnamnesisTemplate>().WithMany().HasForeignKey(r => r.TemplateId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(r => r.FilledByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<AnamnesisAnswer>(b =>
        {
            b.Property(a => a.TextValue).HasMaxLength(1000);
            b.HasIndex(a => new { a.TenantId, a.ResponseId });
            b.HasOne(a => a.Response).WithMany(r => r.Answers).HasForeignKey(a => a.ResponseId);
            b.HasOne<AnamnesisQuestion>().WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PatientNote>(b =>
        {
            b.Property(n => n.NoteText).HasMaxLength(4000);
            b.Property(n => n.ColorHex).HasMaxLength(7);
            b.HasIndex(n => new { n.TenantId, n.PatientId, n.IsPinned });
            b.HasOne<Patient>().WithMany().HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(n => n.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MediaFile>(b =>
        {
            b.Property(m => m.FileName).HasMaxLength(255);
            b.Property(m => m.ContentType).HasMaxLength(100);
            b.Property(m => m.StorageKey).HasMaxLength(400);
            b.Property(m => m.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
            b.Property(m => m.ThumbnailKey).HasMaxLength(400);
            b.Property(m => m.Description).HasMaxLength(500);
            b.Property(m => m.ToothNumber).HasColumnType("char(2)");
            b.HasIndex(m => new { m.TenantId, m.PatientId, m.Category });
            b.HasOne<Clinic>().WithMany().HasForeignKey(m => m.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UploadedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ConsentTemplate>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(150);
            b.Property(t => t.Locale).HasMaxLength(5);
            b.HasIndex(t => new { t.TenantId, t.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<ConsentForm>(b =>
        {
            b.Property(f => f.SignerIp).HasMaxLength(45);
            b.Property(f => f.SignerUserAgent).HasMaxLength(400);
            b.Property(f => f.PdfSha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
            b.HasIndex(f => f.SignToken).IsUnique(); // public token araması (filtresiz, global)
            b.HasIndex(f => new { f.TenantId, f.PatientId });
            b.HasOne<Clinic>().WithMany().HasForeignKey(f => f.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(f => f.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(f => f.Template).WithMany().HasForeignKey(f => f.TemplateId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<TreatmentRecord>().WithMany().HasForeignKey(f => f.TreatmentRecordId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MediaFile>().WithMany().HasForeignKey(f => f.SignatureFileId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MediaFile>().WithMany().HasForeignKey(f => f.PdfFileId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---- E2: Reçete + Laboratuvar + Stok + Epikriz ----

        builder.Entity<Drug>(b =>
        {
            // Bilerek ITenantOwned değil: TenantId NULL = merkezi liste, dolu = kiracı özel satır.
            // Görünürlük servislerde elle filtrelenir (TenantId == null || TenantId == current).
            b.Property(d => d.Barcode).HasMaxLength(20).IsUnicode(false);
            b.Property(d => d.Name).HasMaxLength(300);
            b.Property(d => d.AtcCode).HasMaxLength(10).IsUnicode(false);
            b.Property(d => d.Form).HasMaxLength(50);
            b.Property(d => d.DefaultDose).HasMaxLength(100);
            b.Property(d => d.DefaultUsage).HasMaxLength(100);
            b.HasIndex(d => d.Name);
            b.HasIndex(d => d.Barcode);
            b.HasIndex(d => d.TenantId);
        });

        builder.Entity<PrescriptionTemplate>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(150);
            b.HasIndex(t => new { t.TenantId, t.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<PrescriptionTemplateItem>(b =>
        {
            b.Property(i => i.Dose).HasMaxLength(100);
            b.Property(i => i.Frequency).HasMaxLength(50);
            b.Property(i => i.Duration).HasMaxLength(50);
            b.Property(i => i.UsageNote).HasMaxLength(300);
            b.HasIndex(i => new { i.TenantId, i.TemplateId });
            b.HasOne(i => i.Template).WithMany(t => t.Items).HasForeignKey(i => i.TemplateId);
            b.HasOne(i => i.Drug).WithMany().HasForeignKey(i => i.DrugId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Prescription>(b =>
        {
            b.Property(p => p.PrescriptionNo).HasMaxLength(20).IsUnicode(false);
            b.Property(p => p.RecetemCode).HasMaxLength(20);
            b.HasIndex(p => new { p.TenantId, p.PrescriptionNo }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(p => new { p.TenantId, p.PatientId });
            b.HasOne<Clinic>().WithMany().HasForeignKey(p => p.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(p => p.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Visit>().WithMany().HasForeignKey(p => p.VisitId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MediaFile>().WithMany().HasForeignKey(p => p.PdfFileId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PrescriptionItem>(b =>
        {
            b.Property(i => i.Dose).HasMaxLength(100);
            b.Property(i => i.Frequency).HasMaxLength(50);
            b.Property(i => i.Duration).HasMaxLength(50);
            b.Property(i => i.UsageNote).HasMaxLength(300);
            b.HasIndex(i => new { i.TenantId, i.PrescriptionId });
            b.HasOne(i => i.Prescription).WithMany(p => p.Items).HasForeignKey(i => i.PrescriptionId);
            b.HasOne(i => i.Drug).WithMany().HasForeignKey(i => i.DrugId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Laboratory>(b =>
        {
            b.Property(l => l.Name).HasMaxLength(200);
            b.Property(l => l.Phone).HasMaxLength(20);
            b.Property(l => l.Email).HasMaxLength(200);
            b.Property(l => l.Address).HasMaxLength(300);
            b.Property(l => l.ContactPerson).HasMaxLength(100);
            b.HasIndex(l => new { l.TenantId, l.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<LabCase>(b =>
        {
            b.Property(c => c.CaseNo).HasMaxLength(20).IsUnicode(false);
            b.Property(c => c.WorkType).HasMaxLength(100);
            b.Property(c => c.TeethCsv).HasMaxLength(200).IsUnicode(false);
            b.Property(c => c.Shade).HasMaxLength(10);
            b.Property(c => c.Material).HasMaxLength(100);
            b.Property(c => c.Note).HasMaxLength(1000);
            b.HasIndex(c => new { c.TenantId, c.CaseNo }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(c => new { c.TenantId, c.Status, c.DueDate });
            b.HasIndex(c => new { c.TenantId, c.PatientId });
            b.HasOne<Clinic>().WithMany().HasForeignKey(c => c.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(c => c.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(c => c.Laboratory).WithMany().HasForeignKey(c => c.LaboratoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<LabCaseStatusHistory>(b =>
        {
            b.Property(h => h.Note).HasMaxLength(500);
            b.HasIndex(h => new { h.TenantId, h.LabCaseId });
            b.HasOne<LabCase>().WithMany().HasForeignKey(h => h.LabCaseId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StockCategory>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100);
            b.HasIndex(c => new { c.TenantId, c.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<StockItem>(b =>
        {
            b.Property(i => i.Name).HasMaxLength(200);
            b.Property(i => i.Barcode).HasMaxLength(30).IsUnicode(false);
            b.Property(i => i.Unit).HasMaxLength(20);
            b.Property(i => i.CurrentQty).HasColumnType("decimal(18,3)");
            b.Property(i => i.MinQty).HasColumnType("decimal(18,3)");
            b.HasIndex(i => new { i.TenantId, i.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(i => new { i.TenantId, i.ClinicId });
            b.HasOne<Clinic>().WithMany().HasForeignKey(i => i.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(i => i.Category).WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StockMovement>(b =>
        {
            b.Property(m => m.Qty).HasColumnType("decimal(18,3)");
            b.Property(m => m.Note).HasMaxLength(500);
            b.HasIndex(m => new { m.TenantId, m.StockItemId, m.MovedAtUtc });
            b.HasOne<Clinic>().WithMany().HasForeignKey(m => m.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<StockItem>().WithMany().HasForeignKey(m => m.StockItemId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EpicrisisDocument>(b =>
        {
            b.Property(e => e.Title).HasMaxLength(200);
            b.HasIndex(e => new { e.TenantId, e.PatientId });
            b.HasOne<Clinic>().WithMany().HasForeignKey(e => e.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(e => e.DoctorUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MediaFile>().WithMany().HasForeignKey(e => e.PdfFileId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---- F: e-Fatura / e-Arşiv / e-SMM ----

        builder.Entity<Invoice>(b =>
        {
            b.Property(i => i.ProfileId).HasMaxLength(20).IsUnicode(false);
            b.Property(i => i.TypeCode).HasMaxLength(15).IsUnicode(false);
            b.Property(i => i.InvoiceNumber).HasColumnType("char(16)").IsUnicode(false);
            b.Property(i => i.Serial).HasColumnType("char(3)").IsUnicode(false);
            b.Property(i => i.BuyerName).HasMaxLength(200);
            b.Property(i => i.BuyerTcknVkn).HasMaxLength(11).IsUnicode(false);
            b.Property(i => i.BuyerPassportNo).HasMaxLength(20).IsUnicode(false);
            b.Property(i => i.BuyerNationality).HasMaxLength(3).IsUnicode(false);
            b.Property(i => i.BuyerAddress).HasMaxLength(300);
            b.Property(i => i.BuyerCity).HasMaxLength(100);
            b.Property(i => i.BuyerDistrict).HasMaxLength(100);
            b.Property(i => i.BuyerEmail).HasMaxLength(200);
            b.Property(i => i.BuyerTaxOffice).HasMaxLength(100);
            b.Property(i => i.BuyerAlias).HasMaxLength(150);
            b.Property(i => i.CurrencyCode).HasColumnType("char(3)").HasDefaultValue("TRY");
            b.Property(i => i.ExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m);
            b.Property(i => i.ExemptionCode).HasMaxLength(5).IsUnicode(false);
            b.Property(i => i.ExemptionReason).HasMaxLength(300);
            b.Property(i => i.WithholdingCode).HasMaxLength(5).IsUnicode(false);
            b.Property(i => i.IntegratorRefId).HasMaxLength(100);
            b.Property(i => i.ErrorMessage).HasMaxLength(2000);
            b.HasIndex(i => new { i.TenantId, i.Status });
            b.HasIndex(i => new { i.TenantId, i.IssueDate });
            // Numara Draft'ta NULL; benzersizlik yalnız atanmış numaralarda geçerli.
            b.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique()
                .HasFilter("[InvoiceNumber] IS NOT NULL AND [IsDeleted] = 0");
            b.HasIndex(i => i.Ettn).IsUnique().HasFilter("[Ettn] IS NOT NULL");
            // Kuyruk/durum sorgusu job'ların sıcak yolu.
            b.HasIndex(i => new { i.TenantId, i.Status, i.NextAttemptAtUtc });
            b.HasOne<Clinic>().WithMany().HasForeignKey(i => i.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(i => i.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Company>().WithMany().HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MediaFile>().WithMany().HasForeignKey(i => i.UblFileId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MediaFile>().WithMany().HasForeignKey(i => i.PdfFileId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Invoice>().WithMany().HasForeignKey(i => i.SourceInvoiceId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<InvoiceLine>(b =>
        {
            b.Property(l => l.ItemName).HasMaxLength(300);
            b.Property(l => l.Quantity).HasColumnType("decimal(18,3)");
            b.Property(l => l.UnitCode).HasMaxLength(10).IsUnicode(false);
            b.Property(l => l.VatRate).HasColumnType("decimal(5,2)");
            b.HasIndex(l => new { l.TenantId, l.InvoiceId, l.SeqNo });
            b.HasOne(l => l.Invoice).WithMany(i => i.Lines).HasForeignKey(l => l.InvoiceId);
            b.HasOne<TreatmentRecord>().WithMany().HasForeignKey(l => l.TreatmentRecordId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<InvoiceStatusLog>(b =>
        {
            b.HasIndex(l => new { l.TenantId, l.InvoiceId, l.AtUtc });
            b.HasOne<Invoice>().WithMany().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(l => l.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<NumberSequence>(b =>
        {
            b.Property(s => s.Serial).HasColumnType("char(3)").IsUnicode(false);
            b.HasIndex(s => new { s.TenantId, s.SequenceType, s.Serial, s.Year })
                .IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<GibTaxpayer>(b =>
        {
            // Global (kiracısız) cache — mükellef aynası tüm kiracılar için ortaktır.
            b.HasKey(g => g.Vkn);
            b.Property(g => g.Vkn).HasMaxLength(11).IsUnicode(false);
            b.Property(g => g.Title).HasMaxLength(300);
            b.Property(g => g.Alias).HasMaxLength(150);
            b.Property(g => g.AccountType).HasMaxLength(5).IsUnicode(false);
        });

        builder.Entity<TaxConfig>(b =>
        {
            b.Property(t => t.Code).HasMaxLength(10).IsUnicode(false);
            b.Property(t => t.Description).HasMaxLength(300);
            b.Property(t => t.Rate).HasColumnType("decimal(9,4)");
            b.HasIndex(t => new { t.ConfigType, t.Code, t.ValidFrom }).IsUnique();
        });

        // ---- G: mesajlaşma + otomasyon + ödeme linki ----

        builder.Entity<OutboundMessage>(b =>
        {
            b.Property(m => m.TemplateKey).HasMaxLength(50).IsUnicode(false);
            b.Property(m => m.RenderedBody).HasMaxLength(1000);
            b.Property(m => m.ParamsJson).HasMaxLength(2000);
            b.Property(m => m.ToAddress).HasMaxLength(200);
            b.Property(m => m.ProviderKey).HasMaxLength(30).IsUnicode(false);
            b.Property(m => m.ProviderMessageId).HasMaxLength(120);
            b.Property(m => m.Error).HasMaxLength(1000);
            b.Property(m => m.RefType).HasMaxLength(30).IsUnicode(false);
            b.Property(m => m.CorrelationId).HasMaxLength(40).IsUnicode(false);
            b.Property(m => m.CreditCost).HasColumnType("decimal(9,4)");
            // Dispatcher'ın sıcak yolu: kiracı + durum + zamanlama.
            b.HasIndex(m => new { m.TenantId, m.State, m.ScheduledAtUtc });
            b.HasIndex(m => new { m.TenantId, m.PatientId, m.CreatedAtUtc });
            // Teslim webhook'u sağlayıcı kimliğinden kiracıyı bulur (filtresiz, global arama).
            b.HasIndex(m => m.ProviderMessageId).HasFilter("[ProviderMessageId] IS NOT NULL");
            b.HasOne<Patient>().WithMany().HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<OutboundMessage>().WithMany().HasForeignKey(m => m.FallbackOfMessageId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MessageTemplate>(b =>
        {
            b.Property(t => t.TemplateKey).HasMaxLength(50).IsUnicode(false);
            b.Property(t => t.Locale).HasMaxLength(5).IsUnicode(false);
            b.Property(t => t.Body).HasMaxLength(1000);
            b.HasIndex(t => new { t.TenantId, t.TemplateKey, t.Channel, t.Locale })
                .IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<WhatsAppTemplate>(b =>
        {
            b.Property(t => t.TemplateName).HasMaxLength(100).IsUnicode(false);
            b.Property(t => t.Language).HasMaxLength(10).IsUnicode(false);
            b.Property(t => t.Category).HasMaxLength(20).IsUnicode(false);
            b.Property(t => t.BodySpec).HasMaxLength(1024);
            b.Property(t => t.ParamMapJson).HasMaxLength(1000);
            b.Property(t => t.TemplateKey).HasMaxLength(50).IsUnicode(false);
            b.HasIndex(t => new { t.TenantId, t.TemplateName, t.Language })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(t => new { t.TenantId, t.TemplateKey, t.MetaStatus });
        });

        builder.Entity<AutomationRule>(b =>
        {
            b.Property(r => r.TemplateKey).HasMaxLength(50).IsUnicode(false);
            b.HasIndex(r => new { r.TenantId, r.RuleType }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<PaymentIntent>(b =>
        {
            b.Property(i => i.CurrencyCode).HasColumnType("char(3)").HasDefaultValue("TRY");
            b.Property(i => i.Description).HasMaxLength(300);
            b.Property(i => i.ConversationId).HasMaxLength(50).IsUnicode(false);
            b.Property(i => i.ProviderKey).HasMaxLength(30).IsUnicode(false);
            b.Property(i => i.ProviderToken).HasMaxLength(200).IsUnicode(false);
            b.Property(i => i.LinkUrl).HasMaxLength(1000);
            b.Property(i => i.ProviderPaymentId).HasMaxLength(100).IsUnicode(false);
            // İdempotan callback: aynı sağlayıcı ödemesi ikinci kez tahsilat açamaz.
            b.HasIndex(i => i.ProviderPaymentId).IsUnique()
                .HasFilter("[ProviderPaymentId] IS NOT NULL AND [IsDeleted] = 0");
            // Public sayfa ve callback token araması (filtresiz, global).
            b.HasIndex(i => i.PublicToken).IsUnique();
            b.HasIndex(i => i.ProviderToken).HasFilter("[ProviderToken] IS NOT NULL");
            b.HasIndex(i => new { i.TenantId, i.PatientId, i.Status });
            b.HasIndex(i => new { i.TenantId, i.Status, i.ExpiresAtUtc });
            b.HasOne<Clinic>().WithMany().HasForeignKey(i => i.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Patient>().WithMany().HasForeignKey(i => i.PatientId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Payment>().WithMany().HasForeignKey(i => i.PaymentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AppUser>().WithMany().HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---- H: e-Nabız / USS ----

        builder.Entity<EnabizSubmission>(b =>
        {
            b.Property(s => s.FacilityCode).HasMaxLength(20).IsUnicode(false);
            b.Property(s => s.SysTakipNo).HasMaxLength(30).IsUnicode(false);
            b.Property(s => s.LastErrorCode).HasMaxLength(20).IsUnicode(false);
            b.Property(s => s.LastErrorMessage).HasMaxLength(2000);
            b.Property(s => s.RegenerateOnSend).HasDefaultValue(true);
            // Kuyruk taraması: durum + zamanı gelmiş kayıtlar.
            b.HasIndex(s => new { s.TenantId, s.State, s.NextAttemptAtUtc });
            b.HasIndex(s => new { s.TenantId, s.VisitId });
            b.HasOne<Clinic>().WithMany().HasForeignKey(s => s.ClinicId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Visit>().WithMany().HasForeignKey(s => s.VisitId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<TreatmentRecord>().WithMany().HasForeignKey(s => s.TreatmentRecordId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Prescription>().WithMany().HasForeignKey(s => s.PrescriptionId)
                .OnDelete(DeleteBehavior.NoAction);
            // Bağımlılık kendine referans: 101 kabul edilmeden bağımlılar gönderilmez.
            b.HasOne<EnabizSubmission>().WithMany().HasForeignKey(s => s.DependsOnSubmissionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // SKRS kod setleri GLOBAL'dir (kiracıya ait değil) — Bakanlık referansıdır.
        builder.Entity<SkrsCodeSystem>(b =>
        {
            b.Property(s => s.Name).HasMaxLength(200);
            b.Property(s => s.Version).HasMaxLength(20).IsUnicode(false);
            b.HasIndex(s => s.CodeSystemGuid).IsUnique();
        });

        builder.Entity<SkrsCode>(b =>
        {
            b.Property(c => c.Code).HasMaxLength(40).IsUnicode(false);
            b.Property(c => c.Name).HasMaxLength(500);
            b.Property(c => c.ParentCode).HasMaxLength(40).IsUnicode(false);
            b.HasIndex(c => new { c.CodeSystemGuid, c.Code }).IsUnique();
            b.HasOne(c => c.CodeSystem).WithMany(s => s.Codes)
                .HasPrincipalKey(s => s.CodeSystemGuid)
                .HasForeignKey(c => c.CodeSystemGuid)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // D aşamasında düz kolon bırakılan bağ artık gerçek FK.
        builder.Entity<Payment>()
            .HasOne<PaymentIntent>().WithMany().HasForeignKey(p => p.PaymentIntentId)
            .OnDelete(DeleteBehavior.NoAction);

        ApplyGlobalConventions(builder);
    }

    /// <summary>
    /// ITenantOwned + ISoftDelete uygulayan tüm entity'lere convention ile:
    /// (1) tenant + soft-delete global query filter, (2) decimal varsayılan hassasiyeti.
    /// SuperAdmin bağlamında tenant filtresi atlanır (soft-delete filtresi kalır).
    /// </summary>
    private void ApplyGlobalConventions(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;

            if (typeof(ITenantOwned).IsAssignableFrom(clr) && typeof(ISoftDelete).IsAssignableFrom(clr))
            {
                var parameter = Expression.Parameter(clr, "e");
                // e => (BypassTenantFilter || e.TenantId == CurrentTenantId) && !e.IsDeleted
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantOwned.TenantId));
                var isDeletedProp = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var self = Expression.Constant(this);
                var bypass = Expression.Property(self, nameof(BypassTenantFilter));
                var currentTenant = Expression.Property(self, nameof(CurrentTenantId));
                var body = Expression.AndAlso(
                    Expression.OrElse(bypass, Expression.Equal(tenantIdProp, currentTenant)),
                    Expression.Not(isDeletedProp));
                entityType.SetQueryFilter(Expression.Lambda(body, parameter));

                builder.Entity(clr).HasIndex(nameof(ITenantOwned.TenantId));
            }

            foreach (var property in entityType.GetProperties()
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                if (property.GetColumnType() is null)
                    property.SetColumnType("decimal(18,2)");
            }
        }
    }
}
