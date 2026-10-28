using System.Text.RegularExpressions;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (م-٥ · ن-٥١) <b>نوعُ السطر المشترك يجب أن يُخزَّن بالطريقة نفسِها عند كلّ مالك.</b>
///
/// <para><b>ما كان قبلها:</b> <c>SalesInvoiceLine</c> نوعٌ واحد يملكه <b>تسعةُ</b> مستندات،
/// و<c>VatCategory</c> فيه كان نصّاً عند سبعةٍ و<b>عدداً صحيحاً</b> عند اثنين
/// (<c>sales_quotation_lines</c> و<c>purchase_request_lines</c>). <b>ولا بوّابةَ تقارن مالكاً
/// بمالك</b>: مسبارُ المخطّط (<c>tools/SchemaCoverageProbe</c>) يقارن النموذجَ بالقاعدة
/// <b>جدولاً جدولاً</b>، وطرفا ن-٥١ كلاهما متّسقٌ مع نفسه — فالمسبارُ أخضرُ بحقّ والعطلُ قائم.</para>
///
/// <para><b>وتقسيمُ العمل يُكتب هنا كي لا يُحذف أحدُهما بحجّة التكرار:</b> هذه البوّابةُ نصفُ
/// «نموذج ↔ نموذج» — ولا ترى DDL المكتوبَ باليد؛ والمسبارُ نصفُ «نموذج ↔ قاعدة» — ولا يرى
/// اختلافَ مالكَين. <b>ولا واحدَ منهما وحدَه يكشف ن-٥١ مع ارتدادِ DDL معاً</b>، ولذلك يُضاف
/// هنا فحصٌ نصّيٌّ ثالثٌ على موضعَي DDL في <c>BrqoshSchemaUpdater</c>.</para>
///
/// <para><b>وحدُّ البوّابة مكتوبٌ فيها — أربعةُ أخطاءٍ تجنّبتها عمداً:</b></para>
/// <list type="number">
///   <item><b>الخضرةُ بالغياب.</b> قارئٌ يُرجع «مفقود» كنصٍّ قابلٍ للمقارنة يجعل عموداً
///   <b>محذوفاً من التسعة</b> إجماعاً تامّاً. فالقارئان هنا <b>يرميان</b> عند الغياب ولا
///   يُرمّزانه، وتُشترط التسعةُ بأسمائها.</item>
///   <item><b>التماثلُ بلا اتّجاه.</b> بوّابةٌ تشترط «اتّفقوا» تصير خضراءَ إذا نُزع التحويلُ
///   عن السبعة بدل أن يُضاف إلى الاثنين — <b>وهو الإصلاحُ المعكوس</b>. فهنا شرطٌ صريحٌ على
///   <b>القيمة</b>: نصٌّ بسقف ١٦ عند كلّ مالك، و<c>TEXT</c> على القرص — بلا حاجةٍ إلى PostgreSQL.</item>
///   <item><b>مقارنةُ نوع CLR وحدَه.</b> <c>HasConversion&lt;string&gt;()</c> بلا
///   <c>HasMaxLength</c> يُعطي <c>String</c> نفسَه ويُنشئ <c>text</c> بلا حدّ — والمستودعُ
///   عُضّ بهذا من قبل (<c>BrqoshDbContext.PayrollTreasuryZatca.cs</c>). فالمقارنةُ <b>رباعيّة</b>:
///   نوعُ المزوّد ونوعُ العمود والسقفُ والقابليّةُ للعدم.</item>
///   <item><b>اختبارٌ ذاتيٌّ فوق طبقة العطل.</b> فحصُ دالّةِ المقارنة بخريطةٍ مصنوعةٍ باليد
///   يُثبت المقارنةَ ولا يُثبت <b>القارئ</b> — والعطلُ يسكن القارئ. فالاختبارُ الذاتيُّ هنا
///   يمرّ <b>بالقارئَين الحقيقيَّين</b>: سياقٌ مصغَّرٌ بمالكَين مختلفَين، وقاعدةُ SQLite
///   بعمودٍ <c>TEXT</c> وآخرَ <c>INTEGER</c>.</item>
/// </list>
/// </summary>
public class SharedOwnedTypeStorageTests
{
    /// <summary>
    /// أصحابُ السطر المشترك التسعة — مكتوبون صراحةً لا محسوبين، <b>كي يُجبِر العاشرُ على
    /// مراجعةٍ بشريّة</b> بدل أن ينضمّ صامتاً إلى إجماعٍ لم يُقصد أن يشملَه.
    /// </summary>
    private static readonly string[] SharedLineTables =
    [
        "credit_note_lines", "debit_note_lines", "purchase_invoice_lines",
        "purchase_order_lines", "purchase_request_lines", "recurring_invoice_plan_lines",
        "sales_invoice_lines", "sales_order_lines", "sales_quotation_lines",
    ];

