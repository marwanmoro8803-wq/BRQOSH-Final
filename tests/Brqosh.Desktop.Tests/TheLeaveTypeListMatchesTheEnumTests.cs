using System.IO;
using System.Text.RegularExpressions;
using Brqosh.Domain.Payroll;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١١٨) <b>قائمةُ أنواع الإجازة في الحوار بترتيب <see cref="LeaveType"/> نفسِه.</b>
///
/// <para>‏الحوارُ يحوّل رقمَ العنصر المختار إلى النوع مباشرةً (<c>(LeaveType)TypeBox.SelectedIndex</c>)، <b>فعنصرٌ يُدرَج في غير
/// ‏موضعه يُسجّل نوعاً غيرَ المقصود</b> — «ولادة مولود» تصير «امتحاناً» بلا حدٍّ أو العكس — ولا يشتكي شيء. والحارسُ يقرأ XAML
/// ‏بنيويّاً ولا يفتح نافذة.</para>
/// </summary>
public class TheLeaveTypeListMatchesTheEnumTests
{
    private static readonly Dictionary<LeaveType, string> Labels = new()
    {
        [LeaveType.Annual] = "سنوية",
        [LeaveType.Sick] = "مرضية",
        [LeaveType.Unpaid] = "بلا أجر",
        [LeaveType.Maternity] = "وضع",
        [LeaveType.Marriage] = "زواج",
        [LeaveType.Bereavement] = "وفاة",
        [LeaveType.Hajj] = "حج",
        [LeaveType.Exam] = "امتحان",
        [LeaveType.Birth] = "ولادة مولود",
    };

    private static string RepoRoot()
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

    private static List<string> DialogItems()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages", "LeaveRequestDialog.xaml"));
        var box = Regex.Match(xaml, @"<ComboBox x:Name=""TypeBox""[^>]*>(.*?)</ComboBox>", RegexOptions.Singleline);
        Assert.True(box.Success, "لا قائمةَ TypeBox في LeaveRequestDialog.xaml.");
        return Regex.Matches(box.Groups[1].Value, @"<ComboBoxItem Content=""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
    }

    [Fact]
    public void Every_leave_type_has_its_item_at_its_own_index()
    {
        var items = DialogItems();

        // ‏شاهدُ ضبط: جدولُ التسميات يغطّي الأنواعَ كلَّها — وإلّا مرّ نوعٌ جديدٌ بلا عنصر.
        Assert.Equal(Enum.GetValues<LeaveType>().Length, Labels.Count);
        Assert.Equal(Labels.Count, items.Count);
        foreach (var (type, label) in Labels)
            Assert.Equal(label, items[(int)type]);
    }
}
