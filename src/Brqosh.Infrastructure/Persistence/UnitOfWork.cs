using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تنفيذ عمليات ذرية على قاعدة البيانات — كل SaveChanges داخل المعاملة تُلغى عند الفشل.
/// </summary>
public static class UnitOfWork
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        BrqoshDbContext db,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        // معاملة متداخلة: السياق مربوط أصلاً من المستوى الخارجي.
        if (db.Database.CurrentTransaction is not null)
            return await action(ct);

        // ‏الكتابةُ تُنفَّذ مرّةً واحدة: لا إعادةَ تلقائيّةً لترحيلٍ غير متكرّر الأثر على سياقٍ يحمل حالةَ المحاولة الأولى.
        using var noRetry = WriteSafeNpgsqlExecutionStrategy.SuspendForWrite();
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            // Outbox (ع-D2، CEEF-204): ربط السياق يجعل أحداث النطاق تُكتب داخل هذه المعاملة،
            // فتُلغى مع المستند إن ارتدّت — بدل حدث «شبح» عن فاتورة لا وجود لها.
            using var ambient = AmbientDbContext.Bind(db);

            // ‏(م-٤ · ن-١٧) **المستوى يُصرَّح به هنا، ويُتحقَّق منه أوّلَ عبارةٍ في الداخل.**
            //
            // ‏والتصريحُ وحدَه **لا يغيّر شيئاً**، مقيساً: نصُّ السلك عند البِدء متطابقٌ
            // ‏حرفاً بحرف مع النداء بلا وسيط — `BEGIN TRANSACTION ISOLATION LEVEL READ
            // ‏COMMITTED` في الحالتين. **فهو توثيقُ عقدٍ لا تغييرُ سلوك**: بحثٌ عن
            // ‏`IsolationLevel` في الشجرة كان يُعيد صفراً، فلا شيءَ يقول ما الذي يفترضه
            // ‏النظام. وعلى SQLite يُخطَّط الوسيطُ إلى `Serializable` تماماً كما يُخطَّط
            // ‏غيابُه — **فلا أثرَ لهذا السطر هناك أصلاً**.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await EnsureReadCommittedAsync(db, ct);

                var value = await action(ct);
                await tx.CommitAsync(ct);
                return value;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(ct);

                // (ع-D1، CEEF-205) رمز التزامن كشف أن صفاً تغيّر بين القراءة والكتابة —
                // مستخدم آخر رحّل على نفس الصنف/الرصيد. العملية ارتدّت كاملة، فلا فساد.
                // لا نُعيد المحاولة تلقائياً: السياق يحمل كيانات قديمة، وإعادة ترحيل بحالة
                // بائتة في نظام محاسبي أخطر من خطأ واضح يعيده المستخدم بنفسه.
                throw new Domain.Accounting.AccountingException(
                    "تعذّر إتمام العملية: عُدِّلت البيانات من جلسة أخرى أثناء الحفظ. " +
                    "لم يُحفظ أي تغيير — أعد فتح الشاشة وحاول مرة أخرى.", ex);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        // الإرسال الخارجي بعد الالتزام حصراً — لا يُخطَر أحد بما قد يُلغى.
        // فشل الإرسال لا يُفشل العملية المحاسبية؛ الأحداث تبقى في الطابور لإعادة المحاولة.
        await OutboxDispatchHook.OnCommittedAsync(db, ct);
        return result;
    }

    /// <summary>
    /// (م-٤ · ن-١٧) <b>يسأل الخادمَ عن مستوى العزل الفعليّ — ويُجمّده بالسؤال نفسِه.</b>
    ///
    /// <para><b>وما يحرسه:</b> سبعةُ مواضعِ أقفالٍ استشاريّةٍ في هذا النظام تقوم على نمطٍ
    /// واحد: <b>اقفل ← ثمّ أعِد القراءة</b> — وأوضحُها <c>FiscalPeriodService</c>، وتعليقُه
    /// فوق <c>ReloadAsync</c> يسمّيه «الفرقَ بين حارسٍ وزينة». <b>وذلك النمطُ يعتمد كلَّه
    /// على «كلُّ عبارةٍ ترى لقطةً جديدة»</b>، وهي خاصّةُ <c>read committed</c> وحدَها.</para>
    ///
    /// <para><b>والكسرُ مقيسٌ لا مُستنتَج</b> (PostgreSQL 17.10، جلستان متشابكتان، والحجبُ
    /// مُثبَتٌ باستطلاع <c>pg_locks</c> لا بالتوقيت): تحت <c>repeatable read</c> انتظر
    /// القارئُ القفلَ ٩٧٨٨ مِلّي ثانية — <b>فالقفلُ عمل</b> — ثمّ قرأ <c>Open</c> وقيمةُ
    /// الصفّ الملتزمة <c>Closed</c>. وتحت <c>serializable</c> المثلُ، <b>وبلا خطأِ تسلسلٍ
    /// إطلاقاً</b>. وشاهدُ ضبطٍ موجب: بلا تشابكٍ يرى <c>repeatable read</c> القيمةَ الصحيحة.</para>
    ///
    /// <para><b>وعلاجا البند نفسِه قِيسا فكانا يُسبّبان العطلَ لا يمنعانه:</b> «ارفعِ العزلَ
    /// على مسارات الإقفال» هو الكسرُ أعلاه بعينه؛ و«ضع قيداً في القاعدة» — زنادٌ قبل
    /// الإدراج — <b>يُهزَم بالآليّة نفسِها</b> إذ يجري داخل لقطة المعاملة عينِها: قِيس أنّه
    /// يرى <c>Open</c> ويسمح بالإدراج تحت التشابك ذاتِه.</para>
    ///
    /// <para><b>ولماذا أوّلَ عبارةٍ بالذات — وهو لبُّ الحيلة:</b> PostgreSQL يرفض
    /// <c>SET TRANSACTION ISOLATION LEVEL</c> بعد أيّ استعلامٍ في المعاملة (‏<c>25001</c>).
    /// <b>فكونُ هذا السؤال أوّلَ عبارةٍ يتحقّق من المستوى ويُغلق بابَ رفعِه معاً</b> — وذلك
    /// هو الطريقُ الوحيدُ الذي أُعيد به إنتاجُ العطل حيّاً على شجرةِ ما قبل الإصلاح.
    /// <b>فالموضعُ نصفُ الحماية، والمقارنةُ نصفُها الآخر</b>، ويُثبَّت الموضعُ باختبارِ
    /// ترتيبٍ لا بالنيّة.</para>
    ///
    /// <para><b>وأمّا المقارنة — ولمَ ليست زينةً وإن كان فرعُها بعيدَ المنال اليوم:</b>
    /// المُشغِّلُ <c>Npgsql</c> يُرسل المستوى صريحاً مع كلّ <c>BEGIN</c> (مقيسٌ من سجلّ
    /// الخادم)، فلا يبلغ التطبيقَ <c>default_transaction_isolation</c> إطلاقاً — <b>ولذلك
    /// لا يُشعَل الرمي اليوم</b>. <b>لكنّ الحزمةَ مثبَّتةٌ على <c>9.0.*</c> عائم</b>،
    /// وسلوكُ مُشغِّلٍ قِيس مرّةً ليس عقداً. فالمقارنةُ هي الزنادُ الوحيدُ لانحرافِ
    /// المُشغِّل، <b>وثمنُها جولةُ ذهابٍ وإياب واحدةٌ على PostgreSQL وحدَه</b>.</para>
    ///
    /// <para><b>وما لا يحرسه — ويُقال بلا تهوين:</b> معاملةً يفتحها المستدعي بنفسه. فالمسارُ
    /// المتداخل (‏<c>CurrentTransaction is not null</c> أعلاه) يعود قبل أن يبلغ هذا المِسبار،
    /// <b>وينضمّ إلى معاملةٍ بمستوًى لا يفحصه أحد</b>. <b>ولا تُقفل ذلك استراتيجيّةُ إعادة
    /// المحاولة كما قد يُظنّ:</b> هي ترفض استعلاماتِ EF و<c>SaveChanges</c> داخل معاملةِ
    /// مستخدم، <b>و<c>Database.ExecuteSqlRaw</c> لا يمرّ بها أصلاً</b> — والشاهدُ في المستودع
    /// نفسِه: سبعةُ مواضعَ في <c>LedgerClosureVsPostingConcurrencyTests</c> تفتح معاملةً ثمّ
    /// تأخذ قفلَ الدفتر بـSQL خام، وهي خضراءُ على PostgreSQL حيّ.</para>
    ///
    /// <para><b>وهذا حدٌّ مقبولٌ لا ثغرةٌ مسكوتٌ عنها:</b> كلُّ مسارٍ محاسبيٍّ في الإنتاج يمرّ
    /// بـ<c>ExecuteInTransactionAsync</c>، والمواضعُ السبعةُ أدواتُ قياسٍ في مِرفاس. <b>لكنّ
    /// الحارسَ أعمى عنها</b>، فلا يُدّعى أنّه يغطّي ما لا يغطّيه.</para>
    /// </summary>
    internal static async Task EnsureReadCommittedAsync(BrqoshDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;

        const string Sql = """
            SELECT current_setting('transaction_isolation') AS "Value"
            """;

        var level = await db.Database.SqlQueryRaw<string>(Sql).SingleAsync(ct);

        if (string.Equals(level, "read committed", StringComparison.OrdinalIgnoreCase)) return;

        throw new InvalidOperationException(
            $"‏مستوى عزلِ المعاملة «{level}» — والنظامُ يقوم على «read committed». "
            + "‏فأقفالُ الدفتر تنتظر ثمّ تُعيد القراءة، وتحت مستوًى أعلى تُعيد لقطةً بائتةً "
            + "‏فيُرحَّل في فترةٍ أُقفلت وهي تبدو مفتوحة. أُوقفت العمليّة ولم يُكتب شيء.");
    }

    public static Task ExecuteInTransactionAsync(
        BrqoshDbContext db,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default) =>
        ExecuteInTransactionAsync(db, async token =>
        {
            await action(token);
            return true;
        }, ct);
}
