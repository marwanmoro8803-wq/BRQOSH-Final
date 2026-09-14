using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

// (CEEF-236) ‏عبر `DatabaseConfigFile` لا بقراءةٍ خام: معالجُ التهيئة صار يكتب سلسلةَ الاتصال
// ‏**محميّةً بـDPAPI**، والقراءةُ الخام تُعيد `BRQOSH1:…` فتفشل بـ«Format of the initialization
// ‏string does not conform». والقارئُ يقبل الصريحَ كما كان، فملفٌّ قديمٌ لا ينكسر.
var config = DatabaseConfigFile.Load()
    ?? throw new InvalidOperationException(
        $"لا يوجد ملفّ اتصال صالح: {DatabaseConfigFile.Path()} — شغّل معالج التهيئة أولاً.");
var cs = config.ConnectionString;

Console.WriteLine("=== BRQOSH DB Startup Probe ===");

// ‏(م-٦ · ن-٢٢ · تبعيّة) ‏**إذنٌ باسمِ القاعدة قبل أن تُلمس — وهذه أعنفُ الأدوات الأربع.**
//
// ‏ن-٢٢ ركّب هذا الحارسَ على `FullScenarioAudit` وحدَها، **وأربعُ أدواتٍ تحلّ اتّصالَ
// ‏الإنتاج من `DatabaseConfigFile`**. وهذه تفعل بالقاعدة التي تجدها ثلاثةَ أشياء **بلا
// ‏سؤال**: تُنشئ منشأةً باسم «marwan» برقمٍ ضريبيٍّ مُختلق، وتُجري **تعديلاتِ مخطّطٍ
// ‏(DDL)** بـ`BrqoshSchemaUpdater`، وتزرع **حسابَ مدير**. واسمُها «مِسبار» يُقرأ قراءةً.
//
// ‏**والوصفُ يحمل المضيفَ واسمَ القاعدة** لا الحقلَ الأوّلَ من السلسلة: من يقرأ
// ‏`Host=localhost` لا يعرف أيَّ قاعدةٍ على ذلك المضيف.
LiveBooksGuard.RequireConsentFor(
    LiveBooksGuard.DescribeTarget(cs, isSqlite: false),
    "‏تُنشئ منشأةً جديدة، وتُجري تعديلاتِ مخطّطٍ (DDL)، وتزرع حسابَ مديرٍ باسم «probeadmin».");

await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);

Console.WriteLine("\n--- Step 1: DatabaseInitializer ---");
var company = await new DatabaseInitializer(db).InitializeAsync("marwan", "123456789012345", BusinessProfile.Manufacturing);
Console.WriteLine($"Company: {company.NameAr} (Id={company.Id})");

Console.WriteLine("\n--- Step 2: BrqoshSchemaUpdater ---");
await new BrqoshSchemaUpdater(db).ApplyAsync();
Console.WriteLine("Schema patches applied.");

Console.WriteLine("\n--- Step 3: Admin user for probe ---");
const string probeUser = "probeadmin";

// ‏(م-٦ · ن-٢٢ · تبعيّة) **لا كلمةَ افتراضيّةً هنا عمداً.** كانت كلمةٌ حرفيّةٌ في
// ‏الملفّ — **أي حسابُ مديرٍ مفتوحٌ لكلّ من قرأ المستودع**، يُزرع في أيّ قاعدةٍ تُشغَّل عليها
// ‏الأداة **ويبقى بعدها**. وهي العلّةُ نفسُها التي أُغلقت في `FullScenarioAudit` بـن-٢٢.
var probePass = Environment.GetEnvironmentVariable("BRQOSH_PROBE_PASSWORD")
    ?? throw new InvalidOperationException(
        "‏BRQOSH_PROBE_PASSWORD غيرُ مضبوط. **ولا كلمةَ احتياطيّةً هنا عمداً** — "
        + "الاحتياطيّةُ كانت منشورةً في المستودع، فالحسابُ المزروعُ بها مفتوحٌ لكلّ قارئ."
        + Environment.NewLine
        + "    $env:BRQOSH_PROBE_PASSWORD = \"<كلمةٌ تختارها لهذه التشغيلة>\"");

await new UserAuthService(db).CreateInitialAdminAsync(company.Id, probeUser, probePass);
var admin = await new UserAuthService(db).AuthenticateAsync(company.Id, probeUser, probePass);
Console.WriteLine(admin is null ? "FAIL: probe admin login" : $"OK: {probeUser} authenticated ({admin.DisplayNameAr})");

var tables = await db.Database.SqlQuery<string>(
    $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name""")
    .ToListAsync();
Console.WriteLine($"\nTables: {tables.Count}");
Console.WriteLine("\n=== Startup probe SUCCESS ===");
