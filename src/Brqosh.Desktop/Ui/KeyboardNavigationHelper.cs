using System.Windows;
using System.Windows.Controls;

namespace Brqosh.Desktop.Ui;

/// <summary>ترتيب TabIndex موحّد لأشرطة أدوات الصفحات — CEEF-039 a11y.</summary>
public static class KeyboardNavigationHelper
{
    /// <summary>بحث → إجراءات ثانوية → إجراء رئيسي (RTL).</summary>
    public static void ApplyToolbarTabOrder(params Control[] controlsInVisualOrderRightToLeft)
    {
        for (var i = 0; i < controlsInVisualOrderRightToLeft.Length; i++)
        {
            var c = controlsInVisualOrderRightToLeft[i];
            if (c is not null)
                c.TabIndex = i + 1;
        }
    }

    public static void ApplyShellHeader(ComboBox branchCombo, Button themeToggle, Button? logoutButton = null)
    {
        branchCombo.TabIndex = 1;
        themeToggle.TabIndex = 2;
        if (logoutButton is not null)
            logoutButton.TabIndex = 3;
    }
}
