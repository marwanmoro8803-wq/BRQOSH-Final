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
    /// <b>وهويةُ العرض:</b> مجموعُ سطور «منها» تحت وعاء المبيعات == الوعاءُ نفسُه.
    /// فسطرٌ يسقط أو يتكرّر يُكسر هذه المساواة — ولا يكشفه عدُّ السطور وحده.
    /// </summary>
    [Fact]
    public void TheBreakdownRows_SumToTheTotalAboveThem()
    {
        var dto = MixedQuarter();
        var rows = VatReturnRows.Build(dto);

        var total = rows.First(r => r.Label == "مبيعات خاضعة (وعاء)").Value;
        var index = rows.ToList().FindIndex(r => r.Label == "مبيعات خاضعة (وعاء)");

        var children = rows.Skip(index + 1).TakeWhile(r => r.Indented).Sum(r => r.Value);

        Assert.Equal(total, children);
        Assert.Equal(dto.SalesSubtotal, children);
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
}
