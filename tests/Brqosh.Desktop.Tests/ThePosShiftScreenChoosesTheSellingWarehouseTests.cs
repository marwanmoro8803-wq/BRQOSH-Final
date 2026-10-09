using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١٤ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>شاشةُ الورديات تختار مستودعَ بيع الوردية.</b>
/// بنيويٌّ على سُنّة ح-١١٠: الصفحةُ تُبنى على خيط واجهة ولا تُشغَّل هنا — فيُقاس أنّ الحقلَ موجودٌ ومربوطٌ بفتح الوردية، وقائمتُه الفعّالةُ
/// وأوّلُها «الافتراضيّ». <b>والخطةُ الدوريّة لا شاشةَ لإنشائها</b> (تُنشأ من العقد)؛ فمستودعُها يُقاس في الخدمة لا هنا.
/// </summary>
public class ThePosShiftScreenChoosesTheSellingWarehouseTests
{
    /// <summary>الحقلُ في الشكل ويُمرَّر إلى <c>PosShift.Open</c>، والقائمةُ فعّالةٌ وأوّلُها الافتراضيّ.</summary>
    [Fact]
    public void TheShiftsPage_BindsAWarehouseChoiceToOpeningAShift()
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pages, "PosShiftsPage.xaml"));
        var code = File.ReadAllText(Path.Combine(pages, "PosShiftsPage.xaml.cs"));

        Assert.Contains("x:Name=\"ShiftWarehouseCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShiftWarehouseCombo.SelectedValue as long?", code, StringComparison.Ordinal);
        Assert.Contains("PosShift.Open(AppServices.CompanyId, nameDialog.Value.Trim(), cash,", code, StringComparison.Ordinal);
        Assert.Contains("w.IsActive", code, StringComparison.Ordinal);
        Assert.Contains("new(null, \"(المستودع الافتراضي)\")", code, StringComparison.Ordinal);
        Assert.Contains("await LoadWarehousesAsync();", code, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}
