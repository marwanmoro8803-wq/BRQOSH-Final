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
/// </summary>
public class BrqoshSchemaUpdater(BrqoshDbContext db)
{
    private readonly List<SchemaStepFailure> _suppressed = [];

    /// <summary>
    /// (CEEF-231 · ط5-١٠) <b>ما ابتُلع أثناء ترقية المخطّط — ولا يُبتلع بلا أثر.</b>
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
    /// (CEEF-231 · ط5-١٠) فشلُ آخرِ ترقيةٍ جرت في هذه العملية — <b>لتقرأه شاشةُ الحالة</b>.
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
    /// (CEEF-231 · ط5-١٠) مَنفذُ قياسٍ للمُجمِّع — <b>وفرعُ الفشل لا يُبلَغ من الخارج</b>.
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
    /// (جاهزيّةُ الإطلاق · ح-٢٧) يرفع وسمَ «نظاميّ» عن كلّ حسابٍ يقول القالبُ إنّه
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

        // (O-21، CEEF-219) تاريخ إلغاء المستندات الضريبية على القواعد القائمة.
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

        // (CEEF-229 · Z-10) هوية المنشأة النظامية — هنا للسبب نفسه المشروح أعلاه.
        await EnsureRegulatoryIdentityColumnsAsync(isPg, ct);

        // (CEEF-230 · ط1-٣) تتبّعُ الدفعة — **هنا للسبب نفسه، وقد وقعتُ فيه**.
        //
        // ‏وُضع أوّلاً بعد ترقيعات المزوّد فسقط الإقلاع بـ
        // ‏`42703: column p1.BatchNumber does not exist` — والتحذيرُ مكتوبٌ فوقه بأربعة
        // ‏أسطر، وسبقني إليه `Z-10` و`م1-١ب`. **فتعليقٌ يشرح فخّاً لا يمنع الوقوعَ فيه**،
        // ‏والذي منعه مسبارُ المخطّط في ثانية.
        await EnsureBatchTrackingAsync(isPg, ct);

        // (CEEF-229 · م1-١ب الشقّ الثاني) سببُ الإعفاء — **قبل ترقيعات المزوّد للسبب نفسه
        // المشروح أعلاه**، لا بعدها مع الفئة الضريبية. وهو ما كشفه `SchemaCoverageProbe`:
        // وُضع أوّلاً مع `EnsureVatCategoryColumnsAsync` فسقط مسارُ الترقية بـ
        // `42703: column s1.TaxExemptionReason does not exist` — لأنّ ترقيعات المزوّد
        // تُجري استعلامات EF على المستندات، و EF يبني SELECT بكل أعمدة النموذج،
        // وأسطرُ المستندات مملوكةٌ فتُحمَّل معها دائماً.
        await EnsureExemptionReasonColumnsAsync(isPg, ct);

        // ‏(م-٥ · ن-٥١) تحويلُ الفئة الضريبيّة إلى نصّ — **وهذه وحدَها ترمي إن فشلت**،
        // ‏لأنّ نجاحاً كاذباً هنا يترك النموذجَ يقرأ نصّاً وقاعدةً تخزّن عدداً.
        await EnsureVatCategoryIsTextAsync(isPg, ct);

