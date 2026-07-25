using Brqosh.Domain.Tax;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// خيارات الفئة الضريبية لأعمدة الشبكات في حوارات المستندات (CEEF-208).
///
/// مصدر واحد لكل الحوارات: إضافة فئة مستقبلاً تظهر في كل الشاشات دون تعديل كلٍّ منها.
/// العرض عربيّ والقيمة هي <see cref="VatCategory"/> نفسها — فلا تحويل نصيّ هشّ.
/// </summary>
public static class VatCategoryOptions
{
    public record Option(VatCategory Value, string Display);

    /// <summary>الفئات بترتيب شيوعها في الاستخدام — الأساسية أولاً.</summary>
    public static IReadOnlyList<Option> All { get; } =
    [
        new(VatCategory.Standard, $"أساسية 15% ({VatCategory.Standard.UblCode()})"),
        new(VatCategory.ZeroRated, $"صفرية ({VatCategory.ZeroRated.UblCode()})"),
        new(VatCategory.Exempt, $"معفاة ({VatCategory.Exempt.UblCode()})"),
        new(VatCategory.OutOfScope, $"خارج النطاق ({VatCategory.OutOfScope.UblCode()})"),
    ];
}
