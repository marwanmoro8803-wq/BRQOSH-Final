using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Brqosh.Desktop.Views.Pages;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٢٠) <b>عمود اختيار الصنف — أكبر عطلٍ في الحملة، وأصمتُها.</b>
///
/// <para><b>ما كان:</b> <c>LineRow.ProductId</c> معرَّفٌ في حوارات فاتورة المبيعات وفاتورة
/// المشتريات وأمر الشراء، و<b>يُقرأ</b> عند الترحيل (<c>AddLine(..., row.ProductId, ...)</c>)
/// وفي تحذير الأسعار — <b>ولا يُكتب في موضعٍ واحد</b>: لا عمود في XAML يربطه، ولا إسناد في
/// الكود. فتمرّ كلُّ فاتورةٍ تُنشأ من الواجهة بـ <c>ProductId = null</c> على كل سطورها.</para>
///
/// <para><b>والأثر ليس تجميلياً:</b> <c>SalesInvoicePostingService</c> يُصفّي السطور بـ
/// <c>l.ProductId is long pid</c>، فتخرج قائمةُ المخزون فارغة و<c>cogsAmount = 0</c>،
/// فيُسقط القيدُ سطرَي تكلفة البضاعة المباعة والمخزون <b>كلّياً</b>. بضاعةٌ تكلفتها 800
/// تُباع بـ1,000 تُظهر مجمل ربحٍ <b>1,000 لا 200</b>، وحساب المخزون في الأستاذ لا ينزل ريالاً.
/// وفي الشراء: البضاعة <b>لا تدخل المستودع أبداً</b>، فيصطدم أوّلُ بيعٍ بحارس «الرصيد غير كافٍ —
/// المتوفر 0.00» ويقف الكاشير أمام العميل عاجزاً.</para>
///
/// <para><b>ونقطة البيع سليمة</b> (<c>PosSessionPage</c> يضبط <c>ProductId</c> من الصنف نفسه)،
/// فالكسر محصورٌ في البيع الآجل والشراء — وهما بالضبط ما تحتاجه منشأةٌ تجارية لعملاء الأجل
/// ولتوريد بضاعتها.</para>
///
/// <para><b>ولماذا هذا الحارس لا يُشبه جيرانه:</b> اختبارات الواجهة القائمة تقرأ <b>نصّ</b>
/// ملفّ XAML وتبحث عن سلاسل (<c>Assert.Contains("x:Name=\"KpiGrid\"", xaml)</c>) — وذاك
/// يُثبت أن النصّ مكتوب لا أن العمود موجود ولا أنه مربوطٌ بالحقل الصحيح. هذا الحارس
/// <b>يُنشئ الحوار فعلاً</b> على خيط STA ويمشي على <c>DataGrid.Columns</c> الحقيقية،
/// ويقرأ مسار الربط من كائن <c>Binding</c> نفسه.</para>
/// </summary>
public class DocumentLineProductColumnTests
{
    private static readonly Dispatcher UiDispatcher = StartUiThread();

    private static Dispatcher StartUiThread()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();

