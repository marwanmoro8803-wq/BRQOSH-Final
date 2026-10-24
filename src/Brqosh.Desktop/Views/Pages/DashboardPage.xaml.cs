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
        ("الإيرادات", "Revenue", "RV", "#5BA4D6", KpiValueKind.Currency),
        ("المصروفات", "Expenses", "EX", "#F87171", KpiValueKind.Currency),
        ("صافي الربح", "Net Profit", "NP", "#D4AF37", KpiValueKind.Currency),
        ("التدفق النقدي", "Cash Flow", "CF", "#34D399", KpiValueKind.Currency),
        ("ذمم العملاء", "AR", "AR", "#3B82B6", KpiValueKind.Currency),
        ("ذمم الموردين", "AP", "AP", "#EA580C", KpiValueKind.Currency),
        ("صافي ضريبة", "Net VAT", "VT", "#4F46E5", KpiValueKind.Currency),
        ("قيمة المخزون", "Inventory", "ST", "#2A6F9E", KpiValueKind.Currency),
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
        GettingStartedHost.Content = null;
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
        var companyId = AppServices.CompanyId;

        // ‏(أسئلةُ المالك 2026-10-24 · الأداء) ‏**ثلاثةُ استعلاماتٍ مستقلّةٍ تجري معاً، كلٌّ بسياقها** — كانت تجري واحداً بعد واحدٍ فيكون زمنُ
        // ‏الفتح مجموعَها. والآن أبطؤُها. و`CreateDb`/`CreateInfrastructureScope` يُعيدان ربطَ الجلسة على الخيط الذي يُنادي بهما، فلا يضيع الفاعلُ
        // ‏في خيط العمل. والتحليلُ مشتركٌ مع لوحة المساعد (`IntelligenceCache`) فلا يُحسب مرّتين.
        var kpiTask = Task.Run(async () =>
        {
            await using var db = AppServices.CreateDb();
            var kpis = await new DashboardQuery(db).ExecuteAsync(companyId);
            IReadOnlyList<OnboardingStep> steps = [];
            try
            {
                steps = await new OnboardingQuery(db).ExecuteAsync(companyId);
            }
            catch (Exception ex)
            {
                // ‏دليلُ البدء تحسينٌ لا شرط: عطلُه لا يُسقط لوحةً تعمل، لكنّه يُسجَّل ولا يُبتلع.
                Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "DashboardPage.Onboarding");
            }

            return (kpis, steps);
        });

        var intelTask = Task.Run(async () =>
        {
            try
            {
                return (FinancialIntelligenceReport?)await IntelligenceCache.AnalyzeAsync(companyId);
            }
            catch (Exception ex)
            {
                // (CEEF-231 · O-36) ‏**التدهورُ إلى لوحةِ المؤشّراتِ وحدَها يبقى** — فشاشةٌ
                // ‏تسقط لتحليلٍ اختياريٍّ أسوأُ من شاشةٍ بلا تحليل. **لكنّ الابتلاعَ الصامتَ
                // ‏كان يجعل عطلاً دائماً يُقرأ «لا توجد رؤى اليوم»**، فلا يُبلَّغ عنه أبداً.
                Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "DashboardPage.FinancialIntelligence");
                return null;
            }
        });

        var forecastTask = Task.Run(async () =>
        {
            try
            {
                using var scope = AppServices.CreateInfrastructureScope();
                return (CashFlowForecastResult?)await scope.ServiceProvider.GetRequiredService<ICashFlowForecastService>()
                    .ForecastAsync(companyId);
            }
            catch (Exception ex)
            {
                Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "DashboardPage.CashFlowForecast");
                return null;
            }
        });

        var (k, onboarding) = await kpiTask;
        var intel = await intelTask;
        var forecast = await forecastTask;

        RenderExecutive(k, intel, forecast);
        GettingStartedHost.Content = ExecutiveDashboardBuilder.BuildGettingStarted(this, onboarding);
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
                $"توقع تدفق نقدي 13 أسبوعاً: صافي {forecast.TotalProjectedInflows - forecast.TotalProjectedOutflows:N0} {CurrencyLabel.Short} (تحصيل {forecast.TotalProjectedInflows:N0} · سداد {forecast.TotalProjectedOutflows:N0}).",
                $"13-week cash forecast: net {CurrencyLabel.Code} {forecast.TotalProjectedInflows - forecast.TotalProjectedOutflows:N0} (in {forecast.TotalProjectedInflows:N0} · out {forecast.TotalProjectedOutflows:N0}).");

        AlertHost.Content = ExecutiveDashboardBuilder.BuildAlertStrip(this, k);

        var values = new[]
        {
            // (CEEF-214) من قائمة الدخل لا من إجماليات المستندات. كان:
            //   الإيرادات = مبيعات شاملة الضريبة (والضريبة أمانة للهيئة لا إيراد)
            //   المصروفات = مشتريات + سدادات (شراء المخزون أصل لا مصروف، والسداد
            //               حركة نقد — ومع ذلك يُحتسب الاثنان فيزدوج الرقم)
            //   صافي الربح = مبيعات − مشتريات (ليس ربحاً بأي تعريف)
            // فكانت اللوحة تعرض خسارة بينما قائمة الدخل تعرض ربحاً على نفس البيانات.
            k.RevenueThisMonth,
            k.ExpensesThisMonth,
            k.NetProfitThisMonth,
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

        // (CEEF-216) اتجاه نقد **حقيقي** من القاعدة. كان يُصطنع بضرب رقم الشهر الجاري
        // في 0.7 و0.85 ويُعرض كأنه تاريخ شهريّ فعليّ — بيانات مالية ملفّقة على لوحة قرار.
        var cashSeries = (k.CashTrend ?? [])
            .Select(p => new ChartPoint(p.Label, p.Net))
            .ToList();

        // مقارنة بين فئات لا اتجاه زمنيّ — أعمدة لا خط.
        var revSeries = new List<ChartPoint>
        {
            new(LocaleManager.T("إيراد", "Revenue"), k.RevenueThisMonth),
            new(LocaleManager.T("مصروف", "Expenses"), k.ExpensesThisMonth),
            new(LocaleManager.T("ربح", "Profit"), k.NetProfitThisMonth),
        };

        if (cashSeries.Count > 0)
            ChartsColumn.Children.Add(ExecutiveDashboardBuilder.BuildChartPanel(
                this, "اتجاه النقد", "Cash Trend", cashSeries, "صافي شهري", "Monthly net"));

        ChartsColumn.Children.Add(ExecutiveDashboardBuilder.BuildChartPanel(
            this, "الإيرادات مقابل التكلفة", "Revenue vs Cost", revSeries, "مقارنة", "Comparison", asBars: true));
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
