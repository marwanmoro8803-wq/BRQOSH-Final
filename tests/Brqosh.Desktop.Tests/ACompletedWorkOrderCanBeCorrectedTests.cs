using System.IO;
using System.Reflection;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Manufacturing;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٥ · <c>manufacturing#2</c>) <b>أمرُ إنتاجٍ مكتملٌ يُصحَّح من الواجهة — وكانت
/// الواجهةُ ترفضه.</b>
///
/// <para>‏<c>WorkOrderService.CancelAsync</c> يعكس الأمرَ المكتمل منذ أمدٍ — قيدا الإكمال وحركاتُ المخزون،
/// <b>والطبقاتُ بأعيانها منذ ح-٣٠</b>، ويرفض إن بيع شيءٌ من إنتاجه — <b>ومختبَرٌ بستّة اختبارات</b>.
/// <b>ثمّ تمنعه الصفحة</b>: «اختر أمر إنتاج مسودة أو صادراً» لكلّ أمرٍ مكتمل. <b>فكمّيّةُ إنتاجٍ خاطئةٌ
/// دائمة</b> — لا تُصحَّح إلّا بقيدٍ يدويٍّ على المخزون وحسابِ تحت التشغيل، وهو ما يُفسد الطبقات.</para>
///
/// <para>‏<b>والقياسُ بالانعكاس على قرار الصفحة</b> — فالمعالجُ نفسُه يحتاج نافذة، والقرارُ هو ما كان
/// خطأً. وغيابُ الدالّة قبل الإصلاح <b>هو</b> الأحمر: لا قرارَ يُسأل ولا طريقَ من الواجهة.</para>
/// </summary>
public class ACompletedWorkOrderCanBeCorrectedTests
{
    /// <summary>‏<b>القياسُ الحاكم:</b> الأمرُ المكتملُ يُلغى من الصفحة — فتُعكس آثارُه ويُعاد إصدارُه.</summary>
    [Fact]
    public void ACompletedOrder_CanBeCancelledFromThePage() =>
        Assert.True(CanCancel(WorkOrderStatus.Completed),
            "الصفحةُ ترفض إلغاءَ أمرٍ مكتمل — والخدمةُ تعكسه؛ فالكمّيّةُ الخاطئةُ لا تُصحَّح.");

    [Theory]
    [InlineData(WorkOrderStatus.Draft)]
    [InlineData(WorkOrderStatus.Released)]
    public void AnOpenOrder_CanStillBeCancelled(WorkOrderStatus status) =>
        Assert.True(CanCancel(status));

    /// <summary>‏<b>شاهدُ ضبط</b> — الملغى لا يُلغى ثانيةً: القرارُ ليس «نعم» لكلّ حال.</summary>
    [Fact]
    public void ACancelledOrder_CannotBeCancelledAgain() =>
        Assert.False(CanCancel(WorkOrderStatus.Cancelled));

    /// <summary>
    /// ‏<b>والأمرُ المكتملُ يُسأل عنه بتأكيدٍ غيرِ تأكيد المفتوح</b> — يقول إنّ القيود والمخزونَ تُعكس
    /// ثمّ يُعاد الإصدار؛ فالمستعملُ لا يُلغي إنتاجاً مرحَّلاً وهو يظنّه مسودة.
    /// </summary>
    [Fact]
    public void ACompletedOrder_IsConfirmedAsAReversal()
    {
        Assert.True(ReversesPostings(WorkOrderStatus.Completed));
        Assert.False(ReversesPostings(WorkOrderStatus.Released));
        Assert.False(ReversesPostings(WorkOrderStatus.Draft));
    }

    /// <summary>
    /// ‏<b>والمعالجُ يسأل القرارَ ولا يكتب شرطَه بيده</b> — وإلّا عاد الرفضُ من سطرٍ واحدٍ في المعالج لا يراه
    /// قياسُ القرار: الدالّةُ صحيحةٌ ولا يستدعيها أحد.
    /// </summary>
    [Fact]
    public void TheCancelHandler_AsksTheDecision()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", "WorkOrdersPage.xaml.cs");
        var source = File.ReadAllText(path);
        var start = source.IndexOf("private async void OnCancel(", StringComparison.Ordinal);
        Assert.True(start >= 0, "OnCancel غير موجود — القياسُ أعمى.");
        var body = source[start..];
        body = body[..body.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.Contains("CanCancel(row.Status)", body, StringComparison.Ordinal);
        Assert.Contains("ReversesPostings(row.Status)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkOrderStatus.Completed", body, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }

    private static bool CanCancel(WorkOrderStatus status) => Decide("CanCancel", status);

    private static bool ReversesPostings(WorkOrderStatus status) => Decide("ReversesPostings", status);

    private static bool Decide(string name, WorkOrderStatus status)
    {
        var method = typeof(WorkOrdersPage).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(WorkOrderStatus)]);
        Assert.True(method is not null,
            $"WorkOrdersPage.{name} غير موجودة — الصفحةُ لا تملك قراراً يُسأل، والأمرُ المكتملُ مرفوضٌ ضمناً.");
        return (bool)method!.Invoke(null, [status])!;
    }
}
