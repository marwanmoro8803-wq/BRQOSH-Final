using Brqosh.Desktop.Ui;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Treasury;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (ui-ux-i18n-printing#9) <b>حالاتُ النطاق تُعرض بالإنجليزيّة حين تكون الواجهةُ إنجليزيّة.</b>
///
/// <para>يُمرّ على <b>كلّ</b> قيمةٍ في كلّ تعداد — فحالةٌ تُضاف في النطاق بتسميةٍ عربيّةٍ جديدة
/// تُسقط هذا الاختبار حتى تُضاف ترجمتُها، لا تتسرّب إلى الشاشة عربيّةً صامتة.</para>
/// </summary>
public class DomainLabelsAreTranslatedTests
{
    // ‏مميّزةً — فصفٌّ مكرَّرٌ في نظريّةٍ يُتخطّى، والتخطّي يُسقط بوّابةَ «صفر متخطًّى».
    public static IEnumerable<object[]> AllLabels() =>
        Enum.GetValues<ConstructionContractStatus>().Select(ConstructionContract.StatusLabel)
            .Concat(Enum.GetValues<ConstructionProjectStatus>().Select(ConstructionProject.StatusLabel))
            .Concat(Enum.GetValues<ProgressBillingStatus>().Select(ProgressBilling.StatusLabel))
            .Concat(Enum.GetValues<SubcontractAgreementStatus>().Select(SubcontractAgreement.StatusLabel))
            .Concat(Enum.GetValues<WorkOrderStatus>().Select(WorkOrder.StatusLabel))
            .Concat(Enum.GetValues<ChequeStatus>().Select(Cheque.StatusLabel))
            .Distinct(StringComparer.Ordinal)
            .Select(label => new object[] { label });

    [Theory]
    [MemberData(nameof(AllLabels))]
    public void EveryDomainStatus_HasAnEnglishLabel(string arabic)
    {
        if (!LocaleManager.IsEnglish) LocaleManager.Toggle();
        try
        {
            var shown = DomainLabels.Show(arabic);
            Assert.False(shown.Any(c => c is >= '؀' and <= 'ۿ'),
                $"«{arabic}» يُعرض عربيّاً في الوضع الإنجليزيّ — أضفه إلى DomainLabels.");
        }
        finally
        {
            if (LocaleManager.IsEnglish) LocaleManager.Toggle();
        }
    }

    [Fact]
    public void InArabic_TheLabelIsUnchanged()
    {
        Assert.False(LocaleManager.IsEnglish);
        Assert.Equal("مسودة", DomainLabels.Show("مسودة"));
    }
}
