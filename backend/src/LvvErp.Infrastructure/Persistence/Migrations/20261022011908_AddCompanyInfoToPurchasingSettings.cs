using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LvvErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInfoToPurchasingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_address",
                table: "purchasing_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                table: "purchasing_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_phone",
                table: "purchasing_settings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_tax_code",
                table: "purchasing_settings",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "company_address",
                table: "purchasing_settings");

            migrationBuilder.DropColumn(
                name: "company_name",
                table: "purchasing_settings");

            migrationBuilder.DropColumn(
                name: "company_phone",
                table: "purchasing_settings");

            migrationBuilder.DropColumn(
                name: "company_tax_code",
                table: "purchasing_settings");
        }
    }
}
