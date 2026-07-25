using System.Windows.Controls;
using Brqosh.Application.Platform;
using Brqosh.Desktop.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>G-32 تسعير/باقات — MVP يعرض BillingPlan من قاعدة البيانات.</summary>
public partial class PricingPlansPage : UserControl
{
    public PricingPlansPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        using var scope = AppServices.CreateInfrastructureScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IBillingPlanCatalog>();
        var plans = await catalog.ListActiveAsync();

        PlansGrid.ItemsSource = plans;
        UiTestAutomation.PublishGridCount(PlansGrid, plans.Count);
        SummaryText.Text = plans.Count > 0
            ? $"باقات نشطة: {plans.Count} · المصدر: BillingPlans في قاعدة البيانات"
            : "لا توجد باقات — يُزرع TRIAL/STANDARD/ENTERPRISE عند أول تشغيل.";
    }
}
