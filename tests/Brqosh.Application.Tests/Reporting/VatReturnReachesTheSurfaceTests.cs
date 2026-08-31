using Brqosh.Application.Reporting;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-229 · م1-١٠ / Z-12) <b>التفكيكُ الضريبيّ يبلغ السطحَ الذي يقرؤه المستخدم.</b>
///
/// <para><b>الفجوة:</b> <c>VatReturnQuery</c> يحسب الصناديقَ بالفئة منذ A-15، <b>ولا قارئَ
/// لها خارج ملفّه</b>. فـ<c>VatReturnDto</c> — الحدُّ الذي تقرؤه الشاشة — بلا تفكيك،
/// والشاشةُ تعرض سطراً واحداً «مبيعات خاضعة (وعاء)» بمجموعٍ كلّيّ.</para>
///
/// <para><b>والأثر:</b> ربعٌ فيه مبيعات محلية 1,000,000 (ضريبة 150,000) وصادرات صفرية
/// 400,000 يعرض وعاءً 1,400,000 مقابل ضريبة 150,000 — أي <b>10.7%</b>، نسبةٌ لا وجود لها
/// في أيّ نظام ضريبيّ. وهو تعارضٌ آليٌّ فوريّ على بوابة الهيئة.</para>
///
/// <para><b>ويُقاس هنا الحدُّ لا الاستعلام.</b> فاختبارٌ يقيس الاستعلامَ يمرّ اليوم — وقد
/// مرّ فعلاً منذ A-15 — بينما لا يرى المستخدمُ شيئاً منه. وهو نمطُ «الوصلة» الذي أُغلق
/// مرّتين: الطرفان مقيسان والوسطُ مُستنتَج.</para>
/// </summary>
public class VatReturnReachesTheSurfaceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public VatReturnReachesTheSurfaceTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياس المركزيّ — على الحدّ.</b> ربعٌ مختلط ⟵ صندوقان متمايزان، ومجموعُهما
    /// يساوي الوعاءَ الكلّيّ بالضبط.
    /// </summary>
    [Fact]
    public async Task TheCategoryBreakdown_CrossesIntoTheDto()
    {
        var companyId = await SeedAMixedQuarterAsync();

        var dto = await QueryAsync(companyId);

        Assert.NotNull(dto.SalesByCategory);
        Assert.True(dto.SalesByCategory!.Count >= 2,
            $"الصناديقُ {dto.SalesByCategory.Count} — والربعُ فيه فئتان على الأقلّ، "
            + "فالتفكيكُ لم يعبر الحدَّ إلى السطح.");

        var standard = dto.SalesByCategory.Single(c => c.Code == "S");
        var zero = dto.SalesByCategory.Single(c => c.Code == "Z");

        Assert.Equal(1_000_000m, standard.TaxableAmount);
        Assert.Equal(150_000m, standard.VatAmount);
        Assert.Equal(400_000m, zero.TaxableAmount);
        Assert.Equal(0m, zero.VatAmount);

        // ‏والهوية: مجموعُ الصناديق == الوعاء الكلّيّ. فصندوقٌ يُسقط سطراً يُكسر هنا.
        Assert.Equal(dto.SalesSubtotal, dto.SalesByCategory.Sum(c => c.TaxableAmount));
        Assert.Equal(dto.OutputVat, dto.SalesByCategory.Sum(c => c.VatAmount));
    }

    /// <summary>
    /// <b>والنسبةُ المخلوطة تُقاس بوصفها الشيءَ الذي يجب ألّا يُعرَض وحده.</b>
    ///
    /// <para>المجموعُ الكلّيّ يقول 10.71% — ولا وجود لها في نظامٍ ضريبيّ. وهذا الاختبارُ
    /// يُثبّت أنّ التفكيكَ يُنتج نسبتين حقيقيّتين (15% وصفر) بدل واحدةٍ ملفّقة.</para>
    /// </summary>
    [Fact]
    public async Task TheBlendedRate_IsReplacedByRealRates()
    {
        var companyId = await SeedAMixedQuarterAsync();
        var dto = await QueryAsync(companyId);

        var blended = dto.OutputVat / dto.SalesSubtotal;
        Assert.True(blended is > 0.10m and < 0.11m,
            $"النسبةُ المخلوطة {blended:P2} — والتهيئةُ لا تُنتج الحالةَ المقصودة.");

        var standard = dto.SalesByCategory!.Single(c => c.Code == "S");
        var zero = dto.SalesByCategory!.Single(c => c.Code == "Z");

        Assert.Equal(0.15m, standard.VatAmount / standard.TaxableAmount);
        Assert.Equal(0m, zero.VatAmount);
    }

    // ── التهيئة ──────────────────────────────────────────────────────────────

    /// <summary>يعبر المسارَ الحقيقيّ: الاستعلامُ ثمّ العبورُ إلى الحدّ — لا الاستعلامَ وحده.</summary>
    private async Task<VatReturnDto> QueryAsync(long companyId)
    {
        var result = await new Brqosh.Infrastructure.Reporting.VatReturnQuery(_db)
            .ExecuteAsync(companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        return Brqosh.Infrastructure.Reporting.VatReturnMapping.From(result);
    }

    private async Task<long> SeedAMixedQuarterAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الإقرار", "310123456700003", BusinessProfile.Retail);

        var customer = Customer.Create(company.Id, "عميل نقدي");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        await SellAsync(company.Id, customer.Id, 1_000_000m, VatCategory.Standard, null);
        await SellAsync(company.Id, customer.Id, 400_000m, VatCategory.ZeroRated, "VATEX-SA-32");

        return company.Id;
    }

    private async Task SellAsync(
        long companyId, long customerId, decimal amount, VatCategory category, string? reasonCode)
    {
        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("بند", 1m, amount, null, category, reasonCode);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }
}
