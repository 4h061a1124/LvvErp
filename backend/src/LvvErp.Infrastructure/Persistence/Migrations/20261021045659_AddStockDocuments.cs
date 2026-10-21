using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LvvErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_sequences", x => new { x.prefix, x.year });
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_balances", x => x.id);
                    table.CheckConstraint("ck_stock_balances_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_stock_balances_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_balances_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_balances_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_balances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    partner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_documents_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_documents_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_documents_warehouses_to_warehouse_id",
                        column: x => x.to_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_document_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    manufacture_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_document_lines", x => x.id);
                    table.CheckConstraint("ck_stock_document_lines_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_stock_document_lines_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_document_lines_locations_to_location_id",
                        column: x => x.to_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_document_lines_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_document_lines_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_document_lines_stock_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "stock_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_reversal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_transactions", x => x.id);
                    table.CheckConstraint("ck_stock_transactions_quantity", "quantity <> 0");
                    table.ForeignKey(
                        name: "fk_stock_transactions_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_stock_document_lines_document_line_id",
                        column: x => x.document_line_id,
                        principalTable: "stock_document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_stock_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "stock_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_location_id",
                table: "stock_balances",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_lot_id",
                table: "stock_balances",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id",
                table: "stock_balances",
                columns: new[] { "material_id", "warehouse_id", "location_id", "lot_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_warehouse_id",
                table: "stock_balances",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_document_id_line_no",
                table: "stock_document_lines",
                columns: new[] { "document_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_location_id",
                table: "stock_document_lines",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_lot_id",
                table: "stock_document_lines",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_material_id",
                table: "stock_document_lines",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_to_location_id",
                table: "stock_document_lines",
                column: "to_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_department_id",
                table: "stock_documents",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_employee_id",
                table: "stock_documents",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_number",
                table: "stock_documents",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_to_warehouse_id",
                table: "stock_documents",
                column: "to_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_type_document_date",
                table: "stock_documents",
                columns: new[] { "type", "document_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_warehouse_id",
                table: "stock_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_document_id",
                table: "stock_transactions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_document_line_id",
                table: "stock_transactions",
                column: "document_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_location_id",
                table: "stock_transactions",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_lot_id",
                table: "stock_transactions",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_material_id_transaction_date",
                table: "stock_transactions",
                columns: new[] { "material_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_warehouse_id",
                table: "stock_transactions",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropTable(
                name: "stock_balances");

            migrationBuilder.DropTable(
                name: "stock_transactions");

            migrationBuilder.DropTable(
                name: "stock_document_lines");

            migrationBuilder.DropTable(
                name: "stock_documents");
        }
    }
}
