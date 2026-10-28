using Brqosh.Domain.Zatca;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// <b>سطرُ الامتثال المطبوع — واحدٌ لكلّ ورقة.</b>
///
/// <para> الحالةُ تُطبع بالعربيّة، <b>والمحاكاةُ تُعلَن صراحةً</b> — كان يُطبع اسمُ
/// الحالة البرمجيّ («Cleared») على فاتورةٍ عربيّة، والمحاكاةُ تُعلَّم «معتمدة» فيقرأ المدقّقُ أنّ الهيئة
/// اعتمدت مستنداً لم ترَه. <b>وأُصلح ذلك في الفاتورة وحدَها</b>: المستخلصُ بنى سطرَه بيده فبقي يطبع
/// «SimulatedLocally» و«ReadyToClear» كما هما — قارئٌ ثانٍ يشتقّ ما تملكه الشجرةُ (<c>NameAr</c>).</para>
/// </summary>
internal static class PrintedCompliance
{
    internal static string? Line(int? icv, string? complianceStatus)
    {
        var parts = new List<string>();
        if (icv is int counter)
            parts.Add($"ICV: {counter}");

        if (!string.IsNullOrWhiteSpace(complianceStatus))
        {
            parts.Add(Enum.TryParse<ZatcaComplianceStatus>(complianceStatus, out var parsed)
                ? parsed.NameAr()
                : complianceStatus);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
