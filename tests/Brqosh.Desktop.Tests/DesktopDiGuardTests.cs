using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// CEEF-123/124 — بوابة ثابتة: Desktop ممنوع أن يعود لنمط new XxxService(db).
/// المسار الصحيح: DesktopAccountingFacade + DI (CEEF-122).
/// </summary>
public class DesktopDiGuardTests
{
    private static readonly Regex NewServicePattern = new(@"new \w+Service\(", RegexOptions.Compiled);

    /// <summary>الملفات المُهاجَرة Tier P0 — تتوسع مع كل tier (CEEF-123).</summary>
    public static readonly TheoryData<string> MigratedTierP0Files = new()
    {
        @"Views\Pages\SalesInvoiceDialog.xaml.cs",
        @"Views\Pages\PurchaseInvoiceDialog.xaml.cs",
        @"Views\Pages\PaymentVoucherDialog.xaml.cs",
        @"Views\Pages\JournalEntryDialog.xaml.cs",
        @"Views\Pages\CreditNoteDialog.xaml.cs",
        @"Views\Pages\CreditNotesPage.xaml.cs",
        @"Views\Pages\DebitNoteDialog.xaml.cs",
        @"Views\Pages\DebitNotesPage.xaml.cs",
        @"Views\Pages\ReceiptVoucherDialog.xaml.cs",
        @"Views\Pages\ReceiptVouchersPage.xaml.cs",
        @"Views\Pages\PaymentVouchersPage.xaml.cs",
        @"Views\Pages\SalesInvoicesPage.xaml.cs",
        @"Views\Pages\PurchaseInvoicesPage.xaml.cs",
        @"Views\Pages\FiscalPeriodsPage.xaml.cs",
        @"Views\Pages\FxRevaluationDialog.xaml.cs",
    };

    private static string DesktopRoot()
    {
        var dir = AppContext.BaseDirectory;
        var root = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    [Theory]
    [MemberData(nameof(MigratedTierP0Files))]
    public void MigratedFile_DoesNotRegress_ToNewService(string relativePath)
    {
        var path = Path.Combine(DesktopRoot(), relativePath);
        Assert.True(File.Exists(path), $"Expected migrated file missing: {relativePath}");

        var source = File.ReadAllText(path);
        var matches = NewServicePattern.Matches(source);

        Assert.True(matches.Count == 0,
            $"{relativePath} regressed to direct service construction ({matches.Count}x 'new XxxService('). " +
            "Use DesktopAccountingFacade.GetService<T>() — CEEF-122/123.");
    }

    [Fact]
    public void Facade_ItselfUsesDiResolution_NotNewService()
    {
        var path = Path.Combine(DesktopRoot(), "DesktopAccountingFacade.cs");
        var source = File.ReadAllText(path);
        Assert.DoesNotMatch(NewServicePattern, source);
        Assert.Contains("GetRequiredService", source);
    }

    /// <summary>CEEF-124 — صفر new XxxService في كل Desktop (باستثناء تعليقات/سلاسل نصية إن وُجدت لاحقاً).</summary>
    [Fact]
    public void EntireDesktop_HasZeroDirectServiceConstruction()
    {
        var root = DesktopRoot();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file);
            var source = File.ReadAllText(file);
            var count = NewServicePattern.Matches(source).Count;
            if (count > 0)
                violations.Add($"{rel} ({count})");
        }

        Assert.True(violations.Count == 0,
            "Desktop must not use 'new XxxService(' — use DesktopAccountingFacade.GetService<T>(). Violations:\n" +
            string.Join("\n", violations));
    }
}
