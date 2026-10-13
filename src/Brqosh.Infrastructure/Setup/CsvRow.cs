using System.Text;

namespace Brqosh.Infrastructure.Setup;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٥ · <c>data-upgrade-performance#8</c>) <b>قاسمُ صفّ CSV الواحد — بالحقول المُقتبَسة كما تُصدّرها
/// الأنظمةُ فعلاً.</b>
///
/// <para>‏كان ترحيلُ البيانات يقسم بالفاصلة وحدَها: «شركة النور، فرع جدة» — مُقتبَساً في كلّ نظامٍ يُصدّر CSV — يصير عمودين،
/// فيُرفض السطرُ أو يُقرأ اسمٌ مبتور. <b>وكان لكشف البنك قاسمٌ ثانٍ</b> يعرف التنصيصَ ولا يعرف <c>""</c> (علامةً حرفيّةً داخل
/// الحقل) فيأكلها. <b>فقاسمٌ واحدٌ للطرفين</b> — RFC 4180 في الصفّ الواحد: الفاصلةُ داخل التنصيص من الحقل، و<c>""</c> علامةٌ
/// حرفيّة. <b>وحدُّه</b>: سطرٌ جديدٌ داخل حقلٍ مُقتبَس غيرُ مدعوم — فالملفُّ يُقسَم أسطراً قبل هذا.</para>
/// </summary>
public static class CsvRow
{
    public static string[] Split(string row)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < row.Length; i++)
        {
            var ch = row[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < row.Length && row[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }

            if (ch == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
