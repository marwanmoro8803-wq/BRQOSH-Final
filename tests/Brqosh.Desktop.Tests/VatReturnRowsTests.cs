using Brqosh.Application.Reporting;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م1-١٠ / Z-12) <b>ما تعرضه الشاشةُ فعلاً — لا ما يحسبه الاستعلام.</b>
///
/// <para>التفكيكُ الضريبيّ كان محسوباً منذ A-15 و<b>لا يصل سطحاً واحداً</b>. وسببُه البنيويّ
/// أنّ الشاشةَ تبني صفوفَها داخل معالجِ ضغطٍ يحتاج قاعدةَ بيانات وخيطَ واجهة — فلا يُقاس ما
/// تعرضه إلا بتشغيل التطبيق، ولم يُقَس قطّ.</para>
///
/// <para>فيُقاس هنا **الناتجُ المعروض**: صفٌّ لكلّ فئة تحت الإجمالي، ومجموعُ الصفوف الفرعية
/// يساوي الإجماليَّ فوقها. <b>وصندوقٌ يُسقط سطراً يُكسر هذه الهوية.</b></para>
/// </summary>
public class VatReturnRowsTests
{
    private static VatReturnDto MixedQuarter() => new(
        new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
        SalesSubtotal: 1_400_000m, OutputVat: 150_000m,
        ProgressBillingsSubtotal: 0m, ProgressBillingsVat: 0m,
        CreditNotesSubtotal: 0m, CreditNotesVat: 0m,
        PurchasesSubtotal: 0m, DeductibleInputVat: 0m,
        DebitNotesSubtotal: 0m, DebitNotesVat: 0m, DebitNotesDeductibleVat: 0m,
        CancelledOutputVat: 0m, CancelledInputVat: 0m,
        JournalOutputVat: 0m, JournalInputVat: 0m,
        SalesBaseNet: 1_400_000m, PurchasesBaseNet: 0m,
        GlVatNetPayableBalance: null,
        SalesByCategory:
        [
            new VatReturnCategoryDto("S", 1_000_000m, 150_000m),
            new VatReturnCategoryDto("Z", 400_000m, 0m),
        ]);

    /// <summary><b>القياس المركزيّ.</b> الشاشةُ تعرض صفّاً لكلّ فئة، لا مجموعاً واحداً.</summary>
    [Fact]
    public void TheScreen_ShowsALinePerCategory()
    {
        var rows = VatReturnRows.Build(MixedQuarter());

        var breakdown = rows.Where(r => r.Indented).ToList();
        Assert.True(breakdown.Count >= 2,
            $"سطورُ التفكيك {breakdown.Count} — والشاشةُ تعرض مجموعاً واحداً بنسبةٍ مخلوطة. "
            + string.Join(" · ", rows.Select(r => r.Label)));

        Assert.Contains(breakdown, r => r.Label.Contains("(S)", StringComparison.Ordinal) && r.Value == 1_000_000m);
        Assert.Contains(breakdown, r => r.Label.Contains("(Z)", StringComparison.Ordinal) && r.Value == 400_000m);
    }

