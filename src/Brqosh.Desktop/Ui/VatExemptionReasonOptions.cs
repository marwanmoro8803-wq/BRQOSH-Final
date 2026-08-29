using Brqosh.Domain.Tax;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-229 · م1-١ب الشقّ الثاني) خيارات سبب الإعفاء لأعمدة الشبكات — مصدرٌ واحد.
///
/// <para>والحارسُ في النطاق يمنع فئةً غير أساسية بلا سبب، فبلا هذا العمود يصير بابَاً مغلقاً
/// بلا مفتاح: يمنع البيعَ الصفريَّ ولا يهدي إلى حلّ. وهو الدرسُ نفسه من هوية المشتري.</para>
///
/// <para>وتُعرض <b>القائمةُ كاملة</b> لا المرشَّحة بفئة السطر: عمودُ الشبكة لا يرى صفَّه.
/// فيُسبَق كلُّ سبب برمز فئته ليُرى الانتماء بالعين، ويبقى الحارسُ في النطاق هو الفاصل —
/// ورسالتُه تُسمّي الفئتين معاً.</para>
/// </summary>
public static class VatExemptionReasonOptions
{
    public record Option(string Code, string Display);

    /// <summary>«بلا سبب» أوّلاً — وهو حالُ السطر الأساسيّ، وهو السائد.</summary>
    public static IReadOnlyList<Option> All { get; } =
        new[] { new Option("", "— بلا سبب (أساسية) —") }
            .Concat(VatExemptionReason.Catalogue.Select(r =>
                new Option(r.Code, $"{r.Category.UblCode()} — {r.ArabicText} ({r.Code})")))
            .ToList();
}
