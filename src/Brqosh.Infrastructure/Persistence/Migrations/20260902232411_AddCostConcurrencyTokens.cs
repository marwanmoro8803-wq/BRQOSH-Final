using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCostConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-231 · ط5-٨) ‏رموزُ تزامنٍ على التكلفة والطبقات والدفعات.
        ///
        /// <para>‏<b>و<c>xmin</c> عمودٌ نظاميٌّ في PostgreSQL موجودٌ على كلّ جدول</b> —
        /// و<c>AddColumn</c> هنا <b>لا يُنتج DDL</b>: مولّدُ Npgsql يعرفه ويتجاهله.
        /// وهو النمطُ نفسُه المستعمَل في <c>AddVatCategoryToDocumentLines</c> لـ
        /// <c>inventory_balances</c>، <b>ومسبارُ تغطية المخطّط يُثبت مرورَه</b>.</para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "stock_batches",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "inventory_cost_layers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "stock_batches");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "products");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "inventory_cost_layers");
        }
    }
}
