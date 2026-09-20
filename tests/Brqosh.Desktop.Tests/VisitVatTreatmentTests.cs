using System.Windows.Threading;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Tax;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١) <b>زياراتُ العيادة كانت خاضعةً بـ١٥٪ دائماً.</b>
///
/// <para><c>Visit.AddLine</c> يقبل الفئةَ الضريبيّةَ ورمزَها منذ نشأته، <b>وحوارُ الزيارة
/// لا يُمرّر شيئاً</b> — فيفترض النطاقُ «خاضعة». <b>فعيادةٌ تُعالج مواطناً تُحمّله ١٥٪</b>
/// وهي صفريّةٌ عنه (<c>VATEX-SA-HEA</c>)، <b>وصيدليّةٌ تصرف دواءً كذلك</b>
/// (<c>VATEX-SA-35</c>).</para>
///
/// <para><b>والمُدخَلُ مزدوج، والقائمةُ مغلقة:</b> الفئةُ ورمزُها يُسافران في مدخلٍ واحد،
/// <b>فالحالةُ الخاطئةُ غيرُ قابلةٍ للتمثيل</b> — لا فئةٌ صفريّةٌ بلا رمز (يرفضها النطاق)
/// ولا رمزٌ من فئةٍ أخرى. <b>وهذا أمتنُ من حارسٍ يفحص بعد الإدخال.</b></para>
/// </summary>
public class VisitVatTreatmentTests
{
    /// <summary>خيطُ الواجهة المشترَك — <b>وWPF لا يسمح إلا بتطبيقٍ واحدٍ في العملية</b>.</summary>
    private static readonly Dispatcher UiDispatcher = WpfTestHost.Dispatcher;

    /// <summary>
    /// <b>القياسُ الحاكم:</b> كلُّ رمزٍ في القائمة تعرفه الهيئةُ، <b>وفئتُه هي فئةُ مدخله</b>.
    ///
    /// <para>ورمزٌ لا يُطابق فئتَه يرفضه النطاقُ عند بناء السطر — <b>فيسقط المستخدمُ على
    /// رسالةٍ بعد أن ملأ الزيارةَ كلَّها</b>، أو تخرج فاتورةٌ ترفضها الهيئة.</para>
    /// </summary>
    [Fact]
    public void EveryTreatment_PairsItsCodeWithItsOwnCategory()
    {
        foreach (var option in VisitDialog.VisitVatTreatment.Options)
        {
            if (option.Code is null)
            {
                Assert.Equal(VatCategory.Standard, option.Category);
                continue;
            }

            var reason = VatExemptionReason.Find(option.Code);

            Assert.True(reason is not null,
                $"‏الرمز «{option.Code}» لا تعرفه قائمةُ الهيئة — ويُرفض المستند.");
            Assert.Equal(option.Category, reason!.Category);
        }
    }

    /// <summary>
    /// <b>والفئةُ غيرُ الأساسيّة لا تخلو من رمز</b> — فالنطاقُ يشترطه (BR-KSA-CL-06)،
    /// ومدخلٌ بلا رمزٍ يُنتج زيارةً لا تُفوتَر.
    /// </summary>
    [Fact]
    public void EveryNonStandardTreatment_CarriesAReasonCode()
    {
        var offenders = VisitDialog.VisitVatTreatment.Options
            .Where(o => o.Category != VatCategory.Standard && string.IsNullOrWhiteSpace(o.Code))
            .Select(o => o.Label)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// <b>شاهدُ ضبط — الافتراض:</b> أوّلُ الخيارات «خاضعة» بلا رمز، <b>وهو ما يأخذه كلُّ
    /// سطرٍ لم يُلمس</b>. فلا تتغيّر زيارةٌ تُدخَل كما كانت تُدخَل، <b>والصفريّةُ قرارٌ
    /// يُتَّخذ لا يُفترض له</b> — وصفريّةٌ تُمنح بالخطأ نقصٌ في إقرارٍ سُلّم للهيئة.
    /// </summary>
    [Fact]
    public void TheDefaultTreatment_IsStandardRated()
    {
        var @default = VisitDialog.VisitVatTreatment.Options[0];

        Assert.Equal(VatCategory.Standard, @default.Category);
        Assert.Null(@default.Code);
        Assert.Equal(@default, new VisitDialog.LineRow().Treatment);
    }

    /// <summary>
    /// <b>والخياراتُ تشمل الحالتين اللتين يُنتجهما مركزٌ طبيٌّ فعلاً</b> — رعايةٌ لمواطن،
    /// ودواءٌ ومعدّاتٌ طبية. <b>وقائمةٌ بلا الحالتين تترك العطلَ قائماً بواجهةٍ جديدة.</b>
    /// </summary>
    [Fact]
    public void TheOptions_CoverCitizenCareAndMedicines()
    {
        var codes = VisitDialog.VisitVatTreatment.Options
            .Select(o => o.Code)
            .Where(c => c is not null)
            .ToList();

        Assert.Contains("VATEX-SA-HEA", codes);
        Assert.Contains("VATEX-SA-35", codes);
        Assert.All(VisitDialog.VisitVatTreatment.Options,
            o => Assert.False(string.IsNullOrWhiteSpace(o.Label)));
    }

    // ── والسلوكُ نفسُه، لا جدولُ الخيارات وحدَه ─────────────────────────────

    /// <summary>
    /// <b>القياسُ السلوكيّ:</b> سطرٌ وُسم «رعاية صحية لمواطن» يخرج من الحوار
    /// <b>صفريّاً برمزه</b> — فالفئةُ تبلغ النطاقَ فعلاً، لا تبقى في الشاشة.
    ///
    /// <para><b>وهو ما كان يسقط</b>: الحوارُ لا يُمرّر فئةً، فيفترض النطاقُ «خاضعة».</para>
    /// </summary>
    [Fact]
    public void AZeroRatedRow_ReachesTheDomainAsZeroRated()
    {
        var visit = BuildWith(VisitDialog.VisitVatTreatment.Options[1]);
        var line = visit.Lines.Single();

        Assert.Equal(VatCategory.ZeroRated, line.VatCategory);
        Assert.Equal("VATEX-SA-HEA", line.TaxExemptionReasonCode);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ سلوكيّ:</b> سطرٌ تُرك على حاله يبقى خاضعاً بـ١٥٪ بلا رمز —
    /// <b>فلا تتغيّر زيارةٌ تُدخَل كما كانت تُدخَل</b>.
    /// </summary>
    [Fact]
    public void AnUntouchedRow_StaysStandardRated()
    {
        var line = BuildWith(treatment: null).Lines.Single();

        Assert.Equal(VatCategory.Standard, line.VatCategory);
        Assert.Null(line.TaxExemptionReasonCode);
    }

    private static Visit BuildWith(VisitDialog.VisitVatTreatment? treatment) =>
        UiDispatcher.Invoke(() =>
        {
            var dialog = new VisitDialog();
            var row = new VisitDialog.LineRow { Description = "كشف", Quantity = 1m, UnitPrice = 300m };
            if (treatment is not null) row.Treatment = treatment;
            dialog.RowsForTests.Add(row);

            return dialog.BuildVisitFromRows(
                companyId: 1, patientId: 1, "V-1", new DateOnly(2026, 3, 1), "طبيب", policyId: null);
        });
}
