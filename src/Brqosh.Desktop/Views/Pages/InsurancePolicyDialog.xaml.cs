using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) وثيقةُ تأمينٍ لمريض.
///
/// <para><b>وشركةُ التأمين تُنشأ عند أوّل وثيقة</b> إن لم تكن موجودة — <b>ومعها ذمّتُها
/// في دفتر العملاء</b>، فحصّتُها من الفاتورة دَينٌ عليها يُتابَع كأيّ دَين.</para>
/// </summary>
public partial class InsurancePolicyDialog : Window
{
    private sealed record Option(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly long _patientId;

    public InsurancePolicyDialog() : this(0) { }

    public InsurancePolicyDialog(long patientId)
    {
        InitializeComponent();
        _patientId = patientId;
        StartBox.SelectedDate = DateTime.Today;
        EndBox.SelectedDate = DateTime.Today.AddYears(1).AddDays(-1);
        Loaded += async (_, _) => await LoadPayersAsync();
    }

    private async Task LoadPayersAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var payers = await db.InsurancePayers.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
                .OrderBy(p => p.NameAr)
                .Select(p => new { p.Id, p.NameAr })
                .ToListAsync();

            PayerBox.ItemsSource = payers.Select(p => new Option(p.Id, p.NameAr)).ToList();
            PayerBox.IsEditable = payers.Count == 0;

            if (payers.Count == 0)
                PayerBox.Text = string.Empty;
            else
                PayerBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (StartBox.SelectedDate is null || EndBox.SelectedDate is null)
                throw new AccountingException("حدّد بداية السريان ونهايته.");

            await using var db = AppServices.CreateDb();
            await using var transaction = await db.Database.BeginTransactionAsync();

            long payerId;
            if (PayerBox.SelectedItem is Option chosen)
            {
                payerId = chosen.Id;
            }
            else
            {
                var name = (PayerBox.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new AccountingException("اسم شركة التأمين مطلوب.");

                var customer = Customer.Create(AppServices.CompanyId, name);
                db.Customers.Add(customer);
                await db.SaveChangesAsync();

                var payer = InsurancePayer.Create(AppServices.CompanyId,
                    name.Length <= 32 ? name : name[..32], name, customer.Id);
                db.InsurancePayers.Add(payer);
                await db.SaveChangesAsync();
                payerId = payer.Id;
            }

            decimal? limit = string.IsNullOrWhiteSpace(LimitBox.Text)
                ? null
                : Number(LimitBox.Text, "سقف الوثيقة");

            var policy = InsurancePolicy.Create(AppServices.CompanyId, _patientId, payerId,
                PolicyNumberBox.Text, MemberNumberBox.Text,
                Number(CoverageBox.Text, "نسبة التغطية"),
                DateOnly.FromDateTime(StartBox.SelectedDate.Value),
                DateOnly.FromDateTime(EndBox.SelectedDate.Value),
                Number(CopayBox.Text, "التحمل"), limit);

            db.InsurancePolicies.Add(policy);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Number(string text, string field) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
