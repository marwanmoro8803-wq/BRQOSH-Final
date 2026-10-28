using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — ترقياتُ PostgreSQL وSQLite المجمَّعة. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
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
        await EnsurePgColumnAsync("sales_invoices", "PosCardAmount", "NUMERIC(18,2) NULL", ct);
        await EnsurePgColumnAsync("sales_invoices", "StockIssuedByDelivery", "BOOLEAN NOT NULL DEFAULT false", ct);   // ‏صُرف بإذن تسليم   // ‏حصّة البطاقة في الدفع المختلط
        await EnsurePgColumnAsync("zatca_settings", "ComplianceRequestId", "character varying(64) NOT NULL DEFAULT ''", ct);
        await EnsurePgColumnAsync("zatca_settings", "CsidSecret", "character varying(512) NOT NULL DEFAULT ''", ct);
        await EnsurePgColumnAsync("zatca_settings", "SigningMachineId", "character varying(128) NOT NULL DEFAULT ''", ct);   // ‏(tax-zatca#16)
        await EnsurePgColumnAsync("fixed_assets", "AcquisitionJournalEntryId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalJournalEntryId", "BIGINT NULL", ct);
        // بيعُ الأصل: المشتري ورقمُ فاتورته ومقابلُه وفئتُه وسببُها.
        await EnsurePgColumnAsync("fixed_assets", "DisposalBuyerCustomerId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalInvoiceNumber", "character varying(64) NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalProceeds", "NUMERIC(18,2) NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalVatCategory", "character varying(16) NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalVatExemptionCode", "character varying(64) NULL", ct);
        // آخرُ يومٍ أُهلك قبل الدفاتر — لأصلٍ قائمٍ يُدخَل برصيده الافتتاحيّ.
        await EnsurePgColumnAsync("fixed_assets", "DepreciatedThrough", "DATE NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "CancelledOn", "DATE NULL", ct);   // ‏(ح-١٥٧)
        await EnsurePgColumnAsync("fixed_assets", "AwaitingInvoice", "BOOLEAN NOT NULL DEFAULT false", ct);   // ‏(ح-١٥٨)
        await EnsurePgColumnAsync("fixed_assets", "DepreciationMethod", "character varying(24) NOT NULL DEFAULT 'StraightLine'", ct);   // ‏(treasury-payroll-assets#11)
        await EnsurePgColumnAsync("companies", "AuditorName", "character varying(120) NULL", ct);   // ‏(أسئلةُ المالك 2026-10-24)
        await EnsurePgColumnAsync("companies", "AddressEn", "character varying(512) NULL", ct);
        await EnsurePgColumnAsync("app_users", "MustChangePasswordOnNextLogin", "BOOLEAN NOT NULL DEFAULT false", ct);
        await EnsurePgColumnAsync("app_users", "MobilePhoneE164", "character varying(16) NULL", ct);
        await EnsurePgColumnAsync("companies", "CostingMethod", "character varying(32) NOT NULL DEFAULT 'WeightedAverage'", ct);
        await EnsurePgColumnAsync("companies", "FiscalYearStartMonth", "INTEGER NOT NULL DEFAULT 1", ct);
        await EnsurePgColumnAsync("products", "CategoryId", "BIGINT NULL", ct);          // ‏فئة الصنف
        await EnsurePgColumnAsync("products", "RevenueAccountId", "BIGINT NULL", ct);    // ‏حسابات الصنف الخاصّة
        await EnsurePgColumnAsync("products", "InventoryAccountId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("products", "CogsAccountId", "BIGINT NULL", ct);   // ‏السنة المالية بشهر بداية
        await EnsurePgColumnAsync("companies", "BaseCurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
        await EnsurePgColumnAsync("companies", "FunctionalCurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
        // ‏(accounting-core#6) ‏حدُّ اعتماد القيد اليدويّ — والقائمةُ تأخذ افتراضَ المنتج.
        await EnsurePgColumnAsync("companies", "ManualJournalApprovalThreshold", "NUMERIC(18,2) NOT NULL DEFAULT 10000", ct);
        // ‏(tax-zatca#12) ‏معاملةُ ضريبة فاتورة الشراء — والقائمةُ محلّيّةٌ بلا احتسابٍ عكسيٍّ ولا قيمةٍ جمركيّة.
        await EnsurePgColumnAsync("purchase_invoices", "VatTreatment", "character varying(24) NOT NULL DEFAULT 'Domestic'", ct);
        await EnsurePgColumnAsync("purchase_invoices", "ReverseChargeVatAmount", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
        await EnsurePgColumnAsync("purchase_invoices", "ImportCustomsValue", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
        await EnsurePgColumnAsync("companies", "NameEn", "character varying(256) NULL", ct);
        await EnsurePgColumnAsync("companies", "AddressAr", "character varying(512) NULL", ct);
        await EnsurePgColumnAsync("companies", "LogoFilePath", "character varying(512) NULL", ct);
        await EnsurePgColumnAsync("companies", "PrintFontFamily", "character varying(64) NOT NULL DEFAULT 'Tahoma'", ct);
        await EnsurePgColumnAsync("bank_accounts", "CurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);

        // ‏ربطُ فاتورة أجرة الشحن — **ولاغٍ افتراضاً**، فشحناتٌ سابقةٌ
        // ‏لم تُفوتَر تبقى غيرَ مفوترة، ولا يُكذَب على الماضي بربطٍ لم يقع.
        await EnsurePgColumnAsync("shipments", "FreightInvoiceId", "BIGINT NULL", ct);

        // ‏ربطُ العقار بأصلٍ ثابتٍ يُهلَك — **ولاغٍ افتراضاً**، فعقاراتٌ
        // ‏قائمةٌ تبقى بلا قيمةٍ دفتريّة حتى يربطها المحاسبُ بعلمه.
        await EnsurePgColumnAsync("properties", "FixedAssetId", "BIGINT NULL", ct);
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
        await ApplyPaymentAllocationReversalColumnsAsync(isPg: true, ct);
        await EnsureDocumentTotalColumnsAsync(isPg: true, ct);

        // ‏آخرَ شيء: يُكنَس ما أنشأه المُرقِّعُ سابقاً بأسماءٍ
        // ‏تخالف النموذج، بعد أن صار يُسمّي بأسمائه فلا يعود يُنشئ تكراراً.
        await DropDuplicateUniqueIndexesAsync(ct);
        await EnsurePgColumnAsync("audit_log", "Reason", "character varying(512) NULL", ct);
        await EnsureV12LifecycleTablesAsync(isPg: true, ct);

        // ‏النداءُ الثاني — على ما أعادت المرحلةُ السابقة بناءَه.
        await EnsureBatchTrackingAsync(isPg: true, ct);
        await EnsureV14CrmTablesAsync(isPg: true, ct);

        // فهارس اختيارية — PascalCase
        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_bank_accounts_CompanyId_Code"
                ON bank_accounts ("CompanyId", "Code");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_construction_projects_CompanyId_Code"
                ON construction_projects ("CompanyId", "Code");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_construction_contracts_CompanyId_ContractNumber"
                ON construction_contracts ("CompanyId", "ContractNumber");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_subcontract_agreements_CompanyId_AgreementNumber"
                ON subcontract_agreements ("CompanyId", "AgreementNumber");
            """, ct);

        // الفهرس الفريد على عدّاد ZATCA — للقواعد القائمة كذلك.
        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_zatca_einvoice_documents_CompanyId_Icv"
                ON zatca_einvoice_documents ("CompanyId", "Icv");
            """, ct);

        await SafePgIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_app_users_CompanyId_Username"
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
        await EnsureSqliteColumnAsync("sales_invoices", "PosCardAmount", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("sales_invoices", "StockIssuedByDelivery", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureSqliteColumnAsync("zatca_settings", "ComplianceRequestId", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("zatca_settings", "CsidSecret", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("zatca_settings", "SigningMachineId", "TEXT NOT NULL DEFAULT ''", ct);   // ‏(tax-zatca#16)
        await EnsureSqliteColumnAsync("fixed_assets", "AcquisitionJournalEntryId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalJournalEntryId", "INTEGER NULL", ct);
        // بيعُ الأصل — انظر النظيرَ في مسار PostgreSQL أعلاه.
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalBuyerCustomerId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalInvoiceNumber", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalProceeds", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalVatCategory", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalVatExemptionCode", "TEXT NULL", ct);
        // آخرُ يومٍ أُهلك قبل الدفاتر — انظر النظيرَ في مسار PostgreSQL أعلاه.
        await EnsureSqliteColumnAsync("fixed_assets", "DepreciatedThrough", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "CancelledOn", "TEXT NULL", ct);   // ‏(ح-١٥٧)
        await EnsureSqliteColumnAsync("fixed_assets", "AwaitingInvoice", "INTEGER NOT NULL DEFAULT 0", ct);   // ‏(ح-١٥٨)
        await EnsureSqliteColumnAsync("fixed_assets", "DepreciationMethod", "TEXT NOT NULL DEFAULT 'StraightLine'", ct);   // ‏(treasury-payroll-assets#11)
        await EnsureSqliteColumnAsync("companies", "AuditorName", "TEXT NULL", ct);   // ‏(أسئلةُ المالك 2026-10-24)
        await EnsureSqliteColumnAsync("companies", "AddressEn", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("app_users", "MustChangePasswordOnNextLogin", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("app_users", "MobilePhoneE164", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "CostingMethod", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "FiscalYearStartMonth", "INTEGER NOT NULL DEFAULT 1", ct);
        await EnsureSqliteColumnAsync("products", "CategoryId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("products", "RevenueAccountId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("products", "InventoryAccountId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("products", "CogsAccountId", "INTEGER NULL", ct);
        // ‏(مساران متوازيان) ‏عمودان كانا يُضمَنان في PostgreSQL وحده — فقاعدةُ SQLite قديمةٌ تفقدهما.
        // ‏والجدولان من وحدتين قد لا تُنشآن في SQLite، فيُضاف العمودُ حيث وُجد الجدولُ فقط.
        await EnsureSqliteColumnIfTableAsync("shipments", "FreightInvoiceId", "INTEGER NULL", ct);
        await EnsureSqliteColumnIfTableAsync("properties", "FixedAssetId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("companies", "BaseCurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
        await EnsureSqliteColumnAsync("companies", "FunctionalCurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
        await EnsureSqliteColumnAsync("companies", "ManualJournalApprovalThreshold", "TEXT NOT NULL DEFAULT '10000'", ct);   // ‏(accounting-core#6)
        await EnsureSqliteColumnAsync("purchase_invoices", "VatTreatment", "TEXT NOT NULL DEFAULT 'Domestic'", ct);   // ‏(tax-zatca#12)
        await EnsureSqliteColumnAsync("purchase_invoices", "ReverseChargeVatAmount", "TEXT NOT NULL DEFAULT '0'", ct);
        await EnsureSqliteColumnAsync("purchase_invoices", "ImportCustomsValue", "TEXT NOT NULL DEFAULT '0'", ct);
        await EnsureSqliteColumnAsync("companies", "NameEn", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "AddressAr", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "LogoFilePath", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("companies", "PrintFontFamily", "TEXT NOT NULL DEFAULT 'Tahoma'", ct);
        await EnsureSqliteColumnAsync("bank_accounts", "CurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
        await EnsureSqliteColumnAsync("construction_projects", "EstimatedTotalCost", "REAL NULL", ct);
        // ‏**وكلُّ تعبئةٍ خطوةٌ باسمها** — ودفعةٌ في `try` واحدٍ تُسقط ما بعد أوّل فشلٍ
        // ‏فيها صامتاً، **فتبقى أعمدةٌ بلا قيمةٍ افتراضية ولا يُعرف أيُّها**.
        await TrySchemaSqlAsync("Backfill.MustChangePassword",
            "UPDATE app_users SET MustChangePasswordOnNextLogin = 0 WHERE MustChangePasswordOnNextLogin IS NULL", ct);
        await TrySchemaSqlAsync("Backfill.CostingMethod",
            "UPDATE companies SET CostingMethod = 'WeightedAverage' WHERE CostingMethod IS NULL", ct);
        await TrySchemaSqlAsync("Backfill.BaseCurrency",
            "UPDATE companies SET BaseCurrencyCode = 'SAR' WHERE BaseCurrencyCode IS NULL OR BaseCurrencyCode = ''", ct);
        await TrySchemaSqlAsync("Backfill.FunctionalCurrency",
            "UPDATE companies SET FunctionalCurrencyCode = 'SAR' WHERE FunctionalCurrencyCode IS NULL OR FunctionalCurrencyCode = ''", ct);
        await TrySchemaSqlAsync("Backfill.EstimatedTotalCost",
            "UPDATE construction_projects SET EstimatedTotalCost = 0 WHERE EstimatedTotalCost IS NULL", ct);
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
        await ApplyPaymentAllocationReversalColumnsAsync(isPg: false, ct);
        await EnsureDocumentTotalColumnsAsync(isPg: false, ct);
        await EnsureSqliteColumnAsync("audit_log", "Reason", "TEXT NULL", ct);
        await EnsureV12LifecycleTablesAsync(isPg: false, ct);

        // ‏النداءُ الثاني — على ما أعادت المرحلةُ السابقة بناءَه.
        await EnsureBatchTrackingAsync(isPg: false, ct);
        await EnsureV14CrmTablesAsync(isPg: false, ct);

        await SafeSqliteIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_bank_accounts_CompanyId_Code"
                ON bank_accounts (CompanyId, Code);
            """, ct);

        await SafeSqliteIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_construction_projects_CompanyId_Code"
                ON construction_projects (CompanyId, Code);
            """, ct);

        await SafeSqliteIndexAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_app_users_CompanyId_Username"
                ON app_users (CompanyId, Username);
            """, ct);
    }
}
