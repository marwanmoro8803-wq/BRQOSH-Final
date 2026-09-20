using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) إنشاءُ زيارةٍ بخدماتها.
///
/// <para><b>وشمولُ كلّ خدمةٍ يُدخَل سطراً سطراً</b> — فالتجميلُ وما استُثني نصّاً غيرُ
/// مشمولٍ مهما بلغت نسبةُ التغطية. <b>وشمولُ كلّ شيءٍ افتراضاً يُطالِب الشركةَ بما
/// ترفضه</b>، ويُكتشف الرفضُ بعد أشهر.</para>
/// </summary>
public partial class VisitDialog : Window
{
    public class LineRow
    {
        public string? Description { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        public bool IsCovered { get; set; } = true;

        /// <summary>
        /// (CEEF-231 · ط3-٢ج) صنفُ الصيدلية المصروف — <b>وفارغٌ لسطر الخدمة</b>.
        /// </summary>
        public long? ProductId { get; set; }

        /// <summary>رقمُ الدفعة — <b>ويلزم لكلّ دواءٍ متتبَّع</b>.</summary>
        public string? BatchNumber { get; set; }

        /// <summary>
        /// (جاهزيّةُ الإطلاق · ح-١١) <b>المعاملةُ الضريبيّةُ للسطر — وكانت مسمَّرةً خاضعة.</b>
        ///
        /// <para><c>Visit.AddLine</c> يقبل الفئةَ ورمزَها منذ نشأته، <b>والحوارُ لا يُمرّر
        /// شيئاً</b> — فيفترض النطاقُ «خاضعة ١٥٪». <b>فعيادةٌ تُعالج مواطناً تُحمّله ١٥٪</b>
        /// وهي صفريّةٌ عنه (<c>VATEX-SA-HEA</c>)، <b>وصيدليّةٌ تصرف دواءً كذلك</b>
        /// (<c>VATEX-SA-35</c>).</para>
        ///
        /// <para><b>والافتراضُ «خاضعة» عمداً</b>: هو سلوكُ ما مضى حرفاً، والصفريّةُ قرارٌ
        /// يُتَّخذ على السطر لا يُفترض له — <b>وصفريّةٌ تُمنح بالخطأ نقصٌ في إقرارٍ سُلّم</b>.</para>
        /// </summary>
        public VisitVatTreatment Treatment { get; set; } = VisitVatTreatment.Options[0];
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١١) معاملةٌ ضريبيّةٌ مختارةٌ من قائمةٍ مغلقة — <b>لا رمزٌ يُكتب بيد</b>.
    ///
    /// <para><b>والفئةُ ورمزُها يُسافران معاً</b>: فئةٌ صفريّةٌ بلا رمزٍ نظاميٍّ يرفضها
    /// النطاقُ (BR-KSA-CL-06)، ورمزٌ من فئةٍ أخرى يرفضه كذلك. <b>فاقترانُهما في مدخلٍ واحدٍ
    /// يجعل الحالةَ الخاطئةَ غيرَ قابلةٍ للتمثيل</b> بدل أن تُحرَس بفحص.</para>
    /// </summary>
    public sealed record VisitVatTreatment(string Label, VatCategory Category, string? Code)
    {
        public static readonly IReadOnlyList<VisitVatTreatment> Options =
        [
            new("خاضعة 15%", VatCategory.Standard, null),
            new("صفرية — رعاية صحية لمواطن", VatCategory.ZeroRated, "VATEX-SA-HEA"),
            new("صفرية — أدوية ومعدات طبية", VatCategory.ZeroRated, "VATEX-SA-35"),
        ];

        public override string ToString() => Label;
    }

    private sealed record Option(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly ObservableCollection<LineRow> _rows = [];

    public VisitDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        TreatmentColumn.ItemsSource = VisitVatTreatment.Options;
        Loaded += async (_, _) =>
        {
            await LoadPatientsAsync();
            await LoadProductsAsync();
        };
    }

    /// <summary>
    /// (CEEF-231 · ط3-٢ج) أصنافُ الصيدلية المخزنية.
    /// </summary>
    /// <remarks>
    /// <b>والمخزنيّةُ وحدَها تُعرض</b>: صنفٌ خدميٌّ لا يُصرف من مستودع، <b>ويرفضه الصرفُ
    /// باسمه</b> — وعرضُه هنا يجعل الشاشةَ تقترح ما سيُرفض.
    /// </remarks>
    private async Task LoadProductsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var products = await db.Products.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive
                            && p.Type == Brqosh.Domain.Inventory.ProductType.Stockable)
                .OrderBy(p => p.Code)
                .Select(p => new { p.Id, p.Code, p.NameAr })
                .ToListAsync();

