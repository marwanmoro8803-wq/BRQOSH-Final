using System.Text.RegularExpressions;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-226) التواريخ والأرقام اللاتينية في المطبوعات معزولة عن إعادة الترتيب.
///
/// **العلّة مقيسة بالعين على PDF مولَّد:** الكود يكتب <c>التاريخ: 2026-03-14</c>
/// والمطبوع يعرض <c>التاريخ: 14-03-2026</c> — الخوارزمية ثنائية الاتجاه تعكس مقاطع
/// الرقم داخل فقرة عربية. والتذييل في **الوثيقة نفسها** يعرض <c>2026-07-25</c>
/// صحيحاً لأنّه يبدأ بحرف لاتينيّ. صيغتان متناقضتان في ورقة واحدة، ومستندٌ ماليّ
/// لا يحتمل لبساً في التاريخ.
///
/// الحارس يمنع رجوع الصيغة الخام: أيّ <c>{x:yyyy-MM-dd}</c> داخل نصّ مطبوع يجب أن
/// يمرّ عبر <see cref="PrintText"/>.
/// </summary>
public class PrintTextDirectionTests
{
    private const char Lri = '⁦';
    private const char Pdi = '⁩';

    [Fact]
    public void Date_IsIsoAndIsolated()
    {
        var text = PrintText.Date(new DateOnly(2026, 3, 14));

        Assert.Equal($"{Lri}2026-03-14{Pdi}", text);
    }

    [Fact]
    public void Timestamp_IsIsolated()
    {
        var text = PrintText.Timestamp(new DateTime(2026, 3, 14, 21, 5, 0));

        Assert.Equal($"{Lri}2026-03-14 21:05{Pdi}", text);
    }

    [Fact]
    public void Code_IsolatesDocumentNumbers()
    {
        Assert.Equal($"{Lri}INV-2026-000001{Pdi}", PrintText.Code("INV-2026-000001"));
    }

    /// <summary>نصّ فارغ لا يُلفّ — وإلا امتلأت المطبوعات بمحارف تحكّم بلا محتوى.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyValues_AreNotWrapped(string? value)
    {
        Assert.Equal(string.Empty, PrintText.Code(value));
    }

    /// <summary>
    /// الحارس البنيويّ: لا مولّد PDF يطبع تاريخاً بالصيغة الخام.
    /// (سجلّات التدقيق مستثناة — تلك بيانات مخزَّنة لا نصّ معروض.)
    /// </summary>
    [Fact]
    public void NoPdfGenerator_FormatsDatesRaw()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Brqosh.Infrastructure", "Reporting"));
        Assert.True(Directory.Exists(root), $"مجلد التقارير غير موجود: {root}");

        var raw = new Regex(@"\{[^{}]*:yyyy-MM-dd[^{}]*\}", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*PdfGenerator.cs")
                     .Concat(Directory.EnumerateFiles(root, "PrintBranding.cs")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (raw.IsMatch(lines[i]))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1} → {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "تواريخ بصيغة خام في مطبوعات — تُعكس مقاطعها في الفقرة العربية:\n  - " +
            string.Join("\n  - ", offenders));
    }
}
