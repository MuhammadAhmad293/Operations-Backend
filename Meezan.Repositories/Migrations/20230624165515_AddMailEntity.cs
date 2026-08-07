using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Meezan.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddMailEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailStatus",
                columns: table => new
                {
                    MailStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArDescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailStatus", x => x.MailStatusId);
                });

            migrationBuilder.CreateTable(
                name: "MailType",
                columns: table => new
                {
                    MailTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArDescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailType", x => x.MailTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Mail",
                columns: table => new
                {
                    MailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    To = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BCC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MailStatusId = table.Column<int>(type: "int", nullable: false),
                    MailTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mail", x => x.MailId);
                    table.ForeignKey(
                        name: "FK_Mail_MailStatus_MailStatusId",
                        column: x => x.MailStatusId,
                        principalTable: "MailStatus",
                        principalColumn: "MailStatusId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mail_MailType_MailTypeId",
                        column: x => x.MailTypeId,
                        principalTable: "MailType",
                        principalColumn: "MailTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailAttachment",
                columns: table => new
                {
                    MailAttachmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MailId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailAttachment", x => x.MailAttachmentId);
                    table.ForeignKey(
                        name: "FK_MailAttachment_Mail_MailId",
                        column: x => x.MailId,
                        principalTable: "Mail",
                        principalColumn: "MailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MailStatus",
                columns: new[] { "MailStatusId", "ArDescription", "ArName", "CreationTime", "EnDescription", "EnName", "IsDeleted", "LastModificationTime" },
                values: new object[,]
                {
                    { 1, "جديد", "جديد", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "New", "New", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(848) },
                    { 2, "تم الإرسال", "تم الإرسال", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sent", "Sent", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(2447) },
                    { 3, "معالجة", "معالجة", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Processing", "Processing", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(2996) },
                    { 4, "فشل", "فشل", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Failed", "Failed", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(3529) }
                });

            migrationBuilder.InsertData(
                table: "MailType",
                columns: new[] { "MailTypeId", "ArDescription", "ArName", "CreationTime", "EnDescription", "EnName", "IsDeleted", "LastModificationTime" },
                values: new object[,]
                {
                    { 1, "نسيان كلمة السر", "نسيان كلمة السر", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Forget Password", "Forget Password", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(4247) },
                    { 2, "بريد الترحيب", "بريد الترحيب", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Welcome Mail", "Welcome Mail", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(4895) },
                    { 3, "بريد التحقق", "بريد التحقق", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Verification Mail", "Verification Mail", false, new DateTime(2023, 6, 24, 19, 55, 15, 550, DateTimeKind.Local).AddTicks(5417) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mail_MailStatusId",
                table: "Mail",
                column: "MailStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Mail_MailTypeId",
                table: "Mail",
                column: "MailTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MailAttachment_MailId",
                table: "MailAttachment",
                column: "MailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailAttachment");

            migrationBuilder.DropTable(
                name: "Mail");

            migrationBuilder.DropTable(
                name: "MailStatus");

            migrationBuilder.DropTable(
                name: "MailType");
        }
    }
}
