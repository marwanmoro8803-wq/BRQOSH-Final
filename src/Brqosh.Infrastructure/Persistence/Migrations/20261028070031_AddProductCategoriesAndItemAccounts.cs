using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCategoriesAndItemAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CategoryId",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CogsAccountId",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InventoryAccountId",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RevenueAccountId",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RevenueAccountId = table.Column<long>(type: "bigint", nullable: true),
                    InventoryAccountId = table.Column<long>(type: "bigint", nullable: true),
                    CogsAccountId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_CompanyId_Code",
                table: "product_categories",
                columns: new[] { "CompanyId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CogsAccountId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "InventoryAccountId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "RevenueAccountId",
                table: "products");
        }
    }
}
