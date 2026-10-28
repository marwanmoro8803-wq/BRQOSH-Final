using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseActivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_activations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nonce = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Edition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MaxCompanies = table.Column<int>(type: "integer", nullable: true),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    CodeSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_activations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_activations_Nonce",
                table: "license_activations",
                column: "Nonce",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_activations");
        }
    }
}
