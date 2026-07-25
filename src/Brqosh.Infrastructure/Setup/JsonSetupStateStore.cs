using System.Text.Json;
using Brqosh.Application.Setup;

namespace Brqosh.Infrastructure.Setup;

/// <summary>
/// تخزين حالة الإعداد في ملف JSON ضمن مجلد بيانات التطبيق.
/// مرحلي — يُستبدل بجدول إعدادات في PostgreSQL عند اكتمال طبقة قاعدة البيانات.
/// </summary>
public class JsonSetupStateStore : ISetupStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _filePath;

    public JsonSetupStateStore()
    {
        var directory = Path.Combine(
            Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path());
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "setup.json");
    }

    public SetupState? Load()
    {
        if (!File.Exists(_filePath))
            return null;
        try
        {
            return JsonSerializer.Deserialize<SetupState>(File.ReadAllText(_filePath));
        }
        catch (JsonException)
        {
            // ملف تالف — نعامل الحالة كأول تشغيل بدل إسقاط التطبيق
            return null;
        }
    }

    public void Save(SetupState state)
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(state, Options));
    }
}
