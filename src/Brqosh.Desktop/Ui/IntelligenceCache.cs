using Brqosh.Application.Intelligence;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل في تباطؤ؟») <b>تحليلٌ ماليٌّ واحدٌ لكلّ طالبَين متقاربَين.</b>
///
/// <para><b>ما كان:</b> فتحُ اللوحة يُشغّل <c>AnalyzeAsync</c> (نحو ٠٫٨ ثانية على دفاترِ خمس سنوات)، <b>ولوحةُ المساعد المفتوحة تُشغّله مرّةً أخرى
/// في اللحظة نفسِها</b> — ثم مرّةً عند كلّ تنقّلٍ إلى أيّ شاشة. فيحمل التطبيقُ القاعدةَ بالتحليل نفسِه مرّتين في فتحةٍ واحدة.</para>
///
/// <para><b>الآن:</b> المهمّةُ الجارية (أو التي انتهت قبل أقلّ من <see cref="Ttl"/>) تُشارَك. والمدّةُ قصيرةٌ عمداً: بطاقاتُ المؤشّرات في
/// اللوحة غيرُ مخزَّنة وتُقرأ طازجةً دائماً، والمخزَّنُ رؤى القواعد وحدَها — ولا يبقى منها قديمٌ بعد ترحيلٍ أكثر من ثوانٍ.</para>
/// </summary>
internal static class IntelligenceCache
{
    internal static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

    private static readonly object Gate = new();
    private static (long CompanyId, DateTime At, Task<FinancialIntelligenceReport> Task)? _entry;

    /// <summary>التحليلُ الجاري أو الأحدثُ لهذه المنشأة. فشلُه لا يُخزَّن.</summary>
    internal static Task<FinancialIntelligenceReport> GetAsync(long companyId, Func<Task<FinancialIntelligenceReport>> factory, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        Task<FinancialIntelligenceReport> task;
        lock (Gate)
        {
            if (_entry is { } e && e.CompanyId == companyId && now - e.At < Ttl && !e.Task.IsFaulted && !e.Task.IsCanceled)
                return e.Task;

            task = factory();
            _entry = (companyId, now, task);
        }

        return task;
    }

    /// <summary>يُسقط المخزَّن — للاختبار، ولمن يعرف أنّ الدفاتر تغيّرت للتوّ.</summary>
    internal static void Invalidate()
    {
        lock (Gate)
            _entry = null;
    }

    /// <summary>التحليلُ بنطاقِ خدماتٍ جديد — الصيغةُ التي تستعملها الشاشاتُ.</summary>
    internal static Task<FinancialIntelligenceReport> AnalyzeAsync(long companyId) =>
        GetAsync(companyId, () => Task.Run(async () =>
        {
            using var scope = AppServices.CreateInfrastructureScope();
            return await scope.ServiceProvider.GetRequiredService<IFinancialIntelligenceService>().AnalyzeAsync(companyId);
        }));
}
