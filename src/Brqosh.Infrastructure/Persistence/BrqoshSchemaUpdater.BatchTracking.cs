using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — تتبُّعُ الدُّفعات. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
    /// <summary>
    /// <b>تتبّعُ الدفعة والصلاحية والرقم التسلسليّ — على قاعدةٍ قائمة.</b>
    ///
    /// <para>جدولُ الدفعات جديد، وأربعةُ أعمدةٍ تُضاف إلى جداولَ قائمة. <b>وكلُّها تقبل
    /// العدم أو لها قيمةٌ افتراضية</b> — فالترقيةُ تمرّ على قاعدةٍ فيها ملايينُ الصفوف بلا
    /// تعبئةٍ ولا قيدٍ يتصادم، وهو الدرسُ المقيسُ في <c>ط0-٢</c>.</para>
    ///
    /// <para><b>و<c>Tracking</c> افتراضُه <c>None</c></b>: فلا يتغيّر سلوكُ صنفٍ قائمٍ
    /// واحدٍ بهذه الترقية — والصنفُ يصير متتبَّعاً بقرارِ المالك وحدَه، ولا يُتّخذ ذلك
    /// القرارُ إلا ورصيدُه صفر.</para>
    /// </summary>
    private async Task EnsureBatchTrackingAsync(bool isPg, CancellationToken ct)
    {
        // ‏**ثمانيةُ جداولٍ لا واحد.** `SalesInvoiceLine` سجلٌّ
        // ‏**مشتركٌ** بين ثمانية مستندات: فاتورةُ المبيعات وأمرُها وعرضُ سعرها، وإشعارا
        // ‏الدائن والمدين، وفاتورةُ المشتريات وأمرُها وطلبُها. **فحقلٌ يُضاف لأجل البيع
        // ‏يظهر في الثمانية** — ويُنسى منها واحدٌ فيسقط إقلاعُ كلّ قاعدةِ عميلٍ قائم،
        // ‏لأنّ EF يبني SELECT بكلّ أعمدة النموذج.
        //
        // ‏وقد نسيتُ أربعةً منها، **وسمّاها مسبارُ المخطّط في ثانية**:
        // ‏`42703: column p1.BatchNumber does not exist`.
        string[] lineTables =
        [
            "sales_invoice_lines", "sales_order_lines", "sales_quotation_lines",
            "credit_note_lines", "debit_note_lines",
            "purchase_invoice_lines", "purchase_order_lines", "purchase_request_lines",
        ];

        // ‏**وحارسُ الوجود لازم**: هذه المرحلةُ تسبق إعادةَ بناء جداول دورة حياة المبيعات،
        // ‏فقاعدةٌ قديمةٌ ينقصها `sales_order_lines` تُسقط الإقلاعَ بـ`42P01` لولا التخطّي.
        //
        // ‏**ولهذا تُستدعى هذه الدالّةُ مرّتين.** كتبتُ هنا أوّلاً أنّ ما يُتخطّى «يُنشأ
        // ‏لاحقاً من النموذج فيحمل العمودَ أصلاً» — **وكان ذلك خطأً**، وأسقطه مسبارُ
        // ‏المخطّط: المُرقِّعُ يُعيد بناء تلك الجداول **من DDL مكتوبٍ بيده** لا من النموذج،
        // ‏فتعود بلا العمود. فنداءٌ ثانٍ بعد إعادة البناء، والدالّةُ مُطاوِعةٌ للتكرار.
        foreach (var table in lineTables)
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
            {
                await EnsurePgColumnAsync(table, "BatchNumber", "character varying(64) NULL", ct);
                // ‏نسبةُ خصم السطر — على الثمانية أنفسِها، فالسجلُّ مشترك.
                await EnsurePgColumnAsync(table, "DiscountPercent", "NUMERIC(9,4) NOT NULL DEFAULT 0", ct);
                // ‏وحدةُ السطر — على الثمانية أنفسِها للسبب نفسِه.
                await EnsurePgColumnAsync(table, "UnitCode", "character varying(16) NULL", ct);
                // ‏ ‏انتهاءُ الدفعة المشتراة — لفاتورة الشراء وحدَها.
                if (table == "purchase_invoice_lines")
                    await EnsurePgColumnAsync(table, "ExpiryDate", "DATE NULL", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync(table, "BatchNumber", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync(table, "DiscountPercent", "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync(table, "UnitCode", "TEXT NULL", ct);
                // ‏ ‏انتهاءُ الدفعة المشتراة — لفاتورة الشراء وحدَها.
                if (table == "purchase_invoice_lines")
                    await EnsureSqliteColumnAsync(table, "ExpiryDate", "TEXT NULL", ct);
            }
        }

        // ‏مركزُ تكلفة المستند على فاتورة المشتريات وسندِ الصرف.
        foreach (var table in new[] { "purchase_invoices", "payment_vouchers" })
        {
            if (!await TableExistsAsync(table, isPg, ct)) continue;

            if (isPg)
                await EnsurePgColumnAsync(table, "CostCenterId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "CostCenterId", "INTEGER NULL", ct);
        }

        // ‏عملةُ المشتريات والسداد — **وكان جانبُ الموردين بلا عملةٍ
        // ‏إطلاقاً**: المستوردُ يُدخل فاتورةَ الدولار محوَّلةً بيده، فتضيع القيمةُ الأصلية
        // ‏ولا يُعرف عند السداد بأيّ سعرٍ نشأت الذمّة — **ولا يُحسب فرقُ عملةٍ محقَّق**.
        if (await TableExistsAsync("purchase_invoices", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("purchase_invoices", "CurrencyCode",
                    "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
                await EnsurePgColumnAsync("purchase_invoices", "ExchangeRate",
                    "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
                await EnsurePgColumnAsync("purchase_invoices", "ForeignSubtotal",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                await EnsurePgColumnAsync("purchase_invoices", "ForeignVatAmount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("purchase_invoices", "CurrencyCode",
                    "TEXT NOT NULL DEFAULT 'SAR'", ct);
                await EnsureSqliteColumnAsync("purchase_invoices", "ExchangeRate",
                    "TEXT NOT NULL DEFAULT '1'", ct);
                await EnsureSqliteColumnAsync("purchase_invoices", "ForeignSubtotal",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("purchase_invoices", "ForeignVatAmount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }

            // ‏**والأساسُ الأجنبيّ للفواتير القائمة يُملأ من الرياليّ** — بسعرِ واحدٍ هما
            // ‏رقمٌ واحد. **وتركُه صفراً يجعل كلَّ فاتورةٍ قديمةٍ تبدو بلا قيمةٍ بعملتها.**
            var purchaseBackfill = isPg
                ? """UPDATE purchase_invoices SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;"""
                : """UPDATE purchase_invoices SET ForeignSubtotal = Subtotal, ForeignVatAmount = VatAmount WHERE CAST(ForeignSubtotal AS REAL) = 0 AND CAST(Subtotal AS REAL) <> 0;""";
            if (isPg) await SafePgIndexAsync(purchaseBackfill, ct);
            else await SafeSqliteIndexAsync(purchaseBackfill, ct);
        }

        if (await TableExistsAsync("payment_vouchers", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("payment_vouchers", "CurrencyCode",
                    "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
                await EnsurePgColumnAsync("payment_vouchers", "ExchangeRate",
                    "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
                await EnsurePgColumnAsync("payment_vouchers", "PayableRelieved",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                // ‏خصمُ السداد المكتسَب.
                await EnsurePgColumnAsync("payment_vouchers", "SettlementDiscount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("payment_vouchers", "CurrencyCode",
                    "TEXT NOT NULL DEFAULT 'SAR'", ct);
                await EnsureSqliteColumnAsync("payment_vouchers", "ExchangeRate",
                    "TEXT NOT NULL DEFAULT '1'", ct);
                await EnsureSqliteColumnAsync("payment_vouchers", "PayableRelieved",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("payment_vouchers", "SettlementDiscount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }
        }

        // ‏عملةُ سند القبض.
        if (await TableExistsAsync("receipt_vouchers", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("receipt_vouchers", "CurrencyCode",
                    "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
                await EnsurePgColumnAsync("receipt_vouchers", "ExchangeRate",
                    "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
                await EnsurePgColumnAsync("receipt_vouchers", "ReceivableRelieved",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                // ‏خصمُ السداد المسموح به.
                await EnsurePgColumnAsync("receipt_vouchers", "SettlementDiscount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("receipt_vouchers", "CurrencyCode",
                    "TEXT NOT NULL DEFAULT 'SAR'", ct);
                await EnsureSqliteColumnAsync("receipt_vouchers", "ExchangeRate",
                    "TEXT NOT NULL DEFAULT '1'", ct);
                await EnsureSqliteColumnAsync("receipt_vouchers", "ReceivableRelieved",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("receipt_vouchers", "SettlementDiscount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }
        }

        // ‏علامةُ «على مستوى المنشأة» — والافتراضُ `false`،
        // ‏فقيدٌ قائمٌ يبقى كما هو.
        if (await TableExistsAsync("journal_entries", isPg, ct))
        {
            if (isPg)
                await EnsurePgColumnAsync("journal_entries", "IsEntityLevel",
                    "BOOLEAN NOT NULL DEFAULT FALSE", ct);
            else
                await EnsureSqliteColumnAsync("journal_entries", "IsEntityLevel",
                    "INTEGER NOT NULL DEFAULT 0", ct);
        }

        // ‏مذكرةُ العملة على سطر القيد — **لاغيةٌ لكلّ سطرٍ قائم**،
        // ‏فالعمودُ يقبل الفراغ ولا افتراضَ له. **وافتراضُ 'SAR' هنا كذبٌ على الماضي**:
        // ‏يجعل قيوداً رُحّلت بلا مذكرةٍ تبدو موثَّقةً بعملةٍ لم يُدخلها أحد.
        if (await TableExistsAsync("journal_entry_lines", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("journal_entry_lines", "CurrencyCode",
                    "character varying(3) NULL", ct);
                await EnsurePgColumnAsync("journal_entry_lines", "ExchangeRate",
                    "NUMERIC(18,6) NULL", ct);
                await EnsurePgColumnAsync("journal_entry_lines", "ForeignAmount",
                    "NUMERIC(18,2) NULL", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("journal_entry_lines", "CurrencyCode", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync("journal_entry_lines", "ExchangeRate", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync("journal_entry_lines", "ForeignAmount", "TEXT NULL", ct);
            }
        }

        if (await TableExistsAsync("sales_invoices", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("sales_invoices", "DocumentDiscountAmount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                // ‏عملةُ المستند — والافتراضُ ريالٌ بسعرِ واحد،
                // ‏فلا يتغيّر رقمٌ في فاتورةٍ قائمة.
                await EnsurePgColumnAsync("sales_invoices", "CurrencyCode",
                    "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
                await EnsurePgColumnAsync("sales_invoices", "ExchangeRate",
                    "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
                await EnsurePgColumnAsync("sales_invoices", "ForeignSubtotal",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                await EnsurePgColumnAsync("sales_invoices", "ForeignVatAmount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("sales_invoices", "DocumentDiscountAmount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("sales_invoices", "CurrencyCode",
                    "TEXT NOT NULL DEFAULT 'SAR'", ct);
                await EnsureSqliteColumnAsync("sales_invoices", "ExchangeRate",
                    "TEXT NOT NULL DEFAULT '1'", ct);
                await EnsureSqliteColumnAsync("sales_invoices", "ForeignSubtotal",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("sales_invoices", "ForeignVatAmount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }
        }

        // ‏**وعملةُ الإشعار المدين** — مرآةُ ح-٣٥.
        // ‏والافتراضُ ريالٌ بسعرِ واحد، **والأجنبيُّ يُعبّأ من الريال لكلّ إشعارٍ قائم**
        // ‏— وإلّا صيّرت `RecalculateTotals` الريالَ صفراً عند أوّل مسّ.
        if (await TableExistsAsync("debit_notes", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("debit_notes", "CurrencyCode",
                    "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
                await EnsurePgColumnAsync("debit_notes", "ExchangeRate",
                    "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
                await EnsurePgColumnAsync("debit_notes", "ForeignSubtotal",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                await EnsurePgColumnAsync("debit_notes", "ForeignVatAmount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("debit_notes", "CurrencyCode",
                    "TEXT NOT NULL DEFAULT 'SAR'", ct);
                await EnsureSqliteColumnAsync("debit_notes", "ExchangeRate",
                    "TEXT NOT NULL DEFAULT '1'", ct);
                await EnsureSqliteColumnAsync("debit_notes", "ForeignSubtotal",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("debit_notes", "ForeignVatAmount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }

            var debitBackfill = isPg
                ? """UPDATE debit_notes SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;"""
                : """UPDATE debit_notes SET ForeignSubtotal = Subtotal, ForeignVatAmount = VatAmount WHERE CAST(ForeignSubtotal AS REAL) = 0 AND CAST(Subtotal AS REAL) <> 0;""";
            if (isPg) await SafePgIndexAsync(debitBackfill, ct);
            else await SafeSqliteIndexAsync(debitBackfill, ct);
        }

        // ‏ ‏**وتكاليفُ التحويل على قائمة الموادّ.**
        //
        // ‏معيارُ المخزون يقضي أن تشمل تكلفةُ التامّ الأجرَ المباشرَ ونصيباً من التكاليف
        // ‏الصناعيّة، **وكانت الموادَّ وحدَها**. **والافتراضُ صفرٌ** فلا تتغيّر تكلفةُ
        // ‏أمرٍ قائمٍ بهذه الترقية.
        if (await TableExistsAsync("bills_of_materials", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("bills_of_materials", "LabourCostPerUnit",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                await EnsurePgColumnAsync("bills_of_materials", "OverheadRatePercent",
                    "NUMERIC(9,4) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("bills_of_materials", "LabourCostPerUnit",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("bills_of_materials", "OverheadRatePercent",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }
        }

        // ‏ ‏**ووسمُ أنّ قيدَ تحصيل الدفع عند الاستلام وقع.**
        //
        // ‏`CodCollected` يُضبط **بالتسليم بلا قيد**، فلم يكن ثمّة ما يمنع تحصيلاً
        // ‏يُقيَّد مرّتين ولا توريداً يسبق التحصيل فيُخرج `2114` مديناً.
        // ‏**والافتراضُ `false`**: بوالصُ قائمةٌ لا يُدّعى أنّ قيدَها وقع.
        if (await TableExistsAsync("shipments", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("shipments", "CodCollectionPosted",
                    "boolean NOT NULL DEFAULT FALSE", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("shipments", "CodCollectionPosted",
                    "INTEGER NOT NULL DEFAULT 0", ct);
            }
        }

        // ‏ ‏**وأسكنيٌّ عقدُ الإيجار أم تجاريّ.**
        //
        // ‏إيجارُ المسكن معفًى (المادة 30) والتجاريُّ خاضعٌ بـ١٥٪، **وكان يُفوتَر بـ١٥٪
        // ‏دائماً**. **والافتراضُ `false` (تجاريّ) عمداً**: هو سلوكُ ما مضى حرفاً،
        // ‏**فلا يتغيّر إقرارٌ لعقدٍ قائمٍ بهذه الترقية** — والتحوّلُ إلى الإعفاء قرارُ
        // ‏مكلَّفٍ يُتَّخذ لا يُفترض.
        if (await TableExistsAsync("lease_contracts", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("lease_contracts", "IsResidential",
                    "boolean NOT NULL DEFAULT FALSE", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("lease_contracts", "IsResidential",
                    "INTEGER NOT NULL DEFAULT 0", ct);
            }
        }

        // ‏ ‏**وخصمُ المستند على الإشعار الدائن.**
        //
        // ‏عمودُ `DiscountPercent` في `credit_note_lines` كان مُعرَّفاً في النموذج منذ ط1-٢
        // ‏**ولا يكتبه أحد** — فالسطرُ يُبنى بصفر. وأمّا خصمُ المستند فلم يكن له عمودٌ أصلاً.
        // ‏والافتراضُ صفرٌ، **فلا يتغيّر رقمٌ في إشعارٍ قائم**.
        if (await TableExistsAsync("credit_notes", isPg, ct))
        {
            if (isPg)
            {
                await EnsurePgColumnAsync("credit_notes", "DocumentDiscountAmount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);

                // ‏عملةُ الإشعار — **والافتراضُ ريالٌ
                // ‏بسعرِ واحد، فلا يتغيّر رقمٌ في إشعارٍ قائم**.
                await EnsurePgColumnAsync("credit_notes", "CurrencyCode",
                    "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
                await EnsurePgColumnAsync("credit_notes", "ExchangeRate",
                    "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
                await EnsurePgColumnAsync("credit_notes", "ForeignSubtotal",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
                await EnsurePgColumnAsync("credit_notes", "ForeignVatAmount",
                    "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync("credit_notes", "DocumentDiscountAmount",
                    "TEXT NOT NULL DEFAULT '0'", ct);

                await EnsureSqliteColumnAsync("credit_notes", "CurrencyCode",
                    "TEXT NOT NULL DEFAULT 'SAR'", ct);
                await EnsureSqliteColumnAsync("credit_notes", "ExchangeRate",
                    "TEXT NOT NULL DEFAULT '1'", ct);
                await EnsureSqliteColumnAsync("credit_notes", "ForeignSubtotal",
                    "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync("credit_notes", "ForeignVatAmount",
                    "TEXT NOT NULL DEFAULT '0'", ct);
            }

            // ‏**وتعبئةُ الأجنبيّ من الريال لكلّ
            // ‏إشعارٍ قائم** — **وتركُ الأجنبيّ صفراً يجعل `RecalculateTotals`
            // ‏تُصيّر الريالَ صفراً عند أوّل مسّ**، فيُفقَد مبلغٌ قائم.
            // ‏**وتعبئةٌ بقيمةٍ مساويةٍ لا تُغيّر معنىً** — بسعرِ واحدٍ هما رقمٌ واحد.
            var creditBackfill = isPg
                ? """UPDATE credit_notes SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;"""
                : """UPDATE credit_notes SET ForeignSubtotal = Subtotal, ForeignVatAmount = VatAmount WHERE CAST(ForeignSubtotal AS REAL) = 0 AND CAST(Subtotal AS REAL) <> 0;""";
            if (isPg) await SafePgIndexAsync(creditBackfill, ct);
            else await SafeSqliteIndexAsync(creditBackfill, ct);

            // ‏**وتعبئةُ الأجنبيّ من الريال لكلّ فاتورةٍ قائمة**: بسعرِ
            // ‏واحدٍ هما رقمٌ واحد، وتركُ الأجنبيّ صفراً يجعل الفوترةَ تُصدر مستنداً
            // ‏بإجماليٍّ صفر. **وتعبئةٌ بقيمةٍ مساوية لا تُغيّر معنىً**، بخلاف تعبئةٍ تخترع.
            var backfill = isPg
                ? """UPDATE sales_invoices SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;"""
                : """UPDATE sales_invoices SET ForeignSubtotal = Subtotal, ForeignVatAmount = VatAmount WHERE CAST(ForeignSubtotal AS REAL) = 0 AND CAST(Subtotal AS REAL) <> 0;""";
            if (isPg) await SafePgIndexAsync(backfill, ct);
            else await SafeSqliteIndexAsync(backfill, ct);
        }

        foreach (var table in new[] { "goods_receipt_lines", "stock_movement_lines" })
        {
            if (!await TableExistsAsync(table, isPg, ct))
                continue;

            if (isPg)
            {
                await EnsurePgColumnAsync(table, "BatchNumber", "character varying(64) NULL", ct);
                await EnsurePgColumnAsync(table, "ExpiryDate", "DATE NULL", ct);
                await EnsurePgColumnAsync(table, "UnitCode", "character varying(16) NULL", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync(table, "BatchNumber", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync(table, "ExpiryDate", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync(table, "UnitCode", "TEXT NULL", ct);
            }
        }

        if (await TableExistsAsync("products", isPg, ct))
        {
            if (isPg)
                await EnsurePgColumnAsync("products", "Tracking",
                    "character varying(16) NOT NULL DEFAULT 'None'", ct);
            else
                await EnsureSqliteColumnAsync("products", "Tracking", "TEXT NOT NULL DEFAULT 'None'", ct);
        }

        // ‏البوالصُ والتحصيلُ نيابةً عن التجّار.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS shipments (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "WaybillNumber" character varying(32) NOT NULL,
                    "ShipperCustomerId" BIGINT NOT NULL,
                    "ConsigneeName" character varying(256) NOT NULL,
                    "ConsigneePhone" character varying(32) NOT NULL,
                    "DestinationCity" character varying(64) NOT NULL,
                    "Pieces" INTEGER NOT NULL DEFAULT 1,
                    "WeightKg" NUMERIC(18,3) NOT NULL DEFAULT 0,
                    "FreightCharge" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "CodAmount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Status" character varying(16) NOT NULL,
                    "ShipDate" DATE NOT NULL,
                    "DeliveredOn" DATE NULL,
                    "CodCollected" BOOLEAN NOT NULL DEFAULT false,
                    "CodRemitted" BOOLEAN NOT NULL DEFAULT false,
                    "FreightInvoiceId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_shipments_CompanyId_WaybillNumber" ON shipments ("CompanyId", "WaybillNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_shipments_CompanyId_ShipperCustomerId_CodCollected_CodRemitted" ON shipments ("CompanyId", "ShipperCustomerId", "CodCollected", "CodRemitted");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_shipments_CompanyId_Status" ON shipments ("CompanyId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_shipments_CompanyId_BranchId" ON shipments ("CompanyId", "BranchId");""", ct);
        }

        // ‏الغرفُ والحجوزُ وحساباتُ النزلاء.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS rooms (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "RoomNumber" character varying(16) NOT NULL,
                    "Kind" character varying(16) NOT NULL,
                    "Floor" INTEGER NOT NULL DEFAULT 0,
                    "NightlyRate" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Status" character varying(16) NOT NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS reservations (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "RoomId" BIGINT NOT NULL,
                    "GuestCustomerId" BIGINT NOT NULL,
                    "ReservationNumber" character varying(32) NOT NULL,
                    "CheckInDate" DATE NOT NULL,
                    "CheckOutDate" DATE NOT NULL,
                    "RatePerNight" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Guests" INTEGER NOT NULL DEFAULT 1,
                    "Status" character varying(16) NOT NULL,
                    "SettlementInvoiceId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS guest_folios (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "ReservationId" BIGINT NOT NULL,
                    "IsSettled" BOOLEAN NOT NULL DEFAULT false
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS folio_charges (
                    id BIGSERIAL PRIMARY KEY,
                    guest_folio_id BIGINT NOT NULL
                        REFERENCES guest_folios("Id") ON DELETE CASCADE,
                    "ChargeDate" DATE NOT NULL,
                    "Kind" character varying(16) NOT NULL,
                    "Description" character varying(512) NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "VatCategory" character varying(16) NOT NULL,
                    "RoomNightDate" DATE NULL
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_rooms_CompanyId_RoomNumber" ON rooms ("CompanyId", "RoomNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_rooms_CompanyId_Status" ON rooms ("CompanyId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_reservations_CompanyId_ReservationNumber" ON reservations ("CompanyId", "ReservationNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_reservations_CompanyId_RoomId_Status" ON reservations ("CompanyId", "RoomId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_reservations_CompanyId_CheckInDate" ON reservations ("CompanyId", "CheckInDate");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_reservations_CompanyId_BranchId" ON reservations ("CompanyId", "BranchId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_guest_folios_CompanyId_ReservationId_IsSettled" ON guest_folios ("CompanyId", "ReservationId", "IsSettled");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_folio_charges_guest_folio_id_RoomNightDate" ON folio_charges (guest_folio_id, "RoomNightDate");""", ct);
        }

        // ‏قائمةُ الطعام ووصفاتُها.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS menu_items (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "Section" character varying(16) NOT NULL,
                    "SalePrice" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "SaleProductId" BIGINT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS menu_item_components (
                    id BIGSERIAL PRIMARY KEY,
                    menu_item_id BIGINT NOT NULL
                        REFERENCES menu_items("Id") ON DELETE CASCADE,
                    "ProductId" BIGINT NOT NULL,
                    "QuantityPerServing" NUMERIC(18,4) NOT NULL DEFAULT 0
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_menu_items_CompanyId_Code" ON menu_items ("CompanyId", "Code");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_menu_item_components_menu_item_id_ProductId" ON menu_item_components (menu_item_id, "ProductId");""", ct);

            // ‏**والقاعدةُ القائمة تُرقَّع**: الجدولُ أُنشئ قبل الربط،
            // ‏و<c>CREATE TABLE IF NOT EXISTS</c> لا يمسّ جدولاً موجوداً — **فالعمودُ لا يصل
            // ‏إلا بترقيعٍ صريح**.
            await EnsurePgColumnAsync("menu_items", "SaleProductId", "BIGINT NULL", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_menu_items_CompanyId_SaleProductId" ON menu_items ("CompanyId", "SaleProductId");""", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("menu_items", "SaleProductId", "INTEGER NULL", ct);
        }

        // ‏مهلةُ السداد — وبها يصير التنبّؤ تنبّؤاً.
        if (isPg)
        {
            await EnsurePgColumnAsync("customers", "PaymentTermDays", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("suppliers", "PaymentTermDays", "INTEGER NOT NULL DEFAULT 0", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("customers", "PaymentTermDays", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsureSqliteColumnAsync("suppliers", "PaymentTermDays", "INTEGER NOT NULL DEFAULT 0", ct);
        }

        // ‏أثرُ تتبّع الشحنة — سندُ التسليم.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS shipment_events (
                    "Id" BIGSERIAL PRIMARY KEY,
                    shipment_id BIGINT NOT NULL
                        REFERENCES shipments("Id") ON DELETE CASCADE,
                    "Kind" character varying(20) NOT NULL DEFAULT 'PickedUp',
                    "OccurredOn" DATE NOT NULL,
                    "Location" character varying(128) NULL,
                    "Note" character varying(256) NULL,
                    "ReceivedBy" character varying(128) NULL
                );
                """, ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_shipment_events_shipment_id_OccurredOn" ON shipment_events (shipment_id, "OccurredOn");""", ct);
        }

        // ‏الصيانةُ والإكرامياتُ ورحلاتُ التوصيل.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS maintenance_orders (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "PropertyId" BIGINT NOT NULL,
                    "UnitId" BIGINT NULL,
                    "OrderNumber" character varying(64) NOT NULL,
                    "Kind" character varying(16) NOT NULL DEFAULT 'Preventive',
                    "RequestDate" DATE NOT NULL,
                    "CompletedOn" DATE NULL,
                    "Description" character varying(512) NOT NULL,
                    "VendorName" character varying(256) NULL,
                    "Status" character varying(16) NOT NULL DEFAULT 'Open',
                    "LeaseContractId" BIGINT NULL,
                    "TenantChargeAmount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "JournalEntryId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS maintenance_cost_lines (
                    "Id" BIGSERIAL PRIMARY KEY,
                    order_id BIGINT NOT NULL
                        REFERENCES maintenance_orders("Id") ON DELETE CASCADE,
                    "Description" character varying(512) NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_maintenance_orders_CompanyId_OrderNumber" ON maintenance_orders ("CompanyId", "OrderNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_maintenance_orders_CompanyId_PropertyId_UnitId" ON maintenance_orders ("CompanyId", "PropertyId", "UnitId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_maintenance_orders_CompanyId_Status" ON maintenance_orders ("CompanyId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_maintenance_cost_lines_order_id" ON maintenance_cost_lines (order_id);""", ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS tip_pools (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "ShiftId" BIGINT NULL,
                    "PoolDate" DATE NOT NULL,
                    "Status" character varying(16) NOT NULL DEFAULT 'Open',
                    "JournalEntryId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS tip_collections (
                    "Id" BIGSERIAL PRIMARY KEY,
                    pool_id BIGINT NOT NULL REFERENCES tip_pools("Id") ON DELETE CASCADE,
                    "Source" character varying(8) NOT NULL DEFAULT 'Cash',
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Note" character varying(256) NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS tip_distributions (
                    "Id" BIGSERIAL PRIMARY KEY,
                    pool_id BIGINT NOT NULL REFERENCES tip_pools("Id") ON DELETE CASCADE,
                    "EmployeeId" BIGINT NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0
                );
                """, ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_tip_pools_CompanyId_PoolDate" ON tip_pools ("CompanyId", "PoolDate");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_tip_pools_CompanyId_Status" ON tip_pools ("CompanyId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_tip_collections_pool_id" ON tip_collections (pool_id);""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_tip_distributions_pool_id_EmployeeId" ON tip_distributions (pool_id, "EmployeeId");""", ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS delivery_trips (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "TripNumber" character varying(64) NOT NULL,
                    "RouteName" character varying(256) NOT NULL,
                    "VehiclePlate" character varying(32) NOT NULL,
                    "DriverEmployeeId" BIGINT NULL,
                    "DepartureDate" DATE NOT NULL,
                    "ReturnDate" DATE NULL,
                    "Status" character varying(16) NOT NULL DEFAULT 'Planned',
                    "DriverFloat" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ReturnedCash" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "JournalEntryId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS trip_shipments (
                    "Id" BIGSERIAL PRIMARY KEY,
                    trip_id BIGINT NOT NULL REFERENCES delivery_trips("Id") ON DELETE CASCADE,
                    "ShipmentId" BIGINT NOT NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS trip_expenses (
                    "Id" BIGSERIAL PRIMARY KEY,
                    trip_id BIGINT NOT NULL REFERENCES delivery_trips("Id") ON DELETE CASCADE,
                    "Kind" character varying(20) NOT NULL DEFAULT 'Other',
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "FromDriverFloat" BOOLEAN NOT NULL DEFAULT false,
                    "Note" character varying(256) NULL
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_delivery_trips_CompanyId_TripNumber" ON delivery_trips ("CompanyId", "TripNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_delivery_trips_CompanyId_RouteName_DepartureDate" ON delivery_trips ("CompanyId", "RouteName", "DepartureDate");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_trip_shipments_ShipmentId" ON trip_shipments ("ShipmentId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_trip_shipments_trip_id" ON trip_shipments (trip_id);""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_trip_expenses_trip_id" ON trip_expenses (trip_id);""", ct);
        }

        // ‏(sector-modules#11) ‏فاتورةُ المورّد التي سندُ أمر الصيانة — فارغةٌ لكلّ أمرٍ قائم (دُفع نقداً).
        if (await TableExistsAsync("maintenance_orders", isPg, ct))
        {
            if (isPg)
                await EnsurePgColumnAsync("maintenance_orders", "PurchaseInvoiceId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync("maintenance_orders", "PurchaseInvoiceId", "INTEGER NULL", ct);
        }

        // ‏دفعاتُ المطالبات وقراراتُ الشركة عليها.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS insurance_claims (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "PayerId" BIGINT NOT NULL,
                    "ClaimNumber" character varying(64) NOT NULL,
                    "PeriodStart" DATE NOT NULL,
                    "PeriodEnd" DATE NOT NULL,
                    "Status" character varying(16) NOT NULL DEFAULT 'Draft',
                    "SubmittedOn" DATE NULL,
                    "AdjudicatedOn" DATE NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS insurance_claim_lines (
                    "Id" BIGSERIAL PRIMARY KEY,
                    claim_id BIGINT NOT NULL
                        REFERENCES insurance_claims("Id") ON DELETE CASCADE,
                    "VisitId" BIGINT NOT NULL,
                    "PayerInvoiceId" BIGINT NOT NULL,
                    "ClaimedAmount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ApprovedAmount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "RejectedAmount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "RejectionReason" character varying(512) NULL,
                    "Disposition" character varying(16) NOT NULL DEFAULT 'Undecided',
                    "IsAdjudicated" BOOLEAN NOT NULL DEFAULT false,
                    "CreditNoteId" BIGINT NULL,
                    "RebillInvoiceId" BIGINT NULL
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_insurance_claims_CompanyId_PayerId_ClaimNumber" ON insurance_claims ("CompanyId", "PayerId", "ClaimNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_insurance_claims_CompanyId_Status" ON insurance_claims ("CompanyId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_insurance_claim_lines_PayerInvoiceId" ON insurance_claim_lines ("PayerInvoiceId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_insurance_claim_lines_VisitId" ON insurance_claim_lines ("VisitId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_insurance_claim_lines_claim_id" ON insurance_claim_lines (claim_id);""", ct);
        }

        // ‏المرضى وشركاتُ التأمين والوثائقُ والزيارات.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS patients (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "MedicalRecordNumber" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "NationalId" character varying(32) NOT NULL,
                    "DateOfBirth" DATE NOT NULL,
                    "Gender" character varying(16) NOT NULL,
                    "Phone" character varying(32) NOT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS insurance_payers (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "PaymentTermDays" INTEGER NOT NULL DEFAULT 60,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS insurance_policies (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "PatientId" BIGINT NOT NULL,
                    "PayerId" BIGINT NOT NULL,
                    "PolicyNumber" character varying(64) NOT NULL,
                    "MemberNumber" character varying(64) NOT NULL,
                    "CoveragePercent" NUMERIC(9,4) NOT NULL DEFAULT 0,
                    "CopayFixed" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "AnnualLimit" NUMERIC(18,2) NULL,
                    "StartDate" DATE NOT NULL,
                    "EndDate" DATE NOT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS visits (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "PatientId" BIGINT NOT NULL,
                    "VisitNumber" character varying(32) NOT NULL,
                    "VisitDate" DATE NOT NULL,
                    "DoctorName" character varying(128) NOT NULL,
                    "Diagnosis" character varying(512) NULL,
                    "PolicyId" BIGINT NULL,
                    "Status" character varying(16) NOT NULL,
                    "PatientInvoiceId" BIGINT NULL,
                    "PayerInvoiceId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS visit_lines (
                    id BIGSERIAL PRIMARY KEY,
                    visit_id BIGINT NOT NULL
                        REFERENCES visits("Id") ON DELETE CASCADE,
                    "Description" character varying(512) NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "UnitPrice" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "IsCovered" BOOLEAN NOT NULL DEFAULT true,
                    "ProductId" BIGINT NULL,
                    "BatchNumber" character varying(64) NULL,
                    "VatCategory" character varying(16) NOT NULL,
                    "TaxExemptionReasonCode" character varying(32) NULL,
                    "TaxExemptionReason" text NULL
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_patients_CompanyId_MedicalRecordNumber" ON patients ("CompanyId", "MedicalRecordNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_patients_CompanyId_CustomerId" ON patients ("CompanyId", "CustomerId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_patients_CompanyId_BranchId" ON patients ("CompanyId", "BranchId");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_insurance_payers_CompanyId_Code" ON insurance_payers ("CompanyId", "Code");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_insurance_policies_CompanyId_PatientId_IsActive" ON insurance_policies ("CompanyId", "PatientId", "IsActive");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_insurance_policies_CompanyId_PayerId" ON insurance_policies ("CompanyId", "PayerId");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_visits_CompanyId_VisitNumber" ON visits ("CompanyId", "VisitNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_visits_CompanyId_PatientId_VisitDate" ON visits ("CompanyId", "PatientId", "VisitDate");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_visits_CompanyId_PolicyId_VisitDate" ON visits ("CompanyId", "PolicyId", "VisitDate");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_visits_CompanyId_BranchId" ON visits ("CompanyId", "BranchId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_visit_lines_visit_id" ON visit_lines (visit_id);""", ct);
        }

        // ‏**رقمُ دفعة الدواء المصروف — على قاعدةٍ قائمةٍ كذلك.**
        //
        // ‏و`CREATE TABLE IF NOT EXISTS` أعلاه **لا يمسّ جدولاً موجوداً** — فقاعدةُ عميلٍ
        // ‏رُقّيت من نسخةٍ سابقة تبقى بلا العمود، **ويسقط إقلاعُها** لأنّ EF يبني SELECT
        // ‏بكلّ أعمدة النموذج. وهو درسُ `BatchNumber` نفسِه في مستندات البيع.
        if (await TableExistsAsync("visit_lines", isPg, ct))
        {
            if (isPg)
                await EnsurePgColumnAsync("visit_lines", "BatchNumber", "character varying(64) NULL", ct);
            else
                await EnsureSqliteColumnAsync("visit_lines", "BatchNumber", "TEXT NULL", ct);
        }

        // ‏العقاراتُ والوحداتُ وعقودُ الإيجار.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS properties (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "Kind" character varying(16) NOT NULL,
                    "City" character varying(64) NOT NULL,
                    "Address" character varying(512) NOT NULL,
                    "DeedNumber" character varying(64) NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true,
                    "FixedAssetId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS property_units (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "PropertyId" BIGINT NOT NULL,
                    "UnitNumber" character varying(32) NOT NULL,
                    "AreaSquareMeters" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Bedrooms" INTEGER NOT NULL DEFAULT 0,
                    "Status" character varying(16) NOT NULL,
                    "ListedAnnualRent" NUMERIC(18,2) NOT NULL DEFAULT 0
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS lease_contracts (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "PropertyUnitId" BIGINT NOT NULL,
                    "TenantCustomerId" BIGINT NOT NULL,
                    "ContractNumber" character varying(32) NOT NULL,
                    "StartDate" DATE NOT NULL,
                    "EndDate" DATE NOT NULL,
                    "RentPerPeriod" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Frequency" character varying(16) NOT NULL,
                    "SecurityDeposit" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Status" character varying(16) NOT NULL,
                    "RecurringPlanId" BIGINT NULL,
                    "DepositReceivedOn" DATE NULL,
                    "DepositReturnedOn" DATE NULL,
                    "DepositForfeited" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "TerminatedOn" DATE NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_properties_CompanyId_Code" ON properties ("CompanyId", "Code");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_properties_CompanyId_BranchId" ON properties ("CompanyId", "BranchId");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_property_units_PropertyId_UnitNumber" ON property_units ("PropertyId", "UnitNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_property_units_CompanyId_Status" ON property_units ("CompanyId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_lease_contracts_CompanyId_ContractNumber" ON lease_contracts ("CompanyId", "ContractNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_lease_contracts_CompanyId_PropertyUnitId_Status" ON lease_contracts ("CompanyId", "PropertyUnitId", "Status");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_lease_contracts_CompanyId_BranchId" ON lease_contracts ("CompanyId", "BranchId");""", ct);
        }

        // ‏سجلُّ الموظفين ونِسَبُ التأمينات.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS employees (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "NationalId" character varying(10) NOT NULL,
                    "Nationality" character varying(16) NOT NULL,
                    "JobTitle" character varying(128) NOT NULL,
                    "HireDate" DATE NOT NULL,
                    "TerminationDate" DATE NULL,
                    "Iban" character varying(24) NULL,
                    "BasicSalary" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "HousingAllowance" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "TransportAllowance" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "OtherAllowance" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "CostCenterId" BIGINT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS gosi_policies (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "SaudiEmployeePercent" NUMERIC(9,4) NOT NULL DEFAULT 9.75,
                    "SaudiEmployerPercent" NUMERIC(9,4) NOT NULL DEFAULT 11.75,
                    "NonSaudiEmployeePercent" NUMERIC(9,4) NOT NULL DEFAULT 0,
                    "NonSaudiEmployerPercent" NUMERIC(9,4) NOT NULL DEFAULT 2,
                    "ContributoryWageCap" NUMERIC(18,2) NOT NULL DEFAULT 45000,
                    "ContributoryWageFloor" NUMERIC(18,2) NOT NULL DEFAULT 0
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_employees_CompanyId_Code" ON employees ("CompanyId", "Code");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_employees_CompanyId_NationalId" ON employees ("CompanyId", "NationalId");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_employees_CompanyId_IsActive" ON employees ("CompanyId", "IsActive");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_employees_CompanyId_BranchId" ON employees ("CompanyId", "BranchId");""", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_gosi_policies_CompanyId" ON gosi_policies ("CompanyId");""", ct);

            await EnsurePgColumnAsync("payroll_lines", "EmployeeId", "BIGINT NULL", ct);
            await EnsurePgColumnAsync("payroll_lines", "OtherAllowance",
                "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);

            // ‏قسطُ السُّلفة المُستقطَع — وبغيره يقبض الموظّفُ راتبَه
            // ‏كاملاً وقد أخذ سُلفتَه.
            await EnsurePgColumnAsync("payroll_lines", "AdvanceDeduction",
                "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);

            // ‏خصمُ الغياب بلا أجر — **ويُنقص المصروف لا الصافيَ وحدَه**.
            await EnsurePgColumnAsync("payroll_lines", "AbsenceDeduction",
                "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS employee_advances (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "EmployeeId" BIGINT NOT NULL,
                    "AdvanceNumber" character varying(32) NOT NULL,
                    "RequestDate" DATE NOT NULL,
                    "DisbursedOn" DATE NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "InstallmentAmount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "Status" character varying(16) NOT NULL DEFAULT 'Approved',
                    "Reason" character varying(256) NULL,
                    "JournalEntryId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_employee_advances_CompanyId_AdvanceNumber" ON employee_advances ("CompanyId", "AdvanceNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_employee_advances_CompanyId_EmployeeId_Status" ON employee_advances ("CompanyId", "EmployeeId", "Status");""", ct);

            // ‏طلباتُ الإجازة والغياب.
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS leave_requests (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "EmployeeId" BIGINT NOT NULL,
                    "RequestNumber" character varying(32) NOT NULL,
                    "Type" character varying(16) NOT NULL DEFAULT 'Annual',
                    "FromDate" DATE NOT NULL,
                    "ToDate" DATE NOT NULL,
                    "Status" character varying(16) NOT NULL DEFAULT 'Pending',
                    "Reason" character varying(256) NULL,
                    "DecidedOn" DATE NULL,
                    "DecidedBy" character varying(64) NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_leave_requests_CompanyId_RequestNumber" ON leave_requests ("CompanyId", "RequestNumber");""", ct);
            await SafePgIndexAsync("""CREATE INDEX IF NOT EXISTS "IX_leave_requests_CompanyId_EmployeeId_Status" ON leave_requests ("CompanyId", "EmployeeId", "Status");""", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("payroll_lines", "EmployeeId", "INTEGER NULL", ct);
            await EnsureSqliteColumnAsync("payroll_lines", "OtherAllowance",
                "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnAsync("payroll_lines", "AdvanceDeduction",
                "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnAsync("payroll_lines", "AbsenceDeduction",
                "TEXT NOT NULL DEFAULT '0'", ct);
        }

        // ‏الموازنة.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS budgets (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "Year" INTEGER NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS budget_lines (
                    id BIGSERIAL PRIMARY KEY,
                    budget_id BIGINT NOT NULL REFERENCES budgets("Id") ON DELETE CASCADE,
                    "AccountCode" character varying(20) NOT NULL,
                    "PeriodCode" character varying(7) NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "CostCenterId" BIGINT NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_budgets_CompanyId_Code"
                    ON budgets ("CompanyId", "Code");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_budgets_CompanyId_Year" ON budgets ("CompanyId", "Year");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_budget_lines_budget_id" ON budget_lines (budget_id);
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_budget_lines_PeriodCode_AccountCode"
                    ON budget_lines ("PeriodCode", "AccountCode");
                """, ct);
        }

        // ‏خططُ الفوترة الدورية وأثرُ تكراراتها.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS recurring_invoice_plans (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "Description" character varying(512) NOT NULL,
                    "Frequency" character varying(16) NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "StartDate" DATE NOT NULL,
                    "EndDate" DATE NULL,
                    "OccurrencesGenerated" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS recurring_invoice_plan_lines (
                    id BIGSERIAL PRIMARY KEY,
                    recurring_invoice_plan_id BIGINT NOT NULL
                        REFERENCES recurring_invoice_plans("Id") ON DELETE CASCADE,
                    "Description" character varying(512) NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "UnitPrice" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ProductId" BIGINT NULL,
                    "VatCategory" character varying(16) NOT NULL DEFAULT 'Standard',
                    "TaxExemptionReasonCode" character varying(32) NULL,
                    "TaxExemptionReason" text NULL,
                    "BatchNumber" character varying(64) NULL,
                    "DiscountPercent" NUMERIC(9,4) NOT NULL DEFAULT 0,
                    "UnitCode" character varying(16) NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS recurring_invoice_runs (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "PlanId" BIGINT NOT NULL,
                    "OccurrenceDate" DATE NOT NULL,
                    "SalesInvoiceId" BIGINT NOT NULL,
                    "GeneratedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_recurring_invoice_runs_PlanId_OccurrenceDate"
                    ON recurring_invoice_runs ("PlanId", "OccurrenceDate");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_runs_CompanyId_OccurrenceDate"
                    ON recurring_invoice_runs ("CompanyId", "OccurrenceDate");
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_recurring_invoice_plans_CompanyId_Code"
                    ON recurring_invoice_plans ("CompanyId", "Code");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_plans_CompanyId_Status"
                    ON recurring_invoice_plans ("CompanyId", "Status");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_plans_CompanyId_BranchId"
                    ON recurring_invoice_plans ("CompanyId", "BranchId");
                """, ct);
        }

        // ‏جدولُ وحدات القياس — يُبذَر للمنشآت الجديدة في التهيئة،
        // ‏ويُنشأ فارغاً هنا للقواعد القائمة. **وفارغٌ يعني «بلا تحويل»** وهو سلوكُ ما مضى،
        // ‏فلا يتغيّر رقمٌ حتى تُعرِّف المنشأةُ وحداتِها.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS units_of_measure (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(16) NOT NULL,
                    "NameAr" character varying(64) NOT NULL,
                    "UnCode" character varying(8) NOT NULL,
                    "BaseCode" character varying(16) NOT NULL,
                    "Factor" NUMERIC(18,6) NOT NULL DEFAULT 1,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_units_of_measure_CompanyId_Code"
                    ON units_of_measure ("CompanyId", "Code");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS units_of_measure (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    NameAr TEXT NOT NULL,
                    UnCode TEXT NOT NULL,
                    BaseCode TEXT NOT NULL,
                    Factor TEXT NOT NULL DEFAULT '1',
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                """, ct);
        }

        // ‏أذونُ التسليم — جدولان جديدان فارغان.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS delivery_notes (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "SalesOrderId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "WarehouseId" BIGINT NOT NULL,
                    "DeliveryDate" DATE NOT NULL,
                    "Number" character varying(32) NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "StockMovementId" BIGINT NULL,
                    "JournalEntryId" BIGINT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_delivery_notes_CompanyId_Number" ON delivery_notes ("CompanyId", "Number");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_delivery_notes_SalesOrderId" ON delivery_notes ("SalesOrderId");
                """, ct);
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS delivery_note_lines (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "DeliveryNoteId" BIGINT NOT NULL REFERENCES delivery_notes ("Id") ON DELETE CASCADE,
                    "OrderLineIndex" INTEGER NOT NULL,
                    "ProductId" BIGINT NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL,
                    "UnitCost" NUMERIC(18,4) NOT NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_delivery_note_lines_DeliveryNoteId" ON delivery_note_lines ("DeliveryNoteId");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS delivery_notes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    SalesOrderId INTEGER NOT NULL,
                    CustomerId INTEGER NOT NULL,
                    WarehouseId INTEGER NOT NULL,
                    DeliveryDate TEXT NOT NULL,
                    Number TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    StockMovementId INTEGER NULL,
                    JournalEntryId INTEGER NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS IX_delivery_notes_CompanyId_Number ON delivery_notes (CompanyId, Number);
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS delivery_note_lines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeliveryNoteId INTEGER NOT NULL REFERENCES delivery_notes (Id) ON DELETE CASCADE,
                    OrderLineIndex INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity TEXT NOT NULL,
                    UnitCost TEXT NOT NULL
                );
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE INDEX IF NOT EXISTS IX_delivery_note_lines_DeliveryNoteId ON delivery_note_lines (DeliveryNoteId);
                """, ct);
        }

        // ‏خصمُ أمر البيع وعملتُه — للأوامر القائمة صفرٌ وريالٌ بسعر واحد، كما كانت.
        if (isPg)
        {
            await EnsurePgColumnAsync("sales_orders", "DocumentDiscountAmount", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("sales_orders", "CurrencyCode", "character varying(3) NOT NULL DEFAULT 'SAR'", ct);
            await EnsurePgColumnAsync("sales_orders", "ExchangeRate", "NUMERIC(18,6) NOT NULL DEFAULT 1", ct);
        }
        else
        {
            await EnsureSqliteColumnIfTableAsync("sales_orders", "DocumentDiscountAmount", "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnIfTableAsync("sales_orders", "CurrencyCode", "TEXT NOT NULL DEFAULT 'SAR'", ct);
            await EnsureSqliteColumnIfTableAsync("sales_orders", "ExchangeRate", "TEXT NOT NULL DEFAULT '1'", ct);
        }

        // ‏حجزُ المخزون لأوامر البيع — فارغٌ للقواعد القائمة (لا حجزَ لأوامرَ أُكِّدت قبله).
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS stock_reservations (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "SalesOrderId" BIGINT NOT NULL,
                    "ProductId" BIGINT NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL,
                    "ReleasedQuantity" NUMERIC(18,4) NOT NULL DEFAULT 0
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_stock_reservations_CompanyId_ProductId"
                    ON stock_reservations ("CompanyId", "ProductId");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_stock_reservations_SalesOrderId"
                    ON stock_reservations ("SalesOrderId");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS stock_reservations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    SalesOrderId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity TEXT NOT NULL,
                    ReleasedQuantity TEXT NOT NULL DEFAULT '0'
                );
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE INDEX IF NOT EXISTS IX_stock_reservations_CompanyId_ProductId
                    ON stock_reservations (CompanyId, ProductId);
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE INDEX IF NOT EXISTS IX_stock_reservations_SalesOrderId
                    ON stock_reservations (SalesOrderId);
                """, ct);
        }

        // ‏حساباتُ أدوار الترحيل لكلّ منشأة — فارغةٌ للقواعد القائمة، فتُرحِّل الخدماتُ على الأرقام الافتراضيّة كما كانت.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS posting_role_accounts (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Role" character varying(64) NOT NULL,
                    "AccountCode" character varying(16) NOT NULL
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_posting_role_accounts_CompanyId_Role"
                    ON posting_role_accounts ("CompanyId", "Role");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS posting_role_accounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Role TEXT NOT NULL,
                    AccountCode TEXT NOT NULL
                );
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS IX_posting_role_accounts_CompanyId_Role
                    ON posting_role_accounts (CompanyId, Role);
                """, ct);
        }

        // ‏سطورُ فوترة أوامر البيع (الفوترة الجزئية) — فارغةٌ تعني أنّ الأوامرَ القائمة تُفوتر كاملةً كما كانت.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_order_invoice_lines (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "SalesOrderId" BIGINT NOT NULL,
                    "SalesInvoiceId" BIGINT NOT NULL,
                    "LineIndex" INTEGER NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL,
                    "IsCancelled" BOOLEAN NOT NULL DEFAULT false
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_sales_order_invoice_lines_CompanyId_SalesOrderId"
                    ON sales_order_invoice_lines ("CompanyId", "SalesOrderId");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_sales_order_invoice_lines_SalesInvoiceId"
                    ON sales_order_invoice_lines ("SalesInvoiceId");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS sales_order_invoice_lines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    SalesOrderId INTEGER NOT NULL,
                    SalesInvoiceId INTEGER NOT NULL,
                    LineIndex INTEGER NOT NULL,
                    Quantity TEXT NOT NULL,
                    IsCancelled INTEGER NOT NULL DEFAULT 0
                );
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE INDEX IF NOT EXISTS IX_sales_order_invoice_lines_CompanyId_SalesOrderId
                    ON sales_order_invoice_lines (CompanyId, SalesOrderId);
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE INDEX IF NOT EXISTS IX_sales_order_invoice_lines_SalesInvoiceId
                    ON sales_order_invoice_lines (SalesInvoiceId);
                """, ct);
        }

        // ‏فئاتُ الأصناف — تُنشأ فارغةً، وفارغةٌ تعني «الحساباتُ العامّة» كما كان.
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS product_categories (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(128) NOT NULL,
                    "RevenueAccountId" BIGINT NULL,
                    "InventoryAccountId" BIGINT NULL,
                    "CogsAccountId" BIGINT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true
                );
                """, ct);
            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_product_categories_CompanyId_Code"
                    ON product_categories ("CompanyId", "Code");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS product_categories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    NameAr TEXT NOT NULL,
                    RevenueAccountId INTEGER NULL,
                    InventoryAccountId INTEGER NULL,
                    CogsAccountId INTEGER NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                """, ct);
            await SafeSqliteIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS IX_product_categories_CompanyId_Code
                    ON product_categories (CompanyId, Code);
                """, ct);
        }

        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS stock_batches (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "WarehouseId" BIGINT NOT NULL,
                    "ProductId" BIGINT NOT NULL,
                    "BatchNumber" character varying(64) NOT NULL,
                    "ExpiryDate" DATE NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """, ct);

            await SafePgIndexAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_stock_batches_CompanyId_WarehouseId_ProductId_BatchNumber"
                    ON stock_batches ("CompanyId", "WarehouseId", "ProductId", "BatchNumber");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_stock_batches_CompanyId_ProductId_ExpiryDate"
                    ON stock_batches ("CompanyId", "ProductId", "ExpiryDate");
                """, ct);
            await SafePgIndexAsync("""
                CREATE INDEX IF NOT EXISTS "IX_stock_batches_CompanyId_BranchId"
                    ON stock_batches ("CompanyId", "BranchId");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS stock_batches (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BranchId INTEGER NULL,
                    WarehouseId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    BatchNumber TEXT NOT NULL,
                    ExpiryDate TEXT NULL,
                    Quantity TEXT NOT NULL DEFAULT '0',
                    CreatedAtUtc TEXT NOT NULL
                );
                """, ct);
        }
    }
}
