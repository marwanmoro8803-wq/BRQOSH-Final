using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// ترقية تدريجية للمخطط — أعمدة وفهارس ناقصة فقط.
/// أسماء الأعمدة PascalCase لتتوافق مع EF Core EnsureCreated.
/// <para>‏<b>مجمَّدٌ</b> عند <see cref="FrozenAtMigration"/>: يُكمل القواعدَ القديمةَ إلى ذلك المستوى فقط، ولا يُضاف إليه
/// ‏جدولٌ ولا عمود — المخطّطُ الجديدُ هجرةُ EF وحدها (يحرسه <c>SchemaUpdaterIsFrozenTests</c>).</para>
/// </summary>
public partial class BrqoshSchemaUpdater(BrqoshDbContext db)
{
    /// <summary>‏آخرُ هجرةٍ يُغطّي المُرقِّعُ مخطّطَها — وما بعدها لا يصل إلا بـ<c>MigrateAsync</c>.</summary>
    public const string FrozenAtMigration = "20261028091642_AddPostingRoleAccounts";

    private readonly List<SchemaStepFailure> _suppressed = [];

    /// <summary>
    /// <b>ما ابتُلع أثناء ترقية المخطّط — ولا يُبتلع بلا أثر.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>ما كان:</b> خمسةَ عشرَ موضعاً في هذا الملفّ تكتب DDL داخل
    /// <c>try { … } catch { }</c> فارغ. <b>والتسامحُ مقصودٌ ولا يُنزَع</b>: جدولٌ قد
    /// يُنشئه <c>EnsureCreated</c> بعدُ، وعمودٌ قد يوجد سلفاً، وقاعدةٌ قديمةٌ جدّاً قد
    /// تنقصها أعمدةٌ يُرقّعها ما بعده.</para>
    ///
    /// <para><b>لكنّ فشلاً حقيقياً لا يُميَّز عن ذلك</b>: صلاحيةٌ ناقصة، أو قرصٌ ممتلئ،
    /// أو صياغةٌ لا يقبلها إصدارُ PostgreSQL. <b>فترقيةُ مخطّطٍ تفشل تُقرأ نجاحاً</b>،
    /// ويُقلع التطبيق ثمّ يسقط عند أوّل استعلامٍ يمسّ ما لم يُنشأ.</para>
    ///
    /// <para><b>فالتسامحُ باقٍ والصمتُ زال.</b> ويُقرأ هذا من شاشة حالة النظام ومن
    /// الاختبارات، <b>ويُكتب ملفّاً حين لا يكون فارغاً</b>.</para>
    /// </remarks>
    public IReadOnlyList<SchemaStepFailure> SuppressedFailures => _suppressed;

    /// <summary>
    /// فشلُ آخرِ ترقيةٍ جرت في هذه العملية — <b>لتقرأه شاشةُ الحالة</b>.
    /// </summary>
    /// <remarks>
    /// <b>وساكنٌ لأنّ المُرقِّع يُنشأ ويُرمى في كلّ نداء</b> — و<c>DatabaseSchemaService</c>
    /// يستدعيه من خمسة مواضع. <b>فحقلُ نسخةٍ لا يبلغ شاشةً تُفتح بعد الإقلاع بساعة.</b>
    /// </remarks>
    public static IReadOnlyList<SchemaStepFailure> LastRunFailures { get; private set; } = [];

