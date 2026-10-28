using Brqosh.Application.Privacy;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Privacy;

/// <summary>
/// ‏(PDPL) تصديرُ بيانات عميلٍ/موظفٍ وإخفاؤها. <b>والإخفاءُ لا يمسّ قيداً ولا فاتورةً مرحَّلة</b>: المستنداتُ تحمل نسختَها
/// من الاسم (والفاتورةُ الإلكترونيّةُ موقَّعةٌ لا تُغيَّر)، فيُمحى ما في البطاقة ويبقى الدفترُ سليماً، ويُعطَّل العميل.
/// </summary>
public sealed class PersonalDataService(BrqoshDbContext db) : IPersonalDataService
{
    public const string ExportAction = "تصدير بيانات شخصية";
    public const string AnonymizeAction = "إخفاء بيانات شخصية";

    public static string CustomerPlaceholder(long id) => $"عميل مُخفى #{id}";
    public static string EmployeePlaceholder(string code) => $"موظف مُخفى #{code}";

    public async Task<PersonalDataExportDto> ExportAsync(
        long companyId, PersonalDataSubjectType subjectType, long subjectId, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var export = subjectType == PersonalDataSubjectType.Customer
            ? Export(await CustomerAsync(companyId, subjectId, ct), await CustomerDocumentsAsync(companyId, subjectId, ct))
            : Export(await EmployeeAsync(companyId, subjectId, ct));
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await new AuditService(db).LogAsync(companyId, username, ExportAction, subjectType.ToString(), subjectId.ToString(), ct: token);
            return 0;
        }, ct);
        return export;
    }

    public async Task<AnonymizationResultDto> AnonymizeAsync(
        long companyId, PersonalDataSubjectType subjectType, long subjectId, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            string placeholder;
            var retained = 0;
            if (subjectType == PersonalDataSubjectType.Customer)
            {
                var customer = await CustomerAsync(companyId, subjectId, token);
                placeholder = CustomerPlaceholder(customer.Id);
                customer.Update(placeholder, "", "", "", username);
                customer.UpdateRegulatoryIdentity(null, NationalAddress.Empty);
                customer.Deactivate();
                retained = (await CustomerDocumentsAsync(companyId, subjectId, token)).Count;
            }
            else
            {
                var employee = await EmployeeAsync(companyId, subjectId, token);
                placeholder = EmployeePlaceholder(employee.Code);
                employee.ErasePersonalData(placeholder);
            }

            await db.SaveChangesAsync(token);
            await new AuditService(db).LogAsync(companyId, username, AnonymizeAction, subjectType.ToString(), subjectId.ToString(),
                $"{placeholder} · مستندات محفوظة: {retained}", ct: token);
            return new AnonymizationResultDto(subjectType, subjectId, placeholder, retained);
        }, ct);
    }

    private static PersonalDataExportDto Export(Customer c, IReadOnlyList<PersonalDataDocumentDto> documents) =>
        new(PersonalDataSubjectType.Customer, c.Id, DateTime.UtcNow,
        [
            new("الاسم", c.NameAr),
            new("الاسم المسجَّل", c.RegistrationName),
            new("الرقم الضريبي", c.VatRegistrationNumber),
            new("الهاتف", c.Phone),
            new("العنوان", c.Address),
            new("العنوان الوطني", c.NationalAddress == NationalAddress.Empty ? null : c.NationalAddress.ToString()),
            new("نشط", c.IsActive ? "نعم" : "لا"),
            new("تاريخ الإنشاء (UTC)", c.CreatedAtUtc.ToString("O")),
        ], documents);

    private static PersonalDataExportDto Export(Employee e) =>
        new(PersonalDataSubjectType.Employee, e.Id, DateTime.UtcNow,
        [
            new("الرمز", e.Code),
            new("الاسم", e.NameAr),
            new("رقم الهوية/الإقامة", e.NationalId),
            new("الجنسية", e.Nationality.ToString()),
            new("المسمّى الوظيفي", e.JobTitle),
            new("تاريخ الالتحاق", e.HireDate.ToString("yyyy-MM-dd")),
            new("تاريخ انتهاء الخدمة", e.TerminationDate?.ToString("yyyy-MM-dd")),
            new("الآيبان", e.Iban),
            new("الراتب الأساسي", e.BasicSalary.ToString("0.00")),
            new("بدل السكن", e.HousingAllowance.ToString("0.00")),
            new("بدل النقل", e.TransportAllowance.ToString("0.00")),
            new("بدلات أخرى", e.OtherAllowance.ToString("0.00")),
        ], []);

    private async Task<Customer> CustomerAsync(long companyId, long id, CancellationToken ct) =>
        await db.Customers.SingleOrDefaultAsync(c => c.CompanyId == companyId && c.Id == id, ct)
        ?? throw new AccountingException("العميل غير موجود.");

    private async Task<Employee> EmployeeAsync(long companyId, long id, CancellationToken ct) =>
        await db.Employees.SingleOrDefaultAsync(e => e.CompanyId == companyId && e.Id == id, ct)
        ?? throw new AccountingException("الموظف غير موجود.");

    private async Task<IReadOnlyList<PersonalDataDocumentDto>> CustomerDocumentsAsync(long companyId, long customerId, CancellationToken ct) =>
        (await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.CustomerId == customerId && i.InvoiceNumber != null)
            .OrderBy(i => i.InvoiceDate)
            .Select(i => new { i.InvoiceNumber, i.InvoiceDate })
            .ToListAsync(ct))
        .Select(i => new PersonalDataDocumentDto("SalesInvoice", i.InvoiceNumber, i.InvoiceDate))
        .ToList();
}
