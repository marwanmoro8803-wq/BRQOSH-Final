using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-229 · م0-١٧) أساسٌ لصفوف الجداول القابلة للتحرير.
///
/// <para><b>العطل:</b> صفوف السطور كانت أصنافاً عادية بخصائص تلقائية وخاصيةٍ <b>محسوبة</b>
/// (<c>LineTotal</c>, <c>Variance</c>). و<c>DataGrid</c> لا يعرف أن الخاصية المحسوبة تغيّرت حين
/// يُحرَّر مُدخَلها — لأن الصفّ لا يُطلق <c>PropertyChanged</c> أصلاً. فيبقى عمود «الإجمالي»
/// على <c>0.00</c> بينما ملخّص الفاتورة أسفل الشاشة صحيح، ويبقى عمود «الفرق» في الجرد أصفاراً
/// ثم تظهر رسالة «ترحيل 47 فرقاً».</para>
///
/// <para><b>ولماذا هو أخطر من عيب عرض:</b> المستخدم يقرأ الصفر فيظنّ إدخاله لم يُحفَظ فيعيده،
/// أو يقرأ «الفرق صفر» فيمضي في جردٍ يظنّه مطابقاً. الرقم الصحيح موجود في الذاكرة ويُرحَّل —
/// والشاشة تكذب عليه.</para>
///
/// <para><b>الاستعمال:</b> <c>Set</c> يُطلق الإشعار للخاصية المتغيّرة <b>ولكل خاصية محسوبة
/// تعتمد عليها</b> — والثانية هي المقصودة، إذ لا يستطيع WPF استنتاج التبعية.</para>
/// <code>
/// public decimal Quantity
/// {
///     get => _quantity;
///     set => Set(ref _quantity, value, [nameof(LineTotal)]);
/// }
/// </code>
/// </summary>
public abstract class NotifyingRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// يضبط الحقل ويُطلق الإشعار له ولكل خاصية محسوبة تعتمد عليه.
    /// لا يُطلق شيئاً إن لم تتغيّر القيمة — فتحريرُ خليةٍ بلا تعديل لا يُعيد رسم الجدول.
    /// </summary>
    protected void Set<T>(ref T field, T value, string[]? alsoRaise = null,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        Raise(propertyName);
        foreach (var dependent in alsoRaise ?? [])
            Raise(dependent);
    }

    protected void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
