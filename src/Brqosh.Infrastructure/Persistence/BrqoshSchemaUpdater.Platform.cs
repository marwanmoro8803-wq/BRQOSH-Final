using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — المنصّة: الشركات والمستأجرون والفوترة والفروع. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_company_groups_Code" ON company_groups ("Code");
              """
            : """
              CREATE TABLE IF NOT EXISTS company_groups (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Code TEXT NOT NULL,
                  NameAr TEXT NOT NULL,
                  ReportingCurrencyCode TEXT NOT NULL DEFAULT 'SAR',
                  CreatedAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_company_groups_Code" ON company_groups (Code);
              """;
        await TrySchemaSqlAsync("EnsureCompanyPlatformAsync", sql, ct);

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
        await TrySchemaSqlAsync("EnsureExchangeRatesTableAsync", sql, ct);
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_intercompany_account_maps_CompanyGroupId_AccountCode"
                  ON intercompany_account_maps ("CompanyGroupId", "AccountCode");
              """
            : """
              CREATE TABLE IF NOT EXISTS intercompany_account_maps (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyGroupId INTEGER NOT NULL,
                  AccountCode TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_intercompany_account_maps_CompanyGroupId_AccountCode"
                  ON intercompany_account_maps (CompanyGroupId, AccountCode);
              """;
        await TrySchemaSqlAsync("EnsureIntercompanyAccountMapsTableAsync", sql, ct);
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_billing_plans_Code" ON billing_plans ("Code");

              CREATE TABLE IF NOT EXISTS tenant_subscriptions (
                  "Id" BIGSERIAL PRIMARY KEY,
                  "TenantId" UUID NOT NULL,
                  "BillingPlanId" BIGINT NOT NULL,
                  "Status" character varying(16) NOT NULL DEFAULT 'Trial',
                  "StartedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_tenant_subscriptions_TenantId" ON tenant_subscriptions ("TenantId");
              """
            : """
              CREATE TABLE IF NOT EXISTS billing_plans (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  Code TEXT NOT NULL,
                  NameAr TEXT NOT NULL,
                  MaxCompanies INTEGER NOT NULL DEFAULT 1,
                  IsActive INTEGER NOT NULL DEFAULT 1
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_billing_plans_Code" ON billing_plans (Code);

              CREATE TABLE IF NOT EXISTS tenant_subscriptions (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  TenantId TEXT NOT NULL,
                  BillingPlanId INTEGER NOT NULL,
                  Status TEXT NOT NULL DEFAULT 'Trial',
                  StartedAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_tenant_subscriptions_TenantId" ON tenant_subscriptions (TenantId);
              """;
        await TrySchemaSqlAsync("EnsureBillingPlatformTablesAsync", sql, ct);

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

    /// <summary>
    /// ‏<b>كنسُ قيدِ تفرّدٍ أُنشئ مرّتين.</b>
    ///
    /// <para>للمخطّط مصدران: النموذجُ يُنشئ <c>IX_…</c>، وكان المُرقِّعُ يُنشئ
    /// <c>ix_…_snake</c> على <b>الأعمدة نفسِها</b>. فقاعدةٌ مرّت بالاثنين تحمل الفهرسَ
    /// مرّتين — <b>وقِيس: ٢٠ زوجاً</b>.</para>
    ///
    /// <para>وليس عطلاً في الأرقام: القيدُ يُفرَض مرّةً واحدة. لكنّ كلَّ إدراجٍ يُصان به
    /// شجرتان متطابقتان، <b>ويُظهر المخطّطُ قيوداً لا وجودَ لها في النموذج — فيُخفي التكرارُ
    /// فهرساً حقيقياً مفقوداً بين ضجيجه</b>.</para>
    ///
    /// <para><b>وأصلُ العلاج تسميةٌ لا كنس:</b> صار المُرقِّعُ يُسمّي فهارسَه بأسماء النموذج،
    /// فـ<c>IF NOT EXISTS</c> يكفي ولا يُنشأ ثانٍ أصلاً. وهذا الكنسُ <b>لما مضى وحده</b> —
    /// للقواعد التي تحمل التكرارَ من قبل. ولولا التسمية لصار كنساً في كلّ إقلاع وإنشاءً
    /// بعده: <b>دورةُ بناءٍ وهدمٍ دائمة على جداولَ قد تكون كبيرة</b>.</para>
    ///
    /// <para><b>ولا يُسقط إلا ما يبقى قيدُه مضموناً بعده</b>، ولا يمسّ فهرسَ النموذج:
    /// الشرطُ أن يوجد فهرسٌ فريدٌ آخر على الأعمدة نفسِها واسمُه يبدأ بـ<c>IX_</c>.</para>
    /// </summary>
    private async Task DropDuplicateUniqueIndexesAsync(CancellationToken ct)
    {
        var duplicates = await db.Database.SqlQuery<string>($"""
            SELECT d.indexname AS "Value"
            FROM pg_indexes d
            WHERE d.schemaname = 'public'
              AND d.indexdef LIKE '%UNIQUE%'
              AND d.indexname NOT LIKE 'IX!_%' ESCAPE '!'
              AND EXISTS (
                    SELECT 1 FROM pg_indexes k
                    WHERE k.schemaname = 'public'
                      AND k.tablename = d.tablename
                      AND k.indexdef LIKE '%UNIQUE%'
                      AND k.indexname LIKE 'IX!_%' ESCAPE '!'
                      AND regexp_replace(k.indexdef, '.*USING btree ', '')
                          = regexp_replace(d.indexdef, '.*USING btree ', ''))
            """).ToListAsync(ct);

        foreach (var index in duplicates)
        {
            SchemaSqlGuard.ValidateTable(index);
            await db.Database.ExecuteSqlRawAsync($"DROP INDEX IF EXISTS \"{index}\"", ct);
        }
    }
}
