using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;

namespace Brqosh.Domain.Tests.Treasury;

public class VoucherStatusTests
{
    [Fact]
    public void PaymentVoucher_MarkReversed_OnlyWhenPosted()
    {
        var voucher = PaymentVoucher.CreateDraft(1, 1, new DateOnly(2026, 7, 1), 100m,
            ReceiptMethod.Cash, "سداد تجريبي");
        Assert.Throws<AccountingException>(() => voucher.MarkReversed());
    }

    [Fact]
    public void ReceiptVoucher_MarkReversed_OnlyWhenPosted()
    {
        var voucher = ReceiptVoucher.CreateDraft(1, 1, new DateOnly(2026, 7, 1), 100m,
            ReceiptMethod.Cash, "تحصيل تجريبي");
        Assert.Throws<AccountingException>(() => voucher.MarkReversed());
    }
}
