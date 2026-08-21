using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PatientsAndAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoctorWorkingHours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorWorkingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorWorkingHours_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    FileNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SearchName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false, computedColumnSql: "UPPER([FirstName] + N' ' + [LastName])", stored: true),
                    IdentityType = table.Column<byte>(type: "tinyint", nullable: false),
                    TcknEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TcknHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PassportNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NationalityCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "TUR"),
                    LastEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<byte>(type: "tinyint", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Phone2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    ReferralSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Profession = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GeneralNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PhotoFileId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatientTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_PatientTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    SourceTreatmentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    ReminderState = table.Column<byte>(type: "tinyint", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointments_Users_DoctorUserId",
                        column: x => x.DoctorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunicationConsents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    ConsentType = table.Column<byte>(type: "tinyint", nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    ConsentFormId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationConsents_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientTagAssignments",
                columns: table => new
                {
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    PatientTagId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTagAssignments", x => new { x.PatientId, x.PatientTagId });
                    table.ForeignKey(
                        name: "FK_PatientTagAssignments_PatientTags_PatientTagId",
                        column: x => x.PatientTagId,
                        principalTable: "PatientTags",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientTagAssignments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecallPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    SourceTreatmentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    DoctorUserId = table.Column<long>(type: "bigint", nullable: true),
                    SuggestedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    LastReminderAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecallPlans_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecallPlans_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorUserId",
                table: "Appointments",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId",
                table: "Appointments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId_ClinicId_StartUtc",
                table: "Appointments",
                columns: new[] { "TenantId", "ClinicId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId_DoctorUserId_StartUtc",
                table: "Appointments",
                columns: new[] { "TenantId", "DoctorUserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId_PatientId",
                table: "Appointments",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConsents_PatientId",
                table: "CommunicationConsents",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConsents_TenantId",
                table: "CommunicationConsents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConsents_TenantId_PatientId_ConsentType",
                table: "CommunicationConsents",
                columns: new[] { "TenantId", "PatientId", "ConsentType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorWorkingHours_DoctorUserId",
                table: "DoctorWorkingHours",
                column: "DoctorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorWorkingHours_TenantId",
                table: "DoctorWorkingHours",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorWorkingHours_TenantId_DoctorUserId_ClinicId",
                table: "DoctorWorkingHours",
                columns: new[] { "TenantId", "DoctorUserId", "ClinicId" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ClinicId",
                table: "Patients",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId",
                table: "Patients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_FileNo",
                table: "Patients",
                columns: new[] { "TenantId", "FileNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_Phone",
                table: "Patients",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_SearchName",
                table: "Patients",
                columns: new[] { "TenantId", "SearchName" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_TcknHash",
                table: "Patients",
                columns: new[] { "TenantId", "TcknHash" },
                unique: true,
                filter: "[TcknHash] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTagAssignments_PatientTagId",
                table: "PatientTagAssignments",
                column: "PatientTagId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTags_TenantId",
                table: "PatientTags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTags_TenantId_Name",
                table: "PatientTags",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RecallPlans_AppointmentId",
                table: "RecallPlans",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RecallPlans_PatientId",
                table: "RecallPlans",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecallPlans_TenantId",
                table: "RecallPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RecallPlans_TenantId_Status_SuggestedDate",
                table: "RecallPlans",
                columns: new[] { "TenantId", "Status", "SuggestedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationConsents");

            migrationBuilder.DropTable(
                name: "DoctorWorkingHours");

            migrationBuilder.DropTable(
                name: "PatientTagAssignments");

            migrationBuilder.DropTable(
                name: "RecallPlans");

            migrationBuilder.DropTable(
                name: "PatientTags");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Patients");
        }
    }
}