    private const string SharedLineType = "SalesInvoiceLine";

    // ───────────────────────── القارئان ─────────────────────────
    // ‏كلاهما يرمي عند الغياب. **الغيابُ ليس اتّفاقاً** — وهو الخطأ الذي أسقط نُسخاً سابقة.

    /// <summary>يقرأ تخزينَ خاصيّةٍ عند كلّ مالك من <b>النموذج الحيّ</b> (لا من اللقطة المولَّدة).</summary>
    private static SortedDictionary<string, string> StorageByTable(
        DbContext db, string ownedTypeName, string propertyName)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var owned in db.Model.GetEntityTypes().Where(e => e.IsOwned()))
        {
            if (owned.ClrType.Name != ownedTypeName)
                continue;

            var store = StoreObjectIdentifier.Create(owned, StoreObjectType.Table)
                ?? throw new InvalidOperationException(
                    $"‏نوعٌ مملوكٌ بلا جدول: {ownedTypeName} تحت {owned.DisplayName()}.");

            var property = owned.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"‏الخاصيّةُ «{propertyName}» غيرُ موجودةٍ على {ownedTypeName} عند الجدول "
                    + $"«{store.Name}». **الغيابُ ليس اتّفاقاً** — إمّا أن تُعاد أو يُعدَّل الفحص.");

            map[store.Name] = StorageTuple(property, store);
        }

        if (map.Count == 0)
            throw new InvalidOperationException(
                $"‏لا مالكَ واحدٌ لـ{ownedTypeName} في النموذج — القارئُ أعمى، لا النموذجُ فارغ.");

        return map;
    }

    /// <summary>
    /// الرباعيّة: نوعُ المزوّد · نوعُ العمود · السقف · القابليّةُ للعدم.
    /// <b>ونوعُ العمود داخلٌ فيها عمداً</b>: <c>text</c> و<c>character varying(32)</c> يتساويان
    /// في نوع CLR ويختلفان على القرص.
    /// </summary>
    private static string StorageTuple(IReadOnlyProperty p, StoreObjectIdentifier store) =>
        string.Join('/',
            (p.GetProviderClrType() ?? p.ClrType).Name,
            p.GetColumnType(store) ?? "(بلا نوعٍ مُعلَن)",
            p.GetMaxLength()?.ToString() ?? "بلا سقف",
            p.IsNullable ? "يقبل العدم" : "لا يقبل العدم");

    /// <summary>يقرأ النوعَ المُعلَن على قرص SQLite. يرمي عند غياب الجدول أو العمود.</summary>
    private static async Task<SortedDictionary<string, string>> SqliteDeclaredTypesAsync(
        SqliteConnection connection, IEnumerable<string> tables, string column, CancellationToken ct = default)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT type FROM pragma_table_info($table) WHERE name = $column";
            command.Parameters.AddWithValue("$table", table);
            command.Parameters.AddWithValue("$column", column);

            var declared = await command.ExecuteScalarAsync(ct) as string
                ?? throw new InvalidOperationException(
                    $"‏الجدولُ «{table}» لا يحمل عموداً اسمُه «{column}» على القرص. "
                    + "**الغيابُ ليس اتّفاقاً.**");

            map[table] = declared;
        }

        return map;
    }

    /// <summary>يصف الاختلافَ أو يُرجع <c>null</c>. لا يُستدعى إلّا على خريطةٍ أُثبتت كاملة.</summary>
    private static string? Disagreement(string subject, SortedDictionary<string, string> map) =>
        map.Values.Distinct(StringComparer.Ordinal).Count() <= 1
            ? null
            : $"{subject}: " + string.Join(" | ", map.Select(kv => $"{kv.Key}={kv.Value}"));

    // ───────────────────────── الاختباراتُ الذاتيّة ─────────────────────────
    // ‏تمرّ بالقارئَين الحقيقيَّين، لا بدالّة المقارنة وحدَها.

    [Fact]
    public void TheModelReader_SeesADivergenceBetweenTwoOwners()
    {
        using var probe = DivergentProbeContext.Create();

        var map = StorageByTable(probe, nameof(ProbeLine), nameof(ProbeLine.Category));

        // ‏شاهدُ ضبط: القارئُ رأى المالكَين فعلاً قبل أن يُسأل عن اختلافهما.
        Assert.Equal(["probe_a_lines", "probe_b_lines"], map.Keys);

        var complaint = Disagreement(nameof(ProbeLine.Category), map);
        Assert.False(complaint is null,
            $"‏القارئُ لم يرَ اختلافاً مزروعاً عمداً — فالبوّابةُ عمياء: {string.Join(" | ", map)}");
    }

    [Fact]
    public void TheModelReader_RefusesToCallAbsenceAnAgreement()
    {
        using var probe = DivergentProbeContext.Create();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => StorageByTable(probe, nameof(ProbeLine), "GhostColumn"));

        Assert.Contains("الغيابُ ليس اتّفاقاً", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDiskReader_SeesADivergenceBetweenTwoTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE probe_text (Category TEXT NOT NULL);
                CREATE TABLE probe_int  (Category INTEGER NOT NULL);
                CREATE TABLE probe_bare (Other TEXT NULL);
                """;
            await create.ExecuteNonQueryAsync();
        }

        var map = await SqliteDeclaredTypesAsync(connection, ["probe_text", "probe_int"], "Category");
        Assert.Equal(["probe_int", "probe_text"], map.Keys);
        Assert.False(Disagreement("Category", map) is null,
            $"‏القارئُ على القرص لم يرَ TEXT مقابل INTEGER: {string.Join(" | ", map)}");

        // ‏والغيابُ يرمي، ولا يصير قيمةً تُقارَن.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SqliteDeclaredTypesAsync(connection, ["probe_bare"], "Category"));
        Assert.Contains("الغيابُ ليس اتّفاقاً", thrown.Message, StringComparison.Ordinal);
    }

    // ───────────────────────── البوّابةُ العامّة ─────────────────────────

    [Fact]
    public void NoPropertyOfAnySharedOwnedType_DisagreesOnItsStorage()
    {
        using var db = NewSqliteModel();

        var sharedTypes = db.Model.GetEntityTypes()
            .Where(e => e.IsOwned())
            .GroupBy(e => e.ClrType)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.NotEmpty(sharedTypes); // شاهدُ ضبط: النموذجُ فيه أنواعٌ مملوكةٌ مشتركةٌ أصلاً.

        var complaints = new List<string>();

        foreach (var group in sharedTypes)
        {
            // ‏المفاتيحُ الأجنبيّةُ والخفيّةُ تختلف بحقّ (sales_invoice_id مقابل purchase_order_id).
            var properties = group.First().GetProperties()
                .Where(p => !p.IsForeignKey() && !p.IsShadowProperty())
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal);

            foreach (var property in properties)
            {
                var map = StorageByTable(db, group.Key.Name, property);
                if (Disagreement($"{group.Key.Name}.{property}", map) is { } complaint)
                    complaints.Add(complaint);
            }
        }

        Assert.True(complaints.Count == 0,
            "‏نوعٌ مملوكٌ مشتركٌ يُخزَّن بطريقتَين — وهو درسُ «ثمانيةُ جداولٍ لا واحد» بعينِه:\n"
            + string.Join("\n", complaints));
    }

    // ───────────────────────── شرطُ الاتّجاه (بلا PostgreSQL) ─────────────────────────

    [Fact]
    public void TheSharedLine_StoresVatCategoryAsBoundedText_AtEveryOwner()
    {
        using var db = NewSqliteModel();

        var map = StorageByTable(db, SharedLineType, nameof(VatCategory));
        Assert.Equal(SharedLineTables, map.Keys);

        var wrong = new List<string>();

        foreach (var owned in db.Model.GetEntityTypes()
                     .Where(e => e.IsOwned() && e.ClrType.Name == SharedLineType))
        {
            var store = StoreObjectIdentifier.Create(owned, StoreObjectType.Table)!.Value;
            var property = owned.FindProperty(nameof(VatCategory))!;
            var providerType = property.GetProviderClrType() ?? property.ClrType;

            if (providerType != typeof(string) || property.GetMaxLength() != 16)
                wrong.Add($"{store.Name}={providerType.Name}/سقف={property.GetMaxLength()?.ToString() ?? "بلا"}");
        }

        Assert.True(wrong.Count == 0,
            "‏الفئةُ الضريبيّةُ تُخزَّن نصّاً بسقف ١٦ عند كلّ مالك — **والاتّجاهُ مشروطٌ لا مُستنتَجٌ من الاتّفاق**، "
            + "فالتماثلُ على العدد الصحيح يُرضي «اتّفقوا» ويُعيد ن-٥١ مقلوباً:\n  "
            + string.Join("\n  ", wrong));
    }

    [Fact]
    public async Task OnSqlite_VatCategoryIsDeclaredTextOnEveryLineTable()
    {
        var connectionString = $"Data Source=n51-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        await using (var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, connectionString))
            await db.Database.EnsureCreatedAsync();

        var map = await SqliteDeclaredTypesAsync(keepAlive, SharedLineTables, nameof(VatCategory));
        Assert.Equal(SharedLineTables, map.Keys);

        // ‏شاهدُ ضبط: القارئُ حيٌّ على هذه القاعدة نفسِها ويرى عموداً نصّيّاً بلا خلاف.
        var control = await SqliteDeclaredTypesAsync(keepAlive, SharedLineTables, "Description");
        Assert.Null(Disagreement("Description", control));

        var wrong = map.Where(kv => kv.Value != "TEXT").ToList();
        Assert.True(wrong.Count == 0,
            "‏النوعُ المُعلَن على قرص SQLite ليس TEXT عند كلّ مالك:\n  "
            + string.Join("\n  ", wrong.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    // ───────────────────────── DDL المكتوبُ باليد ─────────────────────────

    [Fact]
    public void TheHandWrittenDdl_NeverDeclaresVatCategoryAsInteger()
    {
        var files = SchemaUpdaterSourceFiles.Paths(FindRepoRoot());
        Assert.True(File.Exists(files[0]), $"‏المُرقِّعُ غيرُ موجود: {files[0]}");

        var located = files
            .SelectMany(f => File.ReadAllLines(f).Select((text, index) => (File: Path.GetFileName(f), Line: index + 1, Text: text)))
            .ToList();
        var lines = located.Select(l => l.Text).ToArray();

        // ‏شاهدُ ضبط: التقنيةُ تجد مواضعَ VatCategory في هذا الملفّ فعلاً.
        var mentions = lines.Count(l => l.Contains("VatCategory", StringComparison.Ordinal));
        Assert.True(mentions > 0, "‏لا ذكرَ لـVatCategory في المُرقِّع — الفحصُ يقيس ملفّاً خطأً.");

        var offenders = located
            .Select(l => (l.File, l.Line, Text: StripLineComment(l.Text).Trim()))
            .Where(x => DeclaresVatCategoryAsInteger(x.Text))
            .ToList();

        Assert.True(offenders.Count == 0,
            "‏DDL مكتوبٌ باليد يُعلن VatCategory عدداً صحيحاً — **وهذا الفرعُ هو مُنشئُ المخطّط الوحيد** "
            + "للقواعد القديمة (DatabaseSchemaService: coreTableExists && appliedCount == 0)، فلا هجرةَ تصلها:\n  "
            + string.Join("\n  ", offenders.Select(o => $"{o.File}:{o.Line}  {o.Text}")));
    }

    // ───────────────────────── PostgreSQL ─────────────────────────

    [PostgresFact]
    public async Task OnPostgres_VatCategoryIsVarcharOnEveryLineTable()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n51_column_type");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        var rows = await scope.Db.Database.SqlQuery<string>($"""
            SELECT (table_name || '=' || data_type ||
                    coalesce('(' || character_maximum_length || ')', '')) AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND column_name = 'VatCategory'
            ORDER BY table_name
            """).ToListAsync();

        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var split = row.Split('=', 2);
            map[split[0]] = split[1];
        }

        var missing = SharedLineTables.Where(t => !map.ContainsKey(t)).ToList();
        Assert.True(missing.Count == 0,
            $"‏جداولُ سطرٍ بلا عمودِ فئةٍ ضريبيّةٍ على القاعدة: {string.Join(", ", missing)}");

        var wrong = SharedLineTables
            .Where(t => map[t] != "character varying(16)")
            .Select(t => $"{t}={map[t]}")
            .ToList();

        Assert.True(wrong.Count == 0,
            "‏العمودُ على PostgreSQL ليس character varying(16) عند كلّ مالك:\n  "
            + string.Join("\n  ", wrong));
    }

    [PostgresFact]
    public async Task AVatQueryWrittenForOneDocument_RunsOnAllNine()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n51_vat_query");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        var broken = new List<string>();

        foreach (var table in SharedLineTables)
        {
            try
            {
                _ = await scope.Db.Database
                    .SqlQueryRaw<int>($"""SELECT count(*)::int AS "Value" FROM {table} WHERE "VatCategory" = 'Exempt'""")
                    .SingleAsync();
            }
            catch (Npgsql.PostgresException ex)
            {
                broken.Add($"{table} ⟵ {ex.SqlState} {ex.MessageText}");
            }
        }

        // ‏الرسالةُ تتّهم التخزينَ لا الصيغة. النسخةُ الأولى كانت تقول «الصيغةُ نفسُها خطأ»
        // ‏حين تسقط كلُّ الجداول — **فتدعو قارئَها إلى إصلاح الاختبار بدل المنتج**.
        Assert.True(broken.Count == 0,
            broken.Count == SharedLineTables.Length
                ? "‏استعلامٌ نصّيٌّ على الفئة الضريبيّة يفشل على **كلّ** جدولٍ من التسعة — "
                  + "أي أنّ العمودَ ليس نصّاً في أيٍّ منها:\n  " + string.Join("\n  ", broken)
                : "‏استعلامٌ نصّيٌّ كُتب لمستندٍ واحدٍ يفشل على بعض إخوته — وهو ن-٥١ بعينِه:\n  "
                  + string.Join("\n  ", broken));
    }

    // ───────────────────────── أدوات ─────────────────────────

    private static string StripLineComment(string line)
    {
        var marker = line.IndexOf("//", StringComparison.Ordinal);
        return marker < 0 ? line : line[..marker];
    }

    /// <summary>
    /// <b>إعلانُ عمودٍ، لا مجرّدُ سطرٍ يجمع الكلمتَين.</b>
    ///
    /// <para>النسخةُ الأولى كانت «يحوي VatCategory ويحوي INTEGER»، <b>فسقطت على كودي أنا</b>:
    /// <c>if (await PgColumnTypeAsync(table, "VatCategory", ct) is not "integer")</c> — وهو
    /// السطرُ الذي <b>يُصلح</b> العطل. <b>وسقوطُ حارسٍ على الصواب أسوأُ من مروره على الخطأ</b>،
    /// لأنّه يُعلّم قارئَه أنّ الصوابَ خطأ فيُصلح ما ليس معطوباً. فيُشترط أن يلي النوعُ
    /// اسمَ العمود مباشرةً كما في DDL.</para>
    ///
    /// <para><b>وحدُّه مكتوب:</b> التعليقاتُ تُمحى قبل المسح — فادّعاءٌ نصّيٌّ بائتٌ في تعليق
    /// لا يُمسَك هنا ويبقى على المراجعة البشريّة.</para>
    /// </summary>
    private static bool DeclaresVatCategoryAsInteger(string codeLine) =>
        Regex.IsMatch(codeLine, """(?i)"?\bVatCategory"?\s+INTEGER\b""");

    [Theory]
    // ‏إعلاناتٌ حقيقيّةٌ يجب أن تُمسَك — بصيغتَي PostgreSQL وSQLite:
    [InlineData(@"""VatCategory"" INTEGER NOT NULL DEFAULT 0,", true)]
    [InlineData(@"VatCategory INTEGER NOT NULL DEFAULT 0,", true)]
    [InlineData(@"""VatCategory"" integer NOT NULL,", true)]
    // ‏وكودٌ سليمٌ يجب أن يمرّ — وهذه بعينها أسقطت النسخةَ الأولى:
    [InlineData(@"if (await PgColumnTypeAsync(table, ""VatCategory"", ct) is not ""integer"")", false)]
    [InlineData(@"if (await PgColumnTypeAsync(table, ""VatCategory"", ct) is ""integer"")", false)]
    [InlineData(@"""VatCategory"" character varying(16) NOT NULL DEFAULT 'Standard',", false)]
    [InlineData(@"VatCategory TEXT NOT NULL DEFAULT 'Standard',", false)]
    [InlineData(@"""SomethingId"" INTEGER NOT NULL,", false)]
    public void TheDdlScanner_Discriminates(string codeLine, bool shouldBeCaught) =>
        Assert.Equal(shouldBeCaught, DeclaresVatCategoryAsInteger(codeLine));

    private static BrqoshDbContext NewSqliteModel() =>
        BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source=n51-model-{Guid.NewGuid():N};Mode=Memory");

    private static string FindRepoRoot()
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

    // ‏سياقٌ مصغَّرٌ يحمل الاختلافَ عمداً: مالكان لنوعٍ مملوكٍ واحد، أحدهما يُحوِّل والآخرُ لا.
    private sealed class ProbeParentA
    {
        public long Id { get; set; }
        public List<ProbeLine> Lines { get; } = [];
    }

    private sealed class ProbeParentB
    {
        public long Id { get; set; }
        public List<ProbeLine> Lines { get; } = [];
    }

    private sealed class ProbeLine
    {
        public string Name { get; set; } = "";
        public VatCategory Category { get; set; }
    }

    private sealed class DivergentProbeContext(DbContextOptions<DivergentProbeContext> options)
        : DbContext(options)
    {
        public static DivergentProbeContext Create() =>
            new(new DbContextOptionsBuilder<DivergentProbeContext>()
                .UseSqlite($"Data Source=n51-probe-{Guid.NewGuid():N};Mode=Memory")
                .Options);

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<ProbeParentA>(parent =>
            {
                parent.ToTable("probe_a");
                parent.OwnsMany(p => p.Lines, line =>
                {
                    line.ToTable("probe_a_lines");
                    line.Property(l => l.Category).HasConversion<string>().HasMaxLength(16);
                });
            });

            builder.Entity<ProbeParentB>(parent =>
            {
                parent.ToTable("probe_b");
                parent.OwnsMany(p => p.Lines, line => line.ToTable("probe_b_lines"));
            });
        }
    }
}
