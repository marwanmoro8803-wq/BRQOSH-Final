using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (أسئلةُ المالك · تصدير Excel) <b>كلُّ جدولٍ في البرنامج يُصدَّر إلى Excel بنقرةٍ يمنى</b> — كان ذلك في ثلاث شاشاتٍ من أكثر من مئة.
///
/// <para>يُصدَّر <b>كلُّ ما في مصدر الجدول</b> (لا الصفوفَ المرئيّةَ وحدَها) بأعمدته المعروضة بترتيبها على الشاشة، والأرقامُ أرقامٌ في Excel
/// والتواريخُ تواريخ. والأعمدةُ القالبيّة (أزرارٌ ومربّعاتُ اختيار) تُتجاوَز لأنّ لا قيمةَ نصّيّةً لها.</para>
/// </summary>
public static class GridExport
{
    public sealed record Column(string Header, string Path);

    /// <summary>يستخرج الجدولَ من أعمدةٍ ومصدر — دالّةٌ صافيةٌ تُقاس بلا واجهة.</summary>
    public static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<XlsxWriter.Cell>> Rows) Extract(
        IReadOnlyList<Column> columns, IEnumerable? items)
    {
        var headers = columns.Select(c => c.Header).ToList();
        var rows = new List<IReadOnlyList<XlsxWriter.Cell>>();
        if (items is null)
            return (headers, rows);

        foreach (var item in items)
        {
            if (item is null)
                continue;
            var type = item.GetType();
            rows.Add(columns.Select(c => CellOf(type.GetProperty(FirstSegment(c.Path))?.GetValue(item), c.Path, item)).ToList());
        }

        return (headers, rows);
    }

    private static string FirstSegment(string path) => path.Split('.')[0];

    private static XlsxWriter.Cell CellOf(object? first, string path, object item)
    {
        var value = first;
        foreach (var segment in path.Split('.').Skip(1))
            value = value?.GetType().GetProperty(segment)?.GetValue(value);

        return value switch
        {
            null => XlsxWriter.Cell.Text(""),
            decimal d => XlsxWriter.Cell.Number(d),
            int i => XlsxWriter.Cell.Number(i),
            long l => XlsxWriter.Cell.Number(l),
            DateOnly date => XlsxWriter.Cell.Date(date),
            DateTime dt => XlsxWriter.Cell.Date(DateOnly.FromDateTime(dt)),
            bool b => XlsxWriter.Cell.Text(b ? "نعم" : "لا"),
            _ => XlsxWriter.Cell.Text(value.ToString()),
        };
    }

    /// <summary>أعمدةُ الجدول المعروضةُ المرتبطةُ بخاصّيّة — بترتيب الشاشة.</summary>
    public static IReadOnlyList<Column> ColumnsOf(DataGrid grid) =>
        grid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Where(c => c.Visibility == Visibility.Visible)
            .OfType<DataGridBoundColumn>()
            .Select(c => (Column: c, Path: (c.Binding as Binding)?.Path?.Path))
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .Select(x => new Column(x.Column.Header?.ToString() ?? x.Path!, x.Path!))
            .ToList();

    /// <summary>يسجّل قائمةَ النقر اليمين على كلّ <see cref="DataGrid"/> في البرنامج — مرّةً عند الإقلاع.</summary>
    public static void Register() =>
        EventManager.RegisterClassHandler(typeof(DataGrid), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnGridLoaded));

    private static void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.ContextMenu is not null)
            return;

        var item = new MenuItem { Header = LocaleManager.T("تصدير الجدول إلى Excel", "Export table to Excel") };
        item.Click += (_, _) => Export(grid);
        var print = new MenuItem { Header = LocaleManager.T("طباعة الجدول / PDF", "Print table / PDF") };
        print.Click += async (_, _) => await PrintAsync(grid);
        grid.ContextMenu = new ContextMenu { Items = { item, print } };
    }

    /// <summary>
    /// (المخرجاتُ كلُّها بالقالب الموحَّد) يطبع الجدولَ بترويسة المنشأة وشعارها وخطّها وتذييلها — كما تُطبع التقارير — عبر
    /// <c>DocumentPrintService.ExportTableAsync</c>. العنوانُ هو عنوانُ الصفحة الجارية.
    /// </summary>
    private static async Task PrintAsync(DataGrid grid)
    {
        try
        {
            var columns = ColumnsOf(grid);
            if (columns.Count == 0)
            {
                UiFeedback.ShowWarning(LocaleManager.T("لا أعمدةَ قابلةً للطباعة في هذا الجدول.", "This table has no printable columns."));
                return;
            }

            var (headers, cells) = Extract(columns, grid.ItemsSource);
            var rows = Stringify(cells);
            var title = (System.Windows.Application.Current?.MainWindow?.FindName("PageTitle") as TextBlock)?.Text;
            if (string.IsNullOrWhiteSpace(title))
                title = LocaleManager.T("جدول", "Table");
            var username = Brqosh.Infrastructure.Security.SecurityContext.ResolveUsername();

            await Reporting.DocumentPrintHelper.ExportAndDeliverAsync(
                s => s.ExportTableAsync(AppServices.CompanyId, username, title!, headers, rows),
                Window.GetWindow(grid),
                new Reporting.DocumentDeliverOptions(OfferSaveAs: true));
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>خلايا الجدول نصّاً للطباعة: عمودٌ أعدادُه صحيحةٌ بلا كسور، وعمودٌ فيه كسورٌ بمنزلتين.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Stringify(IReadOnlyList<IReadOnlyList<XlsxWriter.Cell>> cells)
    {
        var width = cells.Count == 0 ? 0 : cells.Max(r => r.Count);
        var fractional = Enumerable.Range(0, width)
            .Select(i => cells.Any(r => i < r.Count && r[i].Kind == XlsxCellKind.Number && r[i].Value is decimal d && d != decimal.Truncate(d)))
            .ToArray();

        return cells.Select(r => (IReadOnlyList<string>)r.Select((c, i) => c.Kind switch
        {
            XlsxCellKind.Number when c.Value is decimal d => d.ToString(fractional[i] ? "N2" : "N0", System.Globalization.CultureInfo.InvariantCulture),
            XlsxCellKind.Date when c.Value is DateOnly day => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            _ => c.Value?.ToString() ?? "",
        }).ToList()).ToList();
    }

    private static void Export(DataGrid grid)
    {
        try
        {
            var columns = ColumnsOf(grid);
            if (columns.Count == 0)
            {
                UiFeedback.ShowWarning(LocaleManager.T("لا أعمدةَ قابلةً للتصدير في هذا الجدول.", "This table has no exportable columns."));
                return;
            }

            var (headers, rows) = Extract(columns, grid.ItemsSource);
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel|*.xlsx",
                FileName = $"brqosh-table-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            };
            if (dialog.ShowDialog() != true)
                return;

            File.WriteAllBytes(dialog.FileName, XlsxWriter.Build(LocaleManager.T("جدول", "Table"), headers, rows, rightToLeft: !LocaleManager.IsEnglish));
            UiFeedback.ShowInfo(LocaleManager.T($"صُدِّر {rows.Count:N0} صفّاً.", $"Exported {rows.Count:N0} rows."));
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
