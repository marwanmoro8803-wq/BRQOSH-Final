using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — جداولُ دورة الحياة V12. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
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
                -- (CEEF-229) ‏الاسمُ منتهٍ بـ~ عمداً: حدُّ PostgreSQL ٦٣ بايتاً، وEF يبتر
                -- ‏بهذه العلامة. فاسمٌ مبتورٌ بغيرها يُنتج فهرساً ثانياً على الأعمدة نفسِها.
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_document_version_history_CompanyId_DocumentType_DocumentKey~"
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_sales_quotations_CompanyId_QuotationNumber"
                    ON sales_quotations ("CompanyId", "QuotationNumber");
                """, ct);

            // سطورُ عرض السعر — كان الأبُ يُنشأ وحده والابنُ منسيّاً.
            // ‏(م-٥ · ن-٥١) وVatCategory هنا **نصٌّ** كإخوته الثمانية. وكان INTEGER، وتعليقٌ
            // ‏في موضعه هذا يُسوّغ التفاوتَ بأنّ «المُرقِّعَ يتبع النموذجَ كما هو لا كما ينبغي
            // ‏أن يكون». **والمبدأُ صحيحٌ وقد بقي**: المُرقِّعُ ما زال يتبع النموذج — لكنّ
            // ‏النموذجَ نفسَه صار يُعلن `HasConversion<string>()` على هذا الجدول، فتبِعه هذا
            // ‏السطر. **فالتصحيحُ كان في النموذج، لا في المُرقِّع** — وهذا فرعُ الإنشاء الوحيد
            // ‏لقاعدةٍ قديمة (`coreTableExists && appliedCount == 0`)، فلا هجرةَ تصلها لتصلحه.
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_quotation_lines (
                    "id" BIGSERIAL PRIMARY KEY,
                    -- (CEEF-229 · م2-١٠) **المفتاحُ الأجنبيّ يُكتب هنا أيضاً.**
                    -- ‏`InitialCreate` تُنشئ الجدولَ بمفتاحه، وهذا الفرعُ يُنشئه بـSQL يدويّ —
                    -- ‏فما لا يُكتب هنا لا يوجد أبداً، **ولا هجرةَ تصل هذه القاعدة لتُصلحه**.
                    -- ‏والفرقُ لا يظهر حتى يُحذف مستندٌ فتبقى سطورُه معلّقةً بلا أب.
                    "sales_quotation_id" BIGINT NOT NULL REFERENCES sales_quotations("Id") ON DELETE CASCADE,
                    "Description" character varying(512) NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL,
                    "UnitPrice" NUMERIC(18,2) NOT NULL,
                    "ProductId" BIGINT NULL,
                    "VatCategory" character varying(16) NOT NULL DEFAULT 'Standard',
                    -- (CEEF-229 · م1-١ب/الشقّ الثاني) سببُ الإعفاء يُنشأ مع الجدول.
                    -- ‏والفرعُ القديم لا هجرةَ تُنقذه: ما لا يُكتب هنا لا يوجد أبداً.
                    "TaxExemptionReasonCode" text NULL,
                    "TaxExemptionReason" text NULL
                );
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_sales_orders_CompanyId_OrderNumber"
                    ON sales_orders ("CompanyId", "OrderNumber");
                """, ct);

            // سطورُ أمر البيع — الابنُ المنسيّ الثاني. وVatCategory نصٌّ هنا
            // ‏لأنّ النموذج يُعلن HasConversion<string>() عليه .
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_order_lines (
                    "id" BIGSERIAL PRIMARY KEY,
                    -- (CEEF-229 · م2-١٠) **المفتاحُ الأجنبيّ يُكتب هنا أيضاً.**
                    -- ‏`InitialCreate` تُنشئ الجدولَ بمفتاحه، وهذا الفرعُ يُنشئه بـSQL يدويّ —
                    -- ‏فما لا يُكتب هنا لا يوجد أبداً، **ولا هجرةَ تصل هذه القاعدة لتُصلحه**.
                    -- ‏والفرقُ لا يظهر حتى يُحذف مستندٌ فتبقى سطورُه معلّقةً بلا أب.
                    "sales_order_id" BIGINT NOT NULL REFERENCES sales_orders("Id") ON DELETE CASCADE,
                    "Description" character varying(512) NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL,
                    "UnitPrice" NUMERIC(18,2) NOT NULL,
                    "ProductId" BIGINT NULL,
                    "VatCategory" character varying(16) NOT NULL DEFAULT 'Standard',
                    "TaxExemptionReasonCode" text NULL,
                    "TaxExemptionReason" text NULL
                );
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_document_version_history_CompanyId_DocumentType_DocumentKey~"
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_sales_quotations_CompanyId_QuotationNumber"
                    ON sales_quotations (CompanyId, QuotationNumber);
                """, ct);

            // الابنُ المنسيّ — انظر تعليق فرع PostgreSQL.
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_quotation_lines (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sales_quotation_id INTEGER NOT NULL,
                    Description TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    UnitPrice REAL NOT NULL,
                    ProductId INTEGER NULL,
                    VatCategory TEXT NOT NULL DEFAULT 'Standard',
                    TaxExemptionReasonCode TEXT NULL,
                    TaxExemptionReason TEXT NULL
                );
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_sales_orders_CompanyId_OrderNumber"
                    ON sales_orders (CompanyId, OrderNumber);
                """, ct);

            // الابنُ المنسيّ — انظر تعليق فرع PostgreSQL.
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_order_lines (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sales_order_id INTEGER NOT NULL,
                    Description TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    UnitPrice REAL NOT NULL,
                    ProductId INTEGER NULL,
                    VatCategory TEXT NOT NULL DEFAULT 'Standard',
                    TaxExemptionReasonCode TEXT NULL,
                    TaxExemptionReason TEXT NULL
                );
                """, ct);
        }
    }
}
