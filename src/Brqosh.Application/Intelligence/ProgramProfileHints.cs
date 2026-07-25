namespace Brqosh.Application.Intelligence;

/// <summary>نصائح إضافية حسب ملف العمل (Construction · Retail · Manufacturing).</summary>
public static class ProgramProfileHints
{
    public static (string? Ar, string? En) TryGet(string? profileName, string? navKey)
    {
        if (string.IsNullOrWhiteSpace(profileName) || string.IsNullOrWhiteSpace(navKey))
            return (null, null);

        var profile = profileName.Trim();
        var key = navKey.Trim();

        if (profile.Contains("مقاول", StringComparison.OrdinalIgnoreCase) ||
            profile.Contains("Construction", StringComparison.OrdinalIgnoreCase))
        {
            if (key is "projects" or "boq" or "contracts" or "progress_billings")
                return ("ملف مقاولات: ابدأ بمشروع ثم BOQ قبل المستخلصات.",
                    "Construction profile: start with project + BOQ before billings.");
        }

        if (profile.Contains("تجزئة", StringComparison.OrdinalIgnoreCase) ||
            profile.Contains("Retail", StringComparison.OrdinalIgnoreCase))
        {
            if (key is "pos_session" or "pos_shifts" or "sales_invoices")
                return ("ملف تجزئة: وردية POS مفتوحة قبل البيع.",
                    "Retail profile: open POS shift before selling.");
        }

        if (profile.Contains("تصنيع", StringComparison.OrdinalIgnoreCase) ||
            profile.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase))
        {
            if (key is "bom" or "work_orders" or "wip")
                return ("ملف تصنيع: عرّف BOM ثم أمر إنتاج.",
                    "Manufacturing profile: define BOM then work order.");
        }

        return (null, null);
    }
}
