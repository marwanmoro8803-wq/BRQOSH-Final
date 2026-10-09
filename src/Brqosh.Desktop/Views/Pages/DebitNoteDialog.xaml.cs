using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class DebitNoteDialog : Window
{
    private record InvoiceOption(long Id, long SupplierId, string Display);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٣٧) <b>سطرٌ من الفاتورة يُختار بعينه — لا وصفٌ حرٌّ
    /// يُنسب إلى أوّل سطر.</b>
    /// </summary>
    private record LineOption(int Index, string Display, decimal Returnable, decimal UnitPrice);

    public DebitNoteDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var invoices = await db.PurchaseInvoices
            .Where(i => i.CompanyId == AppServices.CompanyId && i.Status == PurchaseInvoiceStatus.Posted)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(100)
            .ToListAsync();
        var suppliers = await db.Suppliers
            .Where(s => invoices.Select(i => i.SupplierId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        InvoiceBox.ItemsSource = invoices.Select(i => new InvoiceOption(
            i.Id, i.SupplierId,
            $"{i.InvoiceNumber} — {suppliers.GetValueOrDefault(i.SupplierId, "?")} — {i.GrandTotal():N2}"))
            .ToList();

        InvoiceBox.SelectionChanged += async (_, _) => await LoadLinesAsync();
        if (InvoiceBox.Items.Count > 0) InvoiceBox.SelectedIndex = 0;
    }

    /// <summary>
    /// <b>سطورُ الفاتورة المختارة وما بقي من كلٍّ منها</b> — ومن استُنفد لا يُعرض،
    /// <b>فلا يُطلب ما لا يُرجَع</b>.
    /// </summary>
    private async Task LoadLinesAsync()
    {
        LineBox.ItemsSource = null;
        if (InvoiceBox.SelectedItem is not InvoiceOption inv) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var lines = await new DebitNoteDraftBuilder(db)
                .ReturnableLinesAsync(AppServices.CompanyId, inv.Id);

            LineBox.ItemsSource = lines
                .Where(l => l.ProductId is null || l.Returnable > 0m)
                .Select(l => new LineOption(
                    l.LineIndex,
                    $"{l.Description} — متبقٍّ {l.Returnable:N2} من {l.InvoiceQuantity:N2} @ {l.UnitPrice:N2}",
                    l.Returnable, l.UnitPrice))
                .ToList();

            if (LineBox.Items.Count > 0) LineBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            if (InvoiceBox.SelectedItem is not InvoiceOption inv)
                throw new InvalidOperationException("اختر فاتورة.");
            if (LineBox.SelectedItem is not LineOption line)
                throw new InvalidOperationException("اختر السطر المُرجَع.");
            if (DateBox.SelectedDate is null)
                throw new InvalidOperationException("التاريخ مطلوب.");
            if (!NumericInput.TryParse(QtyBox.Text, out var qty))
                throw new InvalidOperationException("كمية غير صالحة.");

            decimal? price = NumericInput.TryParse(PriceBox.Text, out var typed) ? typed : null;

            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;

            // ‏(ح-٣٧) ‏**البناءُ في خدمةٍ تُقاس** — والشاشةُ تنقل اختيارَ المستخدم لا غير.
            var note = await new DebitNoteDraftBuilder(db).BuildAsync(
                AppServices.CompanyId, inv.Id,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value),
                ReasonBox.Text,
                [new RequestedReturn(line.Index, qty, price,
                    string.IsNullOrWhiteSpace(BatchBox.Text) ? null : BatchBox.Text.Trim())]);

            await facade.GetService<DebitNotePostingService>().PostAsync(note);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