            ProductColumn.ItemsSource = products
                .Select(p => new Option(p.Id, $"{p.Code} — {p.NameAr}"))
                .ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async Task LoadPatientsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var patients = await db.Patients.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
                .OrderBy(p => p.NameAr)
                .Select(p => new { p.Id, p.NameAr, p.MedicalRecordNumber })
                .ToListAsync();

            PatientBox.ItemsSource = patients
                .Select(p => new Option(p.Id, $"{p.MedicalRecordNumber} — {p.NameAr}"))
                .ToList();

            if (patients.Count > 0) PatientBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnPatientChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadPoliciesAsync();
    }

    private async Task LoadPoliciesAsync()
    {
        if (PatientBox.SelectedItem is not Option patient) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var today = DateOnly.FromDateTime(DateBox.SelectedDate ?? DateTime.Today);

            var payers = await db.InsurancePayers.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .ToDictionaryAsync(p => p.Id, p => p.NameAr);

            // ‏**والوثائقُ الساريةُ يومَ الزيارة وحدَها** — والمنتهيةُ تُهمَل في الفوترة
            // ‏فتصير الزيارةُ على الحساب الخاصّ، **فعرضُها هنا يُوهم بتغطيةٍ لا تقع**.
            var policies = await db.InsurancePolicies.AsNoTracking()
                .Where(p => p.PatientId == patient.Id && p.IsActive
                            && p.StartDate <= today && today <= p.EndDate)
                .ToListAsync();

            PolicyBox.ItemsSource = policies.Select(p => new Option(p.Id,
                $"{(payers.TryGetValue(p.PayerId, out var n) ? n : "?")} — {p.PolicyNumber}")).ToList();

            PolicyBox.SelectedIndex = policies.Count > 0 ? 0 : -1;
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
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (PatientBox.SelectedItem is not Option patient)
                throw new AccountingException("اختر المريض.");
            if (DateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الزيارة مطلوب.");

            var lines = _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description)).ToList();
            if (lines.Count == 0)
                throw new AccountingException("الزيارة بلا خدمات.");

            var visit = BuildVisitFromRows(
                AppServices.CompanyId, patient.Id, NumberBox.Text,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value), DoctorBox.Text,
                (PolicyBox.SelectedItem as Option)?.Id);

            await using var db = AppServices.CreateDb();
            db.Visits.Add(visit);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>سطورُ الحوار — <b>درزُ اختبارٍ على سابقة <c>PurchaseInvoiceDialog</c></b>.</summary>
    internal ObservableCollection<LineRow> RowsForTests => _rows;

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١١) <b>بناءُ الزيارة من السطور — مُستخرَجٌ كي يُقاس.</b>
    ///
    /// <para><b>وهذا هو موضعُ العطل</b>: كان يستدعي <c>AddLine</c> بلا فئةٍ ضريبيّةٍ ولا
    /// رمز، <b>فيفترض النطاقُ «خاضعة ١٥٪» لكلّ خدمةٍ ودواء</b> — ومنه الصفريُّ للمواطن.</para>
    ///
    /// <para><b>ومعالجُ الحدث لا يُشغَّل في اختبار</b>، فيبقى ما بداخله بلا قياسٍ مهما
    /// كَثُرت الحالاتُ حولَه. والاستخراجُ يجعل السلوكَ نفسَه — لا جدولَ الخيارات وحدَه —
    /// قابلاً للعضّ. وهي سابقةُ <c>BuildInvoiceFromRows</c> نفسُها.</para>
    /// </summary>
    internal Visit BuildVisitFromRows(
        long companyId, long patientId, string visitNumber,
        DateOnly visitDate, string? doctor, long? policyId)
    {
        var visit = Visit.Create(companyId, patientId, visitNumber, visitDate, doctor, policyId);

        foreach (var line in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description)))
        {
            // ‏**والمعاملةُ الضريبيّةُ تُمرَّر** — وكانت تُترك للافتراض.
            var treatment = line.Treatment ?? VisitVatTreatment.Options[0];

            visit.AddLine(line.Description!, line.Quantity, line.UnitPrice, line.IsCovered,
                line.ProductId, treatment.Category, treatment.Code,
                batchNumber: line.BatchNumber);
        }

        return visit;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