            // الحوارات تستعمل موارد الثيم (AppFont وأنماط البطاقات)، وهي تُدمج في
            // موارد التطبيق عند الإقلاع الحقيقيّ — فتُدمج هنا كي يُنشأ الحوار كما يُنشأ فعلاً.
            var theme = new System.Windows.ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Brqosh.Desktop;component/Themes/BrqoshTheme.xaml",
                    UriKind.Absolute),
            };
            if (!app.Resources.MergedDictionaries.Contains(theme))
                app.Resources.MergedDictionaries.Add(theme);

            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(30)), "خيط الواجهة لم يبدأ.");
        return dispatcher!;
    }

    /// <summary>
    /// يُنشئ الحوار ويعيد أعمدة شبكة سطوره. البانية لا تلمس قاعدة البيانات — تحميلُ البيانات
    /// كلُّه في <c>Loaded</c>، ولا يُطلق بالإنشاء وحده.
    /// </summary>
    private static IReadOnlyList<DataGridColumn> LineColumns<TDialog>() where TDialog : new() =>
        UiDispatcher.Invoke(() =>
        {
            var dialog = new TDialog();
            var grid = (DataGrid)((System.Windows.Window)(object)dialog!).FindName("LinesGrid")!;
            return (IReadOnlyList<DataGridColumn>)[.. grid.Columns];
        });

    /// <summary>هل بين الأعمدة عمودٌ يكتب <c>ProductId</c> فعلاً؟</summary>
    private static bool BindsProductId(IReadOnlyList<DataGridColumn> columns) =>
        columns.Any(c => c switch
        {
            DataGridComboBoxColumn combo =>
                (combo.SelectedValueBinding as Binding)?.Path?.Path == "ProductId",
            DataGridBoundColumn bound =>
                (bound.Binding as Binding)?.Path?.Path == "ProductId",
            _ => false,
        });

    /// <summary>
    /// <b>القياس المركزيّ.</b> فاتورة المبيعات — البيع الآجل الذي يخصم المخزون ويُقيّد التكلفة.
    /// </summary>
    [Fact]
    public void SalesInvoiceDialog_HasAProductColumn()
    {
        var columns = LineColumns<SalesInvoiceDialog>();

        Assert.True(BindsProductId(columns),
            "لا عمود يكتب ProductId في فاتورة المبيعات — فكل فاتورةٍ تُرحَّل بلا صنف: " +
            "لا حركة مخزون ولا تكلفة بضاعة مباعة، ومجمل الربح يُعرض بكامل الإيراد.");
    }

    /// <summary>وفاتورة المشتريات — التي تُدخل البضاعة المستودعَ أصلاً.</summary>
    [Fact]
    public void PurchaseInvoiceDialog_HasAProductColumn()
    {
        var columns = LineColumns<PurchaseInvoiceDialog>();

        Assert.True(BindsProductId(columns),
            "لا عمود يكتب ProductId في فاتورة المشتريات — فالبضاعة لا تدخل المستودع، " +
            "ويصطدم أوّلُ بيعٍ بحارس «الرصيد غير كافٍ».");
    }

    /// <summary>وأمر الشراء الذي يتحوّل إلى فاتورة ويرث سطورَها.</summary>
    [Fact]
    public void PurchaseOrderDialog_HasAProductColumn()
    {
        var columns = LineColumns<PurchaseOrderDialog>();

        Assert.True(BindsProductId(columns),
            "لا عمود يكتب ProductId في أمر الشراء — والفاتورة المولَّدة منه ترث الفراغ.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> شاشة الحركة المخزنية كان فيها منتقي أصنافٍ صحيح من قبلُ — فمرورُها
    /// يُثبت أن القياس يقرأ ما يزعم قراءته، لا أنه يمرّ على كل شيء.
    /// </summary>
    [Fact]
    public void StockMovementDialog_AlreadyHadOne()
    {
        var columns = LineColumns<StockMovementDialog>();

        Assert.True(BindsProductId(columns));
    }

    /// <summary>
    /// وحارسٌ معاكس: العمود موجودٌ <b>ومربوطٌ بالحقل الصحيح</b> لا بأيّ حقل. لولا هذا لكان
    /// عمودٌ يربط <c>Description</c> يُمرّر كل ما سبق.
    /// </summary>
    [Fact]
    public void TheProductColumn_BindsProductId_NotSomethingElse()
    {
        // القراءة كلُّها على خيط الواجهة — كائنات WPF لا تُقرأ من خيطٍ آخر.
        var (selectedValuePath, displayMemberPath) = UiDispatcher.Invoke(() =>
        {
            var dialog = new SalesInvoiceDialog();
            var grid = (DataGrid)dialog.FindName("LinesGrid")!;
            var combo = grid.Columns.OfType<DataGridComboBoxColumn>()
                .FirstOrDefault(c => (c.SelectedValueBinding as Binding)?.Path?.Path == "ProductId");
            Assert.NotNull(combo);
            return (combo!.SelectedValuePath, combo.DisplayMemberPath);
        });

        Assert.Equal("Id", selectedValuePath);
        Assert.Equal("Display", displayMemberPath);
    }
}
