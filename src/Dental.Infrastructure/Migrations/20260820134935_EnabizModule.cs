using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnabizModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnabizSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<long>(type: "bigint", nullable: false),
                    FacilityCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    PacketType = table.Column<short>(type: "smallint", nullable: false),
                    VisitId = table.Column<long>(type: "bigint", nullable: true),
                    TreatmentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    PrescriptionId = table.Column<long>(type: "bigint", nullable: true),
                    PayloadXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<byte>(type: "tinyint", nullable: false),
                    SysTakipNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    DependsOnSubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PhysicianSignState = table.Column<byte>(type: "tinyint", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegenerateOnSend = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnabizSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnabizSubmissions_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnabizSubmissions_EnabizSubmissions_DependsOnSubmissionId",
                        column: x => x.DependsOnSubmissionId,
                        principalTable: "EnabizSubmissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnabizSubmissions_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnabizSubmissions_TreatmentRecords_TreatmentRecordId",
                        column: x => x.TreatmentRecordId,
                        principalTable: "TreatmentRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnabizSubmissions_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SkrsCodeSystems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeSystemGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    LastSyncAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkrsCodeSystems", x => x.Id);
                    table.UniqueConstraint("AK_SkrsCodeSystems_CodeSystemGuid", x => x.CodeSystemGuid);
                });

            migrationBuilder.CreateTable(
                name: "SkrsCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeSystemGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ParentCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkrsCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkrsCodes_SkrsCodeSystems_CodeSystemGuid",
                        column: x => x.CodeSystemGuid,
                        principalTable: "SkrsCodeSystems",
                        principalColumn: "CodeSystemGuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_ClinicId",
                table: "EnabizSubmissions",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_DependsOnSubmissionId",
                table: "EnabizSubmissions",
                column: "DependsOnSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_PrescriptionId",
                table: "EnabizSubmissions",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_TenantId",
                table: "EnabizSubmissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_TenantId_State_NextAttemptAtUtc",
                table: "EnabizSubmissions",
                columns: new[] { "TenantId", "State", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_TenantId_VisitId",
                table: "EnabizSubmissions",
                columns: new[] { "TenantId", "VisitId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_TreatmentRecordId",
                table: "EnabizSubmissions",
                column: "TreatmentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EnabizSubmissions_VisitId",
                table: "EnabizSubmissions",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_SkrsCodes_CodeSystemGuid_Code",
                table: "SkrsCodes",
                columns: new[] { "CodeSystemGuid", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkrsCodeSystems_CodeSystemGuid",
                table: "SkrsCodeSystems",
                column: "CodeSystemGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnabizSubmissions");

            migrationBuilder.DropTable(
                name: "SkrsCodes");

            migrationBuilder.DropTable(
                name: "SkrsCodeSystems");
        }
    }
}
