using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("إزالة BRQOSH")]
[assembly: AssemblyProduct("BRQOSH ERP")]
[assembly: AssemblyCompany("BRQOSH")]
[assembly: AssemblyDescription("برنامج إزالة BRQOSH — يفتح معالج الإزالة الكامل ويسألك عن بياناتك")]

// برنامجُ إزالةٍ مستقلٌّ بأيقونة الشعار وسلّة المهملات. يجد تنصيبَ BRQOSH ERP في سجلّ ويندوز ويفتح معالجَ الإزالة الكامل
// (msiexec /i {ProductCode} REMOVE=ALL) — وهو الذي يسأل: أأحذف بياناتك أيضاً؟ ولا يحذف شيئاً بنفسه.
internal static class BrqoshUninstall
{
    private const string ProductName = "BRQOSH ERP";

    [STAThread]
    private static int Main()
    {
        var code = FindProductCode();
        if (code == null)
        {
            Show("BRQOSH غير منصَّب على هذا الجهاز — لا شيء يُزال.\r\nBRQOSH is not installed on this computer — nothing to remove.", MessageBoxIcon.Information);
            return 0;
        }

        try
        {
            Process.Start(new ProcessStartInfo("msiexec.exe", "/i " + code + " REMOVE=ALL") { UseShellExecute = true });
            return 0;
        }
        catch (Exception ex)
        {
            Show("تعذّر فتح معالج الإزالة: " + ex.Message + "\r\nCould not open the removal wizard.", MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string FindProductCode()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var root = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (root == null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    if (name.Length != 38 || name[0] != '{') continue;
                    using (var key = root.OpenSubKey(name))
                    {
                        if (key != null && string.Equals(key.GetValue("DisplayName") as string, ProductName, StringComparison.Ordinal))
                            return name;
                    }
                }
            }
        }
        return null;
    }

    private static void Show(string text, MessageBoxIcon icon)
    {
        MessageBox.Show(text, "BRQOSH — إزالة / Remove", MessageBoxButtons.OK, icon, MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }
}
