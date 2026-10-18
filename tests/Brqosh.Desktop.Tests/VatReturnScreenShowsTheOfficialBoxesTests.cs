using Brqosh.Application.Reporting;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦٥ · tax-zatca#12) <b>شاشةُ الإقرار تعرض خاناتِ نموذج الهيئة بأرقامها</b> — المبلغُ،
/// والتعديلاتُ متى وُجدت، والضريبة؛ وإجماليّا الخانتين 6 و12؛ <b>وضريبةٌ بلا مستندٍ محسوبٍ صفٌّ مستقلٌّ</b> لا يُدسّ في خانة.
/// </summary>
public class VatReturnScreenShowsTheOfficialBoxesTests
{
    private static VatReturnDto Quarter() => new(
        new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
        SalesSubtotal: 1_500m, OutputVat: 150m,
        ProgressBillingsSubtotal: 0m, ProgressBillingsVat: 0m,
        CreditNotesSubtotal: 400m, CreditNotesVat: 60m,
        PurchasesSubtotal: 0m, DeductibleInputVat: 0m,
        DebitNotesSubtotal: 0m, DebitNotesVat: 0m, DebitNotesDeductibleVat: 0m,
        CancelledOutputVat: 0m, CancelledInputVat: 0m,
        JournalOutputVat: 30m, JournalInputVat: 0m,
        SalesBaseNet: 1_100m, PurchasesBaseNet: 0m,
        SalesBoxes:
        [
            new VatReturnBoxDto(1, 1_000m, -400m, 150m, -60m),
            new VatReturnBoxDto(2, 0m, 0m, 0m, 0m),
            new VatReturnBoxDto(3, 0m, 0m, 0m, 0m),
            new VatReturnBoxDto(4, 500m, 0m, 0m, 0m),
            new VatReturnBoxDto(5, 0m, 0m, 0m, 0m),
        ],
        PurchaseBoxes:
        [
            new VatReturnBoxDto(7, 0m, 0m, 0m, 0m),
            new VatReturnBoxDto(8, 0m, 0m, 0m, 0m),
            new VatReturnBoxDto(9, 0m, 0m, 0m, 0m),
            new VatReturnBoxDto(10, 0m, 0m, 0m, 0m),
            new VatReturnBoxDto(11, 0m, 0m, 0m, 0m),
        ]);

    [Fact]
    public void EveryFormBox_HasARowWithItsNumberAndOfficialTitle()
    {
        var labels = VatReturnRows.Build(Quarter()).Select(r => r.Label).ToList();

        foreach (var box in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })
            Assert.Contains(labels, l => l.StartsWith($"خانة {box} — ", StringComparison.Ordinal));
        Assert.Contains("خانة 4 — الصادرات", labels);
    }

    [Fact]
    public void ABox_ShowsItsAmountAdjustmentsAndVat_AndTheTotalsAddUp()
    {
        var rows = VatReturnRows.Build(Quarter()).ToList();
        var box1 = rows.FindIndex(r => r.Label == "خانة 1 — المبيعات الخاضعة للنسبة الأساسية");

        Assert.Equal(1_000m, rows[box1].Value);
        Assert.Equal(("التعديلات", -400m), (rows[box1 + 1].Label, rows[box1 + 1].Value));
        Assert.Equal(("ضريبة القيمة المضافة", 90m), (rows[box1 + 2].Label, rows[box1 + 2].Value));

        var total = rows.FindIndex(r => r.Label == "خانة 6 — إجمالي المبيعات");
        Assert.Equal(1_100m, rows[total].Value);
        Assert.Equal(90m, rows[total + 1].Value);
    }

    [Fact]
    public void VatWithoutACountedDocument_IsItsOwnRow_NotHiddenInABox()
    {
        var rows = VatReturnRows.Build(Quarter());

        Assert.Contains(rows, r => r.Label.StartsWith("ضريبة مخرجات بلا مستند محسوب", StringComparison.Ordinal) && r.Value == 30m);
    }

    [Fact]
    public void WithoutBoxes_TheScreenAddsNoFormRows()
    {
        var rows = VatReturnRows.Build(Quarter() with { SalesBoxes = null, PurchaseBoxes = null });

        Assert.DoesNotContain(rows, r => r.Label.StartsWith("خانة ", StringComparison.Ordinal));
    }
}
