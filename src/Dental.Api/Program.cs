using System.Text;
using System.Threading.RateLimiting;
using Dental.Api.Auth;
using Dental.Api.Middleware;
using Dental.Infrastructure;
using Dental.Integrations;
using Dental.Infrastructure.Auth;
using Dental.Infrastructure.Persistence;
using Dental.Infrastructure.Seed;
using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Hangfire, EF migration'dan önce şemasını kurmaya çalışır; veritabanı yoksa önce oluştur.
if (builder.Environment.IsDevelopment())
{
    var cs = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Default"));
    var dbName = cs.InitialCatalog;
    cs.InitialCatalog = "master";
    using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs.ConnectionString);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}]";
    cmd.ExecuteNonQuery();
}

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Dental.Application.Patients.PatientUpsertValidator>();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    // NSwag istemci üretimi 3.0 ile uyumlu (denetim düzeltmesi)
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

// JWT
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.MapInboundClaims = false; // claim adları JWT'deki gibi kalsın (sub, perm, tenant_id...)
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.SuperAdmin, p => p.RequireClaim(AppClaimTypes.SuperAdmin, "true"));

// Public/auth uçları için hız sınırı (denetim düzeltmesi: public uçlara rate limit)
// Limit config'ten okunur; entegrasyon testleri (tek IP'den yoğun login) yükseltir.
var authRatePermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
var publicRatePermitLimit = builder.Configuration.GetValue("RateLimiting:PublicPermitLimit", 30);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = authRatePermitLimit }));
    // Anonim token'lı uçlar (onam imza, ileride ödeme): IP başına 30/dk.
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = publicRatePermitLimit }));
});

// Entegrasyon sürücüleri (e-Belge / SMS / WhatsApp / Ödeme): keyed DI + tenant ayarından seçim.
// ISmsProvider'ın eski düz kaydı buraya taşındı; varsayılan sürücü hâlâ FakeSmsProvider'dır
// (Integrations:DefaultSmsProvider ile değiştirilebilir).
builder.Services.AddIntegrationDrivers(builder.Configuration);
builder.Services.AddScoped<Dental.Jobs.EDocumentJobs>();
builder.Services.AddScoped<Dental.Jobs.MessagingJobs>();
builder.Services.AddScoped<Dental.Jobs.EnabizJobs>();
builder.Services.AddScoped<Dental.Jobs.NotificationJobs>();

// CORS (dev: Angular 4200; prod'da SPA aynı origin'den servis edilir)
const string DevCors = "DevCors";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy => policy
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Hangfire — ayrı 'hangfire' şeması; sunucu API host'unda
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default"),
        new SqlServerStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));
builder.Services.AddHangfireServer();

var app = builder.Build();

// Ters vekil arkasında istemci IP'si X-Forwarded-For'dan alınır; aksi halde hız sınırı ve
// denetim kayıtları tüm trafiği vekil IP'sine yazıyordu (inceleme bulgusu).
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors(DevCors);
}

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
// Hangfire panosu yalnız geliştirmede açıktır. Üretimde varsayılan "yalnız yerel istek"
// filtresi ters vekil arkasında herkesi yerel sayacağından (inceleme bulgusu) pano hiç
// yayınlanmaz; gerekirse SuperAdmin doğrulayan bir IDashboardAuthorizationFilter eklenmelidir.
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions());
}
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Dev: migrate + seed
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(app.Services);
}

// Zamanlanmış e-belge işleri. Job gövdeleri her kiracı için ayrı tenant scope açar
// (ITenantScopeFactory) — job bağlamında global query filter'lar aksi hâlde boş döner.
RecurringJob.AddOrUpdate<Dental.Jobs.EDocumentJobs>(
    Dental.Jobs.EDocumentJobs.DispatchJobId,
    job => job.DispatchQueuedAsync(CancellationToken.None),
    Dental.Jobs.EDocumentJobs.DispatchCron);
RecurringJob.AddOrUpdate<Dental.Jobs.EDocumentJobs>(
    Dental.Jobs.EDocumentJobs.StatusPollJobId,
    job => job.PollStatusesAsync(CancellationToken.None),
    Dental.Jobs.EDocumentJobs.StatusPollCron);
