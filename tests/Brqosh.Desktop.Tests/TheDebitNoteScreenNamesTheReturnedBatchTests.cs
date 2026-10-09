using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٠٥ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>شاشةُ الإشعار المدين تُسمّي الدفعةَ المُرجَعة.</b>
///
/// <para>‏سطرُ فاتورة الشراء لا يحمل دفعة — الدفعاتُ على الاستلام — <b>فلا شيءَ يُنسخ</b> كما في شاشة الإشعار الدائن. فتُكتب الدفعةُ
/// في حقلٍ وتبلغ <c>RequestedReturn</c>، والبانيَ يرفض بلا اسمٍ حين تبقى دفعتان ويُسمّيهما. <b>وبدون الحقل</b> لا يُرجَع من الشاشة
/// صنفٌ متتبَّعٌ له دفعتان أبداً.</para>
///
/// <para>‏<b>والقياسُ في النصّ</b> — فالحوارُ يقرأ القاعدةَ من <c>AppServices</c>، وفتحُه في اختبارٍ يبلغ دفاترَ الإنتاج.</para>
/// </summary>
public class TheDebitNoteScreenNamesTheReturnedBatchTests
{
    private static string Page(string file) => File.ReadAllText(Path.Combine(
        RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", file));

    /// <summary>‏<b>الحقلُ قائم</b> في النافذة.</summary>
    [Fact]
    public void TheDialog_HasABatchField() =>
        Assert.Contains("x:Name=\"BatchBox\"", Page("DebitNoteDialog.xaml"), StringComparison.Ordinal);

    /// <summary>‏<b>والطلبُ يُبنى به</b> — حقلٌ لا يُمرَّر كحقلٍ غائب.</summary>
    [Fact]
    public void TheRequest_CarriesTheTypedBatch()
    {
        var source = Page("DebitNoteDialog.xaml.cs");
        var start = source.IndexOf("new RequestedReturn(", StringComparison.Ordinal);
        Assert.True(start >= 0, "RequestedReturn غير موجود — القياسُ أعمى.");
        var call = source[start..source.IndexOf("]);", start, StringComparison.Ordinal)];

        Assert.Contains("BatchBox.Text", call, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
