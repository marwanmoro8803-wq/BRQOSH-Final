namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// <b>خطوةُ مخطّطٍ فشلت وابتُلع فشلُها — باسمها وسببها.</b>
/// </summary>
/// <param name="Step">
/// اسمُ الخطوة — <b>وهو ما يُضيّق البحث من عشرين موضعاً إلى واحد</b>. ورسالةُ المحرّك
/// وحدَها تقول «relation does not exist» <b>ولا تقول أيَّ خطوةٍ طلبتها</b>.
/// </param>
/// <param name="ExceptionType">
/// نوعُ الاستثناء — <b>وهو ما يفرّق «موجودٌ سلفاً» عن «ممنوع» عن «القرص ممتلئ»</b>.
/// </param>
/// <param name="Message">رسالةُ المحرّك كما هي — <b>ولا تُترجَم ولا تُختصر</b>.</param>
public sealed record SchemaStepFailure(string Step, string ExceptionType, string Message)
{
    /// <summary>سطرٌ يصلح للسجلّ وللشاشة معاً.</summary>
    public override string ToString() => $"{Step} — {ExceptionType}: {Message}";
}
