using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelledAtToTaxDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledAt",
                table: "sales_invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledAt",
                table: "purchase_invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledAt",
                table: "progress_billings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledAt",
                table: "debit_notes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancelledAt",
                table: "credit_notes",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "progress_billings");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "debit_notes");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "credit_notes");
        }
    }
}
