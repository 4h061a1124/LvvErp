using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LvvErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVietnameseCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:vi_vn", "vi-VN,vi-VN,icu,True")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "full_name",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "units",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "positions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "materials",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "material_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "full_name",
                table: "employees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "departments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "vi_vn",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .OldAnnotation("Npgsql:CollationDefinition:vi_vn", "vi-VN,vi-VN,icu,True")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "full_name",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "units",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "positions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "materials",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "material_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "full_name",
                table: "employees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldCollation: "vi_vn");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "departments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "vi_vn");
        }
    }
}
