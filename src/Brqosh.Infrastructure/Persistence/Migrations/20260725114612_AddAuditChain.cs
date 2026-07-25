using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntryHash",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousHash",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "audit_log",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_CompanyId_Sequence",
                table: "audit_log",
                columns: new[] { "CompanyId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_log_CompanyId_Sequence",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "EntryHash",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "PreviousHash",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "audit_log");
        }
    }
}
