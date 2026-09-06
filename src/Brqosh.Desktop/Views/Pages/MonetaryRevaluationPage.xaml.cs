using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط4-٧) <b>إعادةُ تقييم الذمم بالعملات — إلزامٌ محاسبيٌّ لا تحسين.</b>
///
/// <para>البنودُ النقدية تُترجَم بسعر الإقفال (معيار المحاسبة الدولي 21). <b>وذمّةٌ
/// بالدولار كانت تبقى بسعر يوم الفاتورة إلى الأبد</b> — فتُقفل المنشأةُ سنتَها بذممٍ
/// تخالف قيمتَها الحقيقية، والفرقُ يكبر كلَّما تحرّك السعر.</para>
///
/// <para><b>والمكشوفُ يُعرض قبل التقييم</b>: كم بالعملة وكم هو مقيَّدٌ بالريال — <b>فيرى
/// المحاسبُ ما سيتغيّر قبل أن يُقيَّد</b>، ولا يُفاجأ برقمٍ في القوائم.</para>
/// </summary>
public partial class MonetaryRevaluationPage : UserControl
{
    public class Row
    {
        public string? CurrencyCode { get; set; }
        public decimal ForeignReceivable { get; set; }
        public decimal BookedReceivable { get; set; }
        public decimal ForeignPayable { get; set; }
        public decimal BookedPayable { get; set; }
        public string? ClosingRate { get; set; }
    }

    private readonly ObservableCollection<Row> _rows = [];

    public MonetaryRevaluationPage()
    {
        InitializeComponent();
        AsOfBox.SelectedDate = new DateTime(DateTime.Today.Year, 12, 31);
        Grid.ItemsSource = _rows;

        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (RunBtn, PermissionService.CanPostAccounting));
            await RefreshAsync();
        };
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (AsOfBox.SelectedDate is null) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var exposures = await facade.GetService<MonetaryRevaluationService>()
                .ExposuresAsync(AppServices.CompanyId, DateOnly.FromDateTime(AsOfBox.SelectedDate.Value));

            _rows.Clear();
            foreach (var exposure in exposures)
                _rows.Add(new Row
                {
                    CurrencyCode = exposure.CurrencyCode,
                    ForeignReceivable = exposure.ForeignReceivable,
                    BookedReceivable = exposure.BookedReceivable,
                    ForeignPayable = exposure.ForeignPayable,
                    BookedPayable = exposure.BookedPayable,
                });

            UiTestAutomation.PublishGridCount(Grid, _rows.Count);

            SummaryText.Text = _rows.Count == 0
                ? "لا ذممَ مفتوحةً بعملةٍ أجنبية — ولا شيءَ يُعاد تقييمه."
                : $"عملات مكشوفة: {_rows.Count:N0} — أدخِلْ سعرَ الإقفال لكلٍّ منها ثم أعِدْ التقييم.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        if (AsOfBox.SelectedDate is null)
        {
            UiFeedback.ShowWarning("حدّد تاريخ الإقفال أولاً.");
            return;
        }

        Grid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var row in _rows)
            {
                if (string.IsNullOrWhiteSpace(row.ClosingRate)) continue;
                if (!NumericInput.TryParse(row.ClosingRate, out var rate))
                    throw new Brqosh.Domain.Accounting.AccountingException(
                        $"سعر إقفال «{row.CurrencyCode}» غير صالح — ووردَ «{row.ClosingRate}».");

                rates[row.CurrencyCode!] = rate;
            }

            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<MonetaryRevaluationService>().RevalueAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(AsOfBox.SelectedDate.Value),
                rates, SecurityContext.ResolveUsername());

            // ‏**والأثرُ يُعرض بشقّيه**: الذمم المدينة والدائنة تتحرّكان في اتّجاهين
            // ‏متعاكسين عند حركة السعر نفسِها — **وجمعُهما في رقمٍ واحدٍ يُخفي ذلك**.
            SummaryText.Text = result.JournalEntryId is null
                ? "لا فرقَ عند هذه الأسعار — ولم يُقيَّد شيء."
                : $"قُيّد التقييم: ذمم مدينة {result.ReceivableAdjustment:N2} · "
                  + $"ذمم دائنة {result.PayableAdjustment:N2} — وعكسُه في اليوم التالي.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