RecurringJob.AddOrUpdate<Dental.Jobs.EDocumentJobs>(
    Dental.Jobs.EDocumentJobs.TaxpayerSyncJobId,
    job => job.SyncGibTaxpayersAsync(CancellationToken.None),
    Dental.Jobs.EDocumentJobs.TaxpayerSyncCron);

// G: mesajlaşma outbox'ı + otomasyon + ödeme linki süre kontrolü.
// Cron ifadeleri UTC'dir; günlük TR saatleri (09:00/10:00) UTC+3 çıkarılarak yazılmıştır.
RecurringJob.AddOrUpdate<Dental.Jobs.MessagingJobs>(
    Dental.Jobs.MessagingJobs.DispatchJobId,
    job => job.DispatchPendingAsync(CancellationToken.None),
    Dental.Jobs.MessagingJobs.DispatchCron);
RecurringJob.AddOrUpdate<Dental.Jobs.MessagingJobs>(
    Dental.Jobs.MessagingJobs.AppointmentReminderJobId,
    job => job.QueueAppointmentRemindersAsync(CancellationToken.None),
    Dental.Jobs.MessagingJobs.AppointmentReminderCron);
RecurringJob.AddOrUpdate<Dental.Jobs.MessagingJobs>(
    Dental.Jobs.MessagingJobs.BirthdayJobId,
    job => job.QueueBirthdayGreetingsAsync(CancellationToken.None),
    Dental.Jobs.MessagingJobs.BirthdayCron);
RecurringJob.AddOrUpdate<Dental.Jobs.MessagingJobs>(
    Dental.Jobs.MessagingJobs.PaymentOverdueJobId,
    job => job.QueuePaymentOverdueRemindersAsync(CancellationToken.None),
    Dental.Jobs.MessagingJobs.PaymentOverdueCron);
RecurringJob.AddOrUpdate<Dental.Jobs.MessagingJobs>(
    Dental.Jobs.MessagingJobs.RecallJobId,
    job => job.QueueRecallRemindersAsync(CancellationToken.None),
    Dental.Jobs.MessagingJobs.RecallCron);
RecurringJob.AddOrUpdate<Dental.Jobs.MessagingJobs>(
    Dental.Jobs.MessagingJobs.PaymentLinkExpiryJobId,
    job => job.ExpirePaymentLinksAsync(CancellationToken.None),
    Dental.Jobs.MessagingJobs.PaymentLinkExpiryCron);

// H: e-Nabız/USS kuyruğu, günlük mutabakat (405) ve SKRS kod seti senkronu.
RecurringJob.AddOrUpdate<Dental.Jobs.EnabizJobs>(
    Dental.Jobs.EnabizJobs.QueueJobId,
    job => job.DispatchQueuedAsync(CancellationToken.None),
    Dental.Jobs.EnabizJobs.QueueCron);
RecurringJob.AddOrUpdate<Dental.Jobs.EnabizJobs>(
    Dental.Jobs.EnabizJobs.ReconcileJobId,
    job => job.ReconcileAsync(CancellationToken.None),
    Dental.Jobs.EnabizJobs.ReconcileCron);
RecurringJob.AddOrUpdate<Dental.Jobs.EnabizJobs>(
    Dental.Jobs.EnabizJobs.SkrsSyncJobId,
    job => job.SyncSkrsAsync(CancellationToken.None),
    Dental.Jobs.EnabizJobs.SkrsSyncCron);

// I: tarama gerektiren bildirimler (düşük stok). Olay bazlı bildirimler servislerde üretilir.
RecurringJob.AddOrUpdate<Dental.Jobs.NotificationJobs>(
    Dental.Jobs.NotificationJobs.LowStockJobId,
    job => job.ScanLowStockAsync(CancellationToken.None),
    Dental.Jobs.NotificationJobs.LowStockCron);

app.Run();

public partial class Program; // WebApplicationFactory (entegrasyon testleri) için
