using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Tests;

/// <summary>(تصدير Excel) كلُّ جدولٍ يُصدَّر: أعمدتُه المعروضةُ بترتيب الشاشة، وكلُّ مصدره، وبأنواعٍ حقيقيّة.</summary>
public class EveryGridExportsToExcelTests
{
    private sealed record Row(string Name, decimal Amount, DateOnly Day, bool Posted, Row? Inner = null);

    [Fact]
    public void TheExtraction_KeepsTypes_AndEveryRow()
    {
        IReadOnlyList<GridExport.Column> columns =
            [new("الاسم", "Name"), new("المبلغ", "Amount"), new("التاريخ", "Day"), new("مرحّل", "Posted"), new("الداخل", "Inner.Name")];
        var items = new[]
        {
            new Row("أ", 1250.5m, new DateOnly(2026, 10, 1), true, new Row("ب", 1m, default, false)),
            new Row("ج", 0m, new DateOnly(2026, 10, 2), false),
        };

        var (headers, rows) = GridExport.Extract(columns, items);

        Assert.Equal(["الاسم", "المبلغ", "التاريخ", "مرحّل", "الداخل"], headers);
        Assert.Equal(2, rows.Count);
        Assert.Equal(XlsxCellKind.Number, rows[0][1].Kind);
        Assert.Equal(1250.5m, rows[0][1].Value);
        Assert.Equal(XlsxCellKind.Date, rows[0][2].Kind);
        Assert.Equal("نعم", rows[0][3].Value);
        Assert.Equal("ب", rows[0][4].Value);
        Assert.Equal("", rows[1][4].Value);          // مسارٌ فارغ لا يرمي

        var bytes = XlsxWriter.Build("t", headers, rows);
        Assert.Equal((byte)'P', bytes[0]);           // ملفّ zip صالح
    }

    [Fact]
    public void TheColumns_FollowTheScreenOrder_AndSkipTemplatesAndHidden()
    {
        var columns = WpfTestHost.Invoke(() =>
        {
            var grid = new DataGrid { AutoGenerateColumns = false };
            grid.Columns.Add(new DataGridTextColumn { Header = "ب", Binding = new Binding("B"), DisplayIndex = 1 });
            grid.Columns.Add(new DataGridTextColumn { Header = "أ", Binding = new Binding("A"), DisplayIndex = 0 });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "زر" });
            grid.Columns.Add(new DataGridTextColumn { Header = "مخفي", Binding = new Binding("H"), Visibility = Visibility.Collapsed });
            return GridExport.ColumnsOf(grid);
        });

        Assert.Equal(["أ", "ب"], columns.Select(c => c.Header).ToArray());
    }

    [Fact]
    public void ThePrintedCells_UseWholeNumbersOrTwoDecimals_PerColumn()
    {
        var (_, cells) = GridExport.Extract(
            [new("الاسم", "Name"), new("المبلغ", "Amount"), new("التاريخ", "Day")],
            new[] { new Row("أ", 1250.5m, new DateOnly(2026, 10, 1), true), new Row("ب", 3m, new DateOnly(2026, 10, 2), false) });

        var text = GridExport.Stringify(cells);

        Assert.Equal(["أ", "1,250.50", "2026-10-01"], text[0]);
        Assert.Equal(["ب", "3.00", "2026-10-02"], text[1]);           // العمودُ فيه كسرٌ فكلُّه منزلتان
    }
}
