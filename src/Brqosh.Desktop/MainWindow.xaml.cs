using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Navigation;
using Brqosh.Domain.Modules;

namespace Brqosh.Desktop;

public partial class MainWindow : Window
{
    private readonly SetupState _setup;

    public MainWindow(SetupState setup)
    {
        _setup = setup;
        InitializeComponent();
        CompanyNameText.Text = setup.CompanyName;
        ProfileBadge.Text = ProfileLabel(setup.Profile);
        BuildNavigation();
        ShowPlaceholder("dashboard", "لوحة التحكم");
    }

    private static string ProfileLabel(BusinessProfile profile) => profile switch
    {
        BusinessProfile.Construction => "مقاولات عامة",
        BusinessProfile.Manufacturing => "مصنع / إنتاج",
        BusinessProfile.Retail => "محل / تسويق",
        _ => "محاسبة عامة",
    };

    private void BuildNavigation()
    {
        foreach (var group in NavigationCatalog.ForProfile(_setup.Profile))
        {
            NavPanel.Children.Add(new TextBlock
            {
                Text = group.TitleAr,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                Margin = new Thickness(24, 18, 24, 6),
            });

            foreach (var page in group.Pages)
            {
                var button = new Button
                {
                    Style = (Style)FindResource("NavItem"),
                    Content = $"{page.Icon}  {page.TitleAr}",
                    Tag = page,
                };
                button.Click += (_, _) => Navigate(page.Key, page.TitleAr);
                NavPanel.Children.Add(button);
            }
        }
    }

    private void Navigate(string pageKey, string title)
    {
        PageTitle.Text = title;
        switch (pageKey)
        {
            case "chart_of_accounts":
                PageHost.Content = new Views.Pages.ChartOfAccountsPage();
                return;
            case "journal_entries":
                PageHost.Content = new Views.Pages.JournalEntriesPage();
                return;
        }
        ShowPlaceholder(pageKey, title);
    }

    private void ShowPlaceholder(string pageKey, string title)
    {
        PageTitle.Text = title;

        // مرحلي: صفحات فعلية تُبنى تباعاً في المراحل القادمة
        PageHost.Content = new Border
        {
            Background = (Brush)FindResource("SurfaceBrush"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(40),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = $"هذه الشاشة قيد البناء ({pageKey}) — ستتوفر في المرحلة القادمة.",
                        FontSize = 14,
                        Foreground = (Brush)FindResource("TextSecondaryBrush"),
                        Margin = new Thickness(0, 10, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                },
            },
        };
    }
}
