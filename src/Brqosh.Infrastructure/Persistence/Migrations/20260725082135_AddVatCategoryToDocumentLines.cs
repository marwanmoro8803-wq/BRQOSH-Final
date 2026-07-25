using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVatCategoryToDocumentLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VatCategory",
                table: "sales_quotation_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "sales_order_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "sales_invoice_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "VatCategory",
                table: "purchase_request_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "purchase_order_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "purchase_invoice_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "inventory_balances",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "debit_note_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "credit_note_lines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "sales_quotation_lines");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "sales_order_lines");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "purchase_invoice_lines");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "inventory_balances");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "debit_note_lines");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "credit_note_lines");
        }
    }
}
