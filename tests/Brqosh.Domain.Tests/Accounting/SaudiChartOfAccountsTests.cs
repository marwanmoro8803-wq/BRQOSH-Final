using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests.Accounting;

public class SaudiChartOfAccountsTests
{
    [Fact]
    public void Template_PassesStructuralValidation()
    {
        var errors = SaudiChartOfAccounts.Validate();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void AllSystemCodes_ExistInTemplate_AndArePostable()
    {
        var byCode = SaudiChartOfAccounts.Template.ToDictionary(a => a.Code);
        var systemCodes = typeof(SaudiChartOfAccounts.SystemCodes)
            .GetFields()
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        Assert.NotEmpty(systemCodes);
        foreach (var code in systemCodes)
        {
            Assert.True(byCode.ContainsKey(code), $"كود نظامي مفقود من القالب: {code}");
            Assert.False(byCode[code].IsGroup, $"الكود النظامي {code} حساب تجميعي — يجب أن يقبل الترحيل.");
            Assert.True(byCode[code].IsSystem, $"الكود {code} غير معلّم كحساب نظامي.");
        }
    }

    [Fact]
    public void Template_CoversAllFiveAccountTypes()
    {
        var types = SaudiChartOfAccounts.Template.Select(a => a.Type).Distinct().ToList();
        Assert.Equal(5, types.Count);
    }

    [Fact]
    public void VatAccounts_HaveCorrectTypes()
    {
        var byCode = SaudiChartOfAccounts.Template.ToDictionary(a => a.Code);

        // ضريبة المدخلات أصل (تُسترد)، وضريبة المخرجات التزام (تُورَّد للهيئة)
        Assert.Equal(AccountType.Asset, byCode[SaudiChartOfAccounts.SystemCodes.VatInput].Type);
        Assert.Equal(AccountType.Liability, byCode[SaudiChartOfAccounts.SystemCodes.VatOutput].Type);
        Assert.Equal(AccountType.Liability, byCode[SaudiChartOfAccounts.SystemCodes.VatNetPayable].Type);
    }

    [Fact]
    public void SystemAccounts_CannotBeDeactivated()
    {
        var account = Account.Create("1111", "ضريبة القيمة المضافة — مدخلات", "VAT Input",
            AccountType.Asset, isSystem: true);

        Assert.Throws<AccountingException>(() => account.Deactivate());
    }

    [Fact]
    public void AccountNature_FollowsAccountingRules()
    {
        Assert.True(AccountType.Asset.IsDebitNature());
        Assert.True(AccountType.Expense.IsDebitNature());
        Assert.False(AccountType.Liability.IsDebitNature());
        Assert.False(AccountType.Equity.IsDebitNature());
        Assert.False(AccountType.Revenue.IsDebitNature());

        Assert.True(AccountType.Asset.IsBalanceSheet());
        Assert.True(AccountType.Revenue.IsIncomeStatement());
    }
}
