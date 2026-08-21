using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalRecordsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnamnesisTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_AnamnesisTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsentTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ConsentTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ThumbnailKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TakenAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ToothNumber = table.Column<string>(type: "char(2)", nullable: true),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFiles_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MediaFiles_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MediaFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatientNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorUserId = table.Column<long>(type: "bigint", nullable: false),
                    NoteText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientNotes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientNotes_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnamnesisQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuestionTextEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnswerType = table.Column<byte>(type: "tinyint", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnamnesisQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnamnesisQuestions_AnamnesisTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AnamnesisTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnamnesisResponses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    FilledByUserId = table.Column<long>(type: "bigint", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnamnesisResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnamnesisResponses_AnamnesisTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AnamnesisTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnamnesisResponses_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnamnesisResponses_Users_FilledByUserId",
                        column: x => x.FilledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ConsentForms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    TreatmentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    RenderedHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SignChannel = table.Column<byte>(type: "tinyint", nullable: true),
                    SignToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignTokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignerIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    SignerUserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SignatureFileId = table.Column<long>(type: "bigint", nullable: true),
                    PdfFileId = table.Column<long>(type: "bigint", nullable: true),
                    PdfSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentForms_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsentForms_ConsentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ConsentTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsentForms_MediaFiles_PdfFileId",
                        column: x => x.PdfFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsentForms_MediaFiles_SignatureFileId",
                        column: x => x.SignatureFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsentForms_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsentForms_TreatmentRecords_TreatmentRecordId",
                        column: x => x.TreatmentRecordId,
                        principalTable: "TreatmentRecords",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnamnesisAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    BoolValue = table.Column<bool>(type: "bit", nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnamnesisAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnamnesisAnswers_AnamnesisQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "AnamnesisQuestions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnamnesisAnswers_AnamnesisResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "AnamnesisResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisAnswers_QuestionId",
                table: "AnamnesisAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisAnswers_ResponseId",
                table: "AnamnesisAnswers",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisAnswers_TenantId",
                table: "AnamnesisAnswers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisAnswers_TenantId_ResponseId",
                table: "AnamnesisAnswers",
                columns: new[] { "TenantId", "ResponseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisQuestions_TemplateId",
                table: "AnamnesisQuestions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisQuestions_TenantId",
                table: "AnamnesisQuestions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisQuestions_TenantId_TemplateId_SortOrder",
                table: "AnamnesisQuestions",
                columns: new[] { "TenantId", "TemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisResponses_FilledByUserId",
                table: "AnamnesisResponses",
                column: "FilledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisResponses_PatientId",
                table: "AnamnesisResponses",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisResponses_TemplateId",
                table: "AnamnesisResponses",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisResponses_TenantId",
                table: "AnamnesisResponses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisResponses_TenantId_PatientId_FilledAtUtc",
                table: "AnamnesisResponses",
                columns: new[] { "TenantId", "PatientId", "FilledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisTemplates_TenantId",
                table: "AnamnesisTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisTemplates_TenantId_Name",
                table: "AnamnesisTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_ClinicId",
                table: "ConsentForms",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_PatientId",
                table: "ConsentForms",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_PdfFileId",
                table: "ConsentForms",
                column: "PdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_SignatureFileId",
                table: "ConsentForms",
                column: "SignatureFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_SignToken",
                table: "ConsentForms",
                column: "SignToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_TemplateId",
                table: "ConsentForms",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_TenantId",
                table: "ConsentForms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_TenantId_PatientId",
                table: "ConsentForms",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_TreatmentRecordId",
                table: "ConsentForms",
                column: "TreatmentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentTemplates_TenantId",
                table: "ConsentTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentTemplates_TenantId_Name",
                table: "ConsentTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ClinicId",
                table: "MediaFiles",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_PatientId",
                table: "MediaFiles",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_TenantId",
                table: "MediaFiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_TenantId_PatientId_Category",
                table: "MediaFiles",
                columns: new[] { "TenantId", "PatientId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_UploadedByUserId",
                table: "MediaFiles",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotes_AuthorUserId",
                table: "PatientNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotes_PatientId",
                table: "PatientNotes",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotes_TenantId",
                table: "PatientNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotes_TenantId_PatientId_IsPinned",
                table: "PatientNotes",
                columns: new[] { "TenantId", "PatientId", "IsPinned" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnamnesisAnswers");

            migrationBuilder.DropTable(
                name: "ConsentForms");

            migrationBuilder.DropTable(
                name: "PatientNotes");

            migrationBuilder.DropTable(
                name: "AnamnesisQuestions");

            migrationBuilder.DropTable(
                name: "AnamnesisResponses");

            migrationBuilder.DropTable(
                name: "ConsentTemplates");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "AnamnesisTemplates");
        }
    }
}
