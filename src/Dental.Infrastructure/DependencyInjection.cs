using Dental.Application.Abstractions;
using Dental.Application.Appointments;
using Dental.Application.Auth;
using Dental.Application.Patients;
using Dental.Application.Tenants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Appointments;
using Dental.Infrastructure.Auth;
using Dental.Infrastructure.Patients;
using Dental.Infrastructure.Persistence;
using Dental.Infrastructure.Persistence.Interceptors;
using Dental.Infrastructure.Security;
using Dental.Application.Clinical;
using Dental.Application.Consents;
using Dental.Application.Enabiz;
using Dental.Application.Epicrisis;
using Dental.Application.Finance;
using Dental.Application.Invoices;
using Dental.Application.Labs;
using Dental.Application.Media;
using Dental.Application.Messaging;
using Dental.Application.Notifications;
using Dental.Application.Payments;
using Dental.Application.Platform;
using Dental.Application.Reports;
using Dental.Application.Settings;
using Dental.Application.Prescriptions;
using Dental.Application.Stock;
using Dental.Application.Treatments;
using Dental.Infrastructure.Clinical;
using Dental.Infrastructure.Consents;
using Dental.Infrastructure.Epicrisis;
using Dental.Infrastructure.Finance;
using Dental.Infrastructure.Integrations;
using Dental.Infrastructure.Invoices;
using Dental.Infrastructure.Labs;
using Dental.Infrastructure.Media;
using Dental.Infrastructure.Messaging;
using Dental.Infrastructure.Payments;
using Dental.Infrastructure.Prescriptions;
using Dental.Infrastructure.Stock;
using Dental.Infrastructure.Storage;
using Dental.Infrastructure.Tenancy;
using Dental.Infrastructure.Tenants;
using Dental.Infrastructure.Treatments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dental.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<ITenantScopeFactory, TenantScopeFactory>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Default"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<TurnstileOptions>(configuration.GetSection(TurnstileOptions.SectionName));
        services.AddHttpClient(nameof(TurnstileVerifier));

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ITurnstileVerifier, TurnstileVerifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        // TCKN şifrelemesi Data Protection anahtarlarına bağlıdır: anahtar halkası kalıcı
        // bir dizinde tutulmazsa yeniden başlatmada/çoklu instance'ta çözülemez hale gelir
        // (inceleme bulgusu). Dizin yapılandırılabilir; üretimde paylaşımlı hacim/KeyVault.
        var keyRingPath = configuration["DataProtection:KeyRingPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "keys");
        Directory.CreateDirectory(keyRingPath);
        services.AddDataProtection()
            .SetApplicationName("DentalTrackingApp")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        services.AddSingleton<ITcknProtector, TcknProtector>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IRecallService, RecallService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ITreatmentService, TreatmentService>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentPlanService, PaymentPlanService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<ICashRegisterService, CashRegisterService>();

        // E1: dosya depolama + klinik kayıtlar + dijital onam
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<PublicOptions>(configuration.GetSection(PublicOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IAnamnesisService, AnamnesisService>();
        services.AddScoped<IPatientNoteService, PatientNoteService>();
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped<IPublicConsentService, PublicConsentService>();

        // E2: reçete + laboratuvar + stok + epikriz
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<ILabService, LabService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IEpicrisisService, EpicrisisService>();

        // F: e-Fatura / e-Arşiv / e-SMM
        // Sürücülerin kendisi Dental.Integrations'ta keyed olarak kaydedilir (AddIntegrationDrivers);
        // burada yalnız iş katmanı ve tenant ayar deposu bağlanır.
        services.AddScoped<IIntegrationSettingsStore, IntegrationSettingsStore>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ITaxConfigService, TaxConfigService>();
        services.AddScoped<IEDocumentDispatcher, EDocumentDispatcher>();
        services.AddScoped<IInvoiceService, InvoiceService>();

        // G: mesajlaşma altyapısı + otomasyon + sanal POS ödeme linki.
        // Sürücüler Dental.Integrations'ta keyed kayıtlıdır; burada yalnız iş katmanı bağlanır.
        services.Configure<WhatsAppWebhookOptions>(
            configuration.GetSection(WhatsAppWebhookOptions.SectionName));
        services.AddScoped<IMessageOutboxService, MessageOutboxService>();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddScoped<IMessageTemplateService, MessageTemplateService>();
        services.AddScoped<IAutomationRuleService, AutomationRuleService>();
        services.AddScoped<IMessageAutomationService, MessageAutomationService>();
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<IPaymentLinkService, PaymentLinkService>();
        services.AddScoped<IPublicPaymentService, PublicPaymentService>();

        // H: e-Nabız / USS (DHBS). Sürücüler (SysSoapClient/Fake) Dental.Integrations'ta keyed
        // kayıtlıdır; burada paket üretimi, kuyruk ve gönderim motoru bağlanır.
        services.AddScoped<Enabiz.EnabizModeResolver>();
        services.AddScoped<Enabiz.EnabizPacketFactory>();
        services.AddScoped<IEnabizSubmissionQueue, Enabiz.EnabizSubmissionQueue>();
        services.AddScoped<IEnabizDispatcher, Enabiz.EnabizDispatcher>();
        services.AddScoped<IEnabizService, Enabiz.EnabizService>();
        services.AddScoped<ISkrsCodeService, Enabiz.SkrsCodeService>();

        // I: raporlar + gösterge paneli + süper admin + ayarlar + bildirimler
        services.AddScoped<INotificationService, Notifications.NotificationService>();
        services.AddScoped<IReportService, Reports.ReportService>();
        services.AddScoped<IDashboardService, Reports.DashboardService>();
        services.AddScoped<IPatientReportService, Reports.PatientReportService>();
        services.AddScoped<IPlatformAdminService, Platform.PlatformAdminService>();
        services.AddScoped<ISettingsService, Settings.SettingsService>();

        return services;
    }
}
