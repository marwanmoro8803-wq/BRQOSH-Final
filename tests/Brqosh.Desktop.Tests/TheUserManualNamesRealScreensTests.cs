using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (ح-١٦٠ · commercial-readiness#8) <b>دليلُ المستخدم يُشحن، ويُسمّي شاشاتٍ وأزراراً موجودة.</b>
///
/// <para>دليلٌ يقول «اضغط فحص التحديث» والزرُّ صار «تحقّق من التحديث» يُضيّع العميلَ أسوأَ من غيابه. فالشاشاتُ التي يُسمّيها
/// تُطابَق بعناوين شجرة التنقّل، والأزرارُ بنصوص صفحاتها — <b>وتغييرُ اسمٍ في البرنامج يُسقط هذا الاختبار</b> حتى يُحدَّث الدليل.</para>
/// </summary>
public class TheUserManualNamesRealScreensTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string Manual = File.ReadAllText(Path.Combine(Root, "docs", "user-manual", "README.md"));

    [Fact]
    public void TheManual_IsShippedBesideTheProgram()
    {
        var csproj = File.ReadAllText(Path.Combine(Root, "src", "Brqosh.Desktop", "Brqosh.Desktop.csproj"));
        Assert.Contains(@"Include=""..\..\docs\user-manual\README.md"" Link=""UserManual-ar.md""", csproj, StringComparison.Ordinal);
    }

    /// <summary>كلُّ شاشةٍ يُسمّيها الدليلُ عنوانٌ في <c>NavigationCatalog</c>.</summary>
    [Theory]
    [InlineData("المستخدمون")]
    [InlineData("إعدادات المنشأة")]
    [InlineData("دليل الحسابات")]
    [InlineData("الحسابات البنكية")]
    [InlineData("الأصناف والمنتجات")]
    [InlineData("وحدات القياس")]
    [InlineData("ترحيل البيانات")]
    [InlineData("فواتير المبيعات")]
    [InlineData("مردودات المبيعات")]
    [InlineData("الفوترة الدورية")]
    [InlineData("الورديات")]
    [InlineData("شاشة البيع")]
    [InlineData("أوامر الشراء")]
    [InlineData("أذونات استلام البضاعة")]
    [InlineData("فواتير المشتريات")]
    [InlineData("حركات المخزون")]
    [InlineData("الجرد")]
    [InlineData("الدفعات والصلاحية")]
    [InlineData("سندات القبض")]
    [InlineData("سندات الصرف")]
    [InlineData("الشيكات")]
    [InlineData("التسوية البنكية")]
    [InlineData("سجل الموظفين")]
    [InlineData("نسب التأمينات")]
    [InlineData("الإجازات والغياب")]
    [InlineData("سلف الموظفين")]
    [InlineData("دورات الرواتب")]
    [InlineData("ملف حماية الأجور")]
    [InlineData("قوائم المواد (BOM)")]
    [InlineData("أوامر الإنتاج")]
    [InlineData("كشف تكلفة الإنتاج")]
    [InlineData("سجل الفواتير الإلكترونية")]
    [InlineData("إعدادات هيئة الزكاة")]
    [InlineData("ميزان المراجعة")]
    [InlineData("إقرار ضريبة القيمة المضافة")]
    [InlineData("الفترات المالية")]
    [InlineData("القيود اليومية")]
    [InlineData("النسخ الاحتياطي")]
    [InlineData("حالة النظام")]
    [InlineData("سجل التدقيق")]
    [InlineData("تفضيلات البرنامج")]
    [InlineData("الدعم والشكاوى")]
    public void EachScreenTheManualNames_IsInTheNavigation(string title)
    {
        var catalog = File.ReadAllText(Path.Combine(Root, "src", "Brqosh.Desktop", "Navigation", "NavigationCatalog.cs"));
        Assert.Contains($"\"{title}\"", catalog, StringComparison.Ordinal);
        Assert.Contains(title, Manual, StringComparison.Ordinal);
    }

    /// <summary>وكلُّ زرٍّ يُسمّيه الدليلُ نصٌّ في صفحته — في XAML أو في الكود بـ<c>LocaleManager.T</c>.</summary>
    [Theory]
    [InlineData("UsersPage", "＋ مستخدم جديد")]
    [InlineData("CompanySettingsPage", "تفعيل الترخيص")]
    [InlineData("CompanySettingsPage", "تفريغ خلفية الشعار (شفافة)")]
    [InlineData("CustomersPage", "إظهار الموقوف")]
    [InlineData("CustomersPage", "إعادة تفعيل")]
    [InlineData("PurchaseOrdersPage", "إنشاء فاتورة من المحدد")]
    [InlineData("GoodsReceiptsPage", "＋ إذن استلام من أمر شراء")]
    [InlineData("StockCountPage", "ترحيل فروقات الجرد")]
    [InlineData("ChequesPage", "تحصيل/صرف")]
    [InlineData("BankReconciliationPage", "استيراد كشف CSV")]
    [InlineData("BankReconciliationPage", "مطابقة تلقائية")]
    [InlineData("BankReconciliationPage", "قيد فروق")]
    [InlineData("BankReconciliationPage", "الرصيد الافتتاحي في الكشف")]
    [InlineData("PayrollPage", "صرف الرواتب")]
    [InlineData("PayrollPage", "سداد التأمينات")]
    [InlineData("PayrollPage", "تخصيص مكافأة نهاية الخدمة")]
    [InlineData("PayrollPage", "تصفية نهاية الخدمة")]
    [InlineData("WageProtectionPage", "تصدير الملف")]
    [InlineData("EmployeesPage", "إنهاء خدمة")]
    [InlineData("FixedAssetsPage", "تشغيل إهلاك شهري")]
    [InlineData("FixedAssetsPage", "عكس تشغيل الإهلاك")]
    [InlineData("FixedAssetsPage", "إلغاء الاقتناء")]
    [InlineData("FixedAssetsPage", "عكس الاستبعاد")]
    [InlineData("FixedAssetDialog", "بفاتورة مورّد (يُقتنى عند ترحيلها)")]
    [InlineData("PurchaseInvoiceDialog", "الأصل الثابت المشترى")]
    [InlineData("WorkOrdersPage", "إصدار المحدد")]
    [InlineData("WorkOrdersPage", "إكمال المحدد")]
    [InlineData("WorkOrdersPage", "هل هذه آخرُ دفعة؟")]
    [InlineData("ZatcaInvoicesPage", "إعادة إرسال المعلَّق")]
    [InlineData("ZatcaInvoicesPage", "عرض XML")]
    [InlineData("VatReturnPage", "عرض الإقرار")]
    [InlineData("FiscalPeriodsPage", "إقفال الفترة المحددة")]
    [InlineData("FiscalPeriodsPage", "إقفال سنوي (SOCPA)")]
    [InlineData("JournalEntriesPage", "↩ عكس القيد")]
    [InlineData("BackupPage", "أخذ نسخة الآن")]
    [InlineData("BackupPage", "نسخ إلى الوجهات")]
    [InlineData("BackupPage", "استعادة من ملفّ...")]
    [InlineData("DiagnosticsPage", "فحص التحديث")]
    [InlineData("DiagnosticsPage", "حزمة الدعم")]
    [InlineData("PreferencesPage", "الشروط والأحكام")]
    [InlineData("PreferencesPage", "دليل المستخدم")]
    [InlineData("PreferencesPage", "فحص التحديث")]
    [InlineData("CustomerSupportPage", "تسجيل الطلب")]
    [InlineData("CustomerSupportPage", "تسجيل وفتح بريد إلى الدعم")]
    [InlineData("CustomerSupportPage", "نسخ نص الطلب")]
    [InlineData("CustomerSupportPage", "إغلاق الطلب")]
    public void EachButtonTheManualNames_IsOnItsScreen(string page, string text)
    {
        var dir = Path.Combine(Root, "src", "Brqosh.Desktop", "Views", "Pages");
        var source = string.Concat(
            new[] { page + ".xaml", page + ".xaml.cs" }
                .Select(f => Path.Combine(dir, f))
                .Where(File.Exists)
                .Select(File.ReadAllText));
        Assert.True(source.Length > 0, $"لا صفحةَ باسم {page}");
        Assert.Contains(text, source, StringComparison.Ordinal);
        Assert.Contains(text, Manual, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
