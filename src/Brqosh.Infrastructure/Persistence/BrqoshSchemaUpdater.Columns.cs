using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — أعمدةٌ أُضيفت للجداول القائمة. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
    /// <summary>
    /// (م-٤ · ن-٢٨) <b>عدّادُ المحاولات الخاطئة — والمخطّطُ من مصدرين فيُضاف في كليهما.</b>
    ///
    /// <para>القاعدةُ الجديدةُ تأخذه من النموذج، <b>والقائمةُ لا تأخذه إلّا من هنا</b> —
    /// وهذا الملفُّ هو ما يجري على قواعد العملاء بعد <c>MigrateAsync</c>. فعمودٌ يُضاف
    /// إلى النموذج وحدَه <b>يُسقط أوّلَ استعلامٍ على تنصيبٍ قائم</b>، لأنّ EF يبني
    /// <c>SELECT</c> بكلّ أعمدة النموذج.</para>
    /// </summary>
    private async Task EnsureOtpAttemptColumnAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("otp_challenges", isPg, ct))
            return;

        if (isPg)
            await EnsurePgColumnAsync("otp_challenges", "FailedAttempts", "INTEGER NOT NULL DEFAULT 0", ct);
        else
            await EnsureSqliteColumnAsync("otp_challenges", "FailedAttempts", "INTEGER NOT NULL DEFAULT 0", ct);
    }

    private async Task EnsureCustomerCreditLimitColumnAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("customers", isPg, ct))
            return;

        if (isPg)
            await EnsurePgColumnAsync("customers", "CreditLimit", "NUMERIC(18,2) NULL", ct);
        else
            await EnsureSqliteColumnAsync("customers", "CreditLimit", "TEXT NULL", ct);
    }

    private async Task EnsureWorkOrderWarehouseColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("work_orders", isPg, ct))
            return;

        foreach (var column in new[] { "MaterialsWarehouseId", "OutputWarehouseId" })
        {
            if (isPg)
                await EnsurePgColumnAsync("work_orders", column, "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync("work_orders", column, "INTEGER NULL", ct);
        }

        // ‏(ح-١٤٧) ‏الصرفُ الفعليّ والتالف — بافتراضاتٍ صفريّة هي سلوكُ الأمر القائم.
        if (isPg)
        {
            await EnsurePgColumnAsync("work_orders", "IssuedMaterialCost", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("work_orders", "IssueCount", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("work_orders", "ScrapQuantity", "NUMERIC(18,4) NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("work_orders", "CompletionCount", "INTEGER NOT NULL DEFAULT 0", ct);   // ‏(ح-١٤٨)
            await EnsurePgColumnAsync("work_orders", "MaterialCostRelieved", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("work_orders", "IssuedMaterialCost", "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnAsync("work_orders", "IssueCount", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsureSqliteColumnAsync("work_orders", "ScrapQuantity", "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnAsync("work_orders", "CompletionCount", "INTEGER NOT NULL DEFAULT 0", ct);   // ‏(ح-١٤٨)
            await EnsureSqliteColumnAsync("work_orders", "MaterialCostRelieved", "TEXT NOT NULL DEFAULT '0'", ct);
        }
    }

    /// <summary>(ح-١٥٤) رصيدا الكشف البنكيّ — فارغان للكشوف القائمة.</summary>
    private async Task EnsureBankStatementBalanceColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("bank_statements", isPg, ct))
            return;

        foreach (var column in new[] { "OpeningBalance", "ClosingBalance" })
        {
            if (isPg)
                await EnsurePgColumnAsync("bank_statements", column, "NUMERIC(18,2) NULL", ct);
            else
                await EnsureSqliteColumnAsync("bank_statements", column, "TEXT NULL", ct);
        }
    }

    /// <summary>
    /// (ح-١٥٥) التسويةُ البنكيّةُ لكلّ سطر: عمودُ السطر (صفرٌ للسجلّات القائمة = القيدُ كلُّه)، <b>والفهرسُ الفريدُ القديمُ يُسقط</b> —
    /// وإلا رفض سطراً ثانياً من القيد نفسِه — ويحلّ محلَّه فريدٌ يشمل السطر.
    /// </summary>
    private async Task EnsureReconciledBankLineAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("reconciled_bank_entries", isPg, ct))
            return;

        if (isPg)
        {
            await EnsurePgColumnAsync("reconciled_bank_entries", "JournalLineId", "BIGINT NOT NULL DEFAULT 0", ct);
            await db.Database.ExecuteSqlRawAsync(
                """DROP INDEX IF EXISTS "IX_reconciled_bank_entries_CompanyId_BankAccountId_JournalEntr~";""", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("reconciled_bank_entries", "JournalLineId", "INTEGER NOT NULL DEFAULT 0", ct);
            await db.Database.ExecuteSqlRawAsync(
                """DROP INDEX IF EXISTS "IX_reconciled_bank_entries_CompanyId_BankAccountId_JournalEntryId";""", ct);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_reconciled_bank_entries_line"
                ON reconciled_bank_entries ("CompanyId", "BankAccountId", "JournalEntryId", "JournalLineId");
            """, ct);
    }

    private async Task EnsureGosiContributorGroupColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (await TableExistsAsync("employees", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("employees", "GosiRegime", "character varying(16) NOT NULL DEFAULT 'Legacy'", ct);
                await EnsurePgColumnAsync("employees", "ProjectId", "BIGINT NULL", ct);   // ‏(ح-١٤٦)
            }
            else
            {
                await EnsureSqliteColumnAsync("employees", "GosiRegime", "TEXT NOT NULL DEFAULT 'Legacy'", ct);
                await EnsureSqliteColumnAsync("employees", "ProjectId", "INTEGER NULL", ct);   // ‏(ح-١٤٦)
            }
        }

        if (!await TableExistsAsync("gosi_policies", isPg, ct))
            return;

        foreach (var (column, pg, sqlite) in new[]
                 {
                     ("NewLawEmployeePercent", "NUMERIC(9,4) NOT NULL DEFAULT 9.75", "TEXT NOT NULL DEFAULT '9.75'"),
                     ("NewLawEmployerPercent", "NUMERIC(9,4) NOT NULL DEFAULT 11.75", "TEXT NOT NULL DEFAULT '11.75'"),
                     ("NewLawAnnualStepPercent", "NUMERIC(9,4) NOT NULL DEFAULT 0.5", "TEXT NOT NULL DEFAULT '0.5'"),
                     ("NewLawFirstStep", "DATE NOT NULL DEFAULT DATE '2025-07-01'", "TEXT NOT NULL DEFAULT '2025-07-01'"),
                     ("NewLawSteps", "INTEGER NOT NULL DEFAULT 4", "INTEGER NOT NULL DEFAULT 4"),
                 })
        {
            if (isPg)
                await EnsurePgColumnAsync("gosi_policies", column, pg, ct);
            else
                await EnsureSqliteColumnAsync("gosi_policies", column, sqlite, ct);
        }
    }

    private async Task EnsureOpeningBalanceItemsTableAsync(bool isPg, CancellationToken ct)
    {
        var sql = isPg
            ? """
              CREATE TABLE IF NOT EXISTS opening_balance_items (
                  "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                  "CompanyId" bigint NOT NULL,
                  "Party" character varying(16) NOT NULL,
                  "PartyId" bigint NOT NULL,
                  "DocumentNumber" character varying(50) NOT NULL,
                  "DocumentDate" date NOT NULL,
                  "DueDate" date NOT NULL,
                  "PostingDate" date NOT NULL,
                  "Amount" numeric(18,2) NOT NULL,
                  "JournalEntryId" bigint NOT NULL,
                  "CreatedAtUtc" timestamp with time zone NOT NULL,
                  CONSTRAINT "PK_opening_balance_items" PRIMARY KEY ("Id")
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_opening_balance_items_CompanyId_Party_PartyId_DocumentNumber"
                  ON opening_balance_items ("CompanyId", "Party", "PartyId", "DocumentNumber");
              CREATE INDEX IF NOT EXISTS "IX_opening_balance_items_JournalEntryId" ON opening_balance_items ("JournalEntryId");
              """
            : """
              CREATE TABLE IF NOT EXISTS opening_balance_items (
                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                  CompanyId INTEGER NOT NULL,
                  Party TEXT NOT NULL,
                  PartyId INTEGER NOT NULL,
                  DocumentNumber TEXT NOT NULL,
                  DocumentDate TEXT NOT NULL,
                  DueDate TEXT NOT NULL,
                  PostingDate TEXT NOT NULL,
                  Amount TEXT NOT NULL,
                  JournalEntryId INTEGER NOT NULL,
                  CreatedAtUtc TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS "IX_opening_balance_items_CompanyId_Party_PartyId_DocumentNumber"
                  ON opening_balance_items (CompanyId, Party, PartyId, DocumentNumber);
              CREATE INDEX IF NOT EXISTS "IX_opening_balance_items_JournalEntryId" ON opening_balance_items (JournalEntryId);
              """;
        await TrySchemaSqlAsync("EnsureOpeningBalanceItemsTableAsync", sql, ct);
    }

    private async Task EnsureMfaLastAcceptedStepColumnAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("app_users", isPg, ct))
            return;

        if (isPg)
            await EnsurePgColumnAsync("app_users", "MfaLastAcceptedStep", "BIGINT NULL", ct);
        else
            await EnsureSqliteColumnAsync("app_users", "MfaLastAcceptedStep", "INTEGER NULL", ct);

        // ‏(security-licensing-tenancy#7) ‏تجزئةُ مفتاح استعادة المدير — فارغةٌ للتركيبات القائمة حتى يُصدر المديرُ مفتاحاً.
        if (isPg)
            await EnsurePgColumnAsync("app_users", "RecoveryKeyHash", "character varying(128) NULL", ct);
        else
            await EnsureSqliteColumnAsync("app_users", "RecoveryKeyHash", "TEXT NULL", ct);
    }

    private async Task EnsurePosShiftAndRecurringPlanWarehouseColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in new[] { "pos_shifts", "recurring_invoice_plans" })
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
                await EnsurePgColumnAsync(table, "WarehouseId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "WarehouseId", "INTEGER NULL", ct);
        }
    }

    private async Task EnsureSalesInvoiceWarehouseColumnAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("sales_invoices", isPg, ct))
            return;

        if (isPg)
            await EnsurePgColumnAsync("sales_invoices", "WarehouseId", "BIGINT NULL", ct);
        else
            await EnsureSqliteColumnAsync("sales_invoices", "WarehouseId", "INTEGER NULL", ct);

        // ‏(sector-modules#6) ‏الحجزُ الذي تُصفّيه فاتورةُ المغادرة — فارغٌ لكلّ فاتورةٍ قائمة.
        if (isPg)
            await EnsurePgColumnAsync("sales_invoices", "SettlesReservationId", "BIGINT NULL", ct);
        else
            await EnsureSqliteColumnAsync("sales_invoices", "SettlesReservationId", "INTEGER NULL", ct);
    }

    private async Task EnsurePurchaseInvoiceWarehouseColumnAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("purchase_invoices", isPg, ct))
            return;

        if (isPg)
        {
            await EnsurePgColumnAsync("purchase_invoices", "WarehouseId", "BIGINT NULL", ct);
            await EnsurePgColumnAsync("purchase_invoices", "ProjectId", "BIGINT NULL", ct);   // ‏(ح-١٤٤)
            await EnsurePgColumnAsync("purchase_invoices", "FixedAssetId", "BIGINT NULL", ct);   // ‏(ح-١٥٨)
        }
        else
        {
            await EnsureSqliteColumnAsync("purchase_invoices", "WarehouseId", "INTEGER NULL", ct);
            await EnsureSqliteColumnAsync("purchase_invoices", "ProjectId", "INTEGER NULL", ct);   // ‏(ح-١٤٤)
            await EnsureSqliteColumnAsync("purchase_invoices", "FixedAssetId", "INTEGER NULL", ct);   // ‏(ح-١٥٨)
        }
    }

    private async Task EnsurePosCashSalesColumnAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("pos_shifts", isPg, ct))
            return;

        if (isPg)
            await EnsurePgColumnAsync("pos_shifts", "CashSales", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
        else
            await EnsureSqliteColumnAsync("pos_shifts", "CashSales", "REAL NOT NULL DEFAULT 0", ct);
    }

    /// <summary>جداول المستندات الضريبية التي تدخل الإقرار .</summary>
    private static readonly string[] CancellableTaxDocumentTables =
    [
        "sales_invoices", "credit_notes", "purchase_invoices",
        "debit_notes", "progress_billings",
    ];

    private async Task EnsureCancelledAtColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in CancellableTaxDocumentTables)
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
                await EnsurePgColumnAsync(table, "CancelledAt", "DATE NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "CancelledAt", "TEXT NULL", ct);
        }
    }

    /// <summary>
    /// أعمدةُ سلسلة التدقيق على القواعد القائمة — <b>ومعها ترقيمُ ما قبل السلسلة وفهرسُه</b>.
    ///
    /// <para> كان يُضيف الأعمدةَ الثلاثة ويقف. فبقيت الصفوفُ القديمة كلُّها
    /// <c>Sequence = 0</c> بلا فهرسٍ فريد — بينما تحمله القاعدةُ الجديدة من النموذج.
    /// <b>فانقسم النظام قسمَين</b>: قاعدةٌ يمنع فيها الفهرسُ ازدواجَ الترتيب وقاعدةٌ لا يمنعه.</para>
    ///
    /// <para>والترقيمُ هنا هو نفسُه الذي في هجرة <c>AddAuditChain</c> حرفاً بحرف: ما قبل
    /// السلسلة في <c>[-N..-1]</c> بترتيب المفتاح الأوّليّ، والسلسلةُ من ١ فصاعداً.
    /// <b>ومشروطٌ بـ<c>EntryHash = ''</c></b> — فالمُرقِّع يعمل عند كلّ إقلاع، ولمسُ صفٍّ
    /// مربوطٍ يكسر تجزئتَه وتجزئةَ كلّ ما بعده.</para>
    /// </summary>
    private async Task EnsureAuditChainColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("audit_log", isPg, ct))
            return;

        if (isPg)
        {
            await EnsurePgColumnAsync("audit_log", "Sequence", "BIGINT NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("audit_log", "PreviousHash", "character varying(64) NOT NULL DEFAULT ''", ct);
            await EnsurePgColumnAsync("audit_log", "EntryHash", "character varying(64) NOT NULL DEFAULT ''", ct);

            await db.Database.ExecuteSqlRawAsync("""
                UPDATE audit_log AS a
                SET "Sequence" = -n.rn
                FROM (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "Id" DESC) AS rn
                    FROM audit_log
                    WHERE "EntryHash" = '' AND "Sequence" = 0
                ) AS n
                WHERE a."Id" = n."Id" AND a."EntryHash" = '' AND a."Sequence" = 0;
                """, ct);

            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_audit_log_CompanyId_Sequence"
                ON audit_log ("CompanyId", "Sequence")
                """, ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("audit_log", "Sequence", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsureSqliteColumnAsync("audit_log", "PreviousHash", "TEXT NOT NULL DEFAULT ''", ct);
            await EnsureSqliteColumnAsync("audit_log", "EntryHash", "TEXT NOT NULL DEFAULT ''", ct);

            await db.Database.ExecuteSqlRawAsync("""
                UPDATE audit_log
                SET "Sequence" = -(
                    SELECT COUNT(*) FROM audit_log AS later
                    WHERE later."CompanyId" = audit_log."CompanyId"
                      AND later."EntryHash" = '' AND later."Sequence" = 0
                      AND later."Id" >= audit_log."Id")
                WHERE "EntryHash" = '' AND "Sequence" = 0;
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_audit_log_company_sequence
                ON audit_log ("CompanyId", "Sequence")
                """, ct);
        }
    }

    /// <summary>
    /// رمزُ سبب الإعفاء ونصُّه على جداول أسطر المستندات — نصّان قابلان للعدم، وفارغُهما
    /// يعني «لم يُختَر». ولا قيمةَ افتراضية: سطرٌ قديمٌ بفئةٍ أساسية لا يحتاج سبباً أصلاً.
    /// </summary>
    /// <summary>
    /// لحظةُ ترحيل الفاتورة والإشعار الدائن — قابلةٌ للعدم، وفارغُها يعني «مستندٌ سبق الوسم».
    /// </summary>
    private async Task EnsurePostedAtColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in new[] { "sales_invoices", "credit_notes" })
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
                await EnsurePgColumnAsync(table, "PostedAtUtc", "timestamp with time zone NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "PostedAtUtc", "TEXT NULL", ct);
        }
    }

    private async Task EnsureExemptionReasonColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in VatCategoryLineTables)
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
            {
                await EnsurePgColumnAsync(table, "TaxExemptionReasonCode", "text NULL", ct);
                await EnsurePgColumnAsync(table, "TaxExemptionReason", "text NULL", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync(table, "TaxExemptionReasonCode", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync(table, "TaxExemptionReason", "TEXT NULL", ct);
            }
        }
    }

    private async Task EnsureVatCategoryColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in VatCategoryLineTables)
        {
            // قاعدة أقدم قد لا تحوي الجدول أصلاً (أوامر البيع مثلاً أُضيفت لاحقاً).
            // تخطّيه صواب لا ابتلاع: الجدول حين يُنشأ من نموذج EF يحمل العمود ضمناً.
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
                await EnsurePgColumnAsync(table, "VatCategory",
                    "character varying(16) NOT NULL DEFAULT 'Standard'", ct);
            else
                await EnsureSqliteColumnAsync(table, "VatCategory",
                    "TEXT NOT NULL DEFAULT 'Standard'", ct);
        }
    }

    /// <summary>اسمُ الخطوة في سجلّ المبتلَع — ثابتٌ لأنّ شرطَ ما بعدُ يبحث به.</summary>
    private const string VatCategoryTextStep = "EnsureVatCategoryIsTextAsync";

    /// <summary>
    /// (م-٥ · ن-٥١) <b>تحويلُ عمود الفئة الضريبيّة من عددٍ صحيحٍ إلى نصّ على قاعدةٍ قائمة.</b>
    ///
    /// <para><b>ولمن هذه الخطوة أصلاً:</b> القاعدةُ الحديثةُ تُنشأ بالهجرات فتأتي نصّاً، ولا
    /// تدخل الحلقةَ إطلاقاً. <b>والقاعدةُ القديمة</b> — <c>coreTableExists &amp;&amp;
    /// appliedCount == 0</c> في <see cref="DatabaseSchemaService"/> — <b>لا تُطبَّق عليها هجرةٌ
    /// أبداً</b>: يُستدعى المُرقِّعُ ثمّ يُرجَع. فهذه الخطوةُ هي طريقُ الإصلاح الوحيدُ لتلك
    /// القواعد، وبدونها يصير كلُّ قراءةٍ لعرض سعرٍ بعد تغيير النموذج
    /// <c>InvalidCastException</c>.</para>
    ///
    /// <para><b>ولا تبتلع.</b> <see cref="TrySchemaSqlAsync"/> يبتلع كلَّ فشلٍ ويُسجّله، وهو
    /// السلوكُ المقصود لإضافة عمود. <b>لكنّ فشلَ هذا التحويل ليس كفشل إضافة</b>: النموذجُ يصير
    /// يقول «نصّ» والقاعدةُ تبقى «عدداً»، <b>فيخضرّ الإقلاعُ ثمّ ترمي أوّلُ قراءةٍ في يد
    /// المستخدم</b> — وذلك أسوأُ من توقّفٍ صريح، لأنّ العطبَ يقع حيث لا إشارةَ عنده. فيُقرأ
    /// النوعُ <b>بعد</b> المحاولة، ويُرمى إن بقي عدداً، والرسالةُ تحمل SQLSTATE المبتلَع.
    /// <b>وهذا الرميُ لا يُدرَك إلّا على قاعدةٍ تحتاج التحويلَ ولم تنلْه</b> — أمّا القاعدةُ
    /// النصّيّةُ أصلاً فلا تدخل الحلقة.</para>
    ///
    /// <para><b>و<c>lock_timeout</c> لأنّ <c>ACCESS EXCLUSIVE</c> يحجب من يصطفّ خلفه.</b> بلا
    /// سقفٍ ينتظر التحويلُ حتّى مهلةِ الأمر الافتراضيّة (٣٠ ثانية) <b>ومعه كلُّ قارئٍ وصل
    /// بعده</b> — فمحطّةٌ واحدةٌ تُقلع صباحاً تُجمّد عروضَ الأسعار على المكتب كلِّه. وبسقفٍ
    /// قصيرٍ يصير التزاحمُ <c>55P03</c> فوريّاً، ثمّ يرفعه شرطُ ما بعدُ صريحاً.</para>
    ///
    /// <para><b>وموضعُها في الحزمة المبكّرة دفاعيٌّ لا حتميّ</b> — ويُقال بدقّة: قِيس أنّ
    /// ترقيعات المزوّد <b>لا</b> تقرأ هذه المجموعةَ المملوكة، فالموضعُ المتأخّر كان ليعمل.
    /// لكنّ هذا الملفّ يحمل عضّةً مسجّلةً بـ<c>42703</c> لعمودٍ وُضع متأخّراً
    /// (<c>EnsureExemptionReasonColumnsAsync</c> أعلاه)، <b>فالمبكّرُ يُختار لأنّ كلفةَ خطئه
    /// صفرٌ وكلفةَ خطأ المتأخّر إسقاطُ الإقلاع</b>.</para>
    /// </summary>
    private async Task EnsureVatCategoryIsTextAsync(bool isPg, CancellationToken ct)
    {
        // ‏SQLite لا يحتاج تحويلاً: قِيس أنّ صفّاً قديماً مخزَّناً عدداً يُقرأ عبر
        // ‏`Enum.Parse` فيعود `Exempt` — فالقيمةُ محفوظةٌ لا محتملةٌ فقط.
        if (!isPg) return;

        var names = Enum.GetValues<Brqosh.Domain.Tax.VatCategory>();
        var whens = string.Join("\n                                   ",
            names.Select(v => $"WHEN {(int)v} THEN '{v}'"));
        var known = string.Join(", ", names.Select(v => ((int)v).ToString()));

        var stubborn = new List<string>();

        foreach (var table in VatCategoryLineTables)
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (await PgColumnTypeAsync(table, "VatCategory", ct) is not "integer")
                continue;

            SchemaSqlGuard.ValidateTable(table);

            // ‏**ما لا اسمَ له يُعدّ قبل أن يُطمَس.** فرعُ ELSE يكتب 'Standard' فوق أيّ قيمةٍ
            // ‏خارجَ التعداد، **والجملةُ نفسُها تُتلف الدليلَ على أنّ شيئاً كُتب فوقه**. ولا
            // ‏سبيلَ اليومَ لوجود مثلها، فإن وُجدت فهي خبرٌ يستحقّ أن يُسجَّل لا أن يُبتلع.
            var unnamed = await db.Database.SqlQueryRaw<int>(
                $"""SELECT count(*)::int AS "Value" FROM {table} WHERE "VatCategory" NOT IN ({known})""")
                .SingleAsync(ct);

            if (unnamed > 0)
                _suppressed.Add(new SchemaStepFailure(
                    $"{VatCategoryTextStep}:{table}", "UnnamedVatCategoryRows",
                    $"‏{unnamed} صفّاً يحمل فئةً لا اسمَ لها في التعداد، وكُتب فوقها 'Standard'."));

            await TrySchemaSqlAsync($"{VatCategoryTextStep}:{table}", $"""
                SET LOCAL lock_timeout = '3s';
                ALTER TABLE {table} ALTER COLUMN "VatCategory" DROP DEFAULT;
                ALTER TABLE {table} ALTER COLUMN "VatCategory" TYPE character varying(16)
                    USING (CASE "VatCategory"
                                   {whens}
                                   ELSE '{names[0]}' END);
                ALTER TABLE {table} ALTER COLUMN "VatCategory" SET DEFAULT '{names[0]}';
                """, ct);

            // ‏**شرطُ ما بعدُ.** لا يُترك النموذجُ يسبق التخزين.
            if (await PgColumnTypeAsync(table, "VatCategory", ct) is "integer")
                stubborn.Add(table);
        }

        if (stubborn.Count == 0)
            return;

        var why = string.Join(" · ", _suppressed
            .Where(f => f.Step.StartsWith(VatCategoryTextStep, StringComparison.Ordinal))
            .Select(f => $"{f.Step} ⟵ {f.Message}"));

        throw new InvalidOperationException(
            "‏تعذّر تحويلُ عمود الفئة الضريبيّة إلى نصّ، والنموذجُ يقرؤه نصّاً — "
            + "**فكلُّ قراءةٍ لمستندٍ من هذه الجداول سترمي**. "
            + "أعِد التشغيلَ بلا عملاءَ آخرين متّصلين، أو أزِل ما يعتمد على العمود (عرضٌ أو قاعدة). "
            + $"الجداولُ الباقيةُ عدداً صحيحاً: {string.Join(", ", stubborn)}. السببُ المُسجَّل: {why}");
    }

    /// <summary>نوعُ عمودٍ على PostgreSQL، أو <c>null</c> إن لم يوجد.</summary>
    private async Task<string?> PgColumnTypeAsync(string table, string column, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);
        SchemaSqlGuard.ValidateColumn(column);

        FormattableString query = $"""
             SELECT data_type AS "Value"
             FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = {table} AND column_name = {column}
             """;

        return await db.Database.SqlQuery<string>(query).SingleOrDefaultAsync(ct);
    }

    private async Task<bool> TableExistsAsync(string table, bool isPg, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);
        var sql = isPg
            ? $"""SELECT COUNT(*) AS "Value" FROM information_schema.tables WHERE table_schema='public' AND table_name='{table}'"""
            : $"""SELECT COUNT(*) AS "Value" FROM sqlite_master WHERE type='table' AND name='{table}'""";

        var count = await db.Database.SqlQueryRaw<int>(sql).SingleAsync(ct);
        return count > 0;
    }

    /// <summary>
    /// جداول أسطر المستندات التي تحمل فئة ضريبية .
    ///
    /// <para>(م-٥ · ن-٥١) <b>كانت ستّةً وهي تسعة.</b> نوعُ السطر <c>SalesInvoiceLine</c> يملكه
    /// تسعةُ مستندات، <b>وثلاثةٌ منها غابت عن هذه القائمة</b>: عرضُ السعر وطلبُ الشراء
    /// وخطّةُ الفوترة الدوريّة. وهو درسُ «ثمانيةُ جداولٍ لا واحد» نفسُه، طُبِّق على
    /// <c>BatchNumber</c> و<c>DiscountPercent</c> و<c>UnitCode</c> <b>ولم يُطبَّق هنا</b>.</para>
    ///
    /// <para><b>وهذه القائمةُ تقود ثلاثَ دوالّ لا واحدة</b> — يُقال صراحةً كي لا تُعدَّل ظنّاً
    /// أنّ أثرَها موضعٌ واحد: <see cref="EnsureExemptionReasonColumnsAsync"/> و
    /// <see cref="EnsureVatCategoryColumnsAsync"/> و<see cref="EnsureVatCategoryIsTextAsync"/>.
    /// <b>وتوسيعُها قيسَ خاملاً على الأوّلَين</b>: الأعمدةُ الثلاثةُ موجودةٌ أصلاً في الجداول
    /// الثلاثة المضافة من هجراتها، فـ<c>EnsurePgColumnAsync</c> يتخطّاها.</para>
    /// </summary>
    private static readonly string[] VatCategoryLineTables =
    [
        "sales_invoice_lines", "credit_note_lines", "purchase_invoice_lines",
        "purchase_order_lines", "debit_note_lines", "sales_order_lines",
        "sales_quotation_lines", "purchase_request_lines", "recurring_invoice_plan_lines",
    ];
}
