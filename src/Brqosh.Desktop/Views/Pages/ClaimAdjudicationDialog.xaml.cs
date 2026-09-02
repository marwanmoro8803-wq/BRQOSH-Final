using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-٢ب) تسجيلُ قرار الشركة سطراً سطراً.
///
/// <para><b>والمصيرُ يُسأل عنه هنا لا لاحقاً</b>: مَن سجّل الرفضَ يعرف سببَه، <b>وهو
/// وحدَه من يستطيع أن يقول أيُحمَّل على المريض أم يُشطب</b>. وتأجيلُه يجعله رقماً
/// معلّقاً لا يعرف أحدٌ بعد شهرٍ ما قصّته.</para>
/// </summary>
public partial class ClaimAdjudicationDialog : Window
{
    public record DispositionOption(RejectionDisposition Value, string Label);

    public class LineRow
    {
        public long PayerInvoiceId { get; set; }
        public string VisitNumber { get; set; } = "";
        public decimal Claimed { get; set; }
        public string Approved { get; set; } = "0";
        public string Rejected { get; set; } = "0";
        public string Reason { get; set; } = "";
        public RejectionDisposition Disposition { get; set; } = RejectionDisposition.Undecided;
    }

    private readonly long _claimId;
    private readonly ObservableCollection<LineRow> _rows = [];

    public ClaimAdjudicationDialog(long claimId)
    {
        InitializeComponent();
        _claimId = claimId;
        LinesGrid.ItemsSource = _rows;

        DispositionColumn.ItemsSource = new List<DispositionOption>
        {
            new(RejectionDisposition.Undecided, "بلا قرار"),
            new(RejectionDisposition.RebillPatient, "على المريض"),
            new(RejectionDisposition.WriteOff, "شطب"),
        };

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var claim = await db.InsuranceClaims.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == _claimId);
            if (claim is null) return;

            var visitIds = claim.Lines.Select(l => l.VisitId).ToList();
            var visits = await db.Visits.AsNoTracking()
                .Where(v => visitIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.VisitNumber);

            _rows.Clear();
            foreach (var line in claim.Lines)
                _rows.Add(new LineRow
                {
                    PayerInvoiceId = line.PayerInvoiceId,
                    VisitNumber = visits.TryGetValue(line.VisitId, out var n) ? n : $"#{line.VisitId}",
                    Claimed = line.ClaimedAmount,
                    Approved = line.IsAdjudicated
                        ? line.ApprovedAmount.ToString(CultureInfo.InvariantCulture)
                        : line.ClaimedAmount.ToString(CultureInfo.InvariantCulture),
                    Rejected = line.RejectedAmount.ToString(CultureInfo.InvariantCulture),
                    Reason = line.RejectionReason ?? "",
                    Disposition = line.Disposition,
                });
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnApproveAll(object sender, RoutedEventArgs e)
    {
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var row in _rows)
        {
            row.Approved = row.Claimed.ToString(CultureInfo.InvariantCulture);
            row.Rejected = "0";
            row.Reason = "";
            row.Disposition = RejectionDisposition.Undecided;
        }

        LinesGrid.Items.Refresh();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            await using var db = AppServices.CreateDb();
            var claim = await db.InsuranceClaims.SingleAsync(c => c.Id == _claimId);

            foreach (var row in _rows)
            {
                claim.Adjudicate(row.PayerInvoiceId,
                    ParseAmount(row.Approved, row.VisitNumber, "المعتمد"),
                    ParseAmount(row.Rejected, row.VisitNumber, "المرفوض"),
                    row.Reason);

                if (row.Disposition != RejectionDisposition.Undecided)
                    claim.DisposeRejection(row.PayerInvoiceId, row.Disposition);
            }

            claim.CloseAdjudication(DateOnly.FromDateTime(DateTime.Today));
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal ParseAmount(string text, string visitNumber, string field)
    {
        if (decimal.TryParse((text ?? "").Trim(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var value))
            return value;

        throw new AccountingException($"مبلغ «{field}» غير صالح في زيارة {visitNumber}.");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