    /// <summary>
    /// <b>وهويةُ العرض:</b> مجموعُ سطور «منها» تحت صافي وعاء المبيعات == الصافي نفسُه.
    /// فسطرٌ يسقط أو يتكرّر يُكسر هذه المساواة — ولا يكشفه عدُّ السطور وحده.
    ///
    /// <para>(ح-٤١) <b>والأبُ تغيّر</b>: كان «مبيعات خاضعة (وعاء)» — <b>الفواتيرُ وحدَها قبل
    /// الإشعارات</b> — والأوعيةُ صافيةٌ من الإشعارات. فتطابقا هنا لأنّ هذا المُثبِّت بلا إشعار.</para>
    /// </summary>
    [Fact]
    public void TheBreakdownRows_SumToTheTotalAboveThem()
    {
        var dto = MixedQuarter();
        var rows = VatReturnRows.Build(dto);

        Assert.Equal(dto.SalesBaseNet, ParentOf(rows, "صافي وعاء المبيعات (من المستندات)"));
        Assert.Equal(dto.SalesBaseNet, ChildrenOf(rows, "صافي وعاء المبيعات (من المستندات)"));
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤١) <b>والهويّةُ قائمةٌ حيث كانت تنكسر: متى وُجد إشعارٌ دائن.</b>
    ///
    /// <para>كانت الأوعيةُ «منها» تحت <b>وعاء الفواتير الإجماليّ</b> وهي صافيةٌ من الإشعارات،
    /// فلا تجمع إليه. <b>ومُثبِّتُ الاختبار أعلاه بلا إشعارٍ فلم يرَ ذلك قطّ</b> — وهو الذي
    /// سمّاه التدقيق: «تُعرض تحت الإجماليّات».</para>
    ///
    /// <para><b>والضريبةُ تحت صافي المخرجات، ومعها «بلا فئة» لِما قُيّد بلا مستند</b> — فهو
    /// في الصافي ولا وعاءَ له يُخترع.</para>
    /// </summary>
    [Fact]
    public void TheCategoryRows_SumToTheirParent_WhenACreditNoteExists()
    {
        var dto = MixedQuarter() with
        {
            SalesSubtotal = 1_000_000m, OutputVat = 150_000m,
            CreditNotesSubtotal = 100_000m, CreditNotesVat = 15_000m,
            JournalOutputVat = 3_000m,
            SalesBaseNet = 900_000m,
            SalesByCategory = [new VatReturnCategoryDto("S", 900_000m, 135_000m)],
        };
        var rows = VatReturnRows.Build(dto);

        // ‏**الأبُ المعروض == مجموعُ أبنائه المعروضين** — لا الأبناءُ وحدَهم، فأبٌ يُقرأ من حقلٍ
        // ‏آخر (وعاءِ الفواتير الإجماليّ مثلاً) يمرّ على فحص الأبناء وحدَهم.
        Assert.Equal(138_000m, ParentOf(rows, "صافي ضريبة المخرجات"));   // ‏150,000 − 15,000 + 3,000
        Assert.Equal(138_000m, ChildrenOf(rows, "صافي ضريبة المخرجات"));
        Assert.Equal(900_000m, ParentOf(rows, "صافي وعاء المبيعات (من المستندات)"));
        Assert.Equal(900_000m, ChildrenOf(rows, "صافي وعاء المبيعات (من المستندات)"));

        // ‏**ووعاءُ الفواتير الإجماليُّ لم يعد له أبناءٌ لا يجمعون إليه.**
        Assert.Equal(0m, ChildrenOf(rows, "مبيعات خاضعة (وعاء)"));
    }

    private static decimal ParentOf(IReadOnlyList<VatReturnRows.Row> rows, string parent) =>
        rows.Single(r => r.Label == parent).Value;

