using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>خطوةٌ من «ابدأ هنا»: ما هي، وأيّ شاشةٍ تُنجزها، وهل أُنجزت.</summary>
public sealed record OnboardingStep(string Code, string TitleAr, string TitleEn, string PageKey, bool Done);

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل في صفحة ترحيب؟ وهل لها مراحل تتبّع؟») <b>مراحلُ البدء الأولى، تُقرأ من بيانات المنشأة لا من علامةٍ تُحفظ.</b>
///
/// <para><b>ما كان:</b> معالجُ الإعداد يقول «مرحباً بك» مرّةً ثم لا شيء: المنشأةُ الجديدةُ تدخل لوحةً بأصفارٍ دون أن يدلّها أحدٌ على
/// ما يلزم قبل أوّل فاتورة (بياناتها النظاميّة، شعارها، عميلها الأوّل، أصنافها).</para>
///
/// <para><b>الآن:</b> قائمةٌ تتتبّع الإنجاز من الدفاتر نفسِها — الخطوةُ منجزةٌ إن وُجد أثرُها فعلاً (عميلٌ مسجَّل، فاتورةٌ مرحَّلة…) فلا تُنسى
/// علامةٌ ولا تَكذب. وتختفي اللوحةُ حين تكتمل.</para>
/// </summary>
public class OnboardingQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<OnboardingStep>> ExecuteAsync(long companyId, CancellationToken ct = default)
    {
        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId, ct);

        var hasCustomer = await db.Customers.AnyAsync(c => c.CompanyId == companyId, ct);
        var hasProduct = await db.Products.AnyAsync(p => p.CompanyId == companyId, ct);
        var hasPostedInvoice = await db.SalesInvoices.AnyAsync(
            i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted, ct);

        return
        [
            new("company-data", "أكمل بيانات المنشأة النظامية (السجل التجاري والعنوان الوطني)",
                "Complete the company's regulatory details (commercial registration and national address)",
                "company_settings", company.IsReadyForEInvoicing),
            new("logo", "ارفع شعار منشأتك ليظهر على فواتيرك وكشوفك",
                "Upload your company logo so it appears on your invoices and statements",
                "company_settings", !string.IsNullOrWhiteSpace(company.LogoFilePath)),
            new("first-customer", "أضف أوّل عميل", "Add your first customer", "customers", hasCustomer),
            new("first-product", "أضف أوّل صنف أو خدمة", "Add your first item or service", "products", hasProduct),
            new("first-invoice", "أصدر أوّل فاتورة مبيعات", "Issue your first sales invoice", "sales_invoices", hasPostedInvoice),
        ];
    }
}
