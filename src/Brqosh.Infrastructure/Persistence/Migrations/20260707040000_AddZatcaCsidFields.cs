using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddZatcaCsidFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ComplianceRequestId",
            table: "zatca_settings",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "CsidSecret",
            table: "zatca_settings",
            type: "TEXT",
            maxLength: 512,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ComplianceRequestId", table: "zatca_settings");
        migrationBuilder.DropColumn(name: "CsidSecret", table: "zatca_settings");
    }
}
