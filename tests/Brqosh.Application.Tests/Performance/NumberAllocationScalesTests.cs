using System.Data.Common;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brqosh.Application.Tests.Performance;

/// <summary>
/// (CEEF-229 · م2-٤أ) <b>ترقيمُ المستندات كان يطلب الدفترَ كلَّه ليُعطي رقماً واحداً.</b>
///
/// <para><b>العطل — مُثبَتٌ باعتراض الأمر الذي تنفّذه القاعدة:</b> مولّدا الرقم كانا يُصدران
/// <c>SELECT "EntryNumber" … WHERE …</c> <b>بلا حدّ صفوف</b>، ثمّ يُحمّلان الناتج كلَّه
/// (<c>ToListAsync</c>) ويفرزانه في الذاكرة لأخذ الأكبر. فلإعطاء الرقم رقم ن تُنقل ن-١ سلسلة
/// من القاعدة إلى الذاكرة. وهذا يخدم القيود والفواتير والسندات والإشعارات وأوامر الشراء
/// والحركات المخزنية — أي كلَّ ما يُرقَّم.</para>
///
/// <para><b>وحدُّ ما أُثبت — وهذا تصحيحٌ لقراءةٍ أولى كانت خاطئة:</b> ما دفعني إلى هنا قراءةُ
/// بوّابتَي الأداء (٢٠٠ قيداً بمتوسط 4.33ms و٢٠٠٠ بمتوسط 21.40ms)، وقرأتُها نمواً تربيعياً.
/// <b>وكانت خطأً</b>: الاختباران يعملان في عمليةٍ واحدة، والمشدَّدُ سبق فامتصّ ثمنَ الإحماء
/// (JIT وبناء استعلامات EF) فبدا بطيئاً وبدا الآخرُ سريعاً. أي أنّ الفرق كان <b>ترتيبَ
/// التشغيل لا حجمَ الدفتر</b>.</para>
///
/// <para>وبنيتُ قياساً زمنياً يُهمل الإحماء ويقارن كلفةَ القيد عند حجمين، فأعطى على ثلاث
/// جولاتٍ متتالية <c>1.02× · 1.91× · 1.06×</c> بقيمٍ مطلقة تتأرجح بين 32ms و83ms —
/// <b>ضجيجٌ يفوق الأثرَ المقيس</b>. فحُذف: بوّابةٌ لا تُفرّق بين إصلاحٍ وعدمه ليست بوّابة.</para>
///
/// <para><b>فما بقي هو القياس الحتميّ:</b> نصُّ الاستعلام. والسؤال ليس «كم استغرق؟» بل
/// <b>«هل تأخذ القاعدةُ الأكبر، أم يأخذ العميلُ كلَّ شيءٍ ثمّ يفرز؟»</b> — وهو سؤالٌ جوابُه
/// ثابتٌ لا يتقلّب بتقلّب الجهاز. والأثرُ الزمنيّ لم يُقَس عند هذه الأحجام، ويُتوقَّع أن يظهر
/// على دفترٍ أكبر وعلى PostgreSQL عبر الشبكة حيث نقلُ الصفوف ثمنُه حقيقيّ.</para>
/// </summary>
public class NumberAllocationScalesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly SqlCapture _capture = new();

    public NumberAllocationScalesTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_capture)
            .Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>يلتقط نصوص الأوامر التي تصل القاعدة فعلاً — لا ما نظنّ أنّ LINQ يترجمه.</summary>
    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task<long> SeedAsync(int entries)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة الترقيم", "310123456700003", BusinessProfile.Retail);

        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");
        var date = new DateOnly(2026, 10, 1);

        for (var i = 0; i < entries; i++)
        {
            var draft = JournalEntry.CreateDraft(company.Id, date, $"ترقيم {i}", sourceDocument: $"Num:{i}");
            draft.AddLine(cash, 1m, 0m);
            draft.AddLine(capital, 0m, 1m);
            await new JournalPostingService(_db).PostAsync(draft);
        }

        return company.Id;
    }

    /// <summary>
    /// <b>القياس المركزيّ — حتميّ.</b> استعلامُ الترقيم يجب أن يطلب <b>صفّاً واحداً</b> من
    /// القاعدة. فإن خلا من حدّ الصفوف فهو ينقل الدفترَ كلَّه إلى الذاكرة ليأخذ منه رقماً.
    /// </summary>
    [Fact]
    public async Task AllocatingANumber_AsksTheDatabaseForOneRow_NotTheWholeLedger()
    {
        var companyId = await SeedAsync(3);

        _capture.Commands.Clear();
        var number = await new JournalEntryNumberGenerator(_db).NextAsync(companyId, new DateOnly(2026, 10, 1));

        Assert.Equal("JV-2026-000004", number);

        var numbering = _capture.Commands
            .Where(c => c.Contains("EntryNumber", StringComparison.Ordinal))
            .ToList();

        Assert.True(numbering.Count > 0, "لم يُلتقط استعلامُ الترقيم — المعترِضُ لا يعمل.");

        Assert.True(
            numbering.All(c => c.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                            || c.Contains("TOP(", StringComparison.OrdinalIgnoreCase)
                            || c.Contains("FETCH", StringComparison.OrdinalIgnoreCase)),
            "استعلامُ الترقيم بلا حدّ صفوف — ينقل كلّ أرقام الشركة والسنة إلى الذاكرة "
            + "ثمّ يفرزها فيها لأخذ رقمٍ واحد:\n  " + string.Join("\n  ", numbering));
    }

    /// <summary>
    /// <b>وترقيمُ المستندات كذلك</b> — <see cref="DocumentNumberGenerator"/> يخدم الفواتير
    /// والسندات والإشعارات، وكان يحمل العطلَ نفسه. وإصلاحُ القيود وحدها كان يترك أكثرَ
    /// ما يُرقَّم في النظام على حاله.
    /// </summary>
    [Fact]
    public async Task DocumentNumbering_AlsoAsksForOneRow()
    {
        var companyId = await SeedAsync(1);

        _capture.Commands.Clear();
        var number = await DocumentNumberGenerator.NextAsync(
            _db, _db.SalesInvoices.Where(i => i.CompanyId == companyId).Select(i => i.InvoiceNumber),
            "INV", 2026, companyId);

        Assert.Equal("INV-2026-000001", number);

        var numbering = _capture.Commands
            .Where(c => c.Contains("InvoiceNumber", StringComparison.Ordinal))
            .ToList();

        Assert.True(numbering.Count > 0, "لم يُلتقط استعلامُ ترقيم المستند.");
        Assert.True(
            numbering.All(c => c.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                            || c.Contains("TOP(", StringComparison.OrdinalIgnoreCase)
                            || c.Contains("FETCH", StringComparison.OrdinalIgnoreCase)),
            "ترقيمُ المستند بلا حدّ صفوف:\n  " + string.Join("\n  ", numbering));
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو صار الترقيمُ لا يستعلم إطلاقاً — أو تغيّر اسمُ العمود —
    /// لمرّ الاختباران أعلاه على مجموعةٍ فارغة. فيُثبت هنا أنّ الالتقاط يرى استعلاماتٍ حقيقية.
    /// </summary>
    [Fact]
    public async Task TheInterceptor_ReallySeesTheQueries()
    {
        await SeedAsync(1);

        Assert.True(_capture.Commands.Count > 5,
            $"لم يُلتقط إلا {_capture.Commands.Count} أمراً — المعترِضُ غيرُ موصول.");
    }

    /// <summary>
    /// <b>والترقيم يبقى صحيحاً بعد نقل الفرز إلى القاعدة</b> — وهو ما يهمّ المالك أكثر من
    /// السرعة: تسلسلٌ متّصلٌ بلا ثغرةٍ ولا تكرار.
    /// </summary>
    [Fact]
    public async Task TheSequence_StaysContiguous()
    {
        var companyId = await SeedAsync(12);

        var numbers = await _db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.EntryNumber != null)
            .Select(e => e.EntryNumber!)
            .ToListAsync();

        var expected = Enumerable.Range(1, 12).Select(i => $"JV-2026-{i:D6}").ToList();
        Assert.Equal(expected, numbers.OrderBy(n => n, StringComparer.Ordinal).ToList());
    }
}
