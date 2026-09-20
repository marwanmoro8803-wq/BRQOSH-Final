using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;

namespace Brqosh.Domain.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣) <b>فاتورةُ مورّدٍ غيرِ مسجَّلٍ لا تُقفل استحقاقَ الاستلام.</b>
///
/// <para><b>ما كان:</b> <c>AddAccrualRelief</c> — وهي وحدَها ما يُمدِّن حسابَ «بضاعةٌ وردت
/// ولم تُفوتر» — تُستدعى في فرع المورّد المسجَّل وحدَه. <b>والفرعان يصفان المورّدَ لا
/// المستند</b>: إذنُ الاستلام يسبق الفاتورةَ في الحالتين، وحسابُ الاستحقاق يُدان في الحالتين.</para>
///
/// <para><b>والأثرُ أثران بحسب المسار:</b> فاتورةٌ كلُّ بضاعتها على إذنِ استلامٍ
/// <b>تتوازن وتُرحَّل</b> — وتترك <c>2112</c> دائناً إلى الأبد وقد دخلت التكلفةُ مرّتين
/// (<b>والميزانُ سليمٌ تماماً فلا يشكو أحد</b>). وفاتورةٌ مختلطةٌ <b>لا تتوازن أصلاً</b>
/// فلا تُرحَّل.</para>
///
/// <para><b>ولا اختبارَ واحدٌ في المستودع كان يذكر <c>grniRelief</c> ولا
/// <c>VatDeductible</c></b> — فالمسارُ كلُّه بلا قياس.</para>
/// </summary>
public class PurchaseInvoiceClearsTheAccrualTests
{
    private const long CompanyId = 1;

    private static Account Acc(string code, string name, AccountType type) =>
        Account.Create(code, name, code, type, parentId: null, isGroup: false,
            isSystem: false, companyId: CompanyId);

    private static readonly Account Purchases = Acc("5101", "مشتريات", AccountType.Expense);
    private static readonly Account VatInput = Acc("1108", "ضريبة مدخلات", AccountType.Asset);
    private static readonly Account Payable = Acc("2101", "موردون", AccountType.Liability);
    private static readonly Account Inventory = Acc("1107", "مخزون", AccountType.Asset);
    private static readonly Account Grni = Acc("2112", "بضاعة وردت ولم تُفوتر", AccountType.Liability);
    private static readonly Account Variance = Acc("5109", "فروق أسعار", AccountType.Expense);

