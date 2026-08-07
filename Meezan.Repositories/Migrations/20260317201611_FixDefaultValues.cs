using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meezan.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class FixDefaultValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreationTime",
                table: "User",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(7455));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "User",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(7769));

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "MailType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(4022),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "MailType",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "MailType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(3730),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "MailStatus",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(241),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "MailStatus",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "MailStatus",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 247, DateTimeKind.Local).AddTicks(9944),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "MailAttachment",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 246, DateTimeKind.Local).AddTicks(8249),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "MailAttachment",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "MailAttachment",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 243, DateTimeKind.Local).AddTicks(900),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "Mail",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 247, DateTimeKind.Local).AddTicks(6132),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Mail",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "Mail",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 247, DateTimeKind.Local).AddTicks(5754),
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 1,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(241));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 2,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(241));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 3,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(241));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 4,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(241));

            migrationBuilder.UpdateData(
                table: "MailType",
                keyColumn: "MailTypeId",
                keyValue: 1,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(4022));

            migrationBuilder.UpdateData(
                table: "MailType",
                keyColumn: "MailTypeId",
                keyValue: 2,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(4022));

            migrationBuilder.UpdateData(
                table: "MailType",
                keyColumn: "MailTypeId",
                keyValue: 3,
                column: "LastModificationTime",
                value: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(4022));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreationTime",
                table: "User");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "User");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "User");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "MailType",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(4022));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "MailType",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "MailType",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(3730));

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "MailStatus",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 248, DateTimeKind.Local).AddTicks(241));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "MailStatus",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "MailStatus",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 247, DateTimeKind.Local).AddTicks(9944));

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "MailAttachment",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 246, DateTimeKind.Local).AddTicks(8249));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "MailAttachment",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "MailAttachment",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 243, DateTimeKind.Local).AddTicks(900));

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModificationTime",
                table: "Mail",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 247, DateTimeKind.Local).AddTicks(6132));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Mail",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                table: "Mail",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 17, 23, 16, 11, 247, DateTimeKind.Local).AddTicks(5754));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 1,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(848));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 2,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(2447));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 3,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(2996));

            migrationBuilder.UpdateData(
                table: "MailStatus",
                keyColumn: "MailStatusId",
                keyValue: 4,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(3529));

            migrationBuilder.UpdateData(
                table: "MailType",
                keyColumn: "MailTypeId",
                keyValue: 1,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(4247));

            migrationBuilder.UpdateData(
                table: "MailType",
                keyColumn: "MailTypeId",
                keyValue: 2,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(4895));

            migrationBuilder.UpdateData(
                table: "MailType",
                keyColumn: "MailTypeId",
                keyValue: 3,
                column: "LastModificationTime",
                value: new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(5417));
        }
    }
}
