using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrescriptionLabStockEpicrisis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drugs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    Barcode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AtcCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Form = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultDose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultUsage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsControlled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EpicrisisDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DiagnosisJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TreatmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PdfFileId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpicrisisDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpicrisisDocuments_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EpicrisisDocuments_MediaFiles_PdfFileId",
                        column: x => x.PdfFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EpicrisisDocuments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EpicrisisDocuments_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Laboratories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laboratories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    VisitId = table.Column<long>(type: "bigint", nullable: true),
                    PrescriptionNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RecetemCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PdfFileId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Prescriptions_MediaFiles_PdfFileId",
                        column: x => x.PdfFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Prescriptions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Prescriptions_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Prescriptions_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockCategories",
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
                    table.PrimaryKey("PK_StockCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabCases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    LaboratoryId = table.Column<long>(type: "bigint", nullable: false),
                    CaseNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TeethCsv = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Shade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Material = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCases_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_Laboratories_LaboratoryId",
                        column: x => x.LaboratoryId,
                        principalTable: "Laboratories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<long>(type: "bigint", nullable: false),
                    DrugId = table.Column<long>(type: "bigint", nullable: false),
                    BoxCount = table.Column<int>(type: "int", nullable: false),
                    Dose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UsageNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionTemplateItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    DrugId = table.Column<long>(type: "bigint", nullable: false),
                    BoxCount = table.Column<int>(type: "int", nullable: false),
                    Dose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UsageNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionTemplateItems_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrescriptionTemplateItems_PrescriptionTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PrescriptionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Barcode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LastPurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockItems_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockItems_StockCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "StockCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LabCaseStatusHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabCaseId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LabCaseStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCaseStatusHistories_LabCases_LabCaseId",
                        column: x => x.LabCaseId,
                        principalTable: "LabCases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCaseStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    StockItemId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RefType = table.Column<byte>(type: "tinyint", nullable: false),
                    MovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockMovements_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockMovements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Barcode",
                table: "Drugs",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Name",
                table: "Drugs",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_TenantId",
                table: "Drugs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EpicrisisDocuments_ClinicId",
                table: "EpicrisisDocuments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_EpicrisisDocuments_DoctorUserId",
                table: "EpicrisisDocuments",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EpicrisisDocuments_PatientId",
                table: "EpicrisisDocuments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EpicrisisDocuments_PdfFileId",
                table: "EpicrisisDocuments",
                column: "PdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_EpicrisisDocuments_TenantId",
                table: "EpicrisisDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EpicrisisDocuments_TenantId_PatientId",
                table: "EpicrisisDocuments",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_ClinicId",
                table: "LabCases",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_DoctorUserId",
                table: "LabCases",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_LaboratoryId",
                table: "LabCases",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_PatientId",
                table: "LabCases",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_TenantId",
                table: "LabCases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_TenantId_CaseNo",
                table: "LabCases",
                columns: new[] { "TenantId", "CaseNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_TenantId_PatientId",
                table: "LabCases",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_TenantId_Status_DueDate",
                table: "LabCases",
                columns: new[] { "TenantId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseStatusHistories_ChangedByUserId",
                table: "LabCaseStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseStatusHistories_LabCaseId",
                table: "LabCaseStatusHistories",
                column: "LabCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseStatusHistories_TenantId",
                table: "LabCaseStatusHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseStatusHistories_TenantId_LabCaseId",
                table: "LabCaseStatusHistories",
                columns: new[] { "TenantId", "LabCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Laboratories_TenantId",
                table: "Laboratories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Laboratories_TenantId_Name",
                table: "Laboratories",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_DrugId",
                table: "PrescriptionItems",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_PrescriptionId",
                table: "PrescriptionItems",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_TenantId",
                table: "PrescriptionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_TenantId_PrescriptionId",
                table: "PrescriptionItems",
                columns: new[] { "TenantId", "PrescriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ClinicId",
                table: "Prescriptions",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_DoctorUserId",
                table: "Prescriptions",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId",
                table: "Prescriptions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PdfFileId",
                table: "Prescriptions",
                column: "PdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_TenantId",
                table: "Prescriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_TenantId_PatientId",
                table: "Prescriptions",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_TenantId_PrescriptionNo",
                table: "Prescriptions",
                columns: new[] { "TenantId", "PrescriptionNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_VisitId",
                table: "Prescriptions",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionTemplateItems_DrugId",
                table: "PrescriptionTemplateItems",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionTemplateItems_TemplateId",
                table: "PrescriptionTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionTemplateItems_TenantId",
                table: "PrescriptionTemplateItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionTemplateItems_TenantId_TemplateId",
                table: "PrescriptionTemplateItems",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionTemplates_TenantId",
                table: "PrescriptionTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionTemplates_TenantId_Name",
                table: "PrescriptionTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockCategories_TenantId",
                table: "StockCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCategories_TenantId_Name",
                table: "StockCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_CategoryId",
                table: "StockItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_ClinicId",
                table: "StockItems",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId",
                table: "StockItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId_ClinicId",
                table: "StockItems",
                columns: new[] { "TenantId", "ClinicId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId_Name",
                table: "StockItems",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ClinicId",
                table: "StockMovements",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockItemId",
                table: "StockMovements",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId",
                table: "StockMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_StockItemId_MovedAtUtc",
                table: "StockMovements",
                columns: new[] { "TenantId", "StockItemId", "MovedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_UserId",
                table: "StockMovements",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EpicrisisDocuments");

            migrationBuilder.DropTable(
                name: "LabCaseStatusHistories");

            migrationBuilder.DropTable(
                name: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "PrescriptionTemplateItems");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "LabCases");

            migrationBuilder.DropTable(
                name: "Prescriptions");

            migrationBuilder.DropTable(
                name: "Drugs");

            migrationBuilder.DropTable(
                name: "PrescriptionTemplates");

            migrationBuilder.DropTable(
                name: "StockItems");

            migrationBuilder.DropTable(
                name: "Laboratories");

            migrationBuilder.DropTable(
                name: "StockCategories");
        }
    }
}
