using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDispatchState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DispatchAttempts",
                table: "domain_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedAtUtc",
                table: "domain_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDispatchError",
                table: "domain_events",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_DispatchedAtUtc",
                table: "domain_events",
                column: "DispatchedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_domain_events_DispatchedAtUtc",
                table: "domain_events");

            migrationBuilder.DropColumn(
                name: "DispatchAttempts",
                table: "domain_events");

            migrationBuilder.DropColumn(
                name: "DispatchedAtUtc",
                table: "domain_events");

            migrationBuilder.DropColumn(
                name: "LastDispatchError",
                table: "domain_events");
        }
    }
}
