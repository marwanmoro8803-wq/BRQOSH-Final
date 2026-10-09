using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٠٢ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>شاشةُ المرتجع تحمل دفعةَ سطر الفاتورة إلى الإشعار.</b>
///
/// <para>‏الخدمةُ تُرجِع إلى الدفعة التي يسمّيها سطرُ الإشعار، وبترتيب البيع حين لا يسمّي — <b>والتخمينُ يُقيّد مرتجعَ الدفعة
/// القريبة الانتهاء على البعيدة</b> متى حذف المستخدمُ سطراً. فالشاشةُ تنسخ الدفعةَ من سطر الفاتورة وتمرّرها، <b>وتطرح المُرجَعَ
/// سلفاً من دفعته لا من الصنف كلِّه</b> — وكان مرتجعٌ بخمسٍ من سطرين (٤ و٣) يُخفيهما معاً فلا يُرجَع الباقي من الشاشة أبداً.</para>
///
/// <para>‏<b>والقياسُ في نصّ المعالج</b> — فالحوارُ يقرأ القاعدةَ من <c>AppServices</c>، وفتحُه في اختبارٍ يبلغ دفاترَ الإنتاج.</para>
/// </summary>
public class TheReturnScreenCarriesTheSoldBatchTests
{
    private static readonly string Source = File.ReadAllText(Path.Combine(
        RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", "CreditNoteDialog.xaml.cs"));

    /// <summary>‏<b>الصفُّ يأخذ دفعةَ سطر الفاتورة</b> — وإلّا خرج الإشعارُ بلا اسمٍ وعادت الخدمةُ إلى التخمين.</summary>
    [Fact]
    public void TheRow_TakesTheInvoiceLinesBatch() =>
        Assert.Contains("BatchNumber = line.BatchNumber,", Source, StringComparison.Ordinal);

    /// <summary>‏<b>والإشعارُ يُبنى بها</b> — صفٌّ يحملها ولا يمرّرها كصفٍّ لا يحملها.</summary>
    [Fact]
    public void TheNoteLine_IsBuiltWithTheRowsBatch() =>
        Assert.Contains("batchNumber: row.BatchNumber", Source, StringComparison.Ordinal);

    /// <summary>‏<b>والمُرجَعُ سلفاً يُطرح من دفعته</b> — مفتاحُ الطرح الصنفُ والدفعةُ معاً في التجميع وفي السؤال.</summary>
    [Fact]
    public void AlreadyReturned_IsSubtractedPerBatch()
    {
        Assert.Contains(".GroupBy(l => (l.ProductId!.Value, l.BatchNumber))", Source, StringComparison.Ordinal);
        Assert.Contains("alreadyReturned.GetValueOrDefault((pid, line.BatchNumber))", Source, StringComparison.Ordinal);
        Assert.DoesNotContain(".GroupBy(l => l.ProductId!.Value)", Source, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
