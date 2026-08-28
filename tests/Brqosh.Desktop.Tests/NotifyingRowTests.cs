using System.ComponentModel;
using Brqosh.Desktop.Views.Pages;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م0-١٧) صفوف الجداول تُشعِر بتغيّر خصائصها المحسوبة.
///
/// <para><b>العطل:</b> صفوف السطور كانت أصنافاً عادية بخصائص تلقائية وخاصيةٍ <b>محسوبة</b>.
/// و<c>DataGrid</c> لا يعرف أن المحسوبة تغيّرت حين يُحرَّر مُدخَلها — لأن الصفّ لا يُطلق
/// <c>PropertyChanged</c> أصلاً. فيبقى عمود «الإجمالي» على <c>0.00</c> بينما ملخّص الفاتورة
/// أسفل الشاشة صحيح، ويبقى عمود «الفرق» في الجرد أصفاراً ثم تظهر رسالة «ترحيل 47 فرقاً».</para>
///
/// <para><b>وهو أخطر من عيب عرض:</b> المستخدم يقرأ الصفر فيظنّ إدخاله لم يُحفَظ فيعيده، أو يقرأ
/// «الفرق صفر» فيمضي في جردٍ يظنّه مطابقاً. الرقم الصحيح موجود في الذاكرة ويُرحَّل — والشاشة
/// تكذب عليه.</para>
///
/// <para>هذه الاختبارات تُنشئ الصفوف <b>الحقيقية</b> وتصغي لحدث الإشعار — لا تفحص نصّ ملف.</para>
/// </summary>
public class NotifyingRowTests
{
    private static List<string> Watch(INotifyPropertyChanged row)
    {
        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");
        return raised;
    }

    // ── سطر الفاتورة: الكمية والسعر يُحدّثان الإجمالي ────────────────────────

    /// <summary><b>القياس الأساسيّ.</b> تحرير الكمية يُشعِر بتغيّر «الإجمالي».</summary>
    [Fact]
    public void InvoiceLine_QuantityChange_RaisesLineTotal()
    {
        var row = new SalesInvoiceDialog.LineRow { UnitPrice = 250m };
        var raised = Watch(row);

        row.Quantity = 4m;

        Assert.Contains(nameof(SalesInvoiceDialog.LineRow.Quantity), raised);
        Assert.Contains(nameof(SalesInvoiceDialog.LineRow.LineTotal), raised);
        Assert.Equal(1000m, row.LineTotal);
    }

    /// <summary>وتحرير السعر كذلك — الطرف الثاني من نفس الحاصل.</summary>
    [Fact]
    public void InvoiceLine_UnitPriceChange_RaisesLineTotal()
    {
        var row = new SalesInvoiceDialog.LineRow { Quantity = 3m };
        var raised = Watch(row);

        row.UnitPrice = 99.99m;

        Assert.Contains(nameof(SalesInvoiceDialog.LineRow.LineTotal), raised);
        Assert.Equal(299.97m, row.LineTotal);
    }

    /// <summary>
    /// إعادة ضبط القيمة نفسها لا تُطلق شيئاً — فتحريرُ خليةٍ بلا تعديل لا يُعيد رسم الجدول،
    /// ولا يُغرق الواجهة بإشعارات في جدولٍ بمئة سطر.
    /// </summary>
    [Fact]
    public void SettingTheSameValue_RaisesNothing()
    {
        var row = new SalesInvoiceDialog.LineRow { Quantity = 5m };
        var raised = Watch(row);

        row.Quantity = 5m;

        Assert.Empty(raised);
    }

    // ── صفّ الجرد: الكمية المعدودة تُحدّث الفرق ──────────────────────────────

    /// <summary>
    /// الجرد: عمود «الفرق» يتبع الكمية المعدودة. رصيد نظاميّ 100 وعدٌّ 95 ⟵ الفرق −5.
    /// </summary>
    [Fact]
    public void CountRow_CountedQuantityChange_RaisesVariance()
    {
        var row = new StockCountPage.CountRow { SystemQty = 100m };
        var raised = Watch(row);

        row.CountedQty = 95m;

        Assert.Contains(nameof(StockCountPage.CountRow.Variance), raised);
        Assert.Equal(-5m, row.Variance);
    }

    /// <summary>وعدٌّ مطابق يُشعِر أيضاً — الفرق صفرٌ <b>محسوب</b> لا صفرٌ لم يُحدَّث.</summary>
    [Fact]
    public void CountRow_MatchingCount_StillRaisesVariance()
    {
        var row = new StockCountPage.CountRow { SystemQty = 40m };
        var raised = Watch(row);

        row.CountedQty = 40m;

        Assert.Contains(nameof(StockCountPage.CountRow.Variance), raised);
        Assert.Equal(0m, row.Variance);
    }

    // ── سلة نقطة البيع ──────────────────────────────────────────────────────

    /// <summary>سلة الكاشير: تعديل الكمية يُحدّث إجمالي السطر فوراً.</summary>
    [Fact]
    public void CartRow_QuantityChange_RaisesLineTotal()
    {
        var row = new PosSessionPage.CartRow { UnitPrice = 12.50m };
        var raised = Watch(row);

        row.Quantity = 3m;

        Assert.Contains(nameof(PosSessionPage.CartRow.LineTotal), raised);
        Assert.Equal(37.50m, row.LineTotal);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> الصفوف الثلاثة تُطبّق <see cref="INotifyPropertyChanged"/> فعلاً.
    /// لولا هذا لكانت الاختبارات أعلاه تُنشئ صفوفاً لا تُشعِر ثم تفشل بسببٍ غامض بدل أن تقول
    /// إن العقد نفسه مفقود.
    /// </summary>
    [Fact]
    public void EditableRows_ImplementNotification()
    {
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new SalesInvoiceDialog.LineRow());
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new StockCountPage.CountRow());
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new PosSessionPage.CartRow());
    }
}