    private static decimal ChildrenOf(IReadOnlyList<VatReturnRows.Row> rows, string parent)
    {
        var index = rows.ToList().FindIndex(r => r.Label == parent);
        Assert.True(index >= 0, $"لا صفَّ «{parent}» — " + string.Join(" · ", rows.Select(r => r.Label)));
        return rows.Skip(index + 1).TakeWhile(r => r.Indented).Sum(r => r.Value);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤٠) <b>لكلّ حدٍّ في الصافي صفٌّ — والصفوفُ تُعيد الصافي.</b>
    ///
    /// <para>كان الصافي يُشتقّ في <c>VatReturnDto</c> بمعادلةٍ أقدمَ من الإقرار <b>ولا صفَّ
    /// للحدّين الغائبين</b>. <b>والقياسُ بقيمٍ متمايزةٍ غيرِ صفريّة</b> — فصفٌّ يسقط أو يُقرأ
    /// من حقلٍ آخر لا يختبئ خلف صفرٍ يساوي صفراً.</para>
    /// </summary>
    [Fact]
    public void EveryTermOfTheNet_HasARow_AndTheRowsRebuildIt()
    {
        var dto = MixedQuarter() with
        {
            ProgressBillingsVat = 700m, CreditNotesVat = 1_100m,
            CancelledOutputVat = 1_300m, JournalOutputVat = 1_900m,
            DeductibleInputVat = 1_700m, DebitNotesDeductibleVat = 230m,
            CancelledInputVat = 290m, JournalInputVat = 310m,
        };
        var rows = VatReturnRows.Build(dto);
        decimal Row(string label) => rows.Single(r => r.Label == label).Value;

        var output = Row("ضريبة المخرجات — مبيعات") + Row("ضريبة المخرجات — مستخلصات")
            - Row("ضريبة مرتجعة (إشعارات دائن)")
            - Row("تسوية إلغاء مستندات فترات سابقة (تُطرح)")
            + Row("ضريبة مخرجات مقيّدة بلا مستند (تُضاف)");
        var input = Row("ضريبة مدخلات قابلة للخصم")
            - Row("ضريبة مدخلات مُخفّضة (إشعارات مدينة)")
            - Row("تسوية إلغاء مشتريات فترات سابقة (تُطرح)")
            + Row("ضريبة مدخلات مقيّدة بلا مستند (تُضاف)");

        // ‏**ورقمٌ مستقلٌّ لا الجمعُ وحدَه**: 150,000 + 700 − 1,100 − 1,300 + 1,900.
        Assert.Equal(150_200m, output);
        Assert.Equal(output, Row("صافي ضريبة المخرجات"));
        Assert.Equal(1_490m, input);                          // 1,700 − 230 − 290 + 310
        Assert.Equal(input, Row("صافي ضريبة المدخلات"));
        Assert.Equal(148_710m, Row("صافي الضريبة المستحقة/المستردة"));
    }

    /// <summary>
    /// <b>وإقرارٌ بلا تفكيك لا ينكسر</b> — منشأةٌ كلُّ مبيعاتها بالنسبة الأساسية، أو بياناتٌ
    /// قديمة. فالحارسُ يمنع الغياب ولا يفرض الحضور.
    /// </summary>
    [Fact]
    public void AReturnWithoutBuckets_StillRenders()
    {
        var plain = MixedQuarter() with { SalesByCategory = null, PurchasesByCategory = null };

        var rows = VatReturnRows.Build(plain);

        Assert.DoesNotContain(rows, r => r.Indented);
        Assert.Contains(rows, r => r.Label == "صافي الضريبة المستحقة/المستردة");
    }

    /// <summary>
    /// (ui-ux-i18n-printing#9) <b>والإقرارُ بالإنجليزية إنجليزيٌّ كلُّه</b> — كانت سطورُه تُبنى في
    /// الشيفرة فلا يبلغها مُترجِمُ XAML، وتبقى عربيّةً بعد التبديل رغم أنّ السجلَّ يحمل ترجمتَها.
    /// </summary>
    [Fact]
    public void InEnglish_NoReturnLineIsArabic()
    {
        if (!LocaleManager.IsEnglish) LocaleManager.Toggle();
        try
        {
            var rows = VatReturnRows.Build(MixedQuarter() with
            {
                PurchasesByCategory = [new VatReturnCategoryDto("E", 10m, 0m)],
            });

            var arabic = rows.Where(r => r.Label.Any(c => c is >= '؀' and <= 'ۿ')).Select(r => r.Label).ToList();
            Assert.True(arabic.Count == 0, "سطورٌ عربيّةٌ في الوضع الإنجليزيّ:\n  " + string.Join("\n  ", arabic));
            Assert.Contains(rows, r => r.Label == "Net VAT payable / refundable");
        }
        finally
        {
            if (LocaleManager.IsEnglish) LocaleManager.Toggle();
        }
    }
}
