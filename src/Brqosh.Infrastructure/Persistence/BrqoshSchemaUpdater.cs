using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// ترقية تدريجية للمخطط — أعمدة وفهارس ناقصة فقط.
/// أسماء الأعمدة PascalCase لتتوافق مع EF Core EnsureCreated.
/// </summary>
public class BrqoshSchemaUpdater(BrqoshDbContext db)
{
    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var isPg = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        if (isPg)
            await ApplyPostgreSqlPatchesAsync(ct);
        else
            await ApplySqlitePatchesAsync(ct);
    }

    private async Task ApplyPostgreSqlPatchesAsync(CancellationToken ct)
    {
        // V12 lifecycle — قبل أي استعلام EF على companies/app_users
        await EnsurePgColumnAsync("companies", "OperationalMode", "character varying(16) NOT NULL DEFAULT 'Active'", ct);
        await EnsurePgColumnAsync("app_users", "MfaSecret", "character varying(64) NULL", ct);
        await EnsurePgColumnAsync("app_users", "SessionVersion", "INTEGER NOT NULL DEFAULT 0", ct);

        // أعمدة أُضيفت بعد إصدارات أقدم — PascalCase كما في EF
        await EnsurePgColumnAsync("receipt_vouchers", "BankAccountId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("payment_vouchers", "BankAccountId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("purchase_invoices", "PurchaseOrderId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("sales_invoices", "PosShiftId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("sales_invoices", "PosPaymentMethod", "character varying(16) NULL", ct);
        await EnsurePgColumnAsync("zatca_settings", "ComplianceRequestId", "character varying(64) NOT NULL DEFAULT ''", ct);
        await EnsurePgColumnAsync("zatca_settings", "CsidSecret", "character varying(512) NOT NULL DEFAULT ''", ct);
        await EnsurePgColumnAsync("fixed_assets", "AcquisitionJournalEntryId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalJournalEntryId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("app_users", "MustChangePasswordOnNextLogin", "BOOLEAN NOT NULL DEFAULT false", ct);
        await EnsurePgColumnAsync("app_users", "MobilePhoneE164", "character varying(16) NULL", ct);
        await EnsurePgColumnAsync("companies", "CostingMethod", "character varying(32) NOT NULL DEFAULT 'WeightedAverage'", ct);
        await EnsurePgColumnAsync("companies", "BaseCurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
        await EnsurePgColumnAsync("companies", "FunctionalCurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
        await EnsurePgColumnAsync("companies", "NameEn", "character varying(256) NULL", ct);
        await EnsurePgColumnAsync("companies", "AddressAr", "character varying(512) NULL", ct);
        await EnsurePgColumnAsync("companies", "LogoFilePath", "character varying(512) NULL", ct);
        await EnsurePgColumnAsync("companies", "PrintFontFamily", "character varying(64) NOT NULL DEFAULT 'Tahoma'", ct);
        await EnsurePgColumnAsync("bank_accounts", "CurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
        await EnsurePgColumnAsync("construction_projects", "EstimatedTotalCost", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
        await EnsureBranchesTableAsync(isPg: true, ct);
        await EnsureInventoryBalancesTableAsync(isPg: true, ct);
        await EnsureFifoAndAllocationTablesAsync(isPg: true, ct);
        await EnsureDomainEventsTableAsync(isPg: true, ct);
        await EnsureReportSnapshotsTableAsync(isPg: true, ct);
        await EnsureBranchIdColumnsAsync(isPg: true, ct);
        await EnsureCompanyPlatformAsync(isPg: true, ct);
        await EnsureExchangeRatesTableAsync(isPg: true, ct);
        await EnsureIntercompanyColumnsAsync(isPg: true, ct);
        await EnsureIntercompanyAccountMapsTableAsync(isPg: true, ct);
        await EnsureBillingPlatformTablesAsync(isPg: true, ct);
        await EnsureWorkflowTablesAsync(isPg: true, ct);
        await EnsureBankStatementTablesAsync(isPg: true, ct);
        await EnsureMasterDataGovernanceColumnsAsync(isPg: true, ct);
        await ApplySoftDeleteColumnsAsync(isPg: true, ct);
        await EnsureDocumentTotalColumnsAsync(isPg: true, ct);
        await EnsurePgColumnAsync("audit_log", "Reason", "character varying(512) NULL", ct);
        await EnsureV12LifecycleTablesAsync(isPg: true, ct);
        await EnsureV14CrmTablesAsync(isPg: true, ct);

        // فهارس اختيارية — PascalCase
        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_bank_accounts_company_code
                ON bank_accounts ("CompanyId", "Code");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_construction_projects_company_code
                ON construction_projects ("CompanyId", "Code");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_construction_contracts_company_number
                ON construction_contracts ("CompanyId", "ContractNumber");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_subcontract_agreements_company_number
                ON subcontract_agreements ("CompanyId", "AgreementNumber");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_app_users_company_username
                ON app_users ("CompanyId", "Username");
            """, ct);
    }

    private async Task ApplySqlitePatchesAsync(CancellationToken ct)
    {
        await EnsureSqliteColumnAsync("companies", "OperationalMode", "TEXT NOT NULL DEFAULT 'Active'", ct);
        await EnsureSqliteColumnAsync("app_users", "MfaSecret", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("app_users", "SessionVersion", "INTEGER NOT NULL DEFAULT 0", ct);

        await EnsureSqliteColumnAsync("receipt_vouchers", "BankAccountId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("payment_vouchers", "BankAccountId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("purchase_invoices", "PurchaseOrderId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("sales_invoices", "PosShiftId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("sales_invoices", "PosPaymentMethod", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("zatca_settings", "ComplianceRequestId", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("zatca_settings", "CsidSecret", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "AcquisitionJournalEntryId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalJournalEntryId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("app_users", "MustChangePasswordOnNextLogin", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("app_users", "MobilePhoneE164", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "CostingMethod", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "BaseCurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
        await EnsureSqliteColumnAsync("companies", "FunctionalCurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
        await EnsureSqliteColumnAsync("companies", "NameEn", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "AddressAr", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "LogoFilePath", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "PrintFontFamily", "TEXT NOT NULL DEFAULT 'Tahoma'", ct);
        await EnsureSqliteColumnAsync("bank_accounts", "CurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
        await EnsureSqliteColumnAsync("construction_projects", "EstimatedTotalCost", "REAL NULL", ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE app_users SET MustChangePasswordOnNextLogin = 0 WHERE MustChangePasswordOnNextLogin IS NULL", ct);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE companies SET CostingMethod = 'WeightedAverage' WHERE CostingMethod IS NULL", ct);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE companies SET BaseCurrencyCode = 'SAR' WHERE BaseCurrencyCode IS NULL OR BaseCurrencyCode = ''", ct);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE companies SET FunctionalCurrencyCode = 'SAR' WHERE FunctionalCurrencyCode IS NULL OR FunctionalCurrencyCode = ''", ct);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE construction_projects SET EstimatedTotalCost = 0 WHERE EstimatedTotalCost IS NULL", ct);
        }
        catch { /* */ }
        await EnsureBranchesTableAsync(isPg: false, ct);
        await EnsureInventoryBalancesTableAsync(isPg: false, ct);
        await EnsureFifoAndAllocationTablesAsync(isPg: false, ct);
        await EnsureDomainEventsTableAsync(isPg: false, ct);
        await EnsureReportSnapshotsTableAsync(isPg: false, ct);
        await EnsureBranchIdColumnsAsync(isPg: false, ct);
        await EnsureCompanyPlatformAsync(isPg: false, ct);
        await EnsureExchangeRatesTableAsync(isPg: false, ct);
        await EnsureIntercompanyColumnsAsync(isPg: false, ct);
        await EnsureIntercompanyAccountMapsTableAsync(isPg: false, ct);
        await EnsureBillingPlatformTablesAsync(isPg: false, ct);
        await EnsureWorkflowTablesAsync(isPg: false, ct);
        await EnsureBankStatementTablesAsync(isPg: false, ct);
        await EnsureMasterDataGovernanceColumnsAsync(isPg: false, ct);
        await ApplySoftDeleteColumnsAsync(isPg: false, ct);
        await EnsureDocumentTotalColumnsAsync(isPg: false, ct);
        await EnsureSqliteColumnAsync("audit_log", "Reason", "TEXT NULL", ct);
        await EnsureV12LifecycleTablesAsync(isPg: false, ct);
        await EnsureV14CrmTablesAsync(isPg: false, ct);

        await SafeSqliteIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_bank_accounts_company_code
                ON bank_accounts (CompanyId, Code);
            """, ct);

        await SafeSqliteIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_construction_projects_company_code
                ON construction_projects (CompanyId, Code);
            """, ct);

        await SafeSqliteIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ix_app_users_company_username
                ON app_users (CompanyId, Username);
            """, ct);
    }

    private async Task EnsureCompanyPlatformAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS company_groups (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "Code" character varying(16) NOT NULL,
                  "NameAr" character varying(256) NOT NULL,
                  "ReportingCurrencyCode" character varying(3) NOT NULL DEFAULT 'SAR',
                  "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_company_groups_code ON company_groups ("Code");
              """
            : """
              CREATE TABLE IF NOT EXISTS company_groups (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Code TEXT NOT NULL,
                  NameAr TEXT NOT NULL,
                  ReportingCurrencyCode TEXT NOT NULL DEFAULT 'SAR',
                  CreatedAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_company_groups_code ON company_groups (Code);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* */ }

        if (isPg)
        {
            await EnsurePgColumnAsync("companies", "TenantId", "UUID NOT NULL DEFAULT gen_random_uuid()", ct);
            await EnsurePgColumnAsync("companies", "CompanyGroupId", "BIGINT NULL", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("companies", "TenantId", "TEXT NULL", ct);
            await EnsureSqliteColumnAsync("companies", "CompanyGroupId", "INTEGER NULL", ct);
        }

        await BackfillCompanyTenantIdsAsync(ct);
    }

    private async Task EnsureExchangeRatesTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS exchange_rates (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "FromCurrencyCode" character varying(3) NOT NULL,
                  "ToCurrencyCode" character varying(3) NOT NULL,
                  "EffectiveDate" DATE NOT NULL,
                  "Rate" NUMERIC(18,6) NOT NULL,
                  "Source" character varying(64) NULL,
                  "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              CREATE INDEX IF NOT EXISTS ix_exchange_rates_lookup
                  ON exchange_rates ("CompanyId", "FromCurrencyCode", "ToCurrencyCode", "EffectiveDate");
              """
            : """
              CREATE TABLE IF NOT EXISTS exchange_rates (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  FromCurrencyCode TEXT NOT NULL,
                  ToCurrencyCode TEXT NOT NULL,
                  EffectiveDate TEXT NOT NULL,
                  Rate REAL NOT NULL,
                  Source TEXT NULL,
                  CreatedAtUtc TEXT NOT NULL
              );
              CREATE INDEX IF NOT EXISTS ix_exchange_rates_lookup
                  ON exchange_rates (CompanyId, FromCurrencyCode, ToCurrencyCode, EffectiveDate);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* */ }
    }

    private async Task EnsureIntercompanyColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (isPg)
        {
            await EnsurePgColumnAsync("journal_entries", "IsIntercompany", "BOOLEAN NOT NULL DEFAULT false", ct);
            await EnsurePgColumnAsync("journal_entries", "CounterpartyCompanyId", "BIGINT NULL", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("journal_entries", "IsIntercompany", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsureSqliteColumnAsync("journal_entries", "CounterpartyCompanyId", "INTEGER NULL", ct);
        }
    }

    private async Task EnsureIntercompanyAccountMapsTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS intercompany_account_maps (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyGroupId" BIGINT NOT NULL,
                  "AccountCode" character varying(16) NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_ic_account_maps_group_code
                  ON intercompany_account_maps ("CompanyGroupId", "AccountCode");
              """
            : """
              CREATE TABLE IF NOT EXISTS intercompany_account_maps (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyGroupId INTEGER NOT NULL,
                  AccountCode TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_ic_account_maps_group_code
                  ON intercompany_account_maps (CompanyGroupId, AccountCode);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* */ }
    }

    private async Task EnsureBillingPlatformTablesAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS billing_plans (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "Code" character varying(32) NOT NULL,
                  "NameAr" character varying(256) NOT NULL,
                  "MaxCompanies" INTEGER NOT NULL DEFAULT 1,
                  "IsActive" BOOLEAN NOT NULL DEFAULT true
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_billing_plans_code ON billing_plans ("Code");

              CREATE TABLE IF NOT EXISTS tenant_subscriptions (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "TenantId" UUID NOT NULL,
                  "BillingPlanId" BIGINT NOT NULL,
                  "Status" character varying(16) NOT NULL DEFAULT 'Trial',
                  "StartedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_tenant_subscriptions_tenant ON tenant_subscriptions ("TenantId");
              """
            : """
              CREATE TABLE IF NOT EXISTS billing_plans (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Code TEXT NOT NULL,
                  NameAr TEXT NOT NULL,
                  MaxCompanies INTEGER NOT NULL DEFAULT 1,
                  IsActive INTEGER NOT NULL DEFAULT 1
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_billing_plans_code ON billing_plans (Code);

              CREATE TABLE IF NOT EXISTS tenant_subscriptions (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  TenantId TEXT NOT NULL,
                  BillingPlanId INTEGER NOT NULL,
                  Status TEXT NOT NULL DEFAULT 'Trial',
                  StartedAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_tenant_subscriptions_tenant ON tenant_subscriptions (TenantId);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* */ }

        await SeedDefaultBillingPlansAsync(ct);
    }

    private async Task SeedDefaultBillingPlansAsync(CancellationToken ct)
    {
        if (await db.BillingPlans.AnyAsync(ct))
            return;

        db.BillingPlans.AddRange(
            BillingPlan.Create("TRIAL", "تجريبي", 1),
            BillingPlan.Create("STANDARD", "قياسي", 1),
            BillingPlan.Create("ENTERPRISE", "مؤسسي", 5));
        await db.SaveChangesAsync(ct);
    }

    private async Task BackfillCompanyTenantIdsAsync(CancellationToken ct)
    {
        var companies = await db.Companies.ToListAsync(ct);
        var changed = false;
        foreach (var company in companies)
        {
            if (company.TenantId == Guid.Empty)
            {
                db.Entry(company).Property(nameof(Company.TenantId)).CurrentValue = Guid.NewGuid();
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    private async Task EnsureBranchIdColumnsAsync(bool isPg, CancellationToken ct)
    {
        string[] tables =
        [
            "sales_invoices", "purchase_invoices", "goods_receipts", "journal_entries",
            "stock_movements", "receipt_vouchers", "payment_vouchers",
            "credit_notes", "debit_notes", "purchase_orders", "cheques", "bank_accounts",
            "payment_allocation_lines", "reconciled_bank_entries", "zatca_einvoice_documents",
            "warehouses", "inventory_balances", "inventory_cost_layers", "fixed_assets",
            "progress_billings", "construction_projects", "construction_contracts",
            "subcontract_agreements", "project_cost_entries", "work_orders",
            "bills_of_materials", "payroll_runs", "pos_shifts",
        ];

        foreach (var table in tables)
        {
            if (isPg)
                await EnsurePgColumnAsync(table, "BranchId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "BranchId", "INTEGER NULL", ct);
        }
    }

    private async Task SafePgIndexAsync(string sql, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            // الجدول غير موجود بعد — EnsureCreated سيتكفل لاحقاً
        }
    }

    private async Task SafeSqliteIndexAsync(string sql, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private async Task EnsurePgColumnAsync(string table, string column, string type, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);
        SchemaSqlGuard.ValidateColumn(column);
        SchemaSqlGuard.ValidatePgType(type);

        FormattableString query = $"""
             SELECT COUNT(*) AS "Value"
             FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = {table} AND column_name = {column}
             """;
        var exists = await db.Database.SqlQuery<int>(query).SingleAsync(ct);

        if (exists == 0)
            await db.Database.ExecuteSqlRawAsync(SchemaSqlGuard.BuildPgAddColumnSql(table, column, type), ct);
    }

    private async Task EnsureSqliteColumnAsync(string table, string column, string type, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);
        SchemaSqlGuard.ValidateColumn(column);
        SchemaSqlGuard.ValidateSqliteType(type);

        FormattableString query = $"""
            SELECT name AS "Value"
            FROM pragma_table_info({table})
            WHERE name = {column}
            """;
        var columns = await db.Database.SqlQuery<string>(query).ToListAsync(ct);

        if (columns.Count == 0)
            await db.Database.ExecuteSqlRawAsync(SchemaSqlGuard.BuildSqliteAddColumnSql(table, column, type), ct);
    }

    private static readonly string[] SoftDeleteTables =
    [
        "customers", "suppliers", "products", "warehouses", "cost_centers",
        "accounts", "bank_accounts", "construction_projects", "bills_of_materials", "app_users",
    ];

    private async Task ApplySoftDeleteColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in SoftDeleteTables)
        {
            if (isPg)
                await EnsurePgColumnAsync(table, "DeletedAtUtc", "TIMESTAMPTZ NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "DeletedAtUtc", "TEXT NULL", ct);

            await BackfillSoftDeleteAsync(table, isPg, ct);
        }
    }

    private async Task EnsureFifoAndAllocationTablesAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS inventory_cost_layers (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "ProductId" BIGINT NOT NULL,
                  "WarehouseId" BIGINT NOT NULL,
                  "ReceivedDate" DATE NOT NULL,
                  "OriginalQuantity" NUMERIC(18,4) NOT NULL,
                  "RemainingQuantity" NUMERIC(18,4) NOT NULL,
                  "UnitCost" NUMERIC(18,4) NOT NULL,
                  "SourceDocument" character varying(128) NOT NULL,
                  "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              CREATE TABLE IF NOT EXISTS payment_allocation_lines (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "ReceiptVoucherId" BIGINT NULL,
                  "PaymentVoucherId" BIGINT NULL,
                  "DocumentType" character varying(32) NOT NULL,
                  "DocumentId" BIGINT NOT NULL,
                  "DocumentNumber" character varying(64) NOT NULL,
                  "Amount" NUMERIC(18,2) NOT NULL,
                  "AllocationDate" DATE NOT NULL
              );
              CREATE TABLE IF NOT EXISTS project_cost_entries (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "ProjectId" BIGINT NOT NULL,
                  "CostDate" DATE NOT NULL,
                  "Description" character varying(512) NOT NULL,
                  "Amount" NUMERIC(18,2) NOT NULL,
                  "SourceDocument" character varying(128) NOT NULL DEFAULT '',
                  "JournalEntryId" BIGINT NULL,
                  "IsReversed" BOOLEAN NOT NULL DEFAULT false,
                  "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              """
            : """
              CREATE TABLE IF NOT EXISTS inventory_cost_layers (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  ProductId INTEGER NOT NULL,
                  WarehouseId INTEGER NOT NULL,
                  ReceivedDate TEXT NOT NULL,
                  OriginalQuantity REAL NOT NULL,
                  RemainingQuantity REAL NOT NULL,
                  UnitCost REAL NOT NULL,
                  SourceDocument TEXT NOT NULL,
                  CreatedAtUtc TEXT NOT NULL
              );
              CREATE TABLE IF NOT EXISTS payment_allocation_lines (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  ReceiptVoucherId INTEGER NULL,
                  PaymentVoucherId INTEGER NULL,
                  DocumentType TEXT NOT NULL,
                  DocumentId INTEGER NOT NULL,
                  DocumentNumber TEXT NOT NULL,
                  Amount REAL NOT NULL,
                  AllocationDate TEXT NOT NULL
              );
              CREATE TABLE IF NOT EXISTS project_cost_entries (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  ProjectId INTEGER NOT NULL,
                  CostDate TEXT NOT NULL,
                  Description TEXT NOT NULL,
                  Amount REAL NOT NULL,
                  SourceDocument TEXT NOT NULL DEFAULT '',
                  JournalEntryId INTEGER NULL,
                  IsReversed INTEGER NOT NULL DEFAULT 0,
                  CreatedAtUtc TEXT NOT NULL
              );
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* EnsureCreated may create later */ }

        await EnsureGoodsReceiptAndDebitNoteTablesAsync(isPg, ct);
    }

    private async Task EnsureDomainEventsTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS domain_events (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "EventId" UUID NOT NULL,
                  "CompanyId" BIGINT NULL,
                  "EventType" character varying(128) NOT NULL,
                  "PayloadJson" TEXT NOT NULL,
                  "OccurredAtUtc" TIMESTAMPTZ NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_domain_events_event_id ON domain_events ("EventId");
              CREATE INDEX IF NOT EXISTS ix_domain_events_company_occurred ON domain_events ("CompanyId", "OccurredAtUtc");
              """
            : """
              CREATE TABLE IF NOT EXISTS domain_events (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  EventId TEXT NOT NULL,
                  CompanyId INTEGER NULL,
                  EventType TEXT NOT NULL,
                  PayloadJson TEXT NOT NULL,
                  OccurredAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_domain_events_event_id ON domain_events (EventId);
              CREATE INDEX IF NOT EXISTS ix_domain_events_company_occurred ON domain_events (CompanyId, OccurredAtUtc);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* EnsureCreated may create later */ }

        // أعمدة طابور Outbox على القواعد القائمة (ع-D2، CEEF-204).
        if (isPg)
        {
            await EnsurePgColumnAsync("domain_events", "DispatchedAtUtc", "TIMESTAMPTZ NULL", ct);
            await EnsurePgColumnAsync("domain_events", "DispatchAttempts", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("domain_events", "LastDispatchError", "character varying(512) NULL", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("domain_events", "DispatchedAtUtc", "TEXT NULL", ct);
            await EnsureSqliteColumnAsync("domain_events", "DispatchAttempts", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsureSqliteColumnAsync("domain_events", "LastDispatchError", "TEXT NULL", ct);
        }

        // الأحداث السابقة تُعتبر مُرسَلة — الطابور للجديد فقط، لا لإعادة إرسال التاريخ.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                isPg
                    ? """UPDATE domain_events SET "DispatchedAtUtc" = "OccurredAtUtc" WHERE "DispatchedAtUtc" IS NULL"""
                    : "UPDATE domain_events SET DispatchedAtUtc = OccurredAtUtc WHERE DispatchedAtUtc IS NULL",
                ct);
        }
        catch { /* الأعمدة قد تُنشأ لاحقاً عبر EnsureCreated */ }
    }

    private async Task EnsureReportSnapshotsTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS report_snapshots (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "ReportType" character varying(64) NOT NULL,
                  "AsOfDate" DATE NOT NULL,
                  "CapturedAtUtc" TIMESTAMPTZ NOT NULL,
                  "PayloadJson" TEXT NOT NULL,
                  "RowCount" INTEGER NOT NULL,
                  "Hash" character varying(64) NOT NULL
              );
              CREATE INDEX IF NOT EXISTS ix_report_snapshots_company_type ON report_snapshots ("CompanyId", "ReportType", "CapturedAtUtc");
              """
            : """
              CREATE TABLE IF NOT EXISTS report_snapshots (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  ReportType TEXT NOT NULL,
                  AsOfDate TEXT NOT NULL,
                  CapturedAtUtc TEXT NOT NULL,
                  PayloadJson TEXT NOT NULL,
                  RowCount INTEGER NOT NULL,
                  Hash TEXT NOT NULL
              );
              CREATE INDEX IF NOT EXISTS ix_report_snapshots_company_type ON report_snapshots (CompanyId, ReportType, CapturedAtUtc);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* EnsureCreated may create later */ }
    }

    private async Task EnsureBranchesTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS branches (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "Code" character varying(16) NOT NULL,
                  "NameAr" character varying(256) NOT NULL,
                  "IsHeadquarters" BOOLEAN NOT NULL DEFAULT false,
                  "IsActive" BOOLEAN NOT NULL DEFAULT true,
                  "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_branches_company_code ON branches ("CompanyId", "Code");
              """
            : """
              CREATE TABLE IF NOT EXISTS branches (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  Code TEXT NOT NULL,
                  NameAr TEXT NOT NULL,
                  IsHeadquarters INTEGER NOT NULL DEFAULT 0,
                  IsActive INTEGER NOT NULL DEFAULT 1,
                  CreatedAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_branches_company_code ON branches (CompanyId, Code);
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* */ }
    }

    private async Task EnsureGoodsReceiptAndDebitNoteTablesAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS goods_receipts (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "SupplierId" BIGINT NOT NULL,
                  "PurchaseOrderId" BIGINT NOT NULL,
                  "WarehouseId" BIGINT NOT NULL,
                  "ReceiptNumber" character varying(32) NULL,
                  "ReceiptDate" DATE NOT NULL,
                  "Notes" character varying(1024) NOT NULL DEFAULT '',
                  "Status" character varying(16) NOT NULL,
                  "StockMovementId" BIGINT NULL
              );
              CREATE TABLE IF NOT EXISTS goods_receipt_lines (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "goods_receipt_id" BIGINT NOT NULL,
                  "ProductId" BIGINT NULL,
                  "Description" character varying(512) NOT NULL,
                  "OrderedQuantity" NUMERIC(18,4) NOT NULL,
                  "ReceivedQuantity" NUMERIC(18,4) NOT NULL,
                  "UnitCost" NUMERIC(18,4) NOT NULL
              );
              CREATE TABLE IF NOT EXISTS debit_notes (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "SupplierId" BIGINT NOT NULL,
                  "OriginalPurchaseInvoiceId" BIGINT NOT NULL,
                  "NoteNumber" character varying(32) NULL,
                  "NoteDate" DATE NOT NULL,
                  "Reason" character varying(512) NOT NULL,
                  "Status" character varying(16) NOT NULL,
                  "JournalEntryId" BIGINT NULL
              );
              CREATE TABLE IF NOT EXISTS debit_note_lines (
                  "id" BIGSERIAL PRIMARY KEY,
                  "debit_note_id" BIGINT NOT NULL,
                  "Description" character varying(512) NOT NULL,
                  "Quantity" NUMERIC(18,4) NOT NULL,
                  "UnitPrice" NUMERIC(18,2) NOT NULL,
                  "ProductId" BIGINT NULL
              );
              """
            : """
              CREATE TABLE IF NOT EXISTS goods_receipts (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  SupplierId INTEGER NOT NULL,
                  PurchaseOrderId INTEGER NOT NULL,
                  WarehouseId INTEGER NOT NULL,
                  ReceiptNumber TEXT NULL,
                  ReceiptDate TEXT NOT NULL,
                  Notes TEXT NOT NULL DEFAULT '',
                  Status TEXT NOT NULL,
                  StockMovementId INTEGER NULL
              );
              CREATE TABLE IF NOT EXISTS goods_receipt_lines (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  goods_receipt_id INTEGER NOT NULL,
                  ProductId INTEGER NULL,
                  Description TEXT NOT NULL,
                  OrderedQuantity REAL NOT NULL,
                  ReceivedQuantity REAL NOT NULL,
                  UnitCost REAL NOT NULL
              );
              CREATE TABLE IF NOT EXISTS debit_notes (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  SupplierId INTEGER NOT NULL,
                  OriginalPurchaseInvoiceId INTEGER NOT NULL,
                  NoteNumber TEXT NULL,
                  NoteDate TEXT NOT NULL,
                  Reason TEXT NOT NULL,
                  Status TEXT NOT NULL,
                  JournalEntryId INTEGER NULL
              );
              CREATE TABLE IF NOT EXISTS debit_note_lines (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  debit_note_id INTEGER NOT NULL,
                  Description TEXT NOT NULL,
                  Quantity REAL NOT NULL,
                  UnitPrice REAL NOT NULL,
                  ProductId INTEGER NULL
              );
              """;
        try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
        catch { /* */ }
    }

    private async Task EnsureInventoryBalancesTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS inventory_balances (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "WarehouseId" BIGINT NOT NULL,
                  "ProductId" BIGINT NOT NULL,
                  "QuantityOnHand" NUMERIC(18,4) NOT NULL DEFAULT 0
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_inventory_balances_company_wh_product
                  ON inventory_balances ("CompanyId", "WarehouseId", "ProductId");
              """
            : """
              CREATE TABLE IF NOT EXISTS inventory_balances (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  WarehouseId INTEGER NOT NULL,
                  ProductId INTEGER NOT NULL,
                  QuantityOnHand REAL NOT NULL DEFAULT 0
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_inventory_balances_company_wh_product
                  ON inventory_balances (CompanyId, WarehouseId, ProductId);
              """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // الجدول قد يُنشأ عبر EnsureCreated
        }
    }

    private async Task EnsureWorkflowTablesAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS workflow_instances (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "DocumentKind" character varying(32) NOT NULL,
                  "DocumentId" BIGINT NOT NULL,
                  "SubmittedBy" character varying(128) NOT NULL,
                  "SubmittedAtUtc" TIMESTAMPTZ NOT NULL,
                  "CurrentStepIndex" INTEGER NOT NULL DEFAULT 0,
                  "Status" character varying(16) NOT NULL,
                  "RejectedBy" character varying(128) NULL,
                  "RejectionReason" character varying(1024) NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_workflow_instances_company_kind_doc
                  ON workflow_instances ("CompanyId", "DocumentKind", "DocumentId");
              CREATE TABLE IF NOT EXISTS workflow_approval_records (
                  id BIGSERIAL PRIMARY KEY,
                  workflow_instance_id BIGINT NOT NULL,
                  "Slot" character varying(16) NOT NULL,
                  "ApprovedBy" character varying(128) NOT NULL,
                  "ApprovedAtUtc" TIMESTAMPTZ NOT NULL,
                  "Comment" character varying(512) NULL
              );
              CREATE TABLE IF NOT EXISTS purchase_requests (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "BranchId" BIGINT NULL,
                  "SupplierId" BIGINT NOT NULL,
                  "RequestNumber" character varying(32) NULL,
                  "RequestDate" DATE NOT NULL,
                  "Justification" character varying(1024) NOT NULL,
                  "Status" character varying(24) NOT NULL,
                  "PurchaseOrderId" BIGINT NULL,
                  "WorkflowInstanceId" BIGINT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_purchase_requests_company_number
                  ON purchase_requests ("CompanyId", "RequestNumber");
              CREATE TABLE IF NOT EXISTS purchase_request_lines (
                  id BIGSERIAL PRIMARY KEY,
                  purchase_request_id BIGINT NOT NULL,
                  "Description" character varying(512) NOT NULL,
                  "Quantity" NUMERIC(18,4) NOT NULL,
                  "UnitPrice" NUMERIC(18,2) NOT NULL,
                  "ProductId" BIGINT NULL
              );
              """
            : """
              CREATE TABLE IF NOT EXISTS workflow_instances (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  DocumentKind TEXT NOT NULL,
                  DocumentId INTEGER NOT NULL,
                  SubmittedBy TEXT NOT NULL,
                  SubmittedAtUtc TEXT NOT NULL,
                  CurrentStepIndex INTEGER NOT NULL DEFAULT 0,
                  Status TEXT NOT NULL,
                  RejectedBy TEXT NULL,
                  RejectionReason TEXT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_workflow_instances_company_kind_doc
                  ON workflow_instances (CompanyId, DocumentKind, DocumentId);
              CREATE TABLE IF NOT EXISTS workflow_approval_records (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  workflow_instance_id INTEGER NOT NULL,
                  Slot TEXT NOT NULL,
                  ApprovedBy TEXT NOT NULL,
                  ApprovedAtUtc TEXT NOT NULL,
                  Comment TEXT NULL
              );
              CREATE TABLE IF NOT EXISTS purchase_requests (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  BranchId INTEGER NULL,
                  SupplierId INTEGER NOT NULL,
                  RequestNumber TEXT NULL,
                  RequestDate TEXT NOT NULL,
                  Justification TEXT NOT NULL,
                  Status TEXT NOT NULL,
                  PurchaseOrderId INTEGER NULL,
                  WorkflowInstanceId INTEGER NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ix_purchase_requests_company_number
                  ON purchase_requests (CompanyId, RequestNumber);
              CREATE TABLE IF NOT EXISTS purchase_request_lines (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  purchase_request_id INTEGER NOT NULL,
                  Description TEXT NOT NULL,
                  Quantity REAL NOT NULL,
                  UnitPrice REAL NOT NULL,
                  ProductId INTEGER NULL
              );
              """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // قد يُنشأ عبر EnsureCreated
        }
    }

    private async Task EnsureBankStatementTablesAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS bank_statements (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "CompanyId" BIGINT NOT NULL,
                  "BankAccountId" BIGINT NOT NULL,
                  "SourceFileName" character varying(256) NOT NULL,
                  "ImportedAtUtc" TIMESTAMPTZ NOT NULL
              );
              CREATE TABLE IF NOT EXISTS bank_statement_lines (
                  id BIGSERIAL PRIMARY KEY,
                  bank_statement_id BIGINT NOT NULL,
                  "TransactionDate" DATE NOT NULL,
                  "Description" character varying(512) NOT NULL,
                  "Reference" character varying(128) NOT NULL DEFAULT '',
                  "SignedAmount" NUMERIC(18,2) NOT NULL,
                  "MatchedJournalEntryId" BIGINT NULL
              );
              """
            : """
              CREATE TABLE IF NOT EXISTS bank_statements (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  BankAccountId INTEGER NOT NULL,
                  SourceFileName TEXT NOT NULL,
                  ImportedAtUtc TEXT NOT NULL
              );
              CREATE TABLE IF NOT EXISTS bank_statement_lines (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  bank_statement_id INTEGER NOT NULL,
                  TransactionDate TEXT NOT NULL,
                  Description TEXT NOT NULL,
                  Reference TEXT NOT NULL DEFAULT '',
                  SignedAmount REAL NOT NULL,
                  MatchedJournalEntryId INTEGER NULL
              );
              """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // قد يُنشأ عبر EnsureCreated
        }
    }

    private async Task BackfillSoftDeleteAsync(string table, bool isPg, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);
        try
        {
            var sql = isPg
                ? $"""UPDATE "{table}" SET "DeletedAtUtc" = NOW() AT TIME ZONE 'UTC' WHERE "IsActive" = false AND "DeletedAtUtc" IS NULL"""
                : $"UPDATE {table} SET DeletedAtUtc = datetime('now') WHERE IsActive = 0 AND DeletedAtUtc IS NULL";
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // الجدول قد لا يحتوي IsActive بعد في قواعد قديمة جداً
        }
    }

    private async Task EnsureDocumentTotalColumnsAsync(bool isPg, CancellationToken ct)
    {
        var tables = new[]
        {
            "sales_invoices", "purchase_invoices", "credit_notes", "debit_notes", "purchase_orders",
        };

        var subtotalType = isPg ? "NUMERIC(18,2) NOT NULL DEFAULT 0" : "REAL NOT NULL DEFAULT 0";
        var vatType = subtotalType;

        foreach (var table in tables)
        {
            if (isPg)
            {
                await EnsurePgColumnAsync(table, "Subtotal", subtotalType, ct);
                await EnsurePgColumnAsync(table, "VatAmount", vatType, ct);
            }
            else
            {
                await EnsureSqliteColumnAsync(table, "Subtotal", subtotalType, ct);
                await EnsureSqliteColumnAsync(table, "VatAmount", vatType, ct);
            }
        }

        await BackfillDocumentTotalsAsync(ct);
    }

    private async Task BackfillDocumentTotalsAsync(CancellationToken ct)
    {
        foreach (var invoice in await db.SalesInvoices.ToListAsync(ct))
            invoice.RecalculateTotals();

        foreach (var invoice in await db.PurchaseInvoices.ToListAsync(ct))
            invoice.RecalculateTotals();

        foreach (var note in await db.CreditNotes.ToListAsync(ct))
            note.RecalculateTotals();

        foreach (var note in await db.DebitNotes.ToListAsync(ct))
            note.RecalculateTotals();

        foreach (var order in await db.PurchaseOrders.ToListAsync(ct))
            order.RecalculateTotals();

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureMasterDataGovernanceColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (isPg)
        {
            await EnsurePgColumnAsync("customers", "ModifiedAtUtc", "TIMESTAMPTZ NULL", ct);
            await EnsurePgColumnAsync("customers", "ModifiedBy", "character varying(128) NULL", ct);
            await EnsurePgColumnAsync("suppliers", "ModifiedAtUtc", "TIMESTAMPTZ NULL", ct);
            await EnsurePgColumnAsync("suppliers", "ModifiedBy", "character varying(128) NULL", ct);
            await EnsurePgColumnAsync("purchase_orders", "WorkflowInstanceId", "BIGINT NULL", ct);
            await EnsurePgColumnAsync("payment_vouchers", "WorkflowInstanceId", "BIGINT NULL", ct);
            await EnsurePgColumnAsync("journal_entries", "WorkflowInstanceId", "BIGINT NULL", ct);

            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_customers_company_vat
                    ON customers ("CompanyId", "VatRegistrationNumber")
                    WHERE "VatRegistrationNumber" <> '';
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_suppliers_company_vat
                    ON suppliers ("CompanyId", "VatRegistrationNumber")
                    WHERE "VatRegistrationNumber" <> '';
                """, ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("customers", "ModifiedAtUtc", "TEXT NULL", ct);
            await EnsureSqliteColumnAsync("customers", "ModifiedBy", "TEXT NULL", ct);
            await EnsureSqliteColumnAsync("suppliers", "ModifiedAtUtc", "TEXT NULL", ct);
            await EnsureSqliteColumnAsync("suppliers", "ModifiedBy", "TEXT NULL", ct);
            await EnsureSqliteColumnAsync("purchase_orders", "WorkflowInstanceId", "INTEGER NULL", ct);
            await EnsureSqliteColumnAsync("payment_vouchers", "WorkflowInstanceId", "INTEGER NULL", ct);
            await EnsureSqliteColumnAsync("journal_entries", "WorkflowInstanceId", "INTEGER NULL", ct);

            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_customers_company_vat
                    ON customers (CompanyId, VatRegistrationNumber)
                    WHERE VatRegistrationNumber <> '';
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_suppliers_company_vat
                    ON suppliers (CompanyId, VatRegistrationNumber)
                    WHERE VatRegistrationNumber <> '';
                """, ct);
        }
    }

    private async Task EnsureV12LifecycleTablesAsync(bool isPg, CancellationToken ct)
    {
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS document_version_history (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "DocumentType" character varying(64) NOT NULL,
                    "DocumentKey" character varying(128) NOT NULL,
                    "Version" INTEGER NOT NULL,
                    "Action" character varying(32) NOT NULL,
                    "Username" character varying(64) NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_doc_version_company_type_key_ver
                    ON document_version_history ("CompanyId", "DocumentType", "DocumentKey", "Version");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS login_audit (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Username" character varying(64) NOT NULL,
                    "Success" BOOLEAN NOT NULL,
                    "FailureReason" character varying(256) NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_login_audit_company_created
                    ON login_audit ("CompanyId", "CreatedAtUtc");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS otp_challenges (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Username" character varying(64) NOT NULL,
                    "PhoneE164" character varying(16) NOT NULL,
                    "CodeHash" character varying(64) NOT NULL,
                    "Purpose" character varying(32) NOT NULL,
                    "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
                    "IsConsumed" BOOLEAN NOT NULL DEFAULT false,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_otp_challenges_lookup
                    ON otp_challenges ("CompanyId", "Username", "Purpose", "IsConsumed");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_quotations (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "QuotationNumber" character varying(32) NULL,
                    "QuotationDate" DATE NOT NULL,
                    "ValidUntil" DATE NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "Subtotal" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "VatAmount" NUMERIC(18,2) NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_sales_quotations_company_number
                    ON sales_quotations ("CompanyId", "QuotationNumber");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_orders (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "SalesQuotationId" BIGINT NULL,
                    "OrderNumber" character varying(32) NULL,
                    "OrderDate" DATE NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "SalesInvoiceId" BIGINT NULL,
                    "Subtotal" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "VatAmount" NUMERIC(18,2) NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_sales_orders_company_number
                    ON sales_orders ("CompanyId", "OrderNumber");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS document_version_history (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    DocumentType TEXT NOT NULL,
                    DocumentKey TEXT NOT NULL,
                    Version INTEGER NOT NULL,
                    Action TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_doc_version_company_type_key_ver
                    ON document_version_history (CompanyId, DocumentType, DocumentKey, Version);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS login_audit (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Username TEXT NOT NULL,
                    Success INTEGER NOT NULL,
                    FailureReason TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_login_audit_company_created
                    ON login_audit (CompanyId, CreatedAtUtc);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS otp_challenges (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Username TEXT NOT NULL,
                    PhoneE164 TEXT NOT NULL,
                    CodeHash TEXT NOT NULL,
                    Purpose TEXT NOT NULL,
                    ExpiresAtUtc TEXT NOT NULL,
                    IsConsumed INTEGER NOT NULL DEFAULT 0,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_otp_challenges_lookup
                    ON otp_challenges (CompanyId, Username, Purpose, IsConsumed);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_quotations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    CustomerId INTEGER NOT NULL,
                    QuotationNumber TEXT NULL,
                    QuotationDate TEXT NOT NULL,
                    ValidUntil TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Subtotal REAL NOT NULL DEFAULT 0,
                    VatAmount REAL NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_sales_quotations_company_number
                    ON sales_quotations (CompanyId, QuotationNumber);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    CustomerId INTEGER NOT NULL,
                    SalesQuotationId INTEGER NULL,
                    OrderNumber TEXT NULL,
                    OrderDate TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    SalesInvoiceId INTEGER NULL,
                    Subtotal REAL NOT NULL DEFAULT 0,
                    VatAmount REAL NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_sales_orders_company_number
                    ON sales_orders (CompanyId, OrderNumber);
                """, ct);
        }
    }

    private async Task EnsureV14CrmTablesAsync(bool isPg, CancellationToken ct)
    {
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_leads (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Name" character varying(256) NOT NULL,
                    "Phone" character varying(32) NOT NULL DEFAULT '',
                    "Email" character varying(256) NOT NULL DEFAULT '',
                    "Source" character varying(64) NOT NULL DEFAULT '',
                    "Status" character varying(16) NOT NULL,
                    "CustomerId" BIGINT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true,
                    "DeletedAtUtc" TIMESTAMPTZ NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_crm_leads_company_status
                    ON crm_leads ("CompanyId", "Status");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_opportunities (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NULL,
                    "LeadId" BIGINT NULL,
                    "Title" character varying(256) NOT NULL,
                    "Stage" character varying(16) NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ExpectedCloseDate" DATE NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_crm_opportunities_company_stage
                    ON crm_opportunities ("CompanyId", "Stage");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_activities (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NULL,
                    "OpportunityId" BIGINT NULL,
                    "ActivityType" character varying(16) NOT NULL,
                    "Subject" character varying(256) NOT NULL,
                    "Notes" character varying(2048) NOT NULL DEFAULT '',
                    "DueAtUtc" TIMESTAMPTZ NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_crm_activities_company_due
                    ON crm_activities ("CompanyId", "DueAtUtc");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_leads (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Phone TEXT NOT NULL DEFAULT '',
                    Email TEXT NOT NULL DEFAULT '',
                    Source TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL,
                    CustomerId INTEGER NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    DeletedAtUtc TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_crm_leads_company_status
                    ON crm_leads (CompanyId, Status);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_opportunities (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    CustomerId INTEGER NULL,
                    LeadId INTEGER NULL,
                    Title TEXT NOT NULL,
                    Stage TEXT NOT NULL,
                    Amount REAL NOT NULL DEFAULT 0,
                    ExpectedCloseDate TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_crm_opportunities_company_stage
                    ON crm_opportunities (CompanyId, Stage);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_activities (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    CustomerId INTEGER NULL,
                    OpportunityId INTEGER NULL,
                    ActivityType TEXT NOT NULL,
                    Subject TEXT NOT NULL,
                    Notes TEXT NOT NULL DEFAULT '',
                    DueAtUtc TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_crm_activities_company_due
                    ON crm_activities (CompanyId, DueAtUtc);
                """, ct);
        }
    }
}
