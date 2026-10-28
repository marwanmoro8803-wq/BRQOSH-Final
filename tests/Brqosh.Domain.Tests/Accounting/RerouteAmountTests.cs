using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests.Accounting;

/// <summary>‏توجيهُ نصيب الصنف من الحساب العامّ إلى حسابه الخاصّ — والقيدُ يبقى متوازناً.</summary>
public class RerouteAmountTests
{
    private static Account Acc(long id, string code, AccountType type)
    {
        var a = Account.Create(code, $"حساب {code}", $"Account {code}", type, null, companyId: 1);
        typeof(Account).GetProperty(nameof(Account.Id))!.SetValue(a, id);
        return a;
    }

    [Fact]
    public void MovesTheShareAndKeepsTheEntryBalanced()
    {
        var inventory = Acc(1, "1107", AccountType.Asset);
        var itemInventory = Acc(2, "110701", AccountType.Asset);
        var payable = Acc(3, "2101", AccountType.Liability);
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 1, 5), "شراء");
        entry.AddLine(inventory, 1000m, 0m, "مخزون");
        entry.AddLine(payable, 0m, 1000m, "مورد");

        entry.RerouteAmount(inventory.Id, itemInventory, 300m, debitSide: true);

        Assert.Equal(700m, entry.Lines.Where(l => l.AccountId == 1).Sum(l => l.Debit));
        Assert.Equal(300m, entry.Lines.Where(l => l.AccountId == 2).Sum(l => l.Debit));
        Assert.Equal(entry.TotalDebit, entry.TotalCredit);
    }

    [Fact]
    public void WholeLineMovesWhenTheShareIsEverything()
    {
        var inventory = Acc(1, "1107", AccountType.Asset);
        var itemInventory = Acc(2, "110701", AccountType.Asset);
        var cogs = Acc(4, "5101", AccountType.Expense);
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 1, 5), "بيع");
        entry.AddLine(cogs, 50m, 0m);
        entry.AddLine(inventory, 0m, 50m);

        entry.RerouteAmount(inventory.Id, itemInventory, 50m, debitSide: false);

        Assert.DoesNotContain(entry.Lines, l => l.AccountId == 1);
        Assert.Equal(50m, entry.Lines.Single(l => l.AccountId == 2).Credit);
    }

    [Fact]
    public void RefusesToMoveMoreThanTheLineHolds()
    {
        var inventory = Acc(1, "1107", AccountType.Asset);
        var itemInventory = Acc(2, "110701", AccountType.Asset);
        var payable = Acc(3, "2101", AccountType.Liability);
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 1, 5), "شراء");
        entry.AddLine(inventory, 100m, 0m);
        entry.AddLine(payable, 0m, 100m);

        Assert.Throws<AccountingException>(() => entry.RerouteAmount(inventory.Id, itemInventory, 150m, debitSide: true));
    }
}
