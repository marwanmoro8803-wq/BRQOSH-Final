using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Tax;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦٠ · <c>ui-ux-i18n-printing#6</c>) <b>شاشةُ الإدخال تعرض الضريبةَ التي
/// تُرحَّل.</b>
///
/// <para><b>ما كان:</b> حوارا فاتورة المبيعات وفاتورة المشتريات يضربان الوعاءَ كلَّه في
/// <c>SaudiVatRate</c> — <b>ويتجاهلان فئةَ السطر</b> التي يختارها المستخدمُ في العمود نفسِه.
/// فسطرُ تصديرٍ صفريٌّ بمئة ريالٍ يُعرض عليه خمسةَ عشرَ ريالاً، <b>والمُرحَّلُ صفر</b>: يرى
/// المستخدمُ رقماً ويُصدَر غيرُه. وفي البيع يُضاف أنّ خصمَ المستند يُطرح من الإجمالي
/// ثمّ يُضرب الباقي في النسبة — <b>والنطاقُ يوزّعه على الأوعية</b>.</para>
///
/// <para><b>والإشعارُ الدائن سليمٌ منذ ح-٦</b>: يبني سطورَ النطاق ويستدعي الموزِّعَ نفسَه —
/// <b>فهو الضابطُ الموجب</b> لهذا القياس: الشاشةُ نفسُها والقياسُ نفسُه، فإن مرّ عليه وسقط
/// على جاريه فالعطلُ فيهما لا في القياس.</para>
///
/// <para><b>والقياسُ على الشاشة الحقيقية</b>: يُنشأ الحوارُ على خيط STA، وتُملأ صفوفُه،
/// ويُستدعى <c>UpdateTotals</c> نفسُه، ويُقرأ النصُّ المعروض — <b>ثمّ يُقابَل بما يبنيه زرُّ
/// الحفظ</b> من الصفوف ذاتها.</para>
/// </summary>
public class TheEntryScreenShowsTheTaxItPostsTests
{
    private static SalesInvoiceDialog.LineRow Row(
        decimal quantity, decimal price, VatCategory category = VatCategory.Standard,
        string? reasonCode = null, decimal discountPercent = 0m) => new()
    {
        Description = "صنف",
        Quantity = quantity,
        UnitPrice = price,
        VatCategory = category,
        TaxExemptionReasonCode = reasonCode,
        DiscountPercent = discountPercent,
    };

    /// <summary>سطرٌ أساسيٌّ بمئة، وسطرُ تصديرٍ صفريٌّ بمئة.</summary>
    private static SalesInvoiceDialog.LineRow[] StandardAndExport() =>
        [Row(1m, 100m), Row(1m, 100m, VatCategory.ZeroRated, "VATEX-SA-32")];

