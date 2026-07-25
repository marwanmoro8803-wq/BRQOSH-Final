using System.Reflection;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// إثبات أكواد ثابت: كل orchestrator معروف يجب أن يحتوي مصدره على DomainEventHub.PublishAsync.
/// يفشل إذا أُضيفت خدمة جديدة للقائمة دون ربط الأحداث.
/// </summary>
public class OrchestratorEventWiringEvidenceTests
{
    private static readonly Type[] Orchestrators =
    [
        typeof(JournalPostingService),
        typeof(SalesInvoicePostingService),
        typeof(SalesInvoiceCancellationService),
        typeof(CreditNotePostingService),
        typeof(CreditNoteCancellationService),
        typeof(PurchaseInvoicePostingService),
        typeof(PurchaseInvoiceCancellationService),
        typeof(DebitNotePostingService),
        typeof(GoodsReceiptPostingService),
        typeof(ReceiptVoucherPostingService),
        typeof(PaymentVoucherPostingService),
        typeof(ChequePostingService),
        typeof(ProgressBillingPostingService),
        typeof(ProgressBillingCancellationService),
        typeof(StockMovementPostingService),
        typeof(StockMovementCancellationService),
        typeof(PayrollPostingService),
        typeof(FixedAssetAcquisitionPostingService),
        typeof(FixedAssetDisposalPostingService),
    ];

    [Fact]
    public void All_listed_orchestrators_source_files_publish_domain_events()
    {
        var root = FindRepoRoot();
        var infra = Path.Combine(root, "src", "Brqosh.Infrastructure");
        Assert.True(Directory.Exists(infra), $"Infrastructure path missing: {infra}");

        var missing = new List<string>();
        foreach (var type in Orchestrators)
        {
            var fileName = type.Name + ".cs";
            var matches = Directory.GetFiles(infra, fileName, SearchOption.AllDirectories);
            Assert.True(matches.Length > 0, $"ملف غير موجود: {fileName}");
            var source = File.ReadAllText(matches[0]);
            if (!source.Contains("DomainEventHub.PublishAsync", StringComparison.Ordinal))
                missing.Add(type.FullName!);
        }

        Assert.True(missing.Count == 0,
            "خدمات بلا DomainEventHub.PublishAsync:\n" + string.Join("\n", missing));
        Assert.Equal(19, Orchestrators.Length);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
