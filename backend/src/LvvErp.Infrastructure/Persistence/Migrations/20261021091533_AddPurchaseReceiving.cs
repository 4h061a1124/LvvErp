using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LvvErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "purchase_order_id",
                table: "stock_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_order_line_id",
                table: "stock_document_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "receipt_tolerance_percent",
                table: "materials",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_purchase_order_id",
                table: "stock_documents",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_purchase_order_line_id",
                table: "stock_document_lines",
                column: "purchase_order_line_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_materials_receipt_tolerance",
                table: "materials",
                sql: "receipt_tolerance_percent BETWEEN 0 AND 100");

            migrationBuilder.AddForeignKey(
                name: "fk_stock_document_lines_purchase_order_lines_purchase_order_li",
                table: "stock_document_lines",
                column: "purchase_order_line_id",
                principalTable: "purchase_order_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_documents_purchase_orders_purchase_order_id",
                table: "stock_documents",
                column: "purchase_order_id",
                principalTable: "purchase_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_document_lines_purchase_order_lines_purchase_order_li",
                table: "stock_document_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_stock_documents_purchase_orders_purchase_order_id",
                table: "stock_documents");

            migrationBuilder.DropIndex(
                name: "ix_stock_documents_purchase_order_id",
                table: "stock_documents");

            migrationBuilder.DropIndex(
                name: "ix_stock_document_lines_purchase_order_line_id",
                table: "stock_document_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_materials_receipt_tolerance",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "purchase_order_id",
                table: "stock_documents");

            migrationBuilder.DropColumn(
                name: "purchase_order_line_id",
                table: "stock_document_lines");

            migrationBuilder.DropColumn(
                name: "receipt_tolerance_percent",
                table: "materials");
        }
    }
}
