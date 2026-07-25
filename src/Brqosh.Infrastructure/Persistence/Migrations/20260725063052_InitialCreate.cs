using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayNameAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePasswordOnNextLogin = table.Column<bool>(type: "boolean", nullable: false),
                    MobilePhoneE164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MfaSecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SessionVersion = table.Column<int>(type: "integer", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Details = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BankName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    GlAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_statements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "billing_plans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MaxCompanies = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bills_of_materials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FinishedProductId = table.Column<long>(type: "bigint", nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills_of_materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsHeadquarters = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cheques",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ChequeNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChequeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    PortfolioJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    SettlementJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    SecondarySettlementJournalEntryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AddressAr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LogoFilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PrintFontFamily = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Tahoma"),
                    VatRegistrationNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Profile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CostingMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyGroupId = table.Column<long>(type: "bigint", nullable: true),
                    OperationalMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "company_groups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReportingCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "construction_contracts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContractDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "construction_projects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedTotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Location = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_centers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_centers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    NoteNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_activities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: true),
                    ActivityType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_leads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_opportunities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    LeadId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCloseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "debit_notes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPurchaseInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    NoteNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_version_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_version_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "domain_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    FromCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ToCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_periods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fixed_assets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AcquisitionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SalvageValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GlAssetAccountId = table.Column<long>(type: "bigint", nullable: false),
                    GlDepreciationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AcquisitionJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    DisposalJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intercompany_account_maps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intercompany_account_maps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_balances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_cost_layers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SourceDocument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_cost_layers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    EntryNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReversalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    SourceDocument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    IsIntercompany = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CounterpartyCompanyId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "login_audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PhoneE164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsConsumed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocation_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptVoucherId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentVoucherId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocationDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocation_lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_vouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VoucherDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    PeriodCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pos_shifts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CashierName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosingCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSales = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WeightedAverageCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "progress_billings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    BillingNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BillingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CurrentWorkAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress_billings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_cost_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    CostDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceDocument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_cost_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_requests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    RequestNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Justification = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_vouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VoucherDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reconciled_bank_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: false),
                    ReconciledDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciled_bank_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    SalesQuotationId = table.Column<long>(type: "bigint", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SalesInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_quotations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    QuotationNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    QuotationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_quotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    MovementNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ToWarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SourceDocument = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subcontract_agreements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    AgreementNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AgreementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ScopeDescription = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subcontract_agreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingPlanId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    BomId = table.Column<long>(type: "bigint", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProducedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CompletionJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RejectedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_instances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zatca_einvoice_documents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InvoiceUuid = table.Column<Guid>(type: "uuid", nullable: false),
                    Icv = table.Column<int>(type: "integer", nullable: false),
                    PreviousInvoiceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InvoiceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TransmissionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ComplianceStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    QrTlvBase64 = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    XmlFilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SignedXmlFilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ZatcaResponseJson = table.Column<string>(type: "text", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatusAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zatca_einvoice_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zatca_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CertificateFilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PrivateKeyFilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ApiToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ComplianceRequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CsidSecret = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zatca_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MatchedJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    bank_statement_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_statement_lines_bank_statements_bank_statement_id",
                        column: x => x.bank_statement_id,
                        principalTable: "bank_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponentProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    bill_of_materials_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_bom_lines_bills_of_materials_bill_of_materials_id",
                        column: x => x.bill_of_materials_id,
                        principalTable: "bills_of_materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "construction_boq_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    construction_project_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_boq_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_construction_boq_items_construction_projects_construction_p~",
                        column: x => x.construction_project_id,
                        principalTable: "construction_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_note_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    credit_note_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_note_lines_credit_notes_credit_note_id",
                        column: x => x.credit_note_id,
                        principalTable: "credit_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    PosShiftId = table.Column<long>(type: "bigint", nullable: true),
                    PosPaymentMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_invoices_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debit_note_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    debit_note_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_note_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_debit_note_lines_debit_notes_debit_note_id",
                        column: x => x.debit_note_id,
                        principalTable: "debit_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    goods_receipt_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalTable: "goods_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CostCenterId = table.Column<long>(type: "bigint", nullable: true),
                    journal_entry_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payroll_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BasicSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HousingAllowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransportAllowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GosiEmployee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GosiEmployer = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payroll_run_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_lines_payroll_runs_payroll_run_id",
                        column: x => x.payroll_run_id,
                        principalTable: "payroll_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    purchase_order_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    purchase_request_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_request_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_request_lines_purchase_requests_purchase_request_id",
                        column: x => x.purchase_request_id,
                        principalTable: "purchase_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_lines_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_quotation_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    sales_quotation_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_quotation_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_quotation_lines_sales_quotations_sales_quotation_id",
                        column: x => x.sales_quotation_id,
                        principalTable: "sales_quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_movement_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    stock_movement_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movement_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movement_lines_stock_movements_stock_movement_id",
                        column: x => x.stock_movement_id,
                        principalTable: "stock_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierInvoiceReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VatDeductible = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_approval_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    workflow_instance_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_approval_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_approval_records_workflow_instances_workflow_insta~",
                        column: x => x.workflow_instance_id,
                        principalTable: "workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    sales_invoice_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    purchase_invoice_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_CompanyId_Code",
                table: "accounts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ParentId",
                table: "accounts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_app_users_CompanyId_Username",
                table: "app_users",
                columns: new[] { "CompanyId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_CompanyId_OccurredAtUtc",
                table: "audit_log",
                columns: new[] { "CompanyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_CompanyId_BranchId",
                table: "bank_accounts",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_CompanyId_Code",
                table: "bank_accounts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_GlAccountId",
                table: "bank_accounts",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_lines_bank_statement_id",
                table: "bank_statement_lines",
                column: "bank_statement_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statements_CompanyId_BankAccountId_ImportedAtUtc",
                table: "bank_statements",
                columns: new[] { "CompanyId", "BankAccountId", "ImportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_plans_Code",
                table: "billing_plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_CompanyId_BranchId",
                table: "bills_of_materials",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_CompanyId_Code",
                table: "bills_of_materials",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_FinishedProductId",
                table: "bills_of_materials",
                column: "FinishedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_bom_lines_bill_of_materials_id",
                table: "bom_lines",
                column: "bill_of_materials_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_CompanyId_Code",
                table: "branches",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cheques_BankAccountId",
                table: "cheques",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_CompanyId_BranchId",
                table: "cheques",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_cheques_CompanyId_ChequeNumber_Type",
                table: "cheques",
                columns: new[] { "CompanyId", "ChequeNumber", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_cheques_CustomerId",
                table: "cheques",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_SupplierId",
                table: "cheques",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_CompanyGroupId",
                table: "companies",
                column: "CompanyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_TenantId",
                table: "companies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_groups_Code",
                table: "company_groups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_boq_items_construction_project_id",
                table: "construction_boq_items",
                column: "construction_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_construction_contracts_CompanyId_BranchId",
                table: "construction_contracts",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_construction_contracts_CompanyId_ContractNumber",
                table: "construction_contracts",
                columns: new[] { "CompanyId", "ContractNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_contracts_ProjectId",
                table: "construction_contracts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_projects_CompanyId_BranchId",
                table: "construction_projects",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_construction_projects_CompanyId_Code",
                table: "construction_projects",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_projects_CustomerId",
                table: "construction_projects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_centers_CompanyId_Code",
                table: "cost_centers",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_lines_credit_note_id",
                table: "credit_note_lines",
                column: "credit_note_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_CompanyId_BranchId",
                table: "credit_notes",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_CompanyId_NoteNumber",
                table: "credit_notes",
                columns: new[] { "CompanyId", "NoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_CustomerId",
                table: "credit_notes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_OriginalInvoiceId",
                table: "credit_notes",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_CompanyId_DueAtUtc",
                table: "crm_activities",
                columns: new[] { "CompanyId", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_leads_CompanyId_Status",
                table: "crm_leads",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_opportunities_CompanyId_Stage",
                table: "crm_opportunities",
                columns: new[] { "CompanyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_CompanyId_NameAr",
                table: "customers",
                columns: new[] { "CompanyId", "NameAr" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_CompanyId_VatRegistrationNumber",
                table: "customers",
                columns: new[] { "CompanyId", "VatRegistrationNumber" },
                unique: true,
                filter: "\"VatRegistrationNumber\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_debit_note_lines_debit_note_id",
                table: "debit_note_lines",
                column: "debit_note_id");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_CompanyId_BranchId",
                table: "debit_notes",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_CompanyId_NoteNumber",
                table: "debit_notes",
                columns: new[] { "CompanyId", "NoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_OriginalPurchaseInvoiceId",
                table: "debit_notes",
                column: "OriginalPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_SupplierId",
                table: "debit_notes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_history_CompanyId_DocumentType_DocumentKey~",
                table: "document_version_history",
                columns: new[] { "CompanyId", "DocumentType", "DocumentKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_CompanyId_OccurredAtUtc",
                table: "domain_events",
                columns: new[] { "CompanyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_EventId",
                table: "domain_events",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_CompanyId_FromCurrencyCode_ToCurrencyCode_Ef~",
                table: "exchange_rates",
                columns: new[] { "CompanyId", "FromCurrencyCode", "ToCurrencyCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_periods_CompanyId_Code",
                table: "fiscal_periods",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixed_assets_CompanyId_BranchId",
                table: "fixed_assets",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_fixed_assets_CompanyId_Code",
                table: "fixed_assets",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_goods_receipt_id",
                table: "goods_receipt_lines",
                column: "goods_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_CompanyId_BranchId",
                table: "goods_receipts",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_CompanyId_ReceiptNumber",
                table: "goods_receipts",
                columns: new[] { "CompanyId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_PurchaseOrderId",
                table: "goods_receipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_SupplierId",
                table: "goods_receipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_intercompany_account_maps_CompanyGroupId_AccountCode",
                table: "intercompany_account_maps",
                columns: new[] { "CompanyGroupId", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_CompanyId_BranchId",
                table: "inventory_balances",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_CompanyId_WarehouseId_ProductId",
                table: "inventory_balances",
                columns: new[] { "CompanyId", "WarehouseId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_ProductId",
                table: "inventory_balances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_layers_CompanyId_BranchId",
                table: "inventory_cost_layers",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_layers_CompanyId_ProductId_RemainingQuantity",
                table: "inventory_cost_layers",
                columns: new[] { "CompanyId", "ProductId", "RemainingQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_layers_CompanyId_ProductId_WarehouseId_Recei~",
                table: "inventory_cost_layers",
                columns: new[] { "CompanyId", "ProductId", "WarehouseId", "ReceivedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CompanyId_BranchId",
                table: "journal_entries",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CompanyId_EntryDate",
                table: "journal_entries",
                columns: new[] { "CompanyId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CompanyId_EntryNumber",
                table: "journal_entries",
                columns: new[] { "CompanyId", "EntryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_AccountId",
                table: "journal_entry_lines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_journal_entry_id",
                table: "journal_entry_lines",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_login_audit_CompanyId_CreatedAtUtc",
                table: "login_audit",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_CompanyId_Username_Purpose_IsConsumed",
                table: "otp_challenges",
                columns: new[] { "CompanyId", "Username", "Purpose", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_lines_CompanyId_BranchId",
                table: "payment_allocation_lines",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_lines_CompanyId_DocumentType_DocumentId",
                table: "payment_allocation_lines",
                columns: new[] { "CompanyId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_lines_PaymentVoucherId",
                table: "payment_allocation_lines",
                column: "PaymentVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_lines_ReceiptVoucherId",
                table: "payment_allocation_lines",
                column: "ReceiptVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_vouchers_BankAccountId",
                table: "payment_vouchers",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_vouchers_CompanyId_BranchId",
                table: "payment_vouchers",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_vouchers_CompanyId_VoucherNumber",
                table: "payment_vouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_vouchers_SupplierId",
                table: "payment_vouchers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_vouchers_WorkflowInstanceId",
                table: "payment_vouchers",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_lines_payroll_run_id",
                table: "payroll_lines",
                column: "payroll_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_CompanyId_BranchId",
                table: "payroll_runs",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_CompanyId_PeriodCode",
                table: "payroll_runs",
                columns: new[] { "CompanyId", "PeriodCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_shifts_CompanyId_BranchId",
                table: "pos_shifts",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_shifts_CompanyId_Status",
                table: "pos_shifts",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_products_CompanyId_Code",
                table: "products",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_progress_billings_CompanyId_BillingNumber",
                table: "progress_billings",
                columns: new[] { "CompanyId", "BillingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_progress_billings_CompanyId_BranchId",
                table: "progress_billings",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_progress_billings_CustomerId",
                table: "progress_billings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_progress_billings_ProjectId",
                table: "progress_billings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_cost_entries_CompanyId_BranchId",
                table: "project_cost_entries",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_cost_entries_CompanyId_ProjectId_CostDate",
                table: "project_cost_entries",
                columns: new[] { "CompanyId", "ProjectId", "CostDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_purchase_invoice_id",
                table: "purchase_invoice_lines",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_BranchId",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_InvoiceNumber",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_SupplierId_SupplierInvoiceRefer~",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "SupplierId", "SupplierInvoiceReference" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_PurchaseOrderId",
                table: "purchase_invoices",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_SupplierId",
                table: "purchase_invoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_purchase_order_id",
                table: "purchase_order_lines",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_CompanyId_BranchId",
                table: "purchase_orders",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_CompanyId_OrderNumber",
                table: "purchase_orders",
                columns: new[] { "CompanyId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_WorkflowInstanceId",
                table: "purchase_orders",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_lines_purchase_request_id",
                table: "purchase_request_lines",
                column: "purchase_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requests_CompanyId_BranchId",
                table: "purchase_requests",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requests_CompanyId_RequestNumber",
                table: "purchase_requests",
                columns: new[] { "CompanyId", "RequestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requests_SupplierId",
                table: "purchase_requests",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requests_WorkflowInstanceId",
                table: "purchase_requests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_vouchers_BankAccountId",
                table: "receipt_vouchers",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_vouchers_CompanyId_BranchId",
                table: "receipt_vouchers",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_vouchers_CompanyId_VoucherNumber",
                table: "receipt_vouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_vouchers_CustomerId",
                table: "receipt_vouchers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_reconciled_bank_entries_CompanyId_BankAccountId_JournalEntr~",
                table: "reconciled_bank_entries",
                columns: new[] { "CompanyId", "BankAccountId", "JournalEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciled_bank_entries_CompanyId_BranchId",
                table: "reconciled_bank_entries",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshots_CompanyId_ReportType_CapturedAtUtc",
                table: "report_snapshots",
                columns: new[] { "CompanyId", "ReportType", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_sales_invoice_id",
                table: "sales_invoice_lines",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CompanyId_BranchId",
                table: "sales_invoices",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CompanyId_InvoiceDate",
                table: "sales_invoices",
                columns: new[] { "CompanyId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CompanyId_InvoiceNumber",
                table: "sales_invoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CustomerId",
                table: "sales_invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_PosShiftId",
                table: "sales_invoices",
                column: "PosShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_sales_order_id",
                table: "sales_order_lines",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_CompanyId_OrderNumber",
                table: "sales_orders",
                columns: new[] { "CompanyId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_CustomerId",
                table: "sales_orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_SalesQuotationId",
                table: "sales_orders",
                column: "SalesQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_quotation_lines_sales_quotation_id",
                table: "sales_quotation_lines",
                column: "sales_quotation_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_quotations_CompanyId_QuotationNumber",
                table: "sales_quotations",
                columns: new[] { "CompanyId", "QuotationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movement_lines_stock_movement_id",
                table: "stock_movement_lines",
                column: "stock_movement_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_CompanyId_BranchId",
                table: "stock_movements",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_CompanyId_MovementDate",
                table: "stock_movements",
                columns: new[] { "CompanyId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_CompanyId_MovementNumber",
                table: "stock_movements",
                columns: new[] { "CompanyId", "MovementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subcontract_agreements_CompanyId_AgreementNumber",
                table: "subcontract_agreements",
                columns: new[] { "CompanyId", "AgreementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subcontract_agreements_CompanyId_BranchId",
                table: "subcontract_agreements",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_subcontract_agreements_ProjectId",
                table: "subcontract_agreements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_subcontract_agreements_SupplierId",
                table: "subcontract_agreements",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_CompanyId_NameAr",
                table: "suppliers",
                columns: new[] { "CompanyId", "NameAr" });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_CompanyId_VatRegistrationNumber",
                table: "suppliers",
                columns: new[] { "CompanyId", "VatRegistrationNumber" },
                unique: true,
                filter: "\"VatRegistrationNumber\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_TenantId",
                table: "tenant_subscriptions",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_CompanyId_BranchId",
                table: "warehouses",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_CompanyId_Code",
                table: "warehouses",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_BomId",
                table: "work_orders",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_CompanyId_BranchId",
                table: "work_orders",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_CompanyId_OrderNumber",
                table: "work_orders",
                columns: new[] { "CompanyId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_approval_records_workflow_instance_id",
                table: "workflow_approval_records",
                column: "workflow_instance_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_CompanyId_DocumentKind_DocumentId",
                table: "workflow_instances",
                columns: new[] { "CompanyId", "DocumentKind", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zatca_einvoice_documents_CompanyId_BranchId",
                table: "zatca_einvoice_documents",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_zatca_einvoice_documents_CompanyId_DocumentType_SourceDocum~",
                table: "zatca_einvoice_documents",
                columns: new[] { "CompanyId", "DocumentType", "SourceDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zatca_einvoice_documents_CompanyId_InvoiceNumber",
                table: "zatca_einvoice_documents",
                columns: new[] { "CompanyId", "InvoiceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_zatca_settings_CompanyId",
                table: "zatca_settings",
                column: "CompanyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "bank_statement_lines");

            migrationBuilder.DropTable(
                name: "billing_plans");

            migrationBuilder.DropTable(
                name: "bom_lines");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "cheques");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "company_groups");

            migrationBuilder.DropTable(
                name: "construction_boq_items");

            migrationBuilder.DropTable(
                name: "construction_contracts");

            migrationBuilder.DropTable(
                name: "cost_centers");

            migrationBuilder.DropTable(
                name: "credit_note_lines");

            migrationBuilder.DropTable(
                name: "crm_activities");

            migrationBuilder.DropTable(
                name: "crm_leads");

            migrationBuilder.DropTable(
                name: "crm_opportunities");

            migrationBuilder.DropTable(
                name: "debit_note_lines");

            migrationBuilder.DropTable(
                name: "document_version_history");

            migrationBuilder.DropTable(
                name: "domain_events");

            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "fiscal_periods");

            migrationBuilder.DropTable(
                name: "fixed_assets");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines");

            migrationBuilder.DropTable(
                name: "intercompany_account_maps");

            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "inventory_cost_layers");

            migrationBuilder.DropTable(
                name: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "login_audit");

            migrationBuilder.DropTable(
                name: "otp_challenges");

            migrationBuilder.DropTable(
                name: "payment_allocation_lines");

            migrationBuilder.DropTable(
                name: "payment_vouchers");

            migrationBuilder.DropTable(
                name: "payroll_lines");

            migrationBuilder.DropTable(
                name: "pos_shifts");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "progress_billings");

            migrationBuilder.DropTable(
                name: "project_cost_entries");

            migrationBuilder.DropTable(
                name: "purchase_invoice_lines");

            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DropTable(
                name: "purchase_request_lines");

            migrationBuilder.DropTable(
                name: "receipt_vouchers");

            migrationBuilder.DropTable(
                name: "reconciled_bank_entries");

            migrationBuilder.DropTable(
                name: "report_snapshots");

            migrationBuilder.DropTable(
                name: "sales_invoice_lines");

            migrationBuilder.DropTable(
                name: "sales_order_lines");

            migrationBuilder.DropTable(
                name: "sales_quotation_lines");

            migrationBuilder.DropTable(
                name: "stock_movement_lines");

            migrationBuilder.DropTable(
                name: "subcontract_agreements");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "work_orders");

            migrationBuilder.DropTable(
                name: "workflow_approval_records");

            migrationBuilder.DropTable(
                name: "zatca_einvoice_documents");

            migrationBuilder.DropTable(
                name: "zatca_settings");

            migrationBuilder.DropTable(
                name: "bank_statements");

            migrationBuilder.DropTable(
                name: "bills_of_materials");

            migrationBuilder.DropTable(
                name: "construction_projects");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "debit_notes");

            migrationBuilder.DropTable(
                name: "goods_receipts");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "payroll_runs");

            migrationBuilder.DropTable(
                name: "purchase_invoices");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "purchase_requests");

            migrationBuilder.DropTable(
                name: "sales_invoices");

            migrationBuilder.DropTable(
                name: "sales_orders");

            migrationBuilder.DropTable(
                name: "sales_quotations");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "workflow_instances");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
