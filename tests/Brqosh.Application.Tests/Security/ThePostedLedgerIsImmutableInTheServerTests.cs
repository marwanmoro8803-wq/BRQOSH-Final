using System.Security.Cryptography;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (security-licensing-tenancy#5) <b>القيدُ المُرحَّل لا يُعدَّل ولا يُحذف — يرفضه الخادمُ لا البرنامج.</b>
/// على PostgreSQL حقيقيّ: دورُ التطبيق (وحتى المالك) يُرفض تعديلُه أو حذفُه لقيدٍ مُرحَّل أو لسطوره أو تفريغُه الدفتر،
/// والبرنامجُ نفسُه ما زال يُرحِّل ويعكس ويعدّل المسوّدات.
/// </summary>
public class ThePostedLedgerIsImmutableInTheServerTests
{
    [PostgresFact]
    public async Task APostedEntry_CannotBeEditedOrDeleted_ButTheAppStillPostsAndReverses()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("ledgerguard");
        TestActor.Administrator();
        var (role, password) = ("brq_app_" + Guid.NewGuid().ToString("N")[..10], Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        try
        {
            var booted = await new DatabaseBootstrapService(scope.Db).InitializeAsync("منشأة الدفتر", "300000000000154", BusinessProfile.General);
            await LeastPrivilegeDatabaseRole.ApplyAsync(scope.ConnectionString, role, password);
            await using var app = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql,
                new NpgsqlConnectionStringBuilder(scope.ConnectionString) { Username = role, Password = password, Pooling = false }.ConnectionString);
            var companyId = booted.CompanyId;

            var posted = await PostAsync(app, companyId, 300m);

            foreach (var sql in new[]
                     {
                         $"""UPDATE journal_entry_lines SET "Debit" = 999 WHERE journal_entry_id = {posted}""",
                         $"""DELETE FROM journal_entry_lines WHERE journal_entry_id = {posted}""",
                         $"""UPDATE journal_entries SET "Description" = 'مُزوَّر' WHERE "Id" = {posted}""",
                         $"""UPDATE journal_entries SET "Status" = 'Draft' WHERE "Id" = {posted}""",
                         $"""DELETE FROM journal_entries WHERE "Id" = {posted}""",
                         "TRUNCATE journal_entry_lines",
                         "TRUNCATE journal_entries CASCADE",
                         $"""INSERT INTO {LedgerImmutability.PostedInTransactionTable} VALUES ({posted}, txid_current())""",
                     })
            {
                var ex = await Assert.ThrowsAnyAsync<PostgresException>(() => app.Database.ExecuteSqlRawAsync(sql));
                Assert.True(ex.SqlState is LedgerImmutability.RefusalSqlState or "42501", $"«{sql}» رُفض بـ{ex.SqlState}: {ex.MessageText}");
            }

            // ‏والمالكُ نفسُه لا يعدّل سطراً مُرحَّلاً بلا قصد — المشغِّلُ لا يُفرّق بين الأدوار.
            var owner = await Assert.ThrowsAnyAsync<PostgresException>(() => scope.Db.Database.ExecuteSqlRawAsync(
                $"""UPDATE journal_entry_lines SET "Credit" = 1 WHERE journal_entry_id = {posted}"""));
            Assert.Equal(LedgerImmutability.RefusalSqlState, owner.SqlState);

            // ‏والبرنامجُ يعكس كما كان: مُرحَّل→معكوس ورابطُ العكس.
            await new JournalReversalService(app).ReverseAsync(companyId, posted, new DateOnly(2026, 5, 2), "admin", fromSourceDocument: false);
            app.ChangeTracker.Clear();
            var original = await app.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == posted);
            Assert.Equal(JournalEntryStatus.Reversed, original.Status);
            Assert.NotNull(original.ReversalEntryId);
            Assert.Equal(300m, original.Lines.Sum(l => l.Debit));
        }
        finally
        {
            TestActor.None();
            await DropRoleAsync(scope.ConnectionString, role);
        }
    }

    [PostgresFact]
    public async Task ADraft_StaysFree_AndItsLinesMayChangeInTheTransactionThatPostsIt()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("ledgerguarddraft");
        TestActor.Administrator();
        try
        {
            var booted = await new DatabaseBootstrapService(scope.Db).InitializeAsync("منشأة الدفتر", "300000000000154", BusinessProfile.General);
            var db = scope.Db;
            var draft = await SaveDraftAsync(db, booted.CompanyId);
            var other = await SaveDraftAsync(db, booted.CompanyId);

            await db.Database.ExecuteSqlRawAsync($"""UPDATE journal_entry_lines SET "Description" = 'مسوّدة' WHERE journal_entry_id = {draft}""");
            await db.Database.ExecuteSqlRawAsync($"""DELETE FROM journal_entry_lines WHERE journal_entry_id = {other}""");
            await db.Database.ExecuteSqlRawAsync($"""DELETE FROM journal_entries WHERE "Id" = {other}""");

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                await db.Database.ExecuteSqlRawAsync($"""UPDATE journal_entries SET "Status" = 'Posted', "EntryNumber" = 'JE-G-1' WHERE "Id" = {draft}""");
                await db.Database.ExecuteSqlRawAsync($"""UPDATE journal_entry_lines SET "Description" = 'عند الترحيل' WHERE journal_entry_id = {draft}""");
                await tx.CommitAsync();
            }

            var ex = await Assert.ThrowsAnyAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                $"""UPDATE journal_entry_lines SET "Description" = 'بعده' WHERE journal_entry_id = {draft}"""));
            Assert.Equal(LedgerImmutability.RefusalSqlState, ex.SqlState);
        }
        finally
        {
            TestActor.None();
        }
    }

    [Fact]
    public void TheAppRole_CannotTruncateTheLedger_NorWriteTheTriggersOwnTable()
    {
        var statements = LeastPrivilegeDatabaseRole.Statements("brq_app", "a-long-enough-password", "db");
        Assert.Contains(statements, s => s == "REVOKE TRUNCATE ON journal_entries, journal_entry_lines FROM \"brq_app\";");
        Assert.Contains(statements, s => s.Contains($"REVOKE ALL ON {LedgerImmutability.PostedInTransactionTable} FROM \"brq_app\"", StringComparison.Ordinal));
    }

    private static async Task<long> PostAsync(BrqoshDbContext db, long companyId, decimal amount)
    {
        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 5, 1), "قيدٌ للحدّ");
        entry.AddLine(await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "5101"), amount, 0m);
        entry.AddLine(await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand), 0m, amount);
        return (await new JournalPostingService(db).PostAsync(entry)).Id;
    }

    private static async Task<long> SaveDraftAsync(BrqoshDbContext db, long companyId)
    {
        var entry = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 5, 1), "مسوّدة");
        entry.AddLine(await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "5101"), 10m, 0m);
        entry.AddLine(await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand), 0m, 10m);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private static async Task DropRoleAsync(string ownerConnection, string role)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(ownerConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role}') THEN "
            + $"EXECUTE 'DROP OWNED BY \"{role}\"'; EXECUTE 'DROP ROLE \"{role}\"'; END IF; END $$;", connection);
        await command.ExecuteNonQueryAsync();
    }
}
