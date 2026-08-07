using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meezan.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMailDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "Mail",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "Mail",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Mail",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Mail",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "Mail",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 1,
                columns: new[] { "ArDescription", "ArName", "EnDescription", "EnName" },
                values: new object[] { "مسودة", "مسودة", "Draft", "Draft" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Mail");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "Mail");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Mail");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Mail");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Mail");

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 1,
                columns: new[] { "ArDescription", "ArName", "EnDescription", "EnName" },
                values: new object[] { "جديد", "جديد", "New", "New" });
        }
    }
}
