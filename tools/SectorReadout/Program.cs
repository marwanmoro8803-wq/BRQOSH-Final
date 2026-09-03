// قارئ قطاعات (CEEF-235) — قراءةٌ فقط، لا كتابة.
//
// السؤال: بعد أن بذر tools/SectorProbe بياناتٍ حقيقيةً في ثماني قواعد PostgreSQL
// (قاعدةٌ لكلّ BusinessProfile)، ما الأرقامُ التي يجب أن تظهر على شاشات التطبيق الحيّ
// لو قِيد إليها عبر UIAutomationClient؟ هذا المسارُ **الآلي المستقلّ الثاني** — يُقرأ
// بلا واجهة، فيُقارَن رقمُه برقم الشاشة كما فعلت CEEF-232 بميزان المراجعة والإقرار
// الضريبي. رقمان من مسارين مستقلّين يتطابقان دليلٌ، لا رقمٌ واحد.
//
// التشغيل: dotnet run --project tools/SectorReadout

using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

const string TestDb = "brqosh_sectorprobe";

var pwPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "pg_password.txt");
if (!File.Exists(pwPath))
{
    Console.WriteLine($"[SKIP] لا يوجد {pwPath}.");
    return 0;
}

var pw = (await File.ReadAllTextAsync(pwPath)).Trim();
var profiles = Enum.GetValues<Brqosh.Domain.Modules.BusinessProfile>();

foreach (var profile in profiles)
{
    var name = $"{TestDb}_{profile.ToString().ToLowerInvariant()}";
    var cs = $"Host=localhost;Port=5433;Database={name};Username=postgres;Password={pw}";

    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);

    var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync();
    if (company is null)
    {
        Console.WriteLine($"[{profile}] لا شركةَ في {name} — تخطٍّ.");
        continue;
    }

    var trial = await new TrialBalanceQuery(db).ExecuteAsync(
        company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
    var debit = Math.Round(trial.Sum(l => l.TotalDebit), 2);
    var credit = Math.Round(trial.Sum(l => l.TotalCredit), 2);

    Console.WriteLine($"══ {profile} ({name}) — الشركة #{company.Id} \"{company.NameAr}\" ══");
    Console.WriteLine($"   ميزان المراجعة: مدين {debit:N2} = دائن {credit:N2} · {(debit == credit ? "متوازن" : "!! غير متوازن")}");

    async Task Count<T>(string label, IQueryable<T> q) where T : class
        => Console.WriteLine($"   {label}: {await q.CountAsync()}");

    await Count("المستخلصات (ProgressBillings)", db.ProgressBillings.Where(x => x.CompanyId == company.Id));
    await Count("مشاريع المقاولات (ConstructionProjects)", db.ConstructionProjects.Where(x => x.CompanyId == company.Id));
    await Count("تكاليف المشاريع (ProjectCostEntries)", db.ProjectCostEntries.Where(x => x.CompanyId == company.Id));
    await Count("أوامر الإنتاج (WorkOrders)", db.WorkOrders.Where(x => x.CompanyId == company.Id));
    await Count("قوائم المواد (BillsOfMaterials)", db.BillsOfMaterials.Where(x => x.CompanyId == company.Id));
    await Count("الورديات (PosShifts)", db.PosShifts.Where(x => x.CompanyId == company.Id));
    await Count("المرضى (Patients)", db.Patients.Where(x => x.CompanyId == company.Id));
    await Count("الزيارات (Visits)", db.Visits.Where(x => x.CompanyId == company.Id));
    await Count("مطالبات التأمين (InsuranceClaims)", db.InsuranceClaims.Where(x => x.CompanyId == company.Id));
    await Count("الشحنات (Shipments)", db.Shipments.Where(x => x.CompanyId == company.Id));
    await Count("رحلات التوصيل (DeliveryTrips)", db.DeliveryTrips.Where(x => x.CompanyId == company.Id));
    await Count("العقارات (Properties)", db.Properties.Where(x => x.CompanyId == company.Id));
    await Count("عقود الإيجار (LeaseContracts)", db.LeaseContracts.Where(x => x.CompanyId == company.Id));
    await Count("أوامر الصيانة (MaintenanceOrders)", db.MaintenanceOrders.Where(x => x.CompanyId == company.Id));
    await Count("أطباق القائمة (MenuItems)", db.MenuItems.Where(x => x.CompanyId == company.Id));
    await Count("وعاء الإكراميات (TipPools)", db.TipPools.Where(x => x.CompanyId == company.Id));
    await Count("فواتير المبيعات (SalesInvoices)", db.SalesInvoices.Where(x => x.CompanyId == company.Id));
    await Count("الغرف (Rooms)", db.Rooms.Where(x => x.CompanyId == company.Id));
    await Count("الحجوزات (Reservations)", db.Reservations.Where(x => x.CompanyId == company.Id));

    Console.WriteLine();
}

return 0;