        // (CEEF-229 · م2-٢٤) لحظةُ الترحيل — **قبل ترقيعات المزوّد** للسبب نفسه: تلك
        // الترقيعات تُجري استعلامات EF على المستندات، و EF يبني SELECT بكل أعمدة النموذج.
        await EnsurePostedAtColumnsAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-٧٧) ‏وصفةُ أمر الإنتاج — **قبل ترقيعات المزوّد للسبب نفسه**: بنودُها مملوكةٌ
        // ‏للأمر فتُحمَّل معه في كلّ استعلام، و EF يبني SELECT بكلّ أعمدة النموذج.
        await EnsureWorkOrderRecipeSchemaAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-٩٥) ‏حدُّ ائتمان العميل — **قبل ترقيعات المزوّد للسبب نفسه**: كلُّ استعلامٍ عن العميل
        // ‏يبني SELECT بكلّ أعمدة النموذج.
        await EnsureCustomerCreditLimitColumnAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-٩٧) ‏مستودعا أمر الإنتاج — **قبل ترقيعات المزوّد للسبب نفسه**: كلُّ استعلامٍ عن الأمر
        // ‏يبني SELECT بكلّ أعمدة النموذج.
        await EnsureWorkOrderWarehouseColumnsAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-١١٠) ‏مستودعُ فاتورة المبيعات — **قبل ترقيعات المزوّد للسبب نفسه**: كلُّ استعلامٍ عن الفاتورة
        // ‏يبني SELECT بكلّ أعمدة النموذج.
        await EnsureSalesInvoiceWarehouseColumnAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-١١٤) ‏مستودعُ الوردية والخطة الدورية — **قبل ترقيعات المزوّد للسبب نفسه**.
        await EnsurePosShiftAndRecurringPlanWarehouseColumnsAsync(isPg, ct);

        // ‏(تقويةُ الـAPI · S-5) آخرُ خطوة TOTP قُبلت — **قبل ترقيعات المزوّد للسبب نفسه**.
        await EnsureMfaLastAcceptedStepColumnAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-١٣٤) الذممُ المفتوحةُ عند بدء التشغيل.
        await EnsureOpeningBalanceItemsTableAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-١٣٨) فئتا مشتركي التأمينات وجدولُ النظام الجديد — بافتراضات النطاق لا أصفار.
        await EnsureGosiContributorGroupColumnsAsync(isPg, ct);
        await EnsureBankStatementBalanceColumnsAsync(isPg, ct);   // ‏(ح-١٥٤)
        await EnsureReconciledBankLineAsync(isPg, ct);   // ‏(ح-١٥٥)

        // ‏(جاهزيّةُ الإطلاق · ح-١١١) ‏مستودعُ فاتورة المشتريات — **قبل ترقيعات المزوّد للسبب نفسه**.
        await EnsurePurchaseInvoiceWarehouseColumnAsync(isPg, ct);

        if (isPg)
            await ApplyPostgreSqlPatchesAsync(ct);
        else
            await ApplySqlitePatchesAsync(ct);

        // (جاهزيّةُ الإطلاق · ح-٢٧) ‏**وسمُ «حسابٍ نظاميّ» يلحق القواعدَ القائمة.**
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

        // (CEEF-207) فئة الضريبة لأسطر المستندات — بعد اكتمال إنشاء الجداول أعلاه،
        // فإضافة عمود إلى جدول لم يُنشأ بعد تفشل صراحةً (وهو الصواب: لا ابتلاع أخطاء).
        // الافتراضي Standard يحفظ سلوك كل المستندات القائمة.
        // (CEEF-229 · م1-١٩) قيدُ استحقاق الاستلام — **قبل ترقيعات المزوّد**، فتلك تُجري
        // ‏استعلامات EF على المستندات و EF يبني SELECT بكلّ أعمدة النموذج.
        if (await TableExistsAsync("goods_receipts", isPg, ct))
        {
            if (isPg)
                await EnsurePgColumnAsync("goods_receipts", "JournalEntryId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync("goods_receipts", "JournalEntryId", "INTEGER NULL", ct);
        }

        await EnsureVatCategoryColumnsAsync(isPg, ct);

        // (O-19، CEEF-212) أعمدة سلسلة تجزئة سجل التدقيق على القواعد القائمة.
        // Sequence=0 يعني «ما قبل تفعيل السلسلة» — يتخطاه المدقّق ولا يعدّه كسراً.
        await EnsureAuditChainColumnsAsync(isPg, ct);

        // (CEEF-228) المبيعات النقدية وحدها في ورديات نقطة البيع.
        // القيمة الافتراضية صفر على الورديات القديمة تعني «غير مُصنَّف» — ولا تُصلَح
        // بأثر رجعيّ عمداً: تصنيف مبيعات وردية مغلقة يحتاج بيانات لم تُحفظ حينها،
        // والافتراض الكاذب أسوأ من الصفر الصريح.
        await EnsurePosCashSalesColumnAsync(isPg, ct);

        // ‏(م-٤ · ن-٢٨) ‏عدّادُ المحاولات الخاطئة على تحدّيات الرمز — والصفرُ افتراضٌ صادق:
        // ‏تحدٍّ قديمٌ لم يُعدّ عليه شيءٌ **لم تقع عليه محاولاتٌ مسجَّلة**، لا أنّها ضاعت.
        await EnsureOtpAttemptColumnAsync(isPg, ct);

        // ‏(جاهزيّةُ الإطلاق · ح-٧٧) ‏**وأوامرُ صدرت قبل الترقية تُجمَّد وصفتُها** — آخراً، فأعمدةُ تكاليف
        // ‏التحويل على قائمة الموادّ (ح-١٣) قائمةٌ حينها على كلّ قاعدة.
        await FreezeOpenWorkOrderRecipesAsync(isPg, ct);

        // (CEEF-231 · ط5-١٠) ‏**وما ابتُلع يُسجَّل ولا يُنسى.**
        //
        // ‏والتسامحُ باقٍ — لا يُرمى شيءٌ هنا. **والصمتُ وحده ما زال**: كان فشلُ خطوةٍ
        // ‏لا يُميَّز عن نجاحها، **فتُقلع القاعدةُ ثمّ تسقط عند أوّل استعلامٍ يمسّ ما
        // ‏لم يُنشأ**.
        LastRunFailures = _suppressed.AsReadOnly();
        WriteReportIfAny();
    }

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

    /// <summary>جداول المستندات الضريبية التي تدخل الإقرار (CEEF-219).</summary>
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
    /// <para>(CEEF-229) كان يُضيف الأعمدةَ الثلاثة ويقف. فبقيت الصفوفُ القديمة كلُّها
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
    /// جداول أسطر المستندات التي تحمل فئة ضريبية (CEEF-207).
    ///
    /// <para>(م-٥ · ن-٥١) <b>كانت ستّةً وهي تسعة.</b> نوعُ السطر <c>SalesInvoiceLine</c> يملكه
    /// تسعةُ مستندات، <b>وثلاثةٌ منها غابت عن هذه القائمة</b>: عرضُ السعر وطلبُ الشراء
    /// وخطّةُ الفوترة الدوريّة. وهو درسُ «ثمانيةُ جداولٍ لا واحد» (CEEF-230) نفسُه، طُبِّق على
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
        // (جاهزيّةُ الإطلاق · ح-١٦) بيعُ الأصل: المشتري ورقمُ فاتورته ومقابلُه وفئتُه وسببُها.
        await EnsurePgColumnAsync("fixed_assets", "DisposalBuyerCustomerId", "BIGINT NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalInvoiceNumber", "character varying(64) NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalProceeds", "NUMERIC(18,2) NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalVatCategory", "character varying(16) NULL", ct);
        await EnsurePgColumnAsync("fixed_assets", "DisposalVatExemptionCode", "character varying(64) NULL", ct);
        // (جاهزيّةُ الإطلاق · ح-١٠٠) آخرُ يومٍ أُهلك قبل الدفاتر — لأصلٍ قائمٍ يُدخَل برصيده الافتتاحيّ.
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

        // (CEEF-232 · ل-١) ‏ربطُ فاتورة أجرة الشحن — **ولاغٍ افتراضاً**، فشحناتٌ سابقةٌ
        // ‏لم تُفوتَر تبقى غيرَ مفوترة، ولا يُكذَب على الماضي بربطٍ لم يقع.
        await EnsurePgColumnAsync("shipments", "FreightInvoiceId", "BIGINT NULL", ct);

        // (CEEF-232 · ع-١) ‏ربطُ العقار بأصلٍ ثابتٍ يُهلَك — **ولاغٍ افتراضاً**، فعقاراتٌ
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

        // (CEEF-229 · م2-١٠ / D-5) ‏آخرَ شيء: يُكنَس ما أنشأه المُرقِّعُ سابقاً بأسماءٍ
        // ‏تخالف النموذج، بعد أن صار يُسمّي بأسمائه فلا يعود يُنشئ تكراراً.
        await DropDuplicateUniqueIndexesAsync(ct);
        await EnsurePgColumnAsync("audit_log", "Reason", "character varying(512) NULL", ct);
        await EnsureV12LifecycleTablesAsync(isPg: true, ct);

        // (CEEF-230 · ط1-٣) ‏النداءُ الثاني — على ما أعادت المرحلةُ السابقة بناءَه.
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

        // (CEEF-229 · Z-4) الفهرس الفريد على عدّاد ZATCA — للقواعد القائمة كذلك.
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
        // (جاهزيّةُ الإطلاق · ح-١٦) بيعُ الأصل — انظر النظيرَ في مسار PostgreSQL أعلاه.
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalBuyerCustomerId", "INTEGER NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalInvoiceNumber", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalProceeds", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalVatCategory", "TEXT NULL", ct);
        await EnsureSqliteColumnAsync("fixed_assets", "DisposalVatExemptionCode", "TEXT NULL", ct);
        // (جاهزيّةُ الإطلاق · ح-١٠٠) آخرُ يومٍ أُهلك قبل الدفاتر — انظر النظيرَ في مسار PostgreSQL أعلاه.
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

        // (CEEF-230 · ط1-٣) ‏النداءُ الثاني — على ما أعادت المرحلةُ السابقة بناءَه.
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
    /// (CEEF-229 · م2-١٠ / D-5) ‏<b>كنسُ قيدِ تفرّدٍ أُنشئ مرّتين.</b>
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

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c>) <b>وصفةُ أمر الإنتاج — أعمدتُها وجدولُ بنودها.</b>
    ///
    /// <para><b>والجدولُ بمفتاحيه باسمَي EF</b> كما تُنشئه الهجرة — وإلّا بقيت بنودُ أمرٍ محذوفٍ معلّقةً بلا أب،
    /// وتعثّرت هجرةٌ قادمةٌ تُسمّي القيدَ باسمه.</para>
    /// </summary>
    private async Task EnsureWorkOrderRecipeSchemaAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("work_orders", isPg, ct))
            return;

        if (isPg)
        {
            await EnsurePgColumnAsync("work_orders", "RecipeLabourCostPerUnit", "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("work_orders", "RecipeOverheadRatePercent", "NUMERIC(9,4) NOT NULL DEFAULT 0", ct);
            await EnsurePgColumnAsync("work_orders", "RecipeFrozen", "BOOLEAN NOT NULL DEFAULT false", ct);
            if (await TrySchemaSqlAsync("WorkOrderRecipe.Table", """
                    CREATE TABLE IF NOT EXISTS work_order_materials (
                        id bigint GENERATED BY DEFAULT AS IDENTITY,
                        "ComponentProductId" bigint NOT NULL,
                        "Quantity" numeric(18,4) NOT NULL,
                        work_order_id bigint NOT NULL,
                        CONSTRAINT "PK_work_order_materials" PRIMARY KEY (id),
                        CONSTRAINT "FK_work_order_materials_work_orders_work_order_id" FOREIGN KEY (work_order_id)
                            REFERENCES work_orders ("Id") ON DELETE CASCADE
                    );
                    """, ct))
            {
                await TrySchemaSqlAsync("WorkOrderRecipe.Index",
                    """CREATE INDEX IF NOT EXISTS "IX_work_order_materials_work_order_id" ON work_order_materials (work_order_id);""", ct);
            }
        }
        else
        {
            await EnsureSqliteColumnAsync("work_orders", "RecipeLabourCostPerUnit", "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnAsync("work_orders", "RecipeOverheadRatePercent", "TEXT NOT NULL DEFAULT '0'", ct);
            await EnsureSqliteColumnAsync("work_orders", "RecipeFrozen", "INTEGER NOT NULL DEFAULT 0", ct);
            if (await TrySchemaSqlAsync("WorkOrderRecipe.Table", """
                    CREATE TABLE IF NOT EXISTS work_order_materials (
                        id INTEGER NOT NULL CONSTRAINT "PK_work_order_materials" PRIMARY KEY AUTOINCREMENT,
                        ComponentProductId INTEGER NOT NULL,
                        Quantity TEXT NOT NULL,
                        work_order_id INTEGER NOT NULL,
                        CONSTRAINT "FK_work_order_materials_work_orders_work_order_id" FOREIGN KEY (work_order_id)
                            REFERENCES work_orders (Id) ON DELETE CASCADE
                    );
                    """, ct))
            {
                await TrySchemaSqlAsync("WorkOrderRecipe.Index",
                    "CREATE INDEX IF NOT EXISTS IX_work_order_materials_work_order_id ON work_order_materials (work_order_id);", ct);
            }
        }
    }

    /// <summary>
    /// (ح-٧٧) <b>أوامرُ صدرت قبل الترقية تُجمَّد وصفتُها عند أوّل إقلاع</b> — من قائمة الموادّ كما هي لحظتَها، وهي عينُ
    /// ما كانت ستستهلكه لو أُكملت الآن؛ <b>فلا يتغيّر أمرٌ بالترقية، ويكفّ عن التغيّر بعدها</b>.
    ///
    /// <para><b>والمسوّداتُ لا تُجمَّد</b> — تتبع القائمةَ حتى تصدر. <b>والمكتملةُ لا تحتاج</b>: إلغاؤها يعكس حركاتِها
    /// بأعيانها لا الوصفة.</para>
    ///
    /// <para><b>ومُطاوِعةٌ للتكرار بلا معاملة</b>: تُحذف بنودُ كلّ صادرٍ لم يُجمَّد، ثمّ تُنسخ، ثمّ يُعلَّم — فإقلاعٌ
    /// انقطع بين خطوتين يُعيدها كاملةً ولا يُكرّر بنداً. <b>وكلُّ خطوةٍ تسقط تُسجَّل</b> في تقرير المُرقِّع، وأمرٌ
    /// بقي بلا وصفةٍ يرفض الإكمالَ برسالة.</para>
    /// </summary>
    private async Task FreezeOpenWorkOrderRecipesAsync(bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync("work_orders", isPg, ct)
            || !await TableExistsAsync("work_order_materials", isPg, ct)
            || !await TableExistsAsync("bom_lines", isPg, ct))
            return;

        var steps = isPg
            ? new[]
            {
                """
                DELETE FROM work_order_materials WHERE work_order_id IN
                    (SELECT "Id" FROM work_orders WHERE "Status" = 'Released' AND NOT "RecipeFrozen");
                """,
                """
                INSERT INTO work_order_materials ("ComponentProductId", "Quantity", work_order_id)
                SELECT l."ComponentProductId", l."Quantity", w."Id"
                FROM work_orders w JOIN bom_lines l ON l.bill_of_materials_id = w."BomId"
                WHERE w."Status" = 'Released' AND NOT w."RecipeFrozen";
                """,
                """
                UPDATE work_orders w SET
                    "RecipeLabourCostPerUnit" = b."LabourCostPerUnit",
                    "RecipeOverheadRatePercent" = b."OverheadRatePercent",
                    "RecipeFrozen" = TRUE
                FROM bills_of_materials b
                WHERE b."Id" = w."BomId" AND w."Status" = 'Released' AND NOT w."RecipeFrozen";
                """,
            }
            : new[]
            {
                """
                DELETE FROM work_order_materials WHERE work_order_id IN
                    (SELECT Id FROM work_orders WHERE Status = 'Released' AND RecipeFrozen = 0);
                """,
                """
                INSERT INTO work_order_materials (ComponentProductId, Quantity, work_order_id)
                SELECT l.ComponentProductId, l.Quantity, w.Id
                FROM work_orders w JOIN bom_lines l ON l.bill_of_materials_id = w.BomId
                WHERE w.Status = 'Released' AND w.RecipeFrozen = 0;
                """,
                """
                UPDATE work_orders SET
                    RecipeLabourCostPerUnit = (SELECT b.LabourCostPerUnit FROM bills_of_materials b WHERE b.Id = work_orders.BomId),
                    RecipeOverheadRatePercent = (SELECT b.OverheadRatePercent FROM bills_of_materials b WHERE b.Id = work_orders.BomId),
                    RecipeFrozen = 1
                WHERE Status = 'Released' AND RecipeFrozen = 0
                  AND EXISTS (SELECT 1 FROM bills_of_materials b WHERE b.Id = work_orders.BomId);
                """,
            };

        var names = new[] { "WorkOrderRecipe.Freeze.Clear", "WorkOrderRecipe.Freeze.Copy", "WorkOrderRecipe.Freeze.Mark" };
        for (var i = 0; i < steps.Length; i++)
        {
            if (!await TrySchemaSqlAsync(names[i], steps[i], ct))
                return;
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

    private async Task EnsureSqliteColumnIfTableAsync(string table, string column, string type, CancellationToken ct)
    {
        SchemaSqlGuard.ValidateTable(table);
        FormattableString exists = $"""
            SELECT name AS "Value" FROM sqlite_master WHERE type = 'table' AND name = {table}
            """;
        if ((await db.Database.SqlQuery<string>(exists).ToListAsync(ct)).Count > 0)
            await EnsureSqliteColumnAsync(table, column, type, ct);
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

    /// <summary>
    /// (CEEF-229 · Z-10) أعمدة هوية المنشأة النظامية — تُضاف للقواعد القائمة بنفس أسماء
    /// النموذج، وإلا خرج المولّد بحقولٍ فارغة على كل تركيبٍ سابق.
    /// </summary>
    /// <para><b>قابلية العدم ليست تفصيلاً — إنها تتبع النموذج حرفاً بحرف.</b>
    /// <c>RegistrationName</c> و<c>CommercialRegistrationNumber</c> نوعهما <c>string?</c>،
    /// والعدم فيهما يعني «لم يُدخَل»، فعمودهما يجب أن يقبل العدم. أمّا حقول العنوان فنوعها
    /// <c>string</c> غير قابل للعدم داخل كائن القيمة، ففارغُها هو «لم يُدخَل» وعمودها لا يقبل
    /// العدم. عكسُ أيٍّ منهما يُسقط النظام: <c>NOT NULL</c> على الأولَين يمنع إنشاء أيّ منشأة،
    /// و<c>NULL</c> على الأخيرة يُسقط تجسيد أيّ منشأة قائمة.</para>
    internal static readonly (string Column, string PgType, bool Nullable)[] RegulatoryIdentityColumns =
    [
        ("RegistrationName", "character varying(256) NULL", true),
        ("CommercialRegistrationNumber", "character varying(64) NULL", true),
        ("AddrBuildingNumber", "character varying(16) NOT NULL DEFAULT ''", false),
        ("AddrAdditionalNumber", "character varying(16) NOT NULL DEFAULT ''", false),
        ("AddrStreetName", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrDistrictName", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrCityName", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrPostalZone", "character varying(16) NOT NULL DEFAULT ''", false),
        ("AddrCountrySubentity", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrCountryCode", "character varying(16) NOT NULL DEFAULT ''", false),
    ];

    /// <summary>
    /// (CEEF-229 · Z-10) أعمدة هوية المنشأة النظامية على القواعد القائمة.
    ///
    /// <para><b>موضع الاستدعاء حاسم — في المرحلة المبكّرة قبل ترقيعات المزوّد،</b> للسبب نفسه
    /// المشروح عند <see cref="EnsureCancelledAtColumnsAsync"/>: تلك الترقيعات تُجري استعلامات EF
    /// على <c>Companies</c>، و EF يبني <c>SELECT</c> بكل أعمدة النموذج — فعمودٌ مفقود يُسقط
    /// الإقلاع كلّه بـ <c>42703: column c.CommercialRegistrationNumber does not exist</c>.
    /// وضعتُها أوّل مرّة داخل ترقيعات المزوّد فوقع هذا بالضبط على قاعدة PostgreSQL حيّة.</para>
    ///
    /// <para>وأنواع الأعمدة تُكتب بإملاء <c>character varying(N)</c> لا <c>VARCHAR(N)</c> —
    /// فقائمة <see cref="SchemaSqlGuard"/> البيضاء حرفية، ترفض ما لا تعرفه بدل أن تمرّره.</para>
    /// </summary>
    private async Task EnsureRegulatoryIdentityColumnsAsync(bool isPg, CancellationToken ct)
    {
        await EnsureRegulatoryColumnsOnAsync("companies", RegulatoryIdentityColumns, isPg, ct);

        // (CEEF-229 · م1-١ب) وطرفُ المشتري يأخذ الشكلَ نفسه — الاسمُ المسجَّل والعنوانُ الوطنيّ.
        // ورقمُ السجل التجاريّ لا يُطلب من المشتري: رقمُه الضريبيّ هو معرّفه.
        await EnsureRegulatoryColumnsOnAsync("customers", BuyerIdentityColumns, isPg, ct);
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب) أعمدةُ هوية المشتري — الشكلُ نفسه بلا رقم سجلٍّ تجاريّ.
    /// وقابليةُ العدم تتبع القاعدة نفسها المشروحة عند <see cref="RegulatoryIdentityColumns"/>.
    /// </summary>
    internal static readonly (string Column, string PgType, bool Nullable)[] BuyerIdentityColumns =
    [
        ("RegistrationName", "character varying(256) NULL", true),
        ("AddrBuildingNumber", "character varying(16) NOT NULL DEFAULT ''", false),
        ("AddrAdditionalNumber", "character varying(16) NOT NULL DEFAULT ''", false),
        ("AddrStreetName", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrDistrictName", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrCityName", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrPostalZone", "character varying(16) NOT NULL DEFAULT ''", false),
        ("AddrCountrySubentity", "character varying(128) NOT NULL DEFAULT ''", false),
        ("AddrCountryCode", "character varying(16) NOT NULL DEFAULT ''", false),
    ];

    /// <summary>يُنشئ أعمدةَ هويةٍ نظامية على جدولٍ ما ويُطابق قابليةَ عدمها.</summary>
    private async Task EnsureRegulatoryColumnsOnAsync(
        string table, (string Column, string PgType, bool Nullable)[] columns, bool isPg, CancellationToken ct)
    {
        if (!await TableExistsAsync(table, isPg, ct))
            return;

        foreach (var (column, pgType, nullable) in columns)
        {
            if (isPg)
                await EnsurePgColumnAsync(table, column, pgType, ct);
            else
                await EnsureSqliteColumnAsync(
                    table, column, nullable ? "TEXT NULL" : "TEXT NOT NULL DEFAULT ''", ct);

            await AlignRegulatoryColumnNullabilityAsync(table, column, nullable, isPg, ct);
        }
    }

    /// <summary>
    /// (CEEF-229 · Z-10) مطابقة قابلية العدم على قاعدةٍ قائمة — <b>وإصلاح ما أفسدته نسخةٌ
    /// سابقة من هذا الترقيع نفسه</b>.
    ///
    /// <para><c>ADD COLUMN IF NOT EXISTS</c> لا يمسّ عموداً موجوداً، فتغييرُ نوعٍ في مصفوفة
    /// الأعمدة لا يصل إلى قاعدةٍ رُقِّعت من قبل. ولذلك تُطابَق القابلية صراحةً وفي كل تشغيل:
    /// العملية عديمة الأثر إن كانت القاعدة مطابقة أصلاً، ومُصلِحة إن لم تكن.</para>
    ///
    /// <para>الحالتان وقعتا فعلاً في هذا البند: عمودُ عنوانٍ قابل للعدم أسقط تجسيد المنشآت بـ
    /// <c>InvalidCastException: Column 'AddrBuildingNumber' is null</c> على قاعدة حيّة، ثم
    /// <c>NOT NULL</c> على <c>RegistrationName</c> منع إدراج أيّ منشأة بـ
    /// <c>23502: null value in column "RegistrationName"</c> على قاعدةٍ نظيفة — والثاني لم
    /// يكشفه إلا مسبار الهجرات، فاختباراتُ القاعدة الحيّة تقرأ ولا تُدرج.</para>
    ///
    /// <para>على SQLite تُعبَّأ القيم العادمة فقط: المحرّك لا يدعم <c>ALTER COLUMN</c>، وما
    /// يُسقط التجسيد هو القيمة العادمة لا وسمُ العمود.</para>
    /// </summary>
    private async Task AlignRegulatoryColumnNullabilityAsync(
        string table, string column, bool nullable, bool isPg, CancellationToken ct)
    {
        // المعرّف يمرّ بحارس النمط قبل أي تركيب — وهو الشرط الذي يسمح بـ ExecuteSqlRawAsync هنا:
        // اسم عمودٍ لا يمكن أن يكون معاملاً مرتبطاً، فالحماية بالتحقق لا بالربط.
        SchemaSqlGuard.ValidateColumn(column);

        if (!isPg)
        {
            if (!nullable)
            {
                var fill = $"UPDATE {table} SET {column} = '' WHERE {column} IS NULL;";
                await db.Database.ExecuteSqlRawAsync(fill, ct);
            }
            return;
        }

        if (nullable)
        {
            var dropNotNull = $"""ALTER TABLE {table} ALTER COLUMN "{column}" DROP NOT NULL;""";
            var dropDefault = $"""ALTER TABLE {table} ALTER COLUMN "{column}" DROP DEFAULT;""";
            await db.Database.ExecuteSqlRawAsync(dropNotNull, ct);
            await db.Database.ExecuteSqlRawAsync(dropDefault, ct);
            return;
        }

        var fillNulls = $"""UPDATE {table} SET "{column}" = '' WHERE "{column}" IS NULL;""";
        var setDefault = $"""ALTER TABLE {table} ALTER COLUMN "{column}" SET DEFAULT '';""";
        var setNotNull = $"""ALTER TABLE {table} ALTER COLUMN "{column}" SET NOT NULL;""";
        await db.Database.ExecuteSqlRawAsync(fillNulls, ct);
        await db.Database.ExecuteSqlRawAsync(setDefault, ct);
        await db.Database.ExecuteSqlRawAsync(setNotNull, ct);
    }

    private static readonly string[] SoftDeleteTables =
    [
        "customers", "suppliers", "products", "warehouses", "cost_centers",
        "accounts", "bank_accounts", "construction_projects", "bills_of_materials", "app_users",
    ];

    /// <summary>عكسُ السند يَسِم سطر التخصيص معكوساً بدل حذفه — عمودان إضافيّان فقط، بلا حذف ولا تعبئة.
    /// والسطورُ التي حُذفت سابقاً تبقى كما هي.</summary>
    private async Task ApplyPaymentAllocationReversalColumnsAsync(bool isPg, CancellationToken ct)
    {
        if (isPg)
        {
            await EnsurePgColumnAsync("payment_allocation_lines", "IsReversed", "BOOLEAN NOT NULL DEFAULT false", ct);
            await EnsurePgColumnAsync("payment_allocation_lines", "ReversedAt", "TIMESTAMPTZ NULL", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("payment_allocation_lines", "IsReversed", "INTEGER NOT NULL DEFAULT 0", ct);
            await EnsureSqliteColumnAsync("payment_allocation_lines", "ReversedAt", "TEXT NULL", ct);
        }
    }

    private async Task ApplySoftDeleteColumnsAsync(bool isPg, CancellationToken ct)
    {
        foreach (var table in SoftDeleteTables)
        {
            if (isPg)
                await EnsurePgColumnAsync(table, "DeletedAtUtc", "TIMESTAMPTZ NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "DeletedAtUtc", "TEXT NULL", ct);

            // (CEEF-229 · م0-١٣) التعبئة أُوقفت — كانت خاملةً ثم صارت خطراً.
            //
            // كانت تُنفّذ في **كل إقلاع**: UPDATE … SET DeletedAtUtc = NOW() WHERE IsActive = false.
            // وقد بقيت بلا أثر لأن كل دوال Deactivate() كانت تضبط الحقلين معاً. ولحظةَ فصلنا
            // «الإيقاف» عن «الحذف» في Account، انقلبت إلى ما يُبطل الإصلاح: كل حساب يوقفه
            // المحاسب يعود محذوفاً ناعماً عند الإقلاع التالي، ويستحيل الإقفال السنويّ من جديد.
            //
            // وهي ترقيعُ هجرةٍ لمرّة واحدة لا حارسُ سلامة: أُنجزت على كل قاعدة قائمة منذ إضافتها،
            // فإسقاطها لا يترك صفّاً غير مُرحَّل. أمّا إضافة العمود نفسه فتبقى.
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
    /// (CEEF-206) كان يحمّل **كل** الفواتير والإشعارات والأوامر بسطورها المملوكة إلى
    /// الذاكرة في كل إقلاع ويجري تتبّع تغييرات عليها — يتناقض مع هدف «إقلاع أقل من
    /// 3 ثوانٍ» ويتحوّل إلى دقائق مع نمو البيانات. الآن يُرشَّح على ما يحتاج المعالجة
    /// فعلاً (إجمالي صفر مع وجود سطور)، فالحالة الشائعة استعلام يعود فارغاً.
    /// </summary>
    /// <summary>
    /// (CEEF-229 · م2-١١ / D-4) إعادةُ حساب إجماليات المستندات التي لم تُحفظ إجمالياتُها قطّ —
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
    /// (CEEF-229) ‏<b>يقارن ما أصلحه الإقلاعُ بما يقوله القيدُ المرحَّل.</b>
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

            // (CEEF-229 · م2-٩) سطورُ عرض السعر — كان الأبُ يُنشأ وحده والابنُ منسيّاً.
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

            // (CEEF-229 · م2-٩) سطورُ أمر البيع — الابنُ المنسيّ الثاني. وVatCategory نصٌّ هنا
            // ‏لأنّ النموذج يُعلن HasConversion<string>() عليه (CEEF-207).
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

            // (CEEF-229 · م2-٩) الابنُ المنسيّ — انظر تعليق فرع PostgreSQL.
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

            // (CEEF-229 · م2-٩) الابنُ المنسيّ — انظر تعليق فرع PostgreSQL.
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

    /// <summary>
    /// (CEEF-230 · ط1-٣) <b>تتبّعُ الدفعة والصلاحية والرقم التسلسليّ — على قاعدةٍ قائمة.</b>
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
        // (CEEF-230 · ط1-٣) ‏**ثمانيةُ جداولٍ لا واحد.** `SalesInvoiceLine` سجلٌّ
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
                // (CEEF-230 · ط1-٢) ‏نسبةُ خصم السطر — على الثمانية أنفسِها، فالسجلُّ مشترك.
                await EnsurePgColumnAsync(table, "DiscountPercent", "NUMERIC(9,4) NOT NULL DEFAULT 0", ct);
                // (CEEF-230 · ط1-١) ‏وحدةُ السطر — على الثمانية أنفسِها للسبب نفسِه.
                await EnsurePgColumnAsync(table, "UnitCode", "character varying(16) NULL", ct);
                // ‏(جاهزيّةُ الإطلاق · ح-١٠٨) ‏انتهاءُ الدفعة المشتراة — لفاتورة الشراء وحدَها.
                if (table == "purchase_invoice_lines")
                    await EnsurePgColumnAsync(table, "ExpiryDate", "DATE NULL", ct);
            }
            else
            {
                await EnsureSqliteColumnAsync(table, "BatchNumber", "TEXT NULL", ct);
                await EnsureSqliteColumnAsync(table, "DiscountPercent", "TEXT NOT NULL DEFAULT '0'", ct);
                await EnsureSqliteColumnAsync(table, "UnitCode", "TEXT NULL", ct);
                // ‏(جاهزيّةُ الإطلاق · ح-١٠٨) ‏انتهاءُ الدفعة المشتراة — لفاتورة الشراء وحدَها.
                if (table == "purchase_invoice_lines")
                    await EnsureSqliteColumnAsync(table, "ExpiryDate", "TEXT NULL", ct);
            }
        }

        // (CEEF-230 · ط1-٧) ‏مركزُ تكلفة المستند على فاتورة المشتريات وسندِ الصرف.
        foreach (var table in new[] { "purchase_invoices", "payment_vouchers" })
        {
            if (!await TableExistsAsync(table, isPg, ct)) continue;

            if (isPg)
                await EnsurePgColumnAsync(table, "CostCenterId", "BIGINT NULL", ct);
            else
                await EnsureSqliteColumnAsync(table, "CostCenterId", "INTEGER NULL", ct);
        }

        // (CEEF-230 · ط4-٥) ‏عملةُ المشتريات والسداد — **وكان جانبُ الموردين بلا عملةٍ
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
                // (CEEF-231 · ط1-٢ج) ‏خصمُ السداد المكتسَب.
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

        // (CEEF-230 · ط1-٤) ‏عملةُ سند القبض.
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
                // (CEEF-231 · ط1-٢ج) ‏خصمُ السداد المسموح به.
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

        // (CEEF-231 · O-32) ‏علامةُ «على مستوى المنشأة» — والافتراضُ `false`،
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

        // (CEEF-231 · O-34) ‏مذكرةُ العملة على سطر القيد — **لاغيةٌ لكلّ سطرٍ قائم**،
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
                // (CEEF-230 · ط1-٤) ‏عملةُ المستند — والافتراضُ ريالٌ بسعرِ واحد،
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

        // (جاهزيّةُ الإطلاق · ح-٣٩) ‏**وعملةُ الإشعار المدين** — مرآةُ ح-٣٥.
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

        // ‏(جاهزيّةُ الإطلاق · ح-١٣) ‏**وتكاليفُ التحويل على قائمة الموادّ.**
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

        // ‏(جاهزيّةُ الإطلاق · ح-١٢) ‏**ووسمُ أنّ قيدَ تحصيل الدفع عند الاستلام وقع.**
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

        // ‏(جاهزيّةُ الإطلاق · ح-١٠) ‏**وأسكنيٌّ عقدُ الإيجار أم تجاريّ.**
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

        // ‏(جاهزيّةُ الإطلاق · ح-٦) ‏**وخصمُ المستند على الإشعار الدائن.**
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

                // (جاهزيّةُ الإطلاق · ح-٣٥) ‏عملةُ الإشعار — **والافتراضُ ريالٌ
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

            // (جاهزيّةُ الإطلاق · ح-٣٥) ‏**وتعبئةُ الأجنبيّ من الريال لكلّ
            // ‏إشعارٍ قائم** — **وتركُ الأجنبيّ صفراً يجعل `RecalculateTotals`
            // ‏تُصيّر الريالَ صفراً عند أوّل مسّ**، فيُفقَد مبلغٌ قائم.
            // ‏**وتعبئةٌ بقيمةٍ مساويةٍ لا تُغيّر معنىً** — بسعرِ واحدٍ هما رقمٌ واحد.
            var creditBackfill = isPg
                ? """UPDATE credit_notes SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;"""
                : """UPDATE credit_notes SET ForeignSubtotal = Subtotal, ForeignVatAmount = VatAmount WHERE CAST(ForeignSubtotal AS REAL) = 0 AND CAST(Subtotal AS REAL) <> 0;""";
            if (isPg) await SafePgIndexAsync(creditBackfill, ct);
            else await SafeSqliteIndexAsync(creditBackfill, ct);

            // (CEEF-230 · ط1-٤) ‏**وتعبئةُ الأجنبيّ من الريال لكلّ فاتورةٍ قائمة**: بسعرِ
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

        // (CEEF-230 · ط3-٤) ‏البوالصُ والتحصيلُ نيابةً عن التجّار.
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

        // (CEEF-230 · ط3-٣ب) ‏الغرفُ والحجوزُ وحساباتُ النزلاء.
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

        // (CEEF-230 · ط3-٣) ‏قائمةُ الطعام ووصفاتُها.
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

            // (CEEF-230 · ط3-٣ج) ‏**والقاعدةُ القائمة تُرقَّع**: الجدولُ أُنشئ قبل الربط،
            // ‏و<c>CREATE TABLE IF NOT EXISTS</c> لا يمسّ جدولاً موجوداً — **فالعمودُ لا يصل
            // ‏إلا بترقيعٍ صريح**.
            await EnsurePgColumnAsync("menu_items", "SaleProductId", "BIGINT NULL", ct);
            await SafePgIndexAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_menu_items_CompanyId_SaleProductId" ON menu_items ("CompanyId", "SaleProductId");""", ct);
        }
        else
        {
            await EnsureSqliteColumnAsync("menu_items", "SaleProductId", "INTEGER NULL", ct);
        }

        // (CEEF-230 · ط4-٣ج) ‏مهلةُ السداد — وبها يصير التنبّؤ تنبّؤاً.
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

        // (CEEF-230 · ط3-٤ج) ‏أثرُ تتبّع الشحنة — سندُ التسليم.
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

        // (CEEF-230 · ط3-١ب و ط3-٣د و ط3-٤ب) ‏الصيانةُ والإكرامياتُ ورحلاتُ التوصيل.
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

        // (CEEF-230 · ط3-٢ب) ‏دفعاتُ المطالبات وقراراتُ الشركة عليها.
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

        // (CEEF-230 · ط3-٢) ‏المرضى وشركاتُ التأمين والوثائقُ والزيارات.
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

        // (CEEF-231 · ط3-٢ج) ‏**رقمُ دفعة الدواء المصروف — على قاعدةٍ قائمةٍ كذلك.**
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

        // (CEEF-230 · ط3-١) ‏العقاراتُ والوحداتُ وعقودُ الإيجار.
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

        // (CEEF-230 · ط2) ‏سجلُّ الموظفين ونِسَبُ التأمينات.
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

            // (CEEF-230 · ط2-٥) ‏قسطُ السُّلفة المُستقطَع — وبغيره يقبض الموظّفُ راتبَه
            // ‏كاملاً وقد أخذ سُلفتَه.
            await EnsurePgColumnAsync("payroll_lines", "AdvanceDeduction",
                "NUMERIC(18,2) NOT NULL DEFAULT 0", ct);

            // (CEEF-233 · ز-٦) ‏خصمُ الغياب بلا أجر — **ويُنقص المصروف لا الصافيَ وحدَه**.
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

            // (CEEF-233 · ز-٢) ‏طلباتُ الإجازة والغياب.
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

        // (CEEF-230 · ط1-٦) ‏الموازنة.
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

        // (CEEF-230 · ط1-٥) ‏خططُ الفوترة الدورية وأثرُ تكراراتها.
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

        // (CEEF-230 · ط1-١) ‏جدولُ وحدات القياس — يُبذَر للمنشآت الجديدة في التهيئة،
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
