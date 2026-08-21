using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GibTaxpayers",
                columns: table => new
                {
                    Vkn = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Alias = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountType = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GibTaxpayers", x => x.Vkn);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentKind = table.Column<byte>(type: "tinyint", nullable: false),
                    ProfileId = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    TypeCode = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "char(16)", unicode: false, nullable: true),
                    Serial = table.Column<string>(type: "char(3)", unicode: false, nullable: true),
                    Ettn = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IssueTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CustomerType = table.Column<byte>(type: "tinyint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BuyerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BuyerTcknVkn = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: true),
                    BuyerPassportNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    BuyerNationality = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    BuyerLastEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BuyerAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BuyerCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuyerDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuyerEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuyerTaxOffice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuyerAlias = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TRY"),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false, defaultValue: 1m),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WithholdingTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GvStopajTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExemptionCode = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ExemptionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WithholdingCode = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    IntegratorProvider = table.Column<byte>(type: "tinyint", nullable: true),
                    IntegratorRefId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastStatusCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UblFileId = table.Column<long>(type: "bigint", nullable: true),
                    PdfFileId = table.Column<long>(type: "bigint", nullable: true),
                    SourceInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_SourceInvoiceId",
                        column: x => x.SourceInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_MediaFiles_PdfFileId",
                        column: x => x.PdfFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_MediaFiles_UblFileId",
                        column: x => x.UblFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SequenceType = table.Column<byte>(type: "tinyint", nullable: false),
                    Serial = table.Column<string>(type: "char(3)", unicode: false, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigType = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    SeqNo = table.Column<int>(type: "int", nullable: false),
                    TreatmentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsAesthetic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_TreatmentRecords_TreatmentRecordId",
                        column: x => x.TreatmentRecordId,
                        principalTable: "TreatmentRecords",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceStatusLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    IntegratorRawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceStatusLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceStatusLogs_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceStatusLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_TenantId",
                table: "InvoiceLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_TenantId_InvoiceId_SeqNo",
                table: "InvoiceLines",
                columns: new[] { "TenantId", "InvoiceId", "SeqNo" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_TreatmentRecordId",
                table: "InvoiceLines",
                column: "TreatmentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClinicId",
                table: "Invoices",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Ettn",
                table: "Invoices",
                column: "Ettn",
                unique: true,
                filter: "[Ettn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PatientId",
                table: "Invoices",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PdfFileId",
                table: "Invoices",
                column: "PdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceInvoiceId",
                table: "Invoices",
                column: "SourceInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId",
                table: "Invoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true,
                filter: "[InvoiceNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_IssueDate",
                table: "Invoices",
                columns: new[] { "TenantId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_Status",
                table: "Invoices",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_Status_NextAttemptAtUtc",
                table: "Invoices",
                columns: new[] { "TenantId", "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UblFileId",
                table: "Invoices",
                column: "UblFileId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceStatusLogs_ActorUserId",
                table: "InvoiceStatusLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceStatusLogs_InvoiceId",
                table: "InvoiceStatusLogs",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceStatusLogs_TenantId",
                table: "InvoiceStatusLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceStatusLogs_TenantId_InvoiceId_AtUtc",
                table: "InvoiceStatusLogs",
                columns: new[] { "TenantId", "InvoiceId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_TenantId",
                table: "NumberSequences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_TenantId_SequenceType_Serial_Year",
                table: "NumberSequences",
                columns: new[] { "TenantId", "SequenceType", "Serial", "Year" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TaxConfigs_ConfigType_Code_ValidFrom",
                table: "TaxConfigs",
                columns: new[] { "ConfigType", "Code", "ValidFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GibTaxpayers");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "InvoiceStatusLogs");

            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "TaxConfigs");

            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
