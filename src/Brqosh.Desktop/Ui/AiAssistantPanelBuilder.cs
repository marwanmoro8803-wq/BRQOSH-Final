using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brqosh.Application.Intelligence;

namespace Brqosh.Desktop.Ui;

/// <summary>بناء واجهة المساعد الذكي — فقاعات، بطاقات رؤى، شريط إدخال.</summary>
public static class AiAssistantPanelBuilder
{
    public static void ClearThread(StackPanel chatHost) => chatHost.Children.Clear();

    public static void AppendUserBubble(FrameworkElement host, StackPanel chatHost, string text)
    {
        chatHost.Children.Add(new Border
        {
            Style = UiResources.StyleOf(host, "AiUserBubble"),
            Margin = new Thickness(24, 0, 0, 8),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
            },
        });
        ScrollChatToEnd(chatHost);
    }

    public static void AppendAssistantBubble(FrameworkElement host, StackPanel chatHost, FinancialAssistantReply reply)
    {
        var answer = LocaleManager.T(reply.AnswerAr, reply.AnswerEn);
        chatHost.Children.Add(new Border
        {
            Style = UiResources.StyleOf(host, "AiInsightCard"),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock
            {
                Text = answer,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
            },
        });
        ScrollChatToEnd(chatHost);
    }

    /// <summary>تمرير تلقائي لآخر رسالة في لوحة المحادثة.</summary>
    public static void ScrollChatToEnd(StackPanel chatHost)
    {
        var scroll = FindAncestor<ScrollViewer>(chatHost);
        if (scroll is null)
            return;

        chatHost.Dispatcher.BeginInvoke(
            () => scroll.ScrollToEnd(),
            DispatcherPriority.Loaded);
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    public static void RenderInsightCards(
        FrameworkElement host, StackPanel insightHost, IReadOnlyList<FinancialInsight> insights)
    {
        insightHost.Children.Clear();
        foreach (var insight in insights.Take(5))
            insightHost.Children.Add(BuildInsightCard(host, insight));
    }

    public static void RenderStarterTips(FrameworkElement host, StackPanel insightHost)
    {
        insightHost.Children.Clear();
        var tips = new[]
        {
            LocaleManager.T("تحقق من ذمم العملاء المتأخرة هذا الأسبوع.", "Review overdue receivables this week."),
            LocaleManager.T("راجع إقرار VAT قبل نهاية الفترة.", "Review VAT return before period close."),
            LocaleManager.T("قارن التدفق النقدي بالميزانية.", "Compare cash flow against budget."),
        };
        foreach (var tip in tips)
        {
            insightHost.Children.Add(new Border
            {
                Style = UiResources.StyleOf(host, "AiInsightCard"),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new TextBlock
                {
                    Text = tip,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12.5,
                    Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
                },
            });
        }
    }

    public static Border BuildInsightCard(FrameworkElement host, FinancialInsight insight)
    {
        // (O-26) البطاقة كانت تعرض `TitleAr`/`DetailAr` دائماً — عربيّ داخل واجهة
        // إنجليزية على كل صفحة من الستّين.
        var title = LocaleManager.T(insight.TitleAr, insight.TitleEn);
        var detail = LocaleManager.T(insight.DetailAr, insight.DetailEn);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(detail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 12,
                Foreground = UiResources.BrushOf(host, "TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        var card = new Border
        {
            Style = UiResources.StyleOf(host, "AiInsightCard"),
            Margin = new Thickness(0, 0, 0, 8),
            Child = stack,
        };

        if (insight.Severity is "Warning" or "Error")
            card.BorderBrush = UiResources.BrushOf(host, "WarningBrush");

        return card;
    }

    public static void WireInput(
        FrameworkElement host,
        TextBox input,
        Button sendButton,
        Func<string, Task> onSendAsync)
    {
        // ‏(م-٥ · ن-٢٤) ‏**`async Task` هنا، و`async void` عند الحدّ المعروف.**
        //
        // ‏كانت دالّةً محلّيّةً `async void Submit()` تُستدعى من لامدا متزامنة —
        // ‏**الشاذُّ الوحيد بين ٣٨٨ موضعاً** تتبع اصطلاحَ WPF بحقّ. وقد قيل إنّ استثناءها
        // ‏«يُنهي المهمّةَ بلا مراقب»، **والمقيسُ أنّ ذلك غيرُ صحيح**: `onSendAsync` هنا هو
        // ‏`MainWindow.OnAiQuestionAsync` وهو يلتقط كلَّ استثناءٍ ويعرضه فقاعةً في اللوحة
        // ‏(‏`MainWindow.xaml.cs:515`)؛ وفوق ذلك يرصد `App.xaml.cs:62`
        // ‏`DispatcherUnhandledException` فيكتب السجلَّ ويعرض ويضع `Handled = true`.
        //
        // ‏**فالتغييرُ محاذاةُ اصطلاحٍ لا إصلاحُ عطل** — ويُقال كذلك: الرميةُ تُسلَّم إلى
        // ‏الموضع نفسِه قبلَه وبعدَه. وقيمتُه أنّ الملفَّ يكفّ عن أن يكون **الاستثناءَ
        // ‏الوحيد** من اصطلاحٍ يتبعه المستودعُ كلُّه، فلا يقرأ أحدٌ الشذوذَ قاعدةً.
        async Task SubmitAsync()
        {
            var text = input.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            input.IsEnabled = false;
            sendButton.IsEnabled = false;
            try
            {
                await onSendAsync(text);
                input.Clear();
            }
            finally
            {
                input.IsEnabled = true;
                sendButton.IsEnabled = true;
                input.Focus();
            }
        }

        sendButton.Click += async (_, _) => await SubmitAsync();
        input.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SubmitAsync();
            }
        };
    }

    /// <summary>
    /// (1.0.32) الخياراتُ الأربعةُ تتبع الشاشةَ المفتوحة (<see cref="PageAssistantPrompts"/>) — كانت أربعةً ثابتةً على كلّ صفحة.
    /// الزرُّ يعرض <c>Label</c> بلغة الواجهة، والنقرُ يسأل <c>Question</c> فيظهر في المحادثة كسؤالٍ كاملٍ يجيب عنه المساعدُ ببيانات المنشأة.
    /// </summary>
    public static void RenderQuickPrompts(
        FrameworkElement host,
        WrapPanel promptHost,
        Func<string, Task> onSendAsync,
        IReadOnlyList<AssistantPrompt> prompts)
    {
        promptHost.Children.Clear();
        foreach (var prompt in prompts)
        {
            var question = LocaleManager.T(prompt.QuestionAr, prompt.QuestionEn);
            var btn = new Button
            {
                Content = LocaleManager.T(prompt.LabelAr, prompt.LabelEn),
                ToolTip = question,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Cursor = Cursors.Hand,
            };

            // Audit fix: guard against double-clicks firing the same question twice.
            btn.Click += async (_, _) =>
            {
                if (!btn.IsEnabled) return;
                btn.IsEnabled = false;
                try { await onSendAsync(question); }
                finally { btn.IsEnabled = true; }
            };
            promptHost.Children.Add(btn);
        }
    }
}
