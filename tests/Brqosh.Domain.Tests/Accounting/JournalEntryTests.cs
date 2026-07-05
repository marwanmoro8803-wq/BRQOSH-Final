using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests.Accounting;

public class JournalEntryTests
{
    private static readonly Account Cash =
        Account.Create("1101", "النقدية بالصندوق", "Cash on Hand", AccountType.Asset);

    private static readonly Account Sales =
        Account.Create("4101", "إيرادات المبيعات", "Sales Revenue", AccountType.Revenue);

    private static readonly Account VatOutput =
        Account.Create("2106", "ضريبة القيمة المضافة — مخرجات", "VAT Output", AccountType.Liability);

    private static readonly Account AssetsGroup =
        Account.Create("1", "الأصول", "Assets", AccountType.Asset, isGroup: true);

    private static JournalEntry NewDraft() =>
        JournalEntry.CreateDraft(companyId: 1, new DateOnly(2026, 7, 1), "فاتورة بيع نقدية");

    [Fact]
    public void BalancedEntry_WithVat_PostsSuccessfully()
    {
        // بيع نقدي 115 = 100 إيراد + 15 ضريبة قيمة مضافة (15%)
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 115m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 100m);
        entry.AddLine(VatOutput, debit: 0m, credit: 15m);

        entry.Post("JV-2026-000001");

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(115m, entry.TotalDebit);
        Assert.Equal(115m, entry.TotalCredit);
        Assert.Equal("JV-2026-000001", entry.EntryNumber);
    }

    [Fact]
    public void UnbalancedEntry_CannotPost()
    {
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 100m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 90m);

        var ex = Assert.Throws<AccountingException>(() => entry.Post("JV-1"));
        Assert.Contains("غير متوازن", ex.Message);
    }

    [Fact]
    public void SingleLineEntry_CannotPost()
    {
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 100m, credit: 0m);

        Assert.Throws<AccountingException>(() => entry.Post("JV-1"));
    }

    [Fact]
    public void GroupAccount_RejectsPosting()
    {
        var entry = NewDraft();
        Assert.Throws<AccountingException>(() => entry.AddLine(AssetsGroup, debit: 100m, credit: 0m));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    public void InvalidLineAmounts_AreRejected(decimal debit, decimal credit)
    {
        var entry = NewDraft();
        Assert.Throws<AccountingException>(() => entry.AddLine(Cash, debit, credit));
    }

    [Fact]
    public void PostedEntry_IsImmutable()
    {
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 100m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 100m);
        entry.Post("JV-1");

        Assert.Throws<AccountingException>(() => entry.AddLine(Cash, debit: 10m, credit: 0m));
    }

    [Fact]
    public void Reversal_SwapsDebitAndCredit_AndMarksOriginalReversed()
    {
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 115m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 100m);
        entry.AddLine(VatOutput, debit: 0m, credit: 15m);
        entry.Post("JV-1");

        var reversal = entry.CreateReversal(new DateOnly(2026, 7, 2));

        Assert.Equal(JournalEntryStatus.Reversed, entry.Status);
        Assert.True(reversal.IsBalanced);
        Assert.Equal(115m, reversal.TotalCredit);
        // السطر الأول كان مديناً 115 → في العكسي دائن 115
        Assert.Equal(115m, reversal.Lines[0].Credit);
        Assert.Equal(0m, reversal.Lines[0].Debit);
    }

    [Fact]
    public void DraftEntry_CannotBeReversed()
    {
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 100m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 100m);

        Assert.Throws<AccountingException>(() => entry.CreateReversal(new DateOnly(2026, 7, 2)));
    }

    [Fact]
    public void Reversal_CannotPredateOriginal()
    {
        var entry = NewDraft();
        entry.AddLine(Cash, debit: 100m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 100m);
        entry.Post("JV-1");

        Assert.Throws<AccountingException>(() => entry.CreateReversal(new DateOnly(2026, 6, 30)));
    }
}
