namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · ح-٢) <b>مِرفاسُ التزاحم — الأداةُ التي تشترطها الخطة لخمسة بنود ولم تكن موجودة.</b>
///
/// <para>خمسةُ بنودٍ في الخطة (م1-١٤ · م1-١٥ · م1-١٦ · م2-١٠ · م2-١١) حالُها
/// <c>testEvidence = NONE</c>، وكلُّها أعطالُ سباق. وسببُ ذلك واحد: <b>لا اختبارَ تزامنٍ في
/// المستودع كلِّه</b>. وأثرُ الغياب مقيسٌ سلفاً — قفلُ ICV في م1-٦ كان <b>حاضراً وميّتاً</b>
/// على مسارَي إلغاء، ومرّ بلا كاشف.</para>
///
/// <para><b>وصدقُ هذه الأداة يقوم على شيئين، وبغيابهما تشهد زوراً:</b></para>
/// <list type="number">
///   <item><b>بوّابةُ انطلاقٍ واحدة.</b> بلا حاجزٍ يجمع الكتّاب، يفرغ أوّلُهم قبل أن يبدأ
///     آخرُهم — فيصير «التوازي» تسلسلاً ويمرّ الاختبارُ على نظامٍ مكسور. يُبنى كلُّ ما يمكن
///     بناؤه قبل الحاجز، ولا يبقى بعده إلا الكتابة.</item>
///   <item><b>اتصالٌ مستقلٌّ لكلّ كاتب</b> — انظر <c>PostgresScope.OpenIndependentContext</c>.
///     فالقفلُ الاستشاريّ مربوطٌ بالمعاملة، ومعاملةٌ واحدة لا تتزاحم مع نفسها.</item>
/// </list>
///
/// <para>ولا يُخفي المِرفاسُ الأخطاء: يُعيدها مصنَّفةً ليقرّر الاختبارُ أيُّها مقبول. فرفضٌ
/// صريحٌ لأحد المتزاحمين سلوكٌ سليم، والتكرارُ الصامت هو العطل.</para>
/// </summary>
internal static class ParallelWriters
{
    /// <summary>حصيلةُ جولةِ تزاحم — النجاحاتُ والإخفاقاتُ كلٌّ باسمه.</summary>
    internal sealed record Outcome<T>(IReadOnlyList<T> Successes, IReadOnlyList<Exception> Failures)
    {
        internal int Total => Successes.Count + Failures.Count;

        /// <summary>وصفٌ يصلح لرسالة تأكيدٍ تُقرأ — لا «Expected true, got false».</summary>
        internal string Describe() =>
            $"نجح {Successes.Count} وأخفق {Failures.Count} من {Total}"
            + (Failures.Count == 0
                ? ""
                : " — " + string.Join(" · ", Failures
                    .Select(e => (e.GetBaseException().GetType().Name, e.GetBaseException().Message))
                    .GroupBy(x => x.Name)
                    .Select(g => $"{g.Key}×{g.Count()}: {g.First().Message.Split('\n')[0]}")));
    }

    /// <summary>
    /// يُشغّل <paramref name="writers"/> كاتباً متزامناً، كلٌّ يُنفّذ <paramref name="write"/>
    /// بمعرّفه. ولا يرمي: تُجمَع الإخفاقاتُ ليحكم عليها الاختبار.
    /// </summary>
    internal static async Task<Outcome<T>> RunAsync<T>(int writers, Func<int, Task<T>> write)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(writers, 2);

        // ‏بوّابةُ الانطلاق: تُفتح مرّةً واحدة بعد أن يبلغها كلُّ كاتب.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new CountdownEvent(writers);

        var runs = Enumerable.Range(0, writers).Select(async i =>
        {
            await Task.Yield();
            ready.Signal();
            await gate.Task;
            return await write(i);
        }).ToArray();

        // ‏لا يُنتظَر الحاجزُ إلى ما لا نهاية: خيطٌ عالقٌ يجب أن يُبلَّغ لا أن يُعلّق التشغيل.
        if (!ready.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException(
                $"لم يبلغ بوّابةَ الانطلاق إلا {writers - ready.CurrentCount} من {writers} كاتباً.");

        gate.SetResult();

        var successes = new List<T>();
        var failures = new List<Exception>();
        foreach (var run in runs)
        {
            try { successes.Add(await run); }
            catch (Exception ex) { failures.Add(ex); }
        }

        return new Outcome<T>(successes, failures);
    }
}
