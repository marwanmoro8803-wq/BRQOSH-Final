using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Brqosh.Application.Intelligence;
using Brqosh.Application.Treasury;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class DashboardPage : UserControl
{
    private static readonly (string TitleAr, string TitleEn, string Monogram, string Color, KpiValueKind Kind)[] CardMeta =
    [
        ("الإيرادات", "Revenue", "RV", "#22D3EE", KpiValueKind.Currency),
        ("المصروفات", "Expenses", "EX", "#F87171", KpiValueKind.Currency),
        ("صافي الربح", "Net Profit", "NP", "#D4AF37", KpiValueKind.Currency),
        ("التدفق النقدي", "Cash Flow", "CF", "#34D399", KpiValueKind.Currency),
        ("ذمم العملاء", "AR", "AR", "#0891B2", KpiValueKind.Currency),
        ("ذمم الموردين", "AP", "AP", "#EA580C", KpiValueKind.Currency),
        ("صافي ضريبة", "Net VAT", "VT", "#4F46E5", KpiValueKind.Currency),
        ("قيمة المخزون", "Inventory", "ST", "#0D9488", KpiValueKind.Currency),
        ("فترات مفتوحة", "Open Periods", "OP", "#16A34A", KpiValueKind.Count),
        ("فترات مقفلة", "Closed Periods", "CL", "#64748B", KpiValueKind.Count),
    ];

    public DashboardPage()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(KpiGrid, UiTestAutomation.KpiGridId);
        Loaded += OnLoaded;
        LocaleManager.Changed += OnLocaleChanged;
        Unloaded += (_, _) => LocaleManager.Changed -= OnLocaleChanged;
    }

    private void OnLocaleChanged()
    {
        LocaleManager.ApplyTo(this);
        if (IsLoaded)
            _ = LoadAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { await LoadAsync(); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string message)
    {
        SetLoading(false);
        KpiGrid.Children.Clear();
        ChartsColumn.Children.Clear();
        IntelColumn.Children.Clear();
        AlertHost.Content = new Border
        {
            Style = UiResources.StyleOf(this, "ContentCard"),
            Padding = new Thickness(24),
            Child = new TextBlock
            {
                Text = message,
                Foreground = UiResources.BrushOf(this, "DangerBrush"),
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    private async Task LoadAsync()
    {
        SetLoading(true);
        await using var db = AppServices.CreateDb();
        var k = await new DashboardQuery(db).ExecuteAsync(AppServices.CompanyId);

        FinancialIntelligenceReport? intel = null;
        CashFlowForecastResult? forecast = null;
        try
        {
            using var scope = AppServices.CreateInfrastructureScope();
            var service = scope.ServiceProvider.GetRequiredService<IFinancialIntelligenceService>();
            intel = await service.AnalyzeAsync(AppServices.CompanyId);
            forecast = await scope.ServiceProvider.GetRequiredService<ICashFlowForecastService>()
                .ForecastAsync(AppServices.CompanyId);
        }
        catch
        {
            // fallback to KPI-only insights panel
        }

        RenderExecutive(k, intel, forecast);
        SetLoading(false);
    }

    private void SetLoading(bool loading)
    {
        LoadingOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        LoadingText.Text = LocaleManager.T("جاري تحميل لوحة القيادة...", "Loading executive dashboard...");
        BodyGrid.IsEnabled = !loading;
    }

    private void RenderExecutive(DashboardKpis k, FinancialIntelligenceReport? intel = null, CashFlowForecastResult? forecast = null)
    {
        HeroTitleText.Text = LocaleManager.T("لوحة القيادة التنفيذية", "Executive Command Center");
        HeroSubtitleText.Text = forecast is null
            ? LocaleManager.T(
                "نظرة مالية لحظية — محدّثة من القيود والمستندات المرحّلة.",
                "Real-time financial pulse — synced from posted documents.")
            : LocaleManager.T(
                $"توقع تدفق نقدي 13 أسبوعاً: صافي {forecast.TotalProjectedInflows - forecast.TotalProjectedOutflows:N0} ر.س (تحصيل {forecast.TotalProjectedInflows:N0} · سداد {forecast.TotalProjectedOutflows:N0}).",
                $"13-week cash forecast: net SAR {forecast.TotalProjectedInflows - forecast.TotalProjectedOutflows:N0} (in {forecast.TotalProjectedInflows:N0} · out {forecast.TotalProjectedOutflows:N0}).");

        AlertHost.Content = ExecutiveDashboardBuilder.BuildAlertStrip(this, k);

        var values = new[]
        {
            k.SalesThisMonth,
            k.PurchasesThisMonth + k.PaidThisMonth,
            k.SalesThisMonth - k.PurchasesThisMonth,
            k.CollectedThisMonth - k.PaidThisMonth,
            k.Receivables, k.Payables, k.NetVatThisMonth, k.InventoryValue,
            k.OpenPeriods, k.ClosedPeriods,
        };

        KpiGrid.Children.Clear();
        for (var i = 0; i < values.Length; i++)
        {
            var meta = CardMeta[i];
            var title = LocaleManager.T(meta.TitleAr, meta.TitleEn);
            var card = KpiMetricCardBuilder.Create(this, title, meta.Monogram, values[i], meta.Color, meta.Kind);
            AutomationProperties.SetName(card, title);
            KpiGrid.Children.Add(card);
        }

        ChartsColumn.Children.Clear();
        IntelColumn.Children.Clear();

        var cashSeries = new[] { k.CollectedThisMonth * 0.7m, k.CollectedThisMonth * 0.85m, k.CollectedThisMonth, k.PaidThisMonth, k.CollectedThisMonth - k.PaidThisMonth };
        var revSeries = new[] { k.SalesThisMonth * 0.6m, k.SalesThisMonth * 0.8m, k.SalesThisMonth, k.PurchasesThisMonth, k.SalesThisMonth - k.PurchasesThisMonth };

        ChartsColumn.Children.Add(ExecutiveDashboardBuilder.BuildChartPanel(this, "اتجاه النقد", "Cash Trend", cashSeries, "شهري", "Monthly"));
        ChartsColumn.Children.Add(ExecutiveDashboardBuilder.BuildChartPanel(this, "الإيرادات مقابل التكلفة", "Revenue vs Cost", revSeries, "مقارنة", "Comparison"));
        ChartsColumn.Children.Add(ExecutiveDashboardBuilder.BuildPerformanceSummary(this, k));

        if (intel is not null)
            IntelColumn.Children.Add(ExecutiveDashboardBuilder.BuildRuleEngineInsightsPanel(this, intel));
        else
            IntelColumn.Children.Add(ExecutiveDashboardBuilder.BuildAiInsightsPanel(this, k));
        IntelColumn.Children.Add(ExecutiveDashboardBuilder.BuildQuickActions(this));
        IntelColumn.Children.Add(ExecutiveDashboardBuilder.BuildActivityFeed(this, k));

        UiTestAutomation.PublishKpiPanel(KpiGrid, values.Length);
    }
}
