using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meezan.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceAdjustmentSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdjustment",
                table: "Transaction",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SystemPurpose",
                table: "Category",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Backfill: every currently-protected category is the Zakat/Charity one — it was the
            // only protected purpose that existed before this migration. Safe and exhaustive
            // (not just "safe for now"): IsProtected only ever got set to true by the Zakat
            // category-seeding code path prior to this change.
            migrationBuilder.Sql("UPDATE [Category] SET [SystemPurpose] = 'Zakat' WHERE [IsProtected] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdjustment",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "SystemPurpose",
                table: "Category");
        }
    }
}
