using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MessagingAndPaymentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleType = table.Column<byte>(type: "tinyint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OffsetHours = table.Column<int>(type: "int", nullable: false),
                    ChannelPolicy = table.Column<byte>(type: "tinyint", nullable: false),
                    TemplateKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SendAtLocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Channel = table.Column<byte>(type: "tinyint", nullable: false),
                    Locale = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    Channel = table.Column<byte>(type: "tinyint", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    TemplateKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RenderedBody = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ParamsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ToAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<byte>(type: "tinyint", nullable: false),
                    SkipReason = table.Column<byte>(type: "tinyint", nullable: true),
                    ProviderKey = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FallbackOfMessageId = table.Column<long>(type: "bigint", nullable: true),
                    RefType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    RefId = table.Column<long>(type: "bigint", nullable: true),
                    CreditCost = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundMessages_OutboundMessages_FallbackOfMessageId",
                        column: x => x.FallbackOfMessageId,
                        principalTable: "OutboundMessages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OutboundMessages_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentIntents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TRY"),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ConversationId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PublicToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ProviderToken = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Category = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BodySpec = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ParamMapJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetaStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    MetaUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TemplateKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentIntentId",
                table: "Payments",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_TenantId",
                table: "AutomationRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_TenantId_RuleType",
                table: "AutomationRules",
                columns: new[] { "TenantId", "RuleType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_TenantId",
                table: "MessageTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_TenantId_TemplateKey_Channel_Locale",
                table: "MessageTemplates",
                columns: new[] { "TenantId", "TemplateKey", "Channel", "Locale" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_FallbackOfMessageId",
                table: "OutboundMessages",
                column: "FallbackOfMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_PatientId",
                table: "OutboundMessages",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_ProviderMessageId",
                table: "OutboundMessages",
                column: "ProviderMessageId",
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_TenantId",
                table: "OutboundMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_TenantId_PatientId_CreatedAtUtc",
                table: "OutboundMessages",
                columns: new[] { "TenantId", "PatientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_TenantId_State_ScheduledAtUtc",
                table: "OutboundMessages",
                columns: new[] { "TenantId", "State", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_ClinicId",
                table: "PaymentIntents",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_CreatedByUserId",
                table: "PaymentIntents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PatientId",
                table: "PaymentIntents",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PaymentId",
                table: "PaymentIntents",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_ProviderPaymentId",
                table: "PaymentIntents",
                column: "ProviderPaymentId",
                unique: true,
                filter: "[ProviderPaymentId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_ProviderToken",
                table: "PaymentIntents",
                column: "ProviderToken",
                filter: "[ProviderToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PublicToken",
                table: "PaymentIntents",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_TenantId",
                table: "PaymentIntents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_TenantId_PatientId_Status",
                table: "PaymentIntents",
                columns: new[] { "TenantId", "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_TenantId_Status_ExpiresAtUtc",
                table: "PaymentIntents",
                columns: new[] { "TenantId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId",
                table: "WhatsAppTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId_TemplateKey_MetaStatus",
                table: "WhatsAppTemplates",
                columns: new[] { "TenantId", "TemplateKey", "MetaStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId_TemplateName_Language",
                table: "WhatsAppTemplates",
                columns: new[] { "TenantId", "TemplateName", "Language" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentIntents_PaymentIntentId",
                table: "Payments",
                column: "PaymentIntentId",
                principalTable: "PaymentIntents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentIntents_PaymentIntentId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "MessageTemplates");

            migrationBuilder.DropTable(
                name: "OutboundMessages");

            migrationBuilder.DropTable(
                name: "PaymentIntents");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentIntentId",
                table: "Payments");
        }
    }
}