    private static Window Open(string screen) => screen switch
    {
        "sales" => new SalesInvoiceDialog(),
        "purchase" => new PurchaseInvoiceDialog(),
        "creditnote" => new CreditNoteDialog(),
        _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, null),
    };

    /// <summary>صفوفُ الشبكة — الحقلُ نفسُه في الحوارات الثلاثة.</summary>
    private static ObservableCollection<SalesInvoiceDialog.LineRow> Rows(Window dialog) =>
        (ObservableCollection<SalesInvoiceDialog.LineRow>)dialog.GetType()
            .GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dialog)!;

    /// <summary>ما تعرضه الشاشةُ بعد أن تُعيد حسابَ مجاميعها بطريقتها هي.</summary>
    private static (string Subtotal, string Vat, string Total) Shown(Window dialog)
    {
        dialog.GetType()
            .GetMethod("UpdateTotals", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialog, null);

        string Text(string name) => ((TextBlock)dialog.FindName(name)!).Text;
        return (Text("SubtotalText"), Text("VatText"), Text("TotalText"));
    }

    private static (string, string, string) Figures(decimal subtotal, decimal vat) =>
        (subtotal.ToString("N2"), vat.ToString("N2"), (subtotal + vat).ToString("N2"));

    /// <summary>
    /// <b>القياسُ المركزيّ.</b> مئتان، نصفُها صفريّ ⟵ ضريبةٌ <b>١٥ لا ٣٠</b>.
    /// </summary>
    [Theory]
    [InlineData("sales")]
    [InlineData("purchase")]
    [InlineData("creditnote")]
    public void AZeroRatedLine_CarriesNoTaxOnTheScreen(string screen)
    {
        var (shown, expected) = WpfTestHost.Invoke(() =>
        {
            var dialog = Open(screen);
            foreach (var row in StandardAndExport())
                Rows(dialog).Add(row);

            return (Shown(dialog), Figures(200m, 15m));
        });

        Assert.Equal(expected, shown);
    }

    /// <summary>
    /// <b>والخصمُ يُوزَّع على الأوعية كما يُوزِّعه النطاق.</b> أساسيٌّ ٩٩٫٩٩ وصفريٌّ ٩٠ (بعد
    /// خصم سطره ١٠٪) ومعفًى ٤٠، وخصمُ مستندٍ ١٩٫٩٩ ⟵ حصّةُ الأساسيّ ٨٫٦٩، فوعاؤه ٩١٫٣٠
    /// <b>وضريبتُه ١٣٫٧٠ — لا ٣١٫٥٠</b> التي يُنتجها ضربُ الصافي كلِّه في النسبة.
    /// </summary>
    [Fact]
    public void TheSalesScreen_ShowsTheTaxItPosts_WithADocumentDiscountSpreadAcrossTheBases()
    {
        var (shown, posted, expected) = WpfTestHost.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            Rows(dialog).Add(Row(3m, 33.33m));
            Rows(dialog).Add(Row(2m, 50m, VatCategory.ZeroRated, "VATEX-SA-32", discountPercent: 10m));
            Rows(dialog).Add(Row(1m, 40m, VatCategory.Exempt, "VATEX-SA-29"));
            ((TextBox)dialog.FindName("DocumentDiscountBox")!).Text = "19.99";

            var invoice = dialog.BuildInvoiceFromRows(1, 1, new DateOnly(2026, 2, 1));
            return (Shown(dialog),
                Figures(invoice.ForeignSubtotal, invoice.ForeignVatAmount),
                Figures(210m, 13.70m));
        });

        Assert.Equal(expected, posted);
        Assert.Equal(posted, shown);
    }

    /// <summary>
    /// <b>وفي الشراء: ما يُعرض هو ما يُقيَّد ذمّةً للمورّد.</b> أساسيٌّ ٣×٣٣٫٣٣ ومعفًى ٤٠.
    /// </summary>
    [Fact]
    public void ThePurchaseScreen_ShowsTheTaxItPosts()
    {
        var (shown, posted, expected) = WpfTestHost.Invoke(() =>
        {
            var dialog = new PurchaseInvoiceDialog();
            Rows(dialog).Add(Row(3m, 33.33m));
            Rows(dialog).Add(Row(1m, 40m, VatCategory.Exempt, "VATEX-SA-29"));

            var invoice = dialog.BuildInvoiceFromRows(1, 1, new DateOnly(2026, 2, 1), "INV-1", vatDeductible: true);
            return (Shown(dialog),
                Figures(invoice.ForeignSubtotal, invoice.ForeignVatAmount),
                Figures(139.99m, 15m));
        });

        Assert.Equal(expected, posted);
        Assert.Equal(posted, shown);
    }

    /// <summary>
    /// <b>وخصمٌ يتجاوز الصافيَ يُعرض صفراً لا ضريبةً سالبة.</b> والنطاقُ يرفضه عند الحفظ؛
    /// والشاشةُ كانت تقصّ الوعاءَ عند الصفر، <b>والتوزيعُ على الأوعية بلا قصٍّ يُعطي الوعاءَ
    /// الأخيرَ باقياً سالباً</b> — فيبقى القصُّ.
    /// </summary>
    [Fact]
    public void ADocumentDiscountAboveTheNet_ShowsZero_NotANegativeTax()
    {
        var (shown, expected) = WpfTestHost.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            foreach (var row in StandardAndExport())
                Rows(dialog).Add(row);
            ((TextBox)dialog.FindName("DocumentDiscountBox")!).Text = "500";

            return (Shown(dialog), Figures(0m, 0m));
        });

        Assert.Equal(expected, shown);
    }

    /// <summary>
    /// <b>ولا شاشةَ تضرب وعاءً في النسبة الأساسية مباشرةً.</b> فالنسبةُ لا تُقرأ إلا من فئة
    /// السطر (<c>RateFor</c>) أو من الموزِّع — <b>وحوارٌ رابعٌ يُكتب بالصيغة القديمة يسقط هنا</b>.
    /// </summary>
    [Fact]
    public void NoScreenMultipliesABaseByTheStandardRateDirectly()
    {
        var desktop = Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop");
        var flat = new Regex(@"\*\s*(?:\w+\.)*SaudiVatRate\b");

        var offenders = Directory.EnumerateFiles(desktop, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .SelectMany(f => File.ReadLines(f)
                .Select(line => Regex.Replace(line, @"//.*$", ""))
                .Where(line => flat.IsMatch(line))
                .Select(line => Path.GetFileName(f) + ": " + line.Trim()))
            .ToList();

        Assert.True(offenders.Count == 0,
            "شاشةٌ تضرب الوعاءَ كلَّه في النسبة الأساسية وتتجاهل فئةَ السطر:\n" + string.Join("\n", offenders));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
