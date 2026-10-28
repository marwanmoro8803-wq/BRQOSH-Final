using System.Windows.Controls;
using Brqosh.Domain.Companies;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// قائمةُ العملة في حوارات المستندات (فواتير البيع والشراء وسندات القبض والصرف) — <b>اختيارٌ لا كتابةٌ حرّة</b>.
///
/// <para><b>ما كان:</b> حقلُ نصٍّ حرّ يقبل أيَّ ثلاثة أحرف، فرمزٌ مغلوطٌ («SR» أو «usd ») يصل إلى <c>SetCurrency</c>.</para>
/// <para><b>الآن:</b> عملةُ الأساس أوّلاً ومختارةٌ افتراضياً، ثم العملاتُ التي لها سعرُ صرفٍ مسجَّل، ثم بقيّةُ
/// <see cref="CurrencyCatalog.Codes"/>. والقائمةُ تُملأ متزامنةً عند البناء، فالمستندُ المُعاد (seed) يجد عملتَه قبل التحميل.</para>
/// </summary>
public static class CurrencyChoices
{
    public static void Fill(ComboBox box, string baseCode, IEnumerable<string>? used = null)
    {
        var current = box.SelectedItem as string ?? baseCode;
        var list = new List<string>();
        void Add(string? code)
        {
            var c = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (c.Length > 0 && !list.Contains(c)) list.Add(c);
        }

        Add(baseCode);
        foreach (var code in used ?? Array.Empty<string>()) Add(code);
        foreach (var code in CurrencyCatalog.Codes) Add(code);
        Add(current);
        box.ItemsSource = list;
        box.SelectedItem = current.Trim().ToUpperInvariant();
    }

    public static void Select(ComboBox box, string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length == 0) return;
        if (box.ItemsSource is List<string> list && !list.Contains(c))
        {
            box.ItemsSource = null;
            list.Add(c);
            box.ItemsSource = list;
        }

        box.SelectedItem = c;
    }
}
