using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

public class PrintPostedGuardTests
{
    [Fact]
    public void Draft_sales_invoice_rejected()
    {
        var invoice = SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 7, 11));
        invoice.AddLine("صنف", 1, 100m);
        var ex = Assert.Throws<AccountingException>(() => PrintPostedGuard.EnsureSalesInvoicePosted(invoice));
        Assert.Contains("غير مرحّلة", ex.Message);
    }
}
