using System.IO;
using Brqosh.Desktop.Navigation;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس» · ترتيبُ القائمة) القائمةُ مرتَّبةٌ كما في برامج ERP الاحترافيّة: لوحةٌ ← قطاعاتُ النشاط (تظهر لمن يملكها وحده) ← الدورةُ التجاريّة
/// ← الخزينة والمحاسبة ← الأصول والرواتب ← الفوترة والتقارير ← الرقابة؛ وداخل كلّ مجموعةٍ: بياناتٌ أساسيّةٌ ثمّ مستنداتٌ ثمّ كشوفٌ وتقارير.
/// وزرُّ المجموعة يطوي ويبسط بالفأرة <b>وبلوحة المفاتيح وقارئ الشاشة</b> (Checked/Unchecked لا Click وحده) — قِيس على النافذة الحقيقيّة.
/// </summary>
public class NavigationIsOrderedLikeAProfessionalErpTests
{
    private static IReadOnlyList<string> Titles => NavigationCatalog.Groups.Select(g => g.TitleAr).ToList();

    private static IReadOnlyList<string> Keys(string group) =>
        NavigationCatalog.Groups.Single(g => g.TitleAr == group).Pages.Select(p => p.Key).ToList();

    private static void InOrder(IReadOnlyList<string> haystack, params string[] needles)
    {
        var last = -1;
        foreach (var n in needles)
        {
            var at = haystack.ToList().IndexOf(n);
            Assert.True(at >= 0, $"«{n}» غائبة.");
            Assert.True(at > last, $"«{n}» في غير موضعها (يجب أن تأتي بعد ما قبلها).");
            last = at;
        }
    }

    [Fact]
    public void TheGroupsFollowTheWorkflow()
    {
        Assert.Equal("المركز التنفيذي", Titles[0]);
        InOrder(Titles, "المركز التنفيذي", "المشاريع والمقاولات", "المبيعات والعملاء", "المشتريات والموردون", "المخزون والمستودعات",
            "الخزينة والبنوك", "المحاسبة العامة", "الأصول الثابتة", "الرواتب", "الفوترة الإلكترونية", "التقارير المالية", "الرقابة والتدقيق", "البرنامج");
        Assert.DoesNotContain("الأمان والتدقيق", Titles);   // ‏لم يبق فيها «أمان»: المستخدمون انتقلوا إلى الإعدادات
    }

    [Fact]
    public void InsideEachGroup_MastersComeBeforeDocuments_AndStatementsComeLast()
    {
        InOrder(Keys("المبيعات والعملاء"), "customers", "sales_quotations", "sales_orders", "sales_invoices", "sales_returns", "customer_statement", "receivable_aging");
        InOrder(Keys("المشتريات والموردون"), "suppliers", "purchase_requests", "purchase_orders", "purchase_invoices", "supplier_statement", "payable_aging");
        InOrder(Keys("المخزون والمستودعات"), "products", "warehouses", "stock_movements", "stock_count");
        InOrder(Keys("الخزينة والبنوك"), "bank_accounts", "receipt_vouchers", "payment_vouchers", "bank_statements", "bank_reconciliation", "cash_position", "exchange_rates");
        InOrder(Keys("المحاسبة العامة"), "chart_of_accounts", "journal_entries", "fiscal_periods", "cost_centers", "budget_variance");
        InOrder(Keys("التقارير المالية"), "trial_balance", "income_statement", "balance_sheet", "cash_flow_statement", "vat_return");
        InOrder(Keys("الرواتب"), "employees", "payroll", "payslips", "wage_protection");
    }

    [Fact]
    public void TheSettingsMenu_StartsWithThePersonal_ThenCompany_ThenCompliance_ThenSystem()
    {
        var sections = NavigationCatalog.SettingsSections;
        Assert.Equal(["preferences"], sections[0].Keys);
        Assert.Equal("company_settings", sections[1].Keys[0]);
        Assert.Contains("zatca_settings", sections[2].Keys);
        Assert.Equal("users", sections[3].Keys[0]);
    }

    [Fact]
    public void ANavGroupHeader_FoldsOnCheckedAndUnchecked_AndTheSettingsMirrorHidesUnlessSearching()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var code = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Brqosh.Desktop", "MainWindow.xaml.cs"));

        Assert.Contains("header.Checked += (_, _) => OnGroupToggled();", code, StringComparison.Ordinal);
        Assert.Contains("header.Unchecked += (_, _) => OnGroupToggled();", code, StringComparison.Ordinal);
        Assert.DoesNotContain("header.Click += (_, _) =>", code, StringComparison.Ordinal);
        Assert.Contains("isSettings ? (searching || KeepNavOpenForAutomation) && matches > 0 : matches > 0", code, StringComparison.Ordinal);
    }
}
