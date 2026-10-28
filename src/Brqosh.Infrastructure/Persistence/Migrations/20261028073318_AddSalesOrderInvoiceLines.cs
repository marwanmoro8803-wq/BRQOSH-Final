using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderInvoiceLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_order_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SalesInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    LineIndex = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_invoice_lines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_invoice_lines_CompanyId_SalesOrderId",
                table: "sales_order_invoice_lines",
                columns: new[] { "CompanyId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_invoice_lines_SalesInvoiceId",
                table: "sales_order_invoice_lines",
                column: "SalesInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_order_invoice_lines");
        }
    }
}
