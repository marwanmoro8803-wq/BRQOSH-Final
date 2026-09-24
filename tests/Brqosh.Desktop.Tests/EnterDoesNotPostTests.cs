using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦١ · <c>ui-ux-i18n-printing#5</c>) <b>زرُّ «Enter» لا يُرحِّل مستنداً.</b>
///
/// <para><b>ما كان:</b> زرُّ الترحيل في حوارات القيد والفواتير والسندات والإشعارات <c>IsDefault</c> —
/// <b>فضغطةُ «Enter» في أيّ حقلٍ تُرحِّل</b> مستنداً لا يُعدَّل، <b>والطرفُ الأوّلُ في القائمة مختارٌ
/// سلفاً</b>. فكاتبٌ يُنهي وصفاً بـ«Enter» يُصدر فاتورةً لأوّل عميلٍ أبجديّاً، ولا يُصلحها إلا إشعارٌ
/// دائنٌ ومستندٌ جديدٌ لدى الهيئة.</para>
///
/// <para><b>والقاعدةُ بالمعنى لا بالقائمة</b>: زرٌّ افتراضيٌّ نصُّه «ترحيل» أو «اعتماد» أو معالجُه
/// <c>OnPost</c>/<c>OnApprove</c> — في كلّ ملفّات <c>Views</c>. <b>والقائمةُ كانت أربعةَ عشر،
/// والقاعدةُ وجدت الخامسَ عشر</b>: «ترحيل الاستبعاد» في حوار استبعاد الأصل.</para>
///
/// <para><b>والحفظُ يبقى افتراضيّاً</b>: مسودةٌ أو بطاقةُ عميلٍ تُعدَّلان بعدُ — فـ«Enter» فيهما
/// لا يُنشئ ما لا يُرَدّ. وهو الضابطُ الموجبُ للقارئ.</para>
/// </summary>
public class EnterDoesNotPostTests
{
    private static readonly string[] PostingWords = ["ترحيل", "اعتماد"];
    private static readonly string[] PostingHandlers = ["OnPost", "OnApprove"];

    /// <summary>أزرارُ XAML الافتراضيّة: (الملفّ، النصّ، المعالج).</summary>
    private static IEnumerable<(string File, string Content, string Click)> DefaultButtons()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop", "Views");
        foreach (var file in Directory.EnumerateFiles(views, "*.xaml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(file);
            foreach (var button in doc.Descendants().Where(e => e.Name.LocalName == "Button"))
            {
                if (!string.Equals((string?)button.Attribute("IsDefault"), "True", StringComparison.OrdinalIgnoreCase))
                    continue;

                var content = (string?)button.Attribute("Content") ?? button.Value;
                yield return (Path.GetFileNameWithoutExtension(file), content, (string?)button.Attribute("Click") ?? "");
            }
        }
    }

    private static bool Posts((string File, string Content, string Click) b) =>
        PostingWords.Any(w => b.Content.Contains(w, StringComparison.Ordinal))
        || PostingHandlers.Contains(b.Click, StringComparer.Ordinal);

    /// <summary><b>القياسُ المركزيّ</b> — ولا زرَّ افتراضيَّ يُرحِّل في أيّ شاشة.</summary>
    [Fact]
    public void NoDefaultButtonPostsADocument()
    {
        var offenders = DefaultButtons().Where(Posts)
            .Select(b => $"{b.File}: «{b.Content}» ({b.Click})")
            .ToList();

        Assert.True(offenders.Count == 0,
            "«Enter» يُرحِّل مستنداً لا يُعدَّل في:\n" + string.Join("\n", offenders));
    }

    /// <summary>والحوارات التي سمّاها التدقيق — صفٌّ لكلٍّ فيُقرأ السقوطُ باسمه.</summary>
    [Theory]
    [InlineData("SalesInvoiceDialog")]
    [InlineData("JournalEntryDialog")]
    [InlineData("CreditNoteDialog")]
    [InlineData("DebitNoteDialog")]
    [InlineData("PurchaseInvoiceDialog")]
    [InlineData("ReceiptVoucherDialog")]
    [InlineData("PaymentVoucherDialog")]
    [InlineData("GoodsReceiptDialog")]
    [InlineData("StockMovementDialog")]
    [InlineData("ChequeDialog")]
    [InlineData("FxRevaluationDialog")]
    [InlineData("ProgressBillingDialog")]
    [InlineData("RetentionReleaseDialog")]
    [InlineData("PurchaseOrderDialog")]
    [InlineData("FixedAssetDisposalDialog")]
    public void TheNamedDialog_HasNoDefaultPostingButton(string dialog)
    {
        var posting = DefaultButtons().Where(b => b.File == dialog).Where(Posts).ToList();

        Assert.True(posting.Count == 0,
            $"{dialog}: «Enter» يضغط «{string.Join("، ", posting.Select(p => p.Content))}».");
    }

    /// <summary>
    /// <b>الضابطُ الموجب</b>: القارئُ يرى زرَّ حفظٍ افتراضيّاً حيث يبقى — فلا يمرّ الحارسُ لأنّه
    /// لا يرى شيئاً.
    /// </summary>
    [Fact]
    public void TheReaderSeesADefaultSaveButton_WhereOneRemains()
    {
        Assert.Contains(DefaultButtons(), b => b.File == "CustomerDialog" && b.Content == "حفظ" && !Posts(b));
    }

    // ── والطرفُ الأوّلُ لا يُختار سلفاً ─────────────────────────────────────────────

    private static readonly Regex PicksFirstParty =
        new(@"\b(?:Customer|Supplier|Party)Combo\.SelectedIndex\s*=\s*0\b");

    private static bool PicksTheFirstParty(string pageFile)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop", "Views", "Pages", pageFile + ".xaml.cs");
        return File.ReadLines(path)
            .Select(line => Regex.Replace(line, @"//.*$", ""))
            .Any(line => PicksFirstParty.IsMatch(line));
    }

    /// <summary>
    /// <b>وحوارٌ يُرحِّل لا يختار الطرفَ عن المستخدم</b> — فالترحيلُ يرفض «اختر العميل» بدل أن
    /// يمضي إلى أوّل اسمٍ أبجديّاً.
    /// </summary>
    [Theory]
    [InlineData("SalesInvoiceDialog")]
    [InlineData("PurchaseInvoiceDialog")]
    [InlineData("ReceiptVoucherDialog")]
    [InlineData("PaymentVoucherDialog")]
    [InlineData("ChequeDialog")]
    [InlineData("PurchaseOrderDialog")]
    public void APostingDialog_DoesNotPickTheFirstParty(string dialog) =>
        Assert.False(PicksTheFirstParty(dialog),
            $"{dialog} يختار أوّلَ طرفٍ في القائمة — فيُرحَّل له ما لم يُقصد.");

    /// <summary>
    /// <b>الضابطُ الموجبُ للنمط</b>: كشفُ الحساب يبقى يعرض أوّلَ عميل — عرضٌ لا ترحيل — والنمطُ
    /// يراه، فلا يمرّ الحارسُ لأنّه أعمى.
    /// </summary>
    [Fact]
    public void ThePatternSeesAPreselection_WhereOneRemains() =>
        Assert.True(PicksTheFirstParty("CustomerStatementPage"));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
