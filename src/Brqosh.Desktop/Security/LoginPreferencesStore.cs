using System.IO;
using System.Text.Json;

namespace Brqosh.Desktop.Security;

public sealed class LoginPreferences
{
    public bool RememberMe { get; set; }
    public string? Username { get; set; }
}

public static class LoginPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "login-prefs.json");

    public static LoginPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new LoginPreferences();
            return JsonSerializer.Deserialize<LoginPreferences>(File.ReadAllText(FilePath)) ?? new LoginPreferences();
        }
        catch (JsonException)
        {
            return new LoginPreferences();
        }
    }

    public static void Save(LoginPreferences prefs)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(prefs, Options));
    }
}