    /// <summary>
    /// يكتب تقريرَ ما ابتُلع — <b>وحين لا يكون فارغاً وحدَه</b>.
    /// </summary>
    /// <remarks>
    /// <b>وملفٌّ يُكتب في كلّ إقلاعٍ ولو بلا فشلٍ يُدرَّب المستخدمُ على تجاهله</b> —
    /// فوجودُه هو الخبر. <b>ولا يرمي أبداً</b>: هذا يجري في مسار الإقلاع، <b>واستثناءٌ
    /// هنا يمنع تشغيلَ المنشأة لأجل تقرير</b>.
    /// </remarks>
    private void WriteReportIfAny()
    {
        if (_suppressed.Count == 0) return;

        try
        {
            // ‏(م-٥ · ن-٤٣) ‏**المجلّدُ يُسمّى من مالكه لا يُعاد بناؤه.** كان يُركَّب هنا
            // ‏حرفيّاً، فبقي التقليمُ في `CrashLog` جاهلاً بهذه العائلة.
            var directory = Operations.CrashLog.Directory;
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, $"schema-{DateTime.Now:yyyy-MM-dd_HHmmss}.log");

            var text = new System.Text.StringBuilder();
            text.AppendLine("BRQOSH — خطوات ترقية مخطّط ابتُلع فشلُها");
            text.AppendLine($"الوقت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"المحرك: {db.Database.ProviderName}");
            text.AppendLine(new string('─', 70));

            foreach (var failure in _suppressed)
                text.AppendLine(failure.ToString());

            File.WriteAllText(path, text.ToString(), new System.Text.UTF8Encoding(true));

            // ‏**وتُقلَّم عائلتُها فوراً** — ولا يُنتظر عطلٌ ليُشغّل التقليم: قاعدةٌ تفشل
            // ‏خطوتُها كلَّ إقلاعٍ قد لا تُعطب أبداً، فتنمو التقاريرُ بلا حدٍّ ولا سببَ يمحوها.
            Operations.CrashLog.PruneSchemaReports();
        }
        catch (Exception)
        {
            // ‏**ولا يرمي** — تقريرٌ يتعذّر لا يمنع الإقلاع.
        }
    }

    /// <summary>
    /// يُنفّذ خطوةَ مخطّطٍ متسامحة — <b>ويُسجّل فشلَها باسم الخطوة</b>.
    /// </summary>
    /// <param name="step">
    /// اسمُ الخطوة. <b>وهو ما يُضيّق البحث من خمسةَ عشرَ موضعاً إلى واحد</b> — ورسالةُ
    /// المحرّك وحدَها تقول «relation does not exist» ولا تقول أيّ خطوةٍ طلبتها.
    /// </param>
    /// <summary>
    /// مَنفذُ قياسٍ للمُجمِّع — <b>وفرعُ الفشل لا يُبلَغ من الخارج</b>.
    /// </summary>
    /// <remarks>
    /// <b>وترقيةُ قاعدةٍ سليمةٍ لا تُفشل خطوة</b>، <b>وقاعدةٌ فارغةٌ تماماً تُسقط
    /// <c>ApplyAsync</c> نفسَه</b> عند مسارٍ غيرِ متسامحٍ قبل أن تبلغ المُجمِّع. فبلا
    /// هذا المَنفذ <b>يبقى فرعُ التسجيل بلا قياس</b> — وقد أخرجت هذه الحملةُ ثلاثَ
    /// طفراتٍ نجت لهذا السبب بعينه.
    /// </remarks>
    internal Task<bool> TrySchemaSqlForTests(string step, string sql, CancellationToken ct) =>
        TrySchemaSqlAsync(step, sql, ct);

