using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_agreements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    LicenseActivationId = table.Column<long>(type: "bigint", nullable: false),
                    AgreementNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Edition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MaxCompanies = table.Column<int>(type: "integer", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstPartyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FirstPartyVatNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    FirstPartyCommercialRegistration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FirstPartyAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SecondPartyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SecondPartyVatNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SecondPartyCommercialRegistration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SecondPartyAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    BodySha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    PdfPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_license_agreements_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_agreements_license_activations_LicenseActivationId",
                        column: x => x.LicenseActivationId,
                        principalTable: "license_activations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_agreements_AgreementNumber",
                table: "license_agreements",
                column: "AgreementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_agreements_CompanyId",
                table: "license_agreements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_license_agreements_LicenseActivationId",
                table: "license_agreements",
                column: "LicenseActivationId");

            // ‏(Phase 7) اتفاقيةُ الترخيص لا تُعدَّل ولا تُحذف: التعديلُ الوحيدُ المسموحُ إرفاقُ PDF مرّةً واحدة (من فارغٍ إلى قيمة).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION brqosh_guard_license_agreement() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'license agreement % is immutable and cannot be deleted', OLD.""Id"";
    END IF;
    IF OLD.""PdfBytes"" IS NULL AND OLD.""PdfPath"" IS NULL
       AND (NEW.""PdfBytes"" IS NOT NULL OR NEW.""PdfPath"" IS NOT NULL)
       AND (to_jsonb(NEW) - 'PdfBytes' - 'PdfPath') = (to_jsonb(OLD) - 'PdfBytes' - 'PdfPath') THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'license agreement % is immutable', OLD.""Id"";
END $$;
CREATE TRIGGER brqosh_guard_license_agreement BEFORE UPDATE OR DELETE ON license_agreements
    FOR EACH ROW EXECUTE FUNCTION brqosh_guard_license_agreement();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS brqosh_guard_license_agreement ON license_agreements; DROP FUNCTION IF EXISTS brqosh_guard_license_agreement();");
            migrationBuilder.DropTable(
                name: "license_agreements");
        }
    }
}
