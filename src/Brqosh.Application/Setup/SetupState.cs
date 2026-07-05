using Brqosh.Domain.Modules;

namespace Brqosh.Application.Setup;

/// <summary>حالة الإعداد الأولي — تُحفظ بعد إتمام معالج أول تشغيل.</summary>
public record SetupState(
    bool IsCompleted,
    BusinessProfile Profile,
    string CompanyName,
    string VatRegistrationNumber,
    DateTime CompletedAtUtc);

/// <summary>مخزن حالة الإعداد — التنفيذ الحالي ملف محلي، وسيصبح PostgreSQL في مرحلة قاعدة البيانات.</summary>
public interface ISetupStateStore
{
    SetupState? Load();
    void Save(SetupState state);
}
