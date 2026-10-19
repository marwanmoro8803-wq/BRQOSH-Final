using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>accounting-core#6</c>) <b>اختبارٌ يُرحّل قيوداً يدويّةً كبيرةً لسببٍ غيرِ الاعتماد</b> — يرفع حدَّ اعتماد منشأته
/// كما تفعل منشأةٌ بمستخدمٍ واحد (لا معتمدَ ثانيَ لها). <b>ولا يُستعمل في اختبار الحدّ نفسِه</b>؛ وكلُّ مستدعٍ يقول لماذا.
/// </summary>
public static class ManualJournals
{
    public static void LiftApprovalThreshold(BrqoshDbContext db)
    {
        foreach (var company in db.Companies.ToList())
            company.SetManualJournalApprovalThreshold(1_000_000_000m);
        db.SaveChanges();
    }
}
