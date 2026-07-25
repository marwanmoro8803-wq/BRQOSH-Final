using System;
using Microsoft.EntityFrameworkCore.Migrations;

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
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ParentId = table.Column<long>(type: "INTEGER", nullable: true),
                    IsGroup = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayNameAr = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BankName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: false),
                    GlAccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bills_of_materials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FinishedProductId = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills_of_materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cheques",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ChequeNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ChequeDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    BankAccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<long>(type: "INTEGER", nullable: true),
                    SupplierId = table.Column<long>(type: "INTEGER", nullable: true),
                    PortfolioJournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    SettlementJournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    SecondarySettlementJournalEntryId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    Profile = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "construction_contracts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<long>(type: "INTEGER", nullable: false),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContractDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "construction_projects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CustomerId = table.Column<long>(type: "INTEGER", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RetentionPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_centers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_centers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<long>(type: "INTEGER", nullable: false),
                    OriginalInvoiceId = table.Column<long>(type: "INTEGER", nullable: false),
                    NoteNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    NoteDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_periods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fixed_assets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AcquisitionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SalvageValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    GlAssetAccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    GlDepreciationAccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    EntryNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReversalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    SourceDocument = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_vouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<long>(type: "INTEGER", nullable: false),
                    VoucherNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    VoucherDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    BankAccountId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    PeriodCode = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    PayDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pos_shifts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OpeningCash = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ClosingCash = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransactionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SalePrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    WeightedAverageCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "progress_billings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<long>(type: "INTEGER", nullable: false),
                    BillingNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    BillingDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodDescription = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CurrentWorkAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RetentionPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress_billings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<long>(type: "INTEGER", nullable: false),
                    SupplierInvoiceReference = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    VatDeductible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<long>(type: "INTEGER", nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_vouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<long>(type: "INTEGER", nullable: false),
                    VoucherNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    VoucherDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    BankAccountId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reconciled_bank_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    BankAccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: false),
                    ReconciledDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciled_bank_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<long>(type: "INTEGER", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    PosShiftId = table.Column<long>(type: "INTEGER", nullable: true),
                    PosPaymentMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    MovementNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    MovementDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    WarehouseId = table.Column<long>(type: "INTEGER", nullable: false),
                    ToWarehouseId = table.Column<long>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SourceDocument = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    JournalEntryId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subcontract_agreements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<long>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<long>(type: "INTEGER", nullable: false),
                    AgreementNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AgreementDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RetentionPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    ScopeDescription = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subcontract_agreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    BomId = table.Column<long>(type: "INTEGER", nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ProducedQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CompletionJournalEntryId = table.Column<long>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zatca_einvoice_documents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceDocumentId = table.Column<long>(type: "INTEGER", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    InvoiceUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Icv = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousInvoiceHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    InvoiceHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TransmissionMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ComplianceStatus = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    QrTlvBase64 = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    XmlFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    SignedXmlFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ZatcaResponseJson = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastStatusAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zatca_einvoice_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zatca_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CertificateFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    PrivateKeyFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ApiToken = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zatca_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bom_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComponentProductId = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    bill_of_materials_id = table.Column<long>(type: "INTEGER", nullable: false)
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    construction_project_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_boq_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_construction_boq_items_construction_projects_construction_project_id",
                        column: x => x.construction_project_id,
                        principalTable: "construction_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_note_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: true),
                    credit_note_id = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "journal_entry_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Debit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CostCenterId = table.Column<long>(type: "INTEGER", nullable: true),
                    journal_entry_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BasicSalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    HousingAllowance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransportAllowance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    GosiEmployee = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    GosiEmployer = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    payroll_run_id = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "purchase_invoice_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: true),
                    purchase_invoice_id = table.Column<long>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: true),
                    purchase_order_id = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "sales_invoice_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: true),
                    sales_invoice_id = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "stock_movement_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    stock_movement_id = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "IX_bank_accounts_CompanyId_Code",
                table: "bank_accounts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_GlAccountId",
                table: "bank_accounts",
                column: "GlAccountId");

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
                name: "IX_cheques_BankAccountId",
                table: "cheques",
                column: "BankAccountId");

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
                name: "IX_construction_boq_items_construction_project_id",
                table: "construction_boq_items",
                column: "construction_project_id");

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
                name: "IX_customers_CompanyId_NameAr",
                table: "customers",
                columns: new[] { "CompanyId", "NameAr" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_periods_CompanyId_Code",
                table: "fiscal_periods",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixed_assets_CompanyId_Code",
                table: "fixed_assets",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

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
                name: "IX_payment_vouchers_BankAccountId",
                table: "payment_vouchers",
                column: "BankAccountId");

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
                name: "IX_payroll_lines_payroll_run_id",
                table: "payroll_lines",
                column: "payroll_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_CompanyId_PeriodCode",
                table: "payroll_runs",
                columns: new[] { "CompanyId", "PeriodCode" },
                unique: true);

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
                name: "IX_progress_billings_CustomerId",
                table: "progress_billings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_progress_billings_ProjectId",
                table: "progress_billings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_purchase_invoice_id",
                table: "purchase_invoice_lines",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_InvoiceNumber",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_SupplierId_SupplierInvoiceReference",
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
                name: "IX_purchase_orders_CompanyId_OrderNumber",
                table: "purchase_orders",
                columns: new[] { "CompanyId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_vouchers_BankAccountId",
                table: "receipt_vouchers",
                column: "BankAccountId");

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
                name: "IX_reconciled_bank_entries_CompanyId_BankAccountId_JournalEntryId",
                table: "reconciled_bank_entries",
                columns: new[] { "CompanyId", "BankAccountId", "JournalEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_sales_invoice_id",
                table: "sales_invoice_lines",
                column: "sales_invoice_id");

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
                name: "IX_stock_movement_lines_stock_movement_id",
                table: "stock_movement_lines",
                column: "stock_movement_id");

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
                name: "IX_warehouses_CompanyId_Code",
                table: "warehouses",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_BomId",
                table: "work_orders",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_CompanyId_OrderNumber",
                table: "work_orders",
                columns: new[] { "CompanyId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_zatca_einvoice_documents_CompanyId_DocumentType_SourceDocumentId",
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
                name: "accounts");

            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "bom_lines");

            migrationBuilder.DropTable(
                name: "cheques");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "construction_boq_items");

            migrationBuilder.DropTable(
                name: "construction_contracts");

            migrationBuilder.DropTable(
                name: "cost_centers");

            migrationBuilder.DropTable(
                name: "credit_note_lines");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "fiscal_periods");

            migrationBuilder.DropTable(
                name: "fixed_assets");

            migrationBuilder.DropTable(
                name: "journal_entry_lines");

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
                name: "purchase_invoice_lines");

            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DropTable(
                name: "receipt_vouchers");

            migrationBuilder.DropTable(
                name: "reconciled_bank_entries");

            migrationBuilder.DropTable(
                name: "sales_invoice_lines");

            migrationBuilder.DropTable(
                name: "stock_movement_lines");

            migrationBuilder.DropTable(
                name: "subcontract_agreements");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "work_orders");

            migrationBuilder.DropTable(
                name: "zatca_einvoice_documents");

            migrationBuilder.DropTable(
                name: "zatca_settings");

            migrationBuilder.DropTable(
                name: "bills_of_materials");

            migrationBuilder.DropTable(
                name: "construction_projects");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "payroll_runs");

            migrationBuilder.DropTable(
                name: "purchase_invoices");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "sales_invoices");

            migrationBuilder.DropTable(
                name: "stock_movements");
        }
    }
}