    /// <summary>فاتورةُ ألفٍ بضريبةٍ نظاميّة — إجماليُّها ١٬١٥٠.</summary>
    private static PurchaseInvoice Invoice(bool vatDeductible)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            CompanyId, supplierId: 7, new DateOnly(2026, 5, 10), "REF-1", vatDeductible);
        invoice.AddLine("بضاعة", 1m, 1_000m);
        return invoice;
    }

    private static decimal Debit(JournalEntry entry, string code) =>
        entry.Lines.Where(l => l.AccountCode == code).Sum(l => l.Debit - l.Credit);

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> كلُّ البضاعة على إذنِ استلام ⟵ الاستحقاقُ يُقفل بألفٍ،
    /// والمشترياتُ لا تحمل إلّا الضريبةَ غيرَ القابلة للخصم (١٥٠) مُرسمَلةً.
    ///
    /// <para><b>وقبل الإصلاح كان القيدُ متوازناً</b>: مشترياتٌ ١٬١٥٠ مقابل موردين ١٬١٥٠،
    /// <b>و<c>2112</c> لا يُمسّ</b> — فالتكلفةُ دخلت مرّةً بإذن الاستلام ومرّةً هنا.</para>
    /// </summary>
    [Fact]
    public void NonRegisteredSupplier_WhenGoodsWereAlreadyReceived_ClearsTheAccrual()
    {
        var invoice = Invoice(vatDeductible: false);

        var entry = invoice.Post("PI-1", Purchases, VatInput, Payable,
            inventorySubtotal: 0m, inventoryAccount: Inventory,
            grniRelief: 1_000m, grniAccount: Grni);

        Assert.True(entry.IsBalanced,
            $"غيرُ متوازن: مدين {entry.TotalDebit:N2} / دائن {entry.TotalCredit:N2}");
        Assert.Equal(1_000m, Debit(entry, "2112"));
        Assert.Equal(150m, Debit(entry, "5101"));
        Assert.Equal(-1_150m, Debit(entry, "2101"));
    }

    /// <summary>
    /// <b>وفاتورةٌ مختلطةٌ كانت لا تتوازن أصلاً</b> — بعضُ سطورها على إذنِ استلامٍ وبعضُها
    /// بضاعةٌ جديدة. المدينُ كان ينقص بمقدار الإقفال بالضبط.
    /// </summary>
    [Fact]
    public void NonRegisteredSupplier_WithAPartlyReceivedInvoice_Balances()
    {
        var invoice = PurchaseInvoice.CreateDraft(
            CompanyId, supplierId: 7, new DateOnly(2026, 5, 10), "REF-2", vatDeductible: false);
        invoice.AddLine("بضاعةٌ على إذن", 1m, 1_000m);
        invoice.AddLine("بضاعةٌ جديدة", 1m, 500m);

        var entry = invoice.Post("PI-2", Purchases, VatInput, Payable,
            inventorySubtotal: 500m, inventoryAccount: Inventory,
            grniRelief: 1_000m, grniAccount: Grni);

        Assert.True(entry.IsBalanced,
            $"غيرُ متوازن: مدين {entry.TotalDebit:N2} / دائن {entry.TotalCredit:N2}");
        Assert.Equal(1_000m, Debit(entry, "2112"));
        Assert.Equal(500m, Debit(entry, "1107"));
        Assert.Equal(225m, Debit(entry, "5101"));   // ‏الضريبةُ غيرُ القابلة للخصم، مُرسمَلة
    }

    /// <summary>وفرقُ السعر يبلغ حسابَه في هذا الفرع كما يبلغه في فرع المسجَّل.</summary>
    [Fact]
    public void NonRegisteredSupplier_PriceVariance_ReachesItsAccount()
    {
        var invoice = Invoice(vatDeductible: false);

        var entry = invoice.Post("PI-3", Purchases, VatInput, Payable,
            inventorySubtotal: 0m, inventoryAccount: Inventory,
            grniRelief: 900m, grniAccount: Grni,
            priceVariance: 100m, varianceAccount: Variance);

        Assert.True(entry.IsBalanced);
        Assert.Equal(900m, Debit(entry, "2112"));
        Assert.Equal(100m, Debit(entry, "5109"));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل:</b> المورّدُ المسجَّل كما كان حرفاً — كان هذا الفرعُ صحيحاً،
    /// والإصلاحُ لم يمسّه.
    /// </summary>
    [Fact]
    public void RegisteredSupplier_IsUnchanged()
    {
        var invoice = Invoice(vatDeductible: true);

        var entry = invoice.Post("PI-4", Purchases, VatInput, Payable,
            inventorySubtotal: 0m, inventoryAccount: Inventory,
            grniRelief: 1_000m, grniAccount: Grni);

        Assert.True(entry.IsBalanced);
        Assert.Equal(1_000m, Debit(entry, "2112"));
        Assert.Equal(150m, Debit(entry, "1108"));   // ‏ضريبةُ مدخلاتٍ قابلةٌ للخصم
        Assert.Equal(0m, Debit(entry, "5101"));
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ ثانٍ:</b> فاتورةٌ بلا إذنِ استلامٍ أصلاً تبقى كما كانت — الإجماليُّ
    /// كلُّه على المشتريات، ولا سطرَ استحقاق. ولولاه لكان «الإصلاح» قد يخترع إقفالاً
    /// لِما لم يُستحقّ.
    /// </summary>
    [Fact]
    public void NonRegisteredSupplier_WithoutAnyReceipt_IsUnchanged()
    {
        var invoice = Invoice(vatDeductible: false);

        var entry = invoice.Post("PI-5", Purchases, VatInput, Payable);

        Assert.True(entry.IsBalanced);
        Assert.Equal(1_150m, Debit(entry, "5101"));
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == "2112");
    }
}
