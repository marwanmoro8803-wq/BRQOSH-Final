namespace Brqosh.Infrastructure.Reporting;

/// <summary>تخزين شعار المنشأة محلياً تحت AppData — مسار يُحفظ في Company.LogoFilePath.</summary>
public static class CompanyBrandingStorage
{
  public static readonly IReadOnlyList<string> AllowedPrintFonts =
  [
    "Tahoma",
    "Arial",
    "Segoe UI",
    "Traditional Arabic",
  ];

  private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
  {
    ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp",
  };

  public static string BrandingRoot => Path.Combine(
    Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(),
    "branding");

  public static string SaveLogoFromFile(long companyId, string sourceFilePath)
  {
    if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
      throw new FileNotFoundException("ملف الشعار غير موجود.", sourceFilePath);

    var ext = Path.GetExtension(sourceFilePath);
    if (!AllowedImageExtensions.Contains(ext))
      throw new InvalidOperationException("صيغة الشعار غير مدعومة — استخدم PNG أو JPEG.");

    var dir = Path.Combine(BrandingRoot, companyId.ToString());
    Directory.CreateDirectory(dir);

    foreach (var existing in Directory.EnumerateFiles(dir, "logo.*"))
      File.Delete(existing);

    var dest = Path.Combine(dir, $"logo{ext.ToLowerInvariant()}");
    File.Copy(sourceFilePath, dest, overwrite: true);
    return dest;
  }

  /// <summary>
  /// (CEEF-227) حفظ شعار مُعالَج (خلفية مفرّغة) — دائماً PNG لأن الشفافية لا تُحفَظ
  /// في JPEG. تُحذف أي نسخة سابقة بأيّ امتداد فلا يبقى ملفّان يتنازعان.
  /// </summary>
  public static string SaveLogoPng(long companyId, byte[] pngBytes)
  {
    ArgumentNullException.ThrowIfNull(pngBytes);
    if (pngBytes.Length == 0)
      throw new InvalidOperationException("بايتات الشعار فارغة.");

    var dir = Path.Combine(BrandingRoot, companyId.ToString());
    Directory.CreateDirectory(dir);

    foreach (var existing in Directory.EnumerateFiles(dir, "logo.*"))
      File.Delete(existing);

    var dest = Path.Combine(dir, "logo.png");
    File.WriteAllBytes(dest, pngBytes);
    return dest;
  }

  public static void RemoveLogo(string? storedPath)
  {
    if (string.IsNullOrWhiteSpace(storedPath))
      return;

    if (File.Exists(storedPath))
      File.Delete(storedPath);
  }

  public static byte[]? TryReadLogo(string? storedPath)
  {
    if (string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath))
      return null;

    try
    {
      return File.ReadAllBytes(storedPath);
    }
    catch
    {
      return null;
    }
  }

  public static string NormalizePrintFont(string? font)
  {
    var f = (font ?? string.Empty).Trim();
    return AllowedPrintFonts.Contains(f) ? f : "Tahoma";
  }
}
