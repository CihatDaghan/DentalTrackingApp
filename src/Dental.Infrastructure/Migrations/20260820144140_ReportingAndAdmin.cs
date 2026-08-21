using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportingAndAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeletedByUserId",
                table: "Tenants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "LinkPath",
                table: "Notifications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TargetTenantId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_Tenants_TargetTenantId",
                        column: x => x.TargetTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MaxPatients = table.Column<int>(type: "int", nullable: false),
                    MonthlySmsQuota = table.Column<int>(type: "int", nullable: false),
                    StorageGb = table.Column<int>(type: "int", nullable: false),
                    PriceMonthly = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_TenantId_Status_PerformedAtUtc",
                table: "TreatmentRecords",
                columns: new[] { "TenantId", "Status", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PlanCode",
                table: "Tenants",
                column: "PlanCode");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_ReceivedAtUtc",
                table: "Payments",
                columns: new[] { "TenantId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_Balance",
                table: "Patients",
                columns: new[] { "TenantId", "Balance" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId_ExpenseDate",
                table: "Expenses",
                columns: new[] { "TenantId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId_StartUtc_Status",
                table: "Appointments",
                columns: new[] { "TenantId", "StartUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_IsActive_StartsAtUtc_EndsAtUtc",
                table: "Announcements",
                columns: new[] { "IsActive", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetTenantId",
                table: "Announcements",
                column: "TargetTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentRecords_TenantId_Status_PerformedAtUtc",
                table: "TreatmentRecords");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_PlanCode",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_ReceivedAtUtc",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Patients_TenantId_Balance",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantId_ExpenseDate",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_TenantId_StartUtc_Status",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tenants");

            migrationBuilder.AlterColumn<string>(
                name: "LinkPath",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
