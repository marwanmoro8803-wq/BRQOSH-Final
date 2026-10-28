using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderDiscountCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "sales_orders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR");

            migrationBuilder.AddColumn<decimal>(
                name: "DocumentDiscountAmount",
                table: "sales_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "sales_orders",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "DocumentDiscountAmount",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "sales_orders");
        }
    }
}