    /// <summary>
    /// يرفع وسمَ «نظاميّ» عن كلّ حسابٍ يقول القالبُ إنّه
    /// نظاميٌّ وقاعدةُ العميل تقول غيرَ ذلك.
    ///
    /// <para><b>والمصدرُ هو القالبُ نفسُه</b> لا قائمةٌ تُكتب بيد — فكلُّ ترقيةٍ لاحقةٍ
    /// تسري بنفسها. <b>ولا يُخفَّض وسمٌ أبداً</b>: الشرطُ اتّجاهٌ واحد، فحسابٌ رفعه
    /// المالكُ بنفسه لا يُمَسّ.</para>
    ///
    /// <para><b>والرموزُ من ثوابتنا لا من مُدخَل</b> — ومع ذلك تُفحص أنّها أرقامٌ قبل
    /// أن تدخل نصَّ الاستعلام، <b>فلا يُبنى SQL من قيمةٍ لم تُتحقَّق</b>.</para>
    /// </summary>
    private async Task PromoteSystemAccountsAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("accounts", isPg, ct))
            return;

        var codes = Domain.Accounting.SaudiChartOfAccounts.Template
            .Where(a => a.IsSystem && !a.IsGroup)
            .Select(a => a.Code)
            .Where(c => c.Length > 0 && c.All(char.IsAsciiDigit))
            .Distinct()
            .ToList();

        if (codes.Count == 0) return;

        var list = string.Join(", ", codes.Select(c => "'" + c + "'"));
        var yes = isPg ? "TRUE" : "1";
        var no = isPg ? "FALSE" : "0";
        var col = isPg ? "\"IsSystem\"" : "IsSystem";
        var code = isPg ? "\"Code\"" : "Code";

        await TrySchemaSqlAsync("Backfill.PromoteSystemAccounts",
            $"UPDATE accounts SET {col} = {yes} WHERE {code} IN ({list}) AND {col} = {no}", ct);
    }

    private async Task<bool> TrySchemaSqlAsync(string step, string sql, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            return true;
        }
        catch (Exception ex)
        {
            // ‏**ولا يُرمى** — التسامحُ هو السلوكُ المقصود منذ بناء هذا الملفّ،
            // ‏**وتغييرُه هنا يُسقط إقلاعَ كلّ قاعدةٍ قائمة**. والتسجيلُ وحده ما أُضيف.
            _suppressed.Add(new SchemaStepFailure(step, ex.GetType().Name, ex.Message));
            return false;
        }
    }
    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var isPg = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        // تاريخ إلغاء المستندات الضريبية على القواعد القائمة.
        // NULL يعني «أُلغي قبل تفعيل الميزة» — يتصرّف كالسلوك السابق تماماً، فلا
        // تتغيّر أرقام إقرارات قديمة بأثر رجعيّ مفاجئ.
        //
        // **موضعه هنا مقصود — قبل ترقيعات المزوّد.** تلك الترقيعات تُجري استعلامات
        // EF على كيانات المستندات (BackfillDocumentTotalsAsync مثلاً)، و EF يبني
        // SELECT بكل أعمدة النموذج — فعمود مفقود يُسقط الإقلاع كلّه بـ
        // 42703: column s.CancelledAt does not exist. الجداول الخمسة أساسية موجودة
        // قبل هذه المرحلة، وحارس TableExistsAsync يتخطّى القاعدة الجديدة التي
        // ينشئها EnsureCreated من النموذج (فالعمود فيها أصلاً).
        await EnsureCancelledAtColumnsAsync(isPg, ct);

        // هوية المنشأة النظامية — هنا للسبب نفسه المشروح أعلاه.
        await EnsureRegulatoryIdentityColumnsAsync(isPg, ct);

        // تتبّعُ الدفعة — **هنا للسبب نفسه، وقد وقعتُ فيه**.
        //
        // ‏وُضع أوّلاً بعد ترقيعات المزوّد فسقط الإقلاع بـ
        // ‏`42703: column p1.BatchNumber does not exist` — والتحذيرُ مكتوبٌ فوقه بأربعة
        // ‏أسطر، وسبقني إليه `Z-10` و`م1-١ب`. **فتعليقٌ يشرح فخّاً لا يمنع الوقوعَ فيه**،
        // ‏والذي منعه مسبارُ المخطّط في ثانية.
        await EnsureBatchTrackingAsync(isPg, ct);

        // سببُ الإعفاء — **قبل ترقيعات المزوّد للسبب نفسه
        // المشروح أعلاه**، لا بعدها مع الفئة الضريبية. وهو ما كشفه `SchemaCoverageProbe`:
        // وُضع أوّلاً مع `EnsureVatCategoryColumnsAsync` فسقط مسارُ الترقية بـ
        // `42703: column s1.TaxExemptionReason does not exist` — لأنّ ترقيعات المزوّد
        // تُجري استعلامات EF على المستندات، و EF يبني SELECT بكل أعمدة النموذج،
        // وأسطرُ المستندات مملوكةٌ فتُحمَّل معها دائماً.
        await EnsureExemptionReasonColumnsAsync(isPg, ct);

        // ‏(م-٥ · ن-٥١) تحويلُ الفئة الضريبيّة إلى نصّ — **وهذه وحدَها ترمي إن فشلت**،
        // ‏لأنّ نجاحاً كاذباً هنا يترك النموذجَ يقرأ نصّاً وقاعدةً تخزّن عدداً.
        await EnsureVatCategoryIsTextAsync(isPg, ct);

        // لحظةُ الترحيل — **قبل ترقيعات المزوّد** للسبب نفسه: تلك
        // الترقيعات تُجري استعلامات EF على المستندات، و EF يبني SELECT بكل أعمدة النموذج.
        await EnsurePostedAtColumnsAsync(isPg, ct);

        // ‏ ‏وصفةُ أمر الإنتاج — **قبل ترقيعات المزوّد للسبب نفسه**: بنودُها مملوكةٌ
        // ‏للأمر فتُحمَّل معه في كلّ استعلام، و EF يبني SELECT بكلّ أعمدة النموذج.
        await EnsureWorkOrderRecipeSchemaAsync(isPg, ct);

        // ‏ ‏حدُّ ائتمان العميل — **قبل ترقيعات المزوّد للسبب نفسه**: كلُّ استعلامٍ عن العميل
        // ‏يبني SELECT بكلّ أعمدة النموذج.
        await EnsureCustomerCreditLimitColumnAsync(isPg, ct);

        // ‏ ‏مستودعا أمر الإنتاج — **قبل ترقيعات المزوّد للسبب نفسه**: كلُّ استعلامٍ عن الأمر
        // ‏يبني SELECT بكلّ أعمدة النموذج.
        await EnsureWorkOrderWarehouseColumnsAsync(isPg, ct);

        // ‏ ‏مستودعُ فاتورة المبيعات — **قبل ترقيعات المزوّد للسبب نفسه**: كلُّ استعلامٍ عن الفاتورة
        // ‏يبني SELECT بكلّ أعمدة النموذج.
        await EnsureSalesInvoiceWarehouseColumnAsync(isPg, ct);

        // ‏ ‏مستودعُ الوردية والخطة الدورية — **قبل ترقيعات المزوّد للسبب نفسه**.
        await EnsurePosShiftAndRecurringPlanWarehouseColumnsAsync(isPg, ct);

        // ‏(تقويةُ الـAPI · S-5) آخرُ خطوة TOTP قُبلت — **قبل ترقيعات المزوّد للسبب نفسه**.
        await EnsureMfaLastAcceptedStepColumnAsync(isPg, ct);

        // ‏ الذممُ المفتوحةُ عند بدء التشغيل.
        await EnsureOpeningBalanceItemsTableAsync(isPg, ct);

        // ‏ فئتا مشتركي التأمينات وجدولُ النظام الجديد — بافتراضات النطاق لا أصفار.
        await EnsureGosiContributorGroupColumnsAsync(isPg, ct);
        await EnsureBankStatementBalanceColumnsAsync(isPg, ct);   // ‏(ح-١٥٤)
        await EnsureReconciledBankLineAsync(isPg, ct);   // ‏(ح-١٥٥)

        // ‏ ‏مستودعُ فاتورة المشتريات — **قبل ترقيعات المزوّد للسبب نفسه**.
        await EnsurePurchaseInvoiceWarehouseColumnAsync(isPg, ct);

        if (isPg)
            await ApplyPostgreSqlPatchesAsync(ct);
        else
            await ApplySqlitePatchesAsync(ct);

        // ‏**وسمُ «حسابٍ نظاميّ» يلحق القواعدَ القائمة.**
        //
        // ‏`DatabaseInitializer.SeedChartOfAccountsAsync` **يخرج مبكّراً إن وُجد حسابٌ
        // ‏واحد** — فالدليلُ يُزرع مرّةً عند التأسيس **ولا يُعاد مطابقتُه بالقالب أبداً**.
        // ‏فحسابٌ يُرقَّى اليومَ إلى «نظاميّ» في القالب **يبقى عاديّاً على كلّ تنصيبٍ
        // ‏قائم**، ويظلّ **قابلاً للحذف وإعادة التسمية** بينما صار محرّكُ الترحيل يعتمد
        // ‏عليه — فيُحذف يوماً وتسقط عمليّةُ ترحيلٍ برسالةٍ لا تدلّ على السبب.
        //
        // ‏**ووقع هذا الآن فعلاً**: `2104` «مصروفات مستحقة» كان عاديّاً، فلمّا صار
        // ‏مقابلَ التكلفة الإضافيّة غير المدفوعة **أسقط حارسُ الدليل الترقيةَ** —
        // ‏«الكود 2104 غير معلّم كحساب نظامي».
        //
        // ‏**والمطابقةُ من القالب لا من قائمةٍ تُكتب بيد**: كلُّ ترقيةٍ لاحقةٍ تسري
        // ‏بنفسها، فلا يُنسى تنصيبٌ قائم.
        await PromoteSystemAccountsAsync(isPg, ct);

        // فئة الضريبة لأسطر المستندات — بعد اكتمال إنشاء الجداول أعلاه،
        // فإضافة عمود إلى جدول لم يُنشأ بعد تفشل صراحةً (وهو الصواب: لا ابتلاع أخطاء).
        // الافتراضي Standard يحفظ سلوك كل المستندات القائمة.
        // قيدُ استحقاق الاستلام — **قبل ترقيعات المزوّد**، فتلك تُجري
        // ‏استعلامات EF على المستندات و EF يبني SELECT بكلّ أعمدة النموذج.
        if (await TableExistsAsync("goods_receipts", isPg, ct))
        {
            if (isPg)
                await EnsurePgColumnAsync("goods_receipts", "JournalEntryId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync("goods_receipts", "JournalEntryId", "INTEGER NULL", ct);
        }

        await EnsureVatCategoryColumnsAsync(isPg, ct);

        // أعمدة سلسلة تجزئة سجل التدقيق على القواعد القائمة.
        // Sequence=0 يعني «ما قبل تفعيل السلسلة» — يتخطاه المدقّق ولا يعدّه كسراً.
        await EnsureAuditChainColumnsAsync(isPg, ct);

        // المبيعات النقدية وحدها في ورديات نقطة البيع.
        // القيمة الافتراضية صفر على الورديات القديمة تعني «غير مُصنَّف» — ولا تُصلَح
        // بأثر رجعيّ عمداً: تصنيف مبيعات وردية مغلقة يحتاج بيانات لم تُحفظ حينها،
        // والافتراض الكاذب أسوأ من الصفر الصريح.
        await EnsurePosCashSalesColumnAsync(isPg, ct);

        // ‏(م-٤ · ن-٢٨) ‏عدّادُ المحاولات الخاطئة على تحدّيات الرمز — والصفرُ افتراضٌ صادق:
        // ‏تحدٍّ قديمٌ لم يُعدّ عليه شيءٌ **لم تقع عليه محاولاتٌ مسجَّلة**، لا أنّها ضاعت.
        await EnsureOtpAttemptColumnAsync(isPg, ct);

        // ‏ ‏**وأوامرُ صدرت قبل الترقية تُجمَّد وصفتُها** — آخراً، فأعمدةُ تكاليف
        // ‏التحويل على قائمة الموادّ (ح-١٣) قائمةٌ حينها على كلّ قاعدة.
        await FreezeOpenWorkOrderRecipesAsync(isPg, ct);

        // ‏**وما ابتُلع يُسجَّل ولا يُنسى.**
        //
        // ‏والتسامحُ باقٍ — لا يُرمى شيءٌ هنا. **والصمتُ وحده ما زال**: كان فشلُ خطوةٍ
        // ‏لا يُميَّز عن نجاحها، **فتُقلع القاعدةُ ثمّ تسقط عند أوّل استعلامٍ يمسّ ما
        // ‏لم يُنشأ**.
        LastRunFailures = _suppressed.AsReadOnly();
        WriteReportIfAny();
    }
}
