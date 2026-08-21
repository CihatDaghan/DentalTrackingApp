using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxOffice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Vkn = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PriceListId = table.Column<long>(type: "bigint", nullable: true),
                    IsEInvoiceUser = table.Column<bool>(type: "bit", nullable: false),
                    EInvoiceAlias = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    AccountType = table.Column<byte>(type: "tinyint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    EntryType = table.Column<byte>(type: "tinyint", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TRY"),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false, defaultValue: 1m),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RefId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Method = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expenses_Users_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlanInstallments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<long>(type: "bigint", nullable: false),
                    SeqNo = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlanInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlanInstallments_PaymentPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "PaymentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TRY"),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false, defaultValue: 1m),
                    Method = table.Column<byte>(type: "tinyint", nullable: false),
                    InstallmentCount = table.Column<byte>(type: "tinyint", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentIntentId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerEntryId = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_LedgerEntries_LedgerEntryId",
                        column: x => x.LedgerEntryId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_LedgerEntryId",
                table: "TreatmentRecords",
                column: "LedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CompanyId",
                table: "Patients",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_PriceListId",
                table: "Companies",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId",
                table: "Companies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Name",
                table: "Companies",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId",
                table: "ExpenseCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId_Name",
                table: "ExpenseCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId",
                table: "Expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ClinicId",
                table: "Expenses",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PaidByUserId",
                table: "Expenses",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId",
                table: "Expenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId_CategoryId",
                table: "Expenses",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId_ClinicId_ExpenseDate",
                table: "Expenses",
                columns: new[] { "TenantId", "ClinicId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ClinicId",
                table: "LedgerEntries",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_CompanyId",
                table: "LedgerEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PatientId",
                table: "LedgerEntries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TenantId",
                table: "LedgerEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TenantId_CompanyId_EntryDate",
                table: "LedgerEntries",
                columns: new[] { "TenantId", "CompanyId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TenantId_PatientId_EntryDate",
                table: "LedgerEntries",
                columns: new[] { "TenantId", "PatientId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_PlanId",
                table: "PaymentPlanInstallments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_TenantId",
                table: "PaymentPlanInstallments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_TenantId_PlanId_SeqNo",
                table: "PaymentPlanInstallments",
                columns: new[] { "TenantId", "PlanId", "SeqNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_TenantId_Status_DueDate",
                table: "PaymentPlanInstallments",
                columns: new[] { "TenantId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_PatientId",
                table: "PaymentPlans",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_TenantId",
                table: "PaymentPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_TenantId_PatientId",
                table: "PaymentPlans",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ClinicId",
                table: "Payments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId",
                table: "Payments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_LedgerEntryId",
                table: "Payments",
                column: "LedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PatientId",
                table: "Payments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReceivedByUserId",
                table: "Payments",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId",
                table: "Payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_ClinicId_ReceivedAtUtc",
                table: "Payments",
                columns: new[] { "TenantId", "ClinicId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_PatientId",
                table: "Payments",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Companies_CompanyId",
                table: "Patients",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentRecords_LedgerEntries_LedgerEntryId",
                table: "TreatmentRecords",
                column: "LedgerEntryId",
                principalTable: "LedgerEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Companies_CompanyId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentRecords_LedgerEntries_LedgerEntryId",
                table: "TreatmentRecords");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "PaymentPlanInstallments");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "PaymentPlans");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentRecords_LedgerEntryId",
                table: "TreatmentRecords");

            migrationBuilder.DropIndex(
                name: "IX_Patients_CompanyId",
                table: "Patients");
        }
    }
}
