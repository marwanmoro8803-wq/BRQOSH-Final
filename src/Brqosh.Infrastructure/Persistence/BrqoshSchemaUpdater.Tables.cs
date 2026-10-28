using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — جداولُ FIFO والأحداث واللقطات والمستندات والإجماليّات. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
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
        await TrySchemaSqlAsync("EnsureFifoAndAllocationTablesAsync", sql, ct);

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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_domain_events_EventId" ON domain_events ("EventId");
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_domain_events_EventId" ON domain_events (EventId);
              CREATE INDEX IF NOT EXISTS ix_domain_events_company_occurred ON domain_events (CompanyId, OccurredAtUtc);
              """;
        await TrySchemaSqlAsync("EnsureDomainEventsTableAsync", sql, ct);

        // أعمدة طابور Outbox على القواعد القائمة .
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
        await TrySchemaSqlAsync("Backfill.DomainEventsDispatched",
            isPg
                ? """UPDATE domain_events SET "DispatchedAtUtc" = "OccurredAtUtc" WHERE "DispatchedAtUtc" IS NULL"""
                : "UPDATE domain_events SET DispatchedAtUtc = OccurredAtUtc WHERE DispatchedAtUtc IS NULL",
            ct);
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
        await TrySchemaSqlAsync("EnsureReportSnapshotsTableAsync", sql, ct);
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_branches_CompanyId_Code" ON branches ("CompanyId", "Code");
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_branches_CompanyId_Code" ON branches (CompanyId, Code);
              """;
        await TrySchemaSqlAsync("EnsureBranchesTableAsync", sql, ct);
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
                  "goods_receipt_id" BIGINT NOT NULL REFERENCES goods_receipts("Id") ON DELETE CASCADE,
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
                  "debit_note_id" BIGINT NOT NULL REFERENCES debit_notes("Id") ON DELETE CASCADE,
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
        await TrySchemaSqlAsync("EnsureGoodsReceiptAndDebitNoteTablesAsync", sql, ct);
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_inventory_balances_CompanyId_WarehouseId_ProductId"
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_inventory_balances_CompanyId_WarehouseId_ProductId"
                  ON inventory_balances (CompanyId, WarehouseId, ProductId);
              """;

        await TrySchemaSqlAsync("EnsureInventoryBalancesTableAsync", sql, ct);
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_workflow_instances_CompanyId_DocumentKind_DocumentId"
                  ON workflow_instances ("CompanyId", "DocumentKind", "DocumentId");
              CREATE TABLE IF NOT EXISTS workflow_approval_records (
                  id BIGSERIAL PRIMARY KEY,
                  workflow_instance_id BIGINT NOT NULL REFERENCES workflow_instances("Id") ON DELETE CASCADE,
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_requests_CompanyId_RequestNumber"
                  ON purchase_requests ("CompanyId", "RequestNumber");
              CREATE TABLE IF NOT EXISTS purchase_request_lines (
                  id BIGSERIAL PRIMARY KEY,
                  purchase_request_id BIGINT NOT NULL REFERENCES purchase_requests("Id") ON DELETE CASCADE,
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_workflow_instances_CompanyId_DocumentKind_DocumentId"
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
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_requests_CompanyId_RequestNumber"
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

        await TrySchemaSqlAsync("EnsureWorkflowTablesAsync", sql, ct);
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
                  -- (CEEF-229 · م2-١٠) **المفاتيحُ الأجنبية تُكتب هنا أيضاً.**
                  -- ‏`InitialCreate` تُنشئ سبعةَ جداولٍ بمفاتيحها، وهذا الفرعُ يُنشئها بـSQL
                  -- ‏يدويّ بلا مفتاحٍ واحد — **وما لا يُكتب هنا لا يوجد أبداً**، ولا هجرةَ تصل
                  -- ‏هذه القاعدة. فقاعدتان بالاسم نفسه: واحدةٌ يحرسها المحرّك من سطرٍ يتيم
                  -- ‏وأخرى لا، والفرقُ لا يظهر حتى يُحذف مستندٌ فتبقى سطورُه معلّقةً بلا أب.
                  --
                  -- ‏والنسخةُ البوستجرسية وحدها: SQLite مزوّدُ تطويرٍ تُبنى قواعدُه من النموذج،
                  -- ‏وإنفاذُ المفاتيح فيه مُطفأٌ افتراضياً. وهو حدٌّ مُصرَّحٌ به لا سهو.
                  bank_statement_id BIGINT NOT NULL REFERENCES bank_statements("Id") ON DELETE CASCADE,
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

        await TrySchemaSqlAsync("EnsureBankStatementTablesAsync", sql, ct);
    }

    private async Task BackfillSoftDeleteAsync(string table, bool isPg, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);

        var sql = isPg
            ? $"""UPDATE "{table}" SET "DeletedAtUtc" = NOW() AT TIME ZONE 'UTC' WHERE "IsActive" = false AND "DeletedAtUtc" IS NULL"""
            : $"UPDATE {table} SET DeletedAtUtc = datetime('now') WHERE IsActive = 0 AND DeletedAtUtc IS NULL";

        // ‏**واسمُ الجدول جزءٌ من اسم الخطوة** — فالدالّةُ تُستدعى لكلّ جدولٍ يحمل
        // ‏الحذفَ الناعم، **واسمٌ واحدٌ لا يقول أيُّها فشل**.
        await TrySchemaSqlAsync($"BackfillSoftDelete[{table}]", sql, ct);
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

    /// <summary>
    /// ملء إجماليات المستندات التي أُنشئت قبل إضافة عمودَي Subtotal/VatAmount.
    ///
    /// كان يحمّل **كل** الفواتير والإشعارات والأوامر بسطورها المملوكة إلى
    /// الذاكرة في كل إقلاع ويجري تتبّع تغييرات عليها — يتناقض مع هدف «إقلاع أقل من
    /// 3 ثوانٍ» ويتحوّل إلى دقائق مع نمو البيانات. الآن يُرشَّح على ما يحتاج المعالجة
    /// فعلاً (إجمالي صفر مع وجود سطور)، فالحالة الشائعة استعلام يعود فارغاً.
    /// </summary>
    /// <summary>
    /// إعادةُ حساب إجماليات المستندات التي لم تُحفظ إجمالياتُها قطّ —
    /// <b>داخل معاملةٍ واحدة، وبأثرِ تدقيقٍ يُسمّي كلّ مستندٍ لُمس وما تغيّر فيه.</b>
    ///
    /// <para><b>ما كان:</b> المسارُ يُعيد كتابةَ الإجماليات لكلّ مستندٍ إجمالُه صفرٌ وله أسطر —
    /// <b>بلا حارسِ حالةٍ ولا معاملةٍ ولا أثر</b>. فمستندٌ <b>مرحَّل</b> تُعاد كتابةُ إجمالياته
    /// عند الإقلاع بينما يبقى قيدُه كما رُحّل، فيقول المستندُ شيئاً ويقول الأستاذُ غيرَه —
    /// <b>ولا سطرَ واحدٌ يدلّ على أنّ الإقلاع غيّره</b>.</para>
    ///
    /// <para><b>والقرار: لا نمنع الإصلاح — نمنع صمتَه.</b> المستنداتُ الصفريّةُ خطأٌ يقيناً
    /// (وعاءٌ صفرٌ مع أسطرَ موجودة)، وتركُها يُبقي التقاريرَ كاذبة. لكنّ تعديلاً على مستندٍ
    /// مرحَّلٍ لا يُقبل في دفترٍ محاسبيّ ما لم يُرَ. فصار كلُّ لمسةٍ سطرَ تدقيقٍ يحمل الرقمَ
    /// القديم والجديد، والمالكُ يرى ما فعله الإقلاعُ بدفاتره.</para>
    ///
    /// <para><b>والمعاملة:</b> خمسةُ أنواعٍ من المستندات تُحفظ مع أسطر تدقيقها معاً أو لا
    /// تُحفظ. وسلسلةُ التدقيق مجزّأةٌ مربوطة، فقفلُها الاستشاريّ في <c>AuditService</c>
    /// من نوع <c>xact</c> — <b>ميّتٌ خارج معاملة</b> (درس Z-4). فالمعاملةُ هنا تُحيِيه.</para>
    ///
    /// <para><b>ولا يُلمس السليم:</b> مرشّحُ <c>Subtotal == 0</c> يمنع إعادةَ الحساب على كلّ
    /// إقلاع، والمقارنةُ بعد الحساب تمنع سطرَ تدقيقٍ لمستندٍ لم يتغيّر فيه شيء — وإلا امتلأ
    /// السجلُّ ضجيجاً وضاع فيه ما يهمّ.</para>
    /// </summary>
    private async Task BackfillDocumentTotalsAsync(CancellationToken ct)
    {
        var repairs = new List<TotalsRepair>();

        await CollectRepairsAsync(db.SalesInvoices.Where(i => i.Subtotal == 0 && i.Lines.Any()),
            "SalesInvoice", d => d.Id, d => d.CompanyId, d => d.InvoiceNumber,
            d => d.Subtotal, d => d.VatAmount, d => d.RecalculateTotals(),
            d => d.JournalEntryId, d => d.GrandTotal(), repairs, ct);

        await CollectRepairsAsync(db.PurchaseInvoices.Where(i => i.Subtotal == 0 && i.Lines.Any()),
            "PurchaseInvoice", d => d.Id, d => d.CompanyId, d => d.InvoiceNumber,
            d => d.Subtotal, d => d.VatAmount, d => d.RecalculateTotals(),
            d => d.JournalEntryId, d => d.GrandTotal(), repairs, ct);

        await CollectRepairsAsync(db.CreditNotes.Where(n => n.Subtotal == 0 && n.Lines.Any()),
            "CreditNote", d => d.Id, d => d.CompanyId, d => d.NoteNumber,
            d => d.Subtotal, d => d.VatAmount, d => d.RecalculateTotals(),
            d => d.JournalEntryId, d => d.GrandTotal(), repairs, ct);

        await CollectRepairsAsync(db.DebitNotes.Where(n => n.Subtotal == 0 && n.Lines.Any()),
            "DebitNote", d => d.Id, d => d.CompanyId, d => d.NoteNumber,
            d => d.Subtotal, d => d.VatAmount, d => d.RecalculateTotals(),
            d => d.JournalEntryId, d => d.GrandTotal(), repairs, ct);

        await CollectRepairsAsync(db.PurchaseOrders.Where(o => o.Subtotal == 0 && o.Lines.Any()),
            "PurchaseOrder", d => d.Id, d => d.CompanyId, d => d.OrderNumber,
            d => d.Subtotal, d => d.VatAmount, d => d.RecalculateTotals(),
            _ => null, d => d.GrandTotal(), repairs, ct);

        if (repairs.Count == 0)
            return;

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await db.SaveChangesAsync(token);

            var audit = new AuditService(db);
            foreach (var repair in repairs)
                await audit.LogAsync(
                    repair.CompanyId,
                    BootstrapActor,
                    "تصحيح إجماليات مستند عند الإقلاع",
                    repair.EntityType,
                    repair.Id.ToString(CultureInfo.InvariantCulture),
                    repair.Describe(await LedgerDisagreementAsync(
                        repair.JournalEntryId, repair.GrandTotal, token)),
                    "إجمالياتٌ صفريّةٌ في القاعدة أُعيد حسابُها من أسطر المستند.",
                    token);
        }, ct);
    }

    /// <summary>اسمُ الفاعل في سطور التدقيق التي يكتبها الإقلاع — فلا مستخدمَ مسجَّلاً بعد.</summary>
    private const string BootstrapActor = "النظام (ترقية القاعدة)";

    /// <summary>
    /// ‏<b>يقارن ما أصلحه الإقلاعُ بما يقوله القيدُ المرحَّل.</b>
    ///
    /// <para>الإصلاحُ صار مُوثَّقاً في م2-١١، <b>وبقي أنّه لا ينظر إلى القيد</b>. فمستندٌ
    /// تغيّر أحدُ سطوره بعد الترحيل (بترقيعٍ أو هجرةٍ أو يدٍ في القاعدة) يُعاد حسابُه فيُنتج
    /// رقماً <b>يخالف الأستاذ</b> — والأثرُ يقول «صُحّح» ولا يقول «وخالف».</para>
    ///
    /// <para><b>وإصلاحٌ يُنتج خلافاً ولا يُعلنه أسوأُ من ترك الصفر</b>: الصفرُ ظاهرٌ للعين
    /// في أيّ تقرير، والخلافُ لا — فالمستندُ سليمُ الشكل والقيدُ متوازن، <b>ولا تكشفهما
    /// هويّةٌ قائمة</b> لأنّ هـ-١ وهـ-٢ تقيسان الأستاذَ بالاستعلام ولا تمرّان بإجماليات
    /// المستند المخزَّنة.</para>
    ///
    /// <para><b>والمقارنةُ بسطر الطرف المقابل</b> — الذمم المدينة أو الدائنة — فهو وحده
    /// يحمل الإجماليَّ شاملَ الضريبة. وبقيّةُ السطور (إيراد · ضريبة · تكلفة مبيعات) أجزاءٌ
    /// منه أو مستقلّةٌ عنه.</para>
    ///
    /// <para>وتُعيد <c>null</c> حين لا سبيلَ إلى المقارنة: لا قيدَ مرتبطٌ، أو لا سطرَ طرفٍ
    /// مقابل. <b>ولا تُعلن اتّفاقاً لم تقسه.</b></para>
    /// </summary>
    private async Task<string?> LedgerDisagreementAsync(
        long? journalEntryId, decimal documentTotal, CancellationToken ct)
    {
        if (journalEntryId is not long entryId)
            return null;

        // ‏سطورُ القيد مملوكةٌ له، وتحمل رمزَ الحساب في نفسها — فلا حاجةَ إلى ربط.
        var entry = await db.JournalEntries.AsNoTracking()
            .Where(e => e.Id == entryId)
            .Select(e => e.Lines)
            .SingleOrDefaultAsync(ct);

        var counterparty = entry?.FirstOrDefault(l =>
            l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable
            || l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable);

        var ledgerTotal = Math.Abs(counterparty.Debit - counterparty.Credit);
        if (Math.Abs(ledgerTotal - documentTotal) <= 0.01m)
            return null;

        return string.Format(CultureInfo.InvariantCulture,
            " — ⚠️ ويخالف القيدَ المرحَّل: الأستاذ {0:N2} والمستند {1:N2}",
            ledgerTotal, documentTotal);
    }

    /// <summary>لمسةٌ واحدة على مستند: مَن هو، وما كان، وما صار.</summary>
    private readonly record struct TotalsRepair(
        long CompanyId, string EntityType, long Id, string? Number,
        decimal OldSubtotal, decimal OldVat, decimal NewSubtotal, decimal NewVat,
        long? JournalEntryId, decimal GrandTotal)
    {
        /// <summary>
        /// بصيغةٍ ثابتةٍ لا تتبع ثقافةَ الجهاز — فسطرُ التدقيق يُقرأ بعد سنواتٍ وعلى
        /// أجهزةٍ أخرى، ولا يجوز أن تتبدّل أرقامُه بتبدّل الإعدادات.
        /// </summary>
        public string Describe(string? disagreement) => string.Format(CultureInfo.InvariantCulture,
            "«{0}»: الوعاء {1:N2} ← {2:N2}، والضريبة {3:N2} ← {4:N2}{5}",
            Number ?? "بلا رقم", OldSubtotal, NewSubtotal, OldVat, NewVat, disagreement ?? "");
    }

    /// <summary>
    /// يُعيد الحسابَ ويُسجّل ما تغيّر فعلاً. <b>الحفظُ ليس هنا</b> — يجري مرّةً واحدة
    /// داخل المعاملة مع أسطر التدقيق.
    /// </summary>
    private static async Task CollectRepairsAsync<T>(
        IQueryable<T> candidates, string entityType,
        Func<T, long> id, Func<T, long> companyId, Func<T, string?> number,
        Func<T, decimal> subtotal, Func<T, decimal> vat, Action<T> recalculate,
        Func<T, long?> journalEntryId, Func<T, decimal> grandTotal,
        List<TotalsRepair> repairs, CancellationToken ct) where T : class
    {
        foreach (var document in await candidates.ToListAsync(ct))
        {
            var oldSubtotal = subtotal(document);
            var oldVat = vat(document);

            recalculate(document);

            var newSubtotal = subtotal(document);
            var newVat = vat(document);
            if (newSubtotal == oldSubtotal && newVat == oldVat)
                continue;

            repairs.Add(new TotalsRepair(companyId(document), entityType, id(document),
                number(document), oldSubtotal, oldVat, newSubtotal, newVat,
                journalEntryId(document), grandTotal(document)));
        }
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_customers_CompanyId_VatRegistrationNumber"
                    ON customers ("CompanyId", "VatRegistrationNumber")
                    WHERE "VatRegistrationNumber" <> '';
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_suppliers_CompanyId_VatRegistrationNumber"
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
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_customers_CompanyId_VatRegistrationNumber"
                    ON customers (CompanyId, VatRegistrationNumber)
                    WHERE VatRegistrationNumber <> '';
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_suppliers_CompanyId_VatRegistrationNumber"
                    ON suppliers (CompanyId, VatRegistrationNumber)
                    WHERE VatRegistrationNumber <> '';
                """, ct);
        }
    }
}
