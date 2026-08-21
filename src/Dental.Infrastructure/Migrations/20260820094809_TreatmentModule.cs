using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IcdCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcdCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TRY"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToothStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    ToothNumber = table.Column<string>(type: "char(2)", nullable: false),
                    Condition = table.Column<byte>(type: "tinyint", nullable: false),
                    UpdatedFromTreatmentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToothStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToothStatuses_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TreatmentCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    ProtocolNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SysTakipNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Visits_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Visits_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TreatmentDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SutCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ToothScope = table.Column<byte>(type: "tinyint", nullable: false),
                    RequiresSurface = table.Column<bool>(type: "bit", nullable: false),
                    ToothStatusEffect = table.Column<byte>(type: "tinyint", nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_TreatmentDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentDefinitions_TreatmentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TreatmentCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PriceListItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceListId = table.Column<long>(type: "bigint", nullable: false),
                    TreatmentDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListItems_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceListItems_TreatmentDefinitions_TreatmentDefinitionId",
                        column: x => x.TreatmentDefinitionId,
                        principalTable: "TreatmentDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TreatmentRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    VisitId = table.Column<long>(type: "bigint", nullable: true),
                    TreatmentDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    ToothNumber = table.Column<string>(type: "char(2)", nullable: true),
                    Surfaces = table.Column<byte>(type: "tinyint", nullable: false),
                    RootCanalCount = table.Column<byte>(type: "tinyint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    DiagnosisIcdCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PlanGroupNo = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TRY"),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LedgerEntryId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceLineId = table.Column<long>(type: "bigint", nullable: true),
                    EnabizStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_TreatmentDefinitions_TreatmentDefinitionId",
                        column: x => x.TreatmentDefinitionId,
                        principalTable: "TreatmentDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IcdCodes_Code",
                table: "IcdCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_PriceListId",
                table: "PriceListItems",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_TenantId",
                table: "PriceListItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_TenantId_PriceListId_TreatmentDefinitionId",
                table: "PriceListItems",
                columns: new[] { "TenantId", "PriceListId", "TreatmentDefinitionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_TreatmentDefinitionId",
                table: "PriceListItems",
                column: "TreatmentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId",
                table: "PriceLists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_Name",
                table: "PriceLists",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ToothStatuses_PatientId",
                table: "ToothStatuses",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ToothStatuses_TenantId",
                table: "ToothStatuses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ToothStatuses_TenantId_PatientId_ToothNumber",
                table: "ToothStatuses",
                columns: new[] { "TenantId", "PatientId", "ToothNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentCategories_TenantId",
                table: "TreatmentCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentCategories_TenantId_Name",
                table: "TreatmentCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentDefinitions_CategoryId",
                table: "TreatmentDefinitions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentDefinitions_TenantId",
                table: "TreatmentDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentDefinitions_TenantId_CategoryId",
                table: "TreatmentDefinitions",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentDefinitions_TenantId_Code",
                table: "TreatmentDefinitions",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_ClinicId",
                table: "TreatmentRecords",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_DoctorUserId",
                table: "TreatmentRecords",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_PatientId",
                table: "TreatmentRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_TenantId",
                table: "TreatmentRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_TenantId_DoctorUserId_PerformedAtUtc",
                table: "TreatmentRecords",
                columns: new[] { "TenantId", "DoctorUserId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_TenantId_PatientId_Status",
                table: "TreatmentRecords",
                columns: new[] { "TenantId", "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_TreatmentDefinitionId",
                table: "TreatmentRecords",
                column: "TreatmentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_VisitId",
                table: "TreatmentRecords",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ClinicId",
                table: "Visits",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_DoctorUserId",
                table: "Visits",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_PatientId",
                table: "Visits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_TenantId",
                table: "Visits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_TenantId_PatientId_VisitDate",
                table: "Visits",
                columns: new[] { "TenantId", "PatientId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_TenantId_ProtocolNo",
                table: "Visits",
                columns: new[] { "TenantId", "ProtocolNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IcdCodes");

            migrationBuilder.DropTable(
                name: "PriceListItems");

            migrationBuilder.DropTable(
                name: "ToothStatuses");

            migrationBuilder.DropTable(
                name: "TreatmentRecords");

            migrationBuilder.DropTable(
                name: "PriceLists");

            migrationBuilder.DropTable(
                name: "TreatmentDefinitions");

            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropTable(
                name: "TreatmentCategories");
        }
    }
}
