using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// ع-R1 (CEEF-201) — الميزانية عبر حدود السنة المالية.
///
/// كانت تضيف أرباح السنة التقويمية الجارية فقط، بينما تجمع الأصول والخصوم تراكمياً منذ التأسيس:
/// فأي سنة سابقة لم يُشغَّل إقفالها (خطوة إدارية يدوية غير مفروضة) تُسقط أرباحها من حقوق الملكية
/// وتظهر الميزانية غير متوازنة طوال السنة التالية.
/// </summary>
public class BalanceSheetAcrossYearsTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public BalanceSheetAcrossYearsTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<(long CompanyId, long CustomerId)> SetupAsync(string vat)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة السنوات", vat, BusinessProfile.General);
        var customer = Customer.Create(company.Id, "عميل السنوات", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return (company.Id, customer.Id);
    }

    /// <summary>ربح سنة سابقة لم تُقفل يجب أن يظهر في حقوق الملكية — والميزانية تتوازن في السنة التالية.</summary>
    [Fact]
    public async Task BalanceSheet_InLaterYear_WithUnclosedPriorYearProfit_StaysBalanced()
    {
        var (companyId, customerId) = await SetupAsync("310123456700011");

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2025, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        // لم يُشغَّل الإقفال السنوي لسنة 2025 — والتقرير يُطلب في السنة التالية
        var sheet = await new BalanceSheetQuery(_db).ExecuteAsync(companyId, new DateOnly(2026, 3, 31));

        Assert.True(sheet.IsBalanced,
            $"الميزانية غير متوازنة عبر حدود السنة: أصول {sheet.TotalAssets} ≠ خصوم+حقوق {sheet.TotalLiabilitiesAndEquity}");
        Assert.Contains(sheet.Equity, e => e.AccountCode == "3999" && e.Amount == 1000m);
    }

    /// <summary>بعد الإقفال السنوي: الربح ينتقل لحقوق الملكية ولا يُحتسب مرتين.</summary>
    [Fact]
    public async Task BalanceSheet_AfterYearEndClose_DoesNotDoubleCountProfit()
    {
        var (companyId, customerId) = await SetupAsync("310123456700022");

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2025, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var beforeClose = await new BalanceSheetQuery(_db).ExecuteAsync(companyId, new DateOnly(2026, 3, 31));
        Assert.True(beforeClose.IsBalanced);
        var equityBeforeClose = beforeClose.TotalEquity;

        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");

        var afterClose = await new BalanceSheetQuery(_db).ExecuteAsync(companyId, new DateOnly(2026, 3, 31));

        Assert.True(afterClose.IsBalanced,
            $"الميزانية اختلت بعد الإقفال: أصول {afterClose.TotalAssets} ≠ خصوم+حقوق {afterClose.TotalLiabilitiesAndEquity}");

        // إجمالي حقوق الملكية لا يتغير بالإقفال — الربح ينتقل من صف «غير مقفلة» إلى حساب حقوق ملكية
        Assert.Equal(equityBeforeClose, afterClose.TotalEquity);

        // ولم يعد هناك صف «أرباح غير مقفلة» لأن حسابات الإيراد/المصروف صُفِّرت
        Assert.DoesNotContain(afterClose.Equity, e => e.AccountCode == "3999");
    }

    /// <summary>
    /// ع-R2: قائمة دخل سنة مقفلة يجب أن تُظهر أداءها الحقيقي — لا صفراً.
    /// قيد الإقفال مؤرَّخ 31 ديسمبر داخل السنة نفسها ويصفّر الإيراد والمصروف،
    /// فإدراجه كان يمحو أداء السنة كاملاً من التقرير.
    /// </summary>
    [Fact]
    public async Task IncomeStatement_OfClosedYear_StillShowsThatYearPerformance()
    {
        var (companyId, customerId) = await SetupAsync("310123456700055");

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2025, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var from = new DateOnly(2025, 1, 1);
        var to = new DateOnly(2025, 12, 31);

        var beforeClose = await new IncomeStatementQuery(_db).ExecuteAsync(companyId, from, to);
        Assert.Equal(1000m, beforeClose.TotalRevenue);

        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2025, "admin");

        var afterClose = await new IncomeStatementQuery(_db).ExecuteAsync(companyId, from, to);
        Assert.Equal(1000m, afterClose.TotalRevenue);
        Assert.Equal(beforeClose.NetIncome, afterClose.NetIncome);
    }

    /// <summary>الميزانية داخل السنة نفسها تبقى متوازنة (لا انحدار في السلوك الأصلي).</summary>
    [Fact]
    public async Task BalanceSheet_WithinSameYear_StaysBalanced()
    {
        var (companyId, customerId) = await SetupAsync("310123456700033");

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 2, 10));
        invoice.AddLine("خدمة", 1m, 750m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var sheet = await new BalanceSheetQuery(_db).ExecuteAsync(companyId, new DateOnly(2026, 6, 30));

        Assert.True(sheet.IsBalanced);
        Assert.Contains(sheet.Equity, e => e.AccountCode == "3999" && e.Amount == 750m);
    }
}
