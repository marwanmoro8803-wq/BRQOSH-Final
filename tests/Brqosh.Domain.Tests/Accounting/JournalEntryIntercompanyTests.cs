using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests.Accounting;

public class JournalEntryIntercompanyTests
{
    [Fact]
    public void MarkIntercompany_sets_flags_on_draft()
    {
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 7, 1), "اختبار");
        entry.MarkIntercompany(2);
        Assert.True(entry.IsIntercompany);
        Assert.Equal(2L, entry.CounterpartyCompanyId);
    }

    [Fact]
    public void MarkIntercompany_rejects_same_company()
    {
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 7, 1), "اختبار");
        Assert.Throws<AccountingException>(() => entry.MarkIntercompany(1));
    }
}
