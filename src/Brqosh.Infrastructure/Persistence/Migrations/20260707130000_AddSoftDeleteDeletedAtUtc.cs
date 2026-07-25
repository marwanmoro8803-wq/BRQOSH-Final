using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSoftDeleteDeletedAtUtc : Migration
{
    private static readonly string[] Tables =
    [
        "customers", "suppliers", "products", "warehouses", "cost_centers",
        "accounts", "bank_accounts", "construction_projects", "bills_of_materials", "app_users",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in Tables)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: table,
                type: "TEXT",
                nullable: true);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in Tables)
        {
            migrationBuilder.DropColumn(name: "DeletedAtUtc", table: table);
        }
    }
}
