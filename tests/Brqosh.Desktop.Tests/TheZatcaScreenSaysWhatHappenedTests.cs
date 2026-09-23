using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Zatca;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٧ · <c>ui-ux-i18n-printing#4</c>) <b>شاشةُ مستندات الهيئة تقول ما وقع — وكانت
/// تقول «تم الإرسال بنجاح» لمحاكاةٍ لم تُرسَل.</b>
///
/// <para>‏<b>ما كان</b>: <c>OnSubmit</c> يعرض «تم الإرسال بنجاح.» متى لم يرمِ <c>SubmitAsync</c> — والمحاكاةُ لا
/// ترمي، فتنتهي <c>SimulatedLocally</c>. <b>ثمّ يُعرض المستندُ في الجدول «مُولَّداً»</b>: <c>StatusLabel</c>
/// يبتلع <c>SimulatedLocally</c> في فرعه الافتراضيّ. <b>والشجرةُ تملك الاسمين</b> — <c>NameAr()</c>
/// و<c>IsAcknowledgedByAuthority()</c> منذ CEEF-229 — والشاشةُ تشتقّهما بيدها. (والجزءُ الثالثُ من سطر
/// التدقيق — المستخلصُ «إشعار دائن» — أُغلق بـح-١٥.)</para>
///
/// <para>‏<b>والقياسُ بالانعكاس على قرارات الصفحة</b> — بلا نافذة، <b>وبلا حالة اللغة العامّة</b>: ثلاثةُ
/// اختباراتٍ تقلب <c>LocaleManager</c>، فالقياسُ على زوج الاسمين لا على <c>T()</c>.</para>
/// </summary>
public class TheZatcaScreenSaysWhatHappenedTests
{
    /// <summary>‏<b>القياسُ الحاكم:</b> المحاكاةُ تُسمّى محاكاةً في الجدول — لا «مُولَّد».</summary>
    [Fact]
    public void ASimulatedDocument_IsNotShownAsGenerated()
    {
        var (arabic, english) = Names(ZatcaComplianceStatus.SimulatedLocally);

        Assert.Equal(ZatcaComplianceStatus.SimulatedLocally.NameAr(), arabic);
        Assert.NotEqual(Names(ZatcaComplianceStatus.Generated).English, english);
    }

    /// <summary>‏<b>والعربيُّ لكلّ حالةٍ هو <c>NameAr()</c> نفسُه</b> — فلا يفترق جدولُ الشاشة عن المطبوع.</summary>
    [Fact]
    public void EveryStatus_UsesTheTreesArabicName()
    {
        foreach (var status in Enum.GetValues<ZatcaComplianceStatus>())
            Assert.Equal(status.NameAr(), Names(status).Arabic);
    }

    /// <summary>‏<b>ولكلّ حالةٍ اسمٌ إنجليزيٌّ خاصٌّ بها</b> — لا فرعَ افتراضيٌّ يجمع اثنتين تحت اسمٍ واحد.</summary>
    [Fact]
    public void EveryStatus_HasItsOwnEnglishName()
    {
        var english = Enum.GetValues<ZatcaComplianceStatus>().Select(s => Names(s).English).ToList();
        Assert.Equal(english.Count, english.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>‏<b>والإرسالُ المُحاكى لا يُعلَن نجاحاً</b> — والمقبولُ عند الهيئة وحدَه نجاح.</summary>
    [Theory]
    [InlineData(ZatcaComplianceStatus.Reported, "Acknowledged")]
    [InlineData(ZatcaComplianceStatus.Cleared, "Acknowledged")]
    [InlineData(ZatcaComplianceStatus.Accepted, "Acknowledged")]
    [InlineData(ZatcaComplianceStatus.SimulatedLocally, "Simulated")]
    [InlineData(ZatcaComplianceStatus.Rejected, "Rejected")]
    [InlineData(ZatcaComplianceStatus.ReadyToClear, "NotYetReceived")]
    [InlineData(ZatcaComplianceStatus.Generated, "NotYetReceived")]
    public void TheSubmitOutcome_FollowsTheReturnedStatus(ZatcaComplianceStatus status, string expected) =>
        Assert.Equal(expected, Outcome(status));

    /// <summary>‏<b>والملخّصُ يعدّ المحاكاةَ وحدَها</b> — كانت لا مُرسلةً ولا منتظِرة، فتختفي من العدّ.</summary>
    [Fact]
    public void TheSummary_CountsSimulationsApart()
    {
        var method = typeof(ZatcaInvoicesPage).GetMethod("SummaryCounts", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(method is not null, "ZatcaInvoicesPage.SummaryCounts غير موجودة — الملخّصُ لا يُسأل.");
        var statuses = new List<ZatcaComplianceStatus>
        {
            ZatcaComplianceStatus.SimulatedLocally, ZatcaComplianceStatus.SimulatedLocally,
            ZatcaComplianceStatus.Cleared, ZatcaComplianceStatus.ReadyToClear,
        };
        var counts = ((int Submitted, int Pending, int Simulated))method!.Invoke(null, [statuses])!;

        Assert.Equal((1, 1, 2), counts);
    }

    /// <summary>
    /// ‏<b>والمعالجُ يسأل القرارَ ولا يكتب نجاحَه بيده</b> — وإلّا عادت «تم الإرسال بنجاح» من سطرٍ لا يراه قياسُ القرار.
    /// </summary>
    [Fact]
    public void TheSubmitHandler_AsksTheOutcome()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", "ZatcaInvoicesPage.xaml.cs");
        var source = File.ReadAllText(path).Replace("\r\n", "\n");
        var start = source.IndexOf("private async void OnSubmit(", StringComparison.Ordinal);
        Assert.True(start >= 0, "OnSubmit غير موجود — القياسُ أعمى.");
        var body = source[start..];
        body = body[..body.IndexOf("\n    }\n", StringComparison.Ordinal)];
        // ‏التعليقُ الذي يشرح لماذا زالت الرسالةُ ليس وقوعاً لها — فالحارسُ على الكود وحدَه.
        var code = Regex.Replace(body, @"^\s*//.*$", "", RegexOptions.Multiline);

        Assert.Contains("OutcomeOf(", code, StringComparison.Ordinal);
        Assert.DoesNotContain("تم الإرسال بنجاح", code, StringComparison.Ordinal);
    }

    private static (string Arabic, string English) Names(ZatcaComplianceStatus status)
    {
        var method = typeof(ZatcaInvoicesPage).GetMethod("StatusNames", BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(ZatcaComplianceStatus)]);
        Assert.True(method is not null,
            "ZatcaInvoicesPage.StatusNames غير موجودة — الشاشةُ تشتقّ الاسمَ بيدها والمحاكاةُ «مُولَّد».");
        return ((string, string))method!.Invoke(null, [status])!;
    }

    private static string Outcome(ZatcaComplianceStatus status)
    {
        var method = typeof(ZatcaInvoicesPage).GetMethod("OutcomeOf", BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(ZatcaComplianceStatus)]);
        Assert.True(method is not null,
            "ZatcaInvoicesPage.OutcomeOf غير موجودة — كلُّ إرسالٍ لم يرمِ يُعلَن نجاحاً.");
        return method!.Invoke(null, [status])!.ToString()!;
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
