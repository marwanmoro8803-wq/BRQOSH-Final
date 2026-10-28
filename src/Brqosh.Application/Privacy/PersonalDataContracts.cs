using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Brqosh.Application.Privacy;

/// <summary>‏(PDPL) صاحبُ البيانات: عميلٌ أو موظف.</summary>
public enum PersonalDataSubjectType
{
    Customer = 0,
    Employee = 1,
}

public record PersonalDataFieldDto(string Name, string? Value);

/// <summary>‏مستندٌ يحمل اسمَ الشخص ويُحتفظ به نظاماً (فاتورةٌ مرحَّلة) — يُذكر في التصدير ولا يُعدَّل عند الإخفاء.</summary>
public record PersonalDataDocumentDto(string Kind, string? Number, DateOnly Date);

public record PersonalDataExportDto(
    PersonalDataSubjectType SubjectType,
    long SubjectId,
    DateTime ExportedAtUtc,
    IReadOnlyList<PersonalDataFieldDto> Fields,
    IReadOnlyList<PersonalDataDocumentDto> RetainedDocuments)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>‏نسخةٌ مقروءةٌ آليّاً تُسلَّم لصاحب البيانات (حقُّ الوصول ونقل البيانات).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

/// <summary>
/// <param name="RetainedDocuments">مستنداتٌ مرحَّلةٌ بقيت كما هي (التزامُ الاحتفاظ بالسجلّات المحاسبيّة والفوترة الإلكترونيّة).</param>
/// </summary>
public record AnonymizationResultDto(PersonalDataSubjectType SubjectType, long SubjectId, string Placeholder, int RetainedDocuments);

/// <summary>
/// ‏(PDPL) <b>تصديرُ بيانات الشخص وإخفاؤها</b> — للمسؤول وحدَه، ويُسجَّل في سجلّ التدقيق.
/// والإخفاءُ يمسّ بطاقةَ العميل/الموظف وحدَها: القيودُ والفواتيرُ المرحَّلةُ لا تُعدَّل.
/// </summary>
public interface IPersonalDataService
{
    Task<PersonalDataExportDto> ExportAsync(
        long companyId, PersonalDataSubjectType subjectType, long subjectId, string username, CancellationToken ct = default);

    Task<AnonymizationResultDto> AnonymizeAsync(
        long companyId, PersonalDataSubjectType subjectType, long subjectId, string username, CancellationToken ct = default);
}
