using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-229 · م3-٢ · <b>ح-٦</b>) <b>الهوية: دفاترُ ما بعد الاستعادة == دفاترُ ما قبل الإتلاف.</b>
///
/// <para>ولا يقيس هذا الملفُّ «هل كُتب ملفّ» — فذاك يمرّ على نسخةٍ ناقصةِ جدول، وعلى نسخةٍ
/// تُعيد الصفوف وتنسى التسلسلات، وعلى نسخةٍ أُخذت وسط معاملة. <b>يقيس الاستعادةَ نفسَها:
/// تُؤخذ النسخة، ثمّ تُتلَف القاعدةُ إتلافاً حقيقياً، ثمّ تُستعاد، ثمّ تُطابَق البصمة.</b></para>
///
/// <para>وهذا هو الفرقُ بين نسخةٍ احتياطية وأُمنية.</para>
/// </summary>
public class BackupRestoreIdentityTests
{
    /// <summary>
    /// <b>القياس المركزيّ على مزوّد الإنتاج.</b> دورةٌ محاسبيةٌ كاملة ⟵ نسخة ⟵ <b>إسقاطُ
    /// المخطّط كلِّه</b> ⟵ استعادة ⟵ البصمةُ نفسُها.
    /// </summary>
    [PostgresFact]
    public async Task RestoredBooks_AreIdenticalToTheOriginal_OnPostgreSql()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("backup");

        await SeedACycleAsync(scope.Db);
        var before = await BooksFingerprint.TakeAsync(scope.Db);

        var backupPath = Path.Combine(Path.GetTempPath(), $"brqosh-{Guid.NewGuid():N}.dump");
        try
        {
            var result = await new BackupService(scope.Db).CreateAsync(backupPath);
            Assert.True(result.SizeBytes > 0, "النسخةُ خرجت فارغة.");

            // ‏إتلافٌ حقيقيّ: يُسقَط المخطّطُ كلُّه. ولو اكتُفي بحذف صفوفٍ لَما قيست
            // ‏استعادةُ البنية — وهي نصفُ ما يُفقد فعلاً.
            await scope.Db.Database.ExecuteSqlRawAsync(
                "DROP SCHEMA public CASCADE; CREATE SCHEMA public;");

            var wiped = await scope.Db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='public'")
                .SingleAsync();
            Assert.True(wiped == 0, $"الإتلافُ لم يقع — بقي {wiped} جدولاً، فالاستعادةُ لا تُقاس.");

            await new BackupService(scope.Db).RestoreAsync(backupPath);

            await using var reopened = scope.OpenIndependentContext();
            var after = await BooksFingerprint.TakeAsync(reopened);

            Assert.True(before == after,
                $"الدفاترُ اختلفت بعد الاستعادة.\n  قبل:  {before.Describe()}\n  بعد:  {after.Describe()}");
        }
        finally
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    /// <summary>
    /// <b>والاستعادةُ تُعيد التسلسلات لا الصفوفَ وحدها.</b> قاعدةٌ مستعادةٌ تقبل مستنداً
    /// جديداً برقمٍ يتلو الأخير — ولو ضاعت التسلسلاتُ لعمل النظامُ دقيقةً ثمّ اصطدم بمفتاحٍ
    /// مكرَّر عند أوّل حركة. وهو عطلٌ لا تكشفه مقارنةُ الصفوف.
    /// </summary>
    [PostgresFact]
    public async Task ARestoredDatabase_StillIssuesTheNextNumber()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("backupseq");

        var companyId = await SeedACycleAsync(scope.Db);
        var backupPath = Path.Combine(Path.GetTempPath(), $"brqosh-{Guid.NewGuid():N}.dump");
        try
        {
            await new BackupService(scope.Db).CreateAsync(backupPath);
            await scope.Db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
            await new BackupService(scope.Db).RestoreAsync(backupPath);

            await using var reopened = scope.OpenIndependentContext();
            TestActor.Administrator();

            var customerId = await reopened.Customers.Select(c => c.Id).FirstAsync();
            var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 7, 10));
            invoice.AddLine("بعد الاستعادة", 1m, 50m);

            var posted = await new SalesInvoicePostingService(reopened).PostAsync(invoice);

            Assert.Equal("INV-2026-000002", posted.InvoiceNumber);
        }
        finally
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    /// <summary>والنسخةُ على SQLite كذلك — فهو مزوّدُ التطوير وقواعدُ التجربة.</summary>
    [Fact]
    public async Task ASqliteBackup_IsAReadableDatabase()
    {
        TestActor.Administrator();
        var root = Path.Combine(Path.GetTempPath(), $"brqosh-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var livePath = Path.Combine(root, "live.db");
        var backupPath = Path.Combine(root, "backup.db");

        try
        {
            await using (var db = BrqoshDbContextFactory.Create(
                DatabaseProvider.Sqlite, $"Data Source={livePath}"))
            {
                await SeedACycleAsync(db);
                var before = await BooksFingerprint.TakeAsync(db);

                await new BackupService(db).CreateAsync(backupPath);

                await using var restored = BrqoshDbContextFactory.Create(
                    DatabaseProvider.Sqlite, $"Data Source={backupPath}");
                var after = await BooksFingerprint.TakeAsync(restored);

                Assert.True(before == after,
                    $"نسخةُ SQLite لا تطابق الأصل.\n  قبل: {before.Describe()}\n  بعد: {after.Describe()}");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { /* مجلّدٌ مؤقّت */ }
        }
    }

    private static async Task<long> SeedACycleAsync(BrqoshDbContext db)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة النسخ", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(db).ApplyAsync();

        var customer = Customer.Create(company.Id, "عميل نقدي");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new SalesInvoicePostingService(db).PostAsync(invoice);

        return company.Id;
    }
}
