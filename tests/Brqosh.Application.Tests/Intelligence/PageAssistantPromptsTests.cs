using System.Reflection;
using Brqosh.Application.Intelligence;
using Brqosh.Infrastructure.Intelligence;

namespace Brqosh.Application.Tests.Intelligence;

/// <summary>
/// (1.0.32) الخياراتُ الأربعةُ في لوحة المساعد تتبع الشاشة: أربعةٌ دائماً، بلغتين، ومن مجال الشاشة — وأسئلتُها تصيب الموضوعَ الذي
/// يجيب عنه المساعدُ فعلاً (لا تقع في «عام» فتعود بجوابٍ لا صلةَ له).
/// </summary>
public class PageAssistantPromptsTests
{
    private sealed class RecordingFinancial : IFinancialAssistantService
    {
        public List<string> Questions { get; } = [];

        public Task<FinancialAssistantReply> AskAsync(long companyId, string question, CancellationToken ct = default)
        {
            Questions.Add(question);
            return Task.FromResult(new FinancialAssistantReply("مالي", "financial", "test"));
        }
    }

    private static string FinancialTopic(string question)
    {
        var method = typeof(FinancialAssistantService).GetMethod("ClassifyTopic", BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException("ClassifyTopic غائبة.");
        return method.Invoke(null, [question])!.ToString()!;
    }

    [Fact]
    public void EveryGroupGetsExactlyFourBilingualPrompts_FirstTwoAboutTheScreen()
    {
        foreach (var group in PageAssistantPrompts.KnownGroups)
        {
            var prompts = PageAssistantPrompts.For("any_page", group, "شاشة تجريبيّة", "Test screen");
            Assert.Equal(PageAssistantPrompts.Count, prompts.Count);
            Assert.All(prompts, p =>
            {
                Assert.False(string.IsNullOrWhiteSpace(p.LabelAr));
                Assert.False(string.IsNullOrWhiteSpace(p.LabelEn));
                Assert.False(string.IsNullOrWhiteSpace(p.QuestionAr));
                Assert.False(string.IsNullOrWhiteSpace(p.QuestionEn));
            });
            Assert.Contains("هذه الشاشة", prompts[0].QuestionAr);
            Assert.Contains("شاشة تجريبيّة", prompts[1].QuestionAr);
            Assert.Contains("Test screen", prompts[1].QuestionEn);
            Assert.Equal(prompts.Count, prompts.Select(p => p.LabelAr).Distinct().Count());
        }
    }

    [Fact]
    public void ThePromptsChangeWithTheScreenDomain_AndAnUnknownGroupStillGetsFour()
    {
        string Key(string group) => string.Join("|", PageAssistantPrompts.For("k", group, "ش", "S").Select(p => p.QuestionAr));
        Assert.NotEqual(Key("المبيعات والعملاء"), Key("الرواتب"));
        Assert.NotEqual(Key("المخزون والمستودعات"), Key("الخزينة والبنوك"));
        Assert.NotEqual(Key("الفوترة الإلكترونية"), Key("التقارير المالية"));
        Assert.Equal(4, PageAssistantPrompts.For(null, null, null, null).Count);
        Assert.Equal(4, PageAssistantPrompts.For("x", "مجموعة لا وجود لها", "", "").Count);
    }

    [Fact]
    public async Task EveryDataPrompt_ReachesTheFinancialEngineOnItsOwnTopic_InBothLanguages()
    {
        var expected = new Dictionary<string, string>
        {
            ["مبيعات الشهر"] = "Sales", ["ذمم العملاء"] = "Receivables", ["ذمم الموردين"] = "Payables", ["قيمة المخزون"] = "Inventory",
            ["التدفق النقدي"] = "CashFlow", ["صافي الضريبة"] = "Vat", ["التنبيهات"] = "Anomaly", ["ملخّص الوضع"] = "General",
        };
        var seen = new HashSet<string>();
        foreach (var group in PageAssistantPrompts.KnownGroups)
        {
            foreach (var prompt in PageAssistantPrompts.For("k", group, "ش", "S"))
            {
                if (!expected.TryGetValue(prompt.LabelAr, out var topic))
                    continue;
                seen.Add(prompt.LabelAr);
                Assert.Equal(topic, FinancialTopic(prompt.QuestionAr));
                Assert.Equal(topic, FinancialTopic(prompt.QuestionEn));

                var fake = new RecordingFinancial();
                var service = new ProgramAssistantService(fake);
                foreach (var question in new[] { prompt.QuestionAr, prompt.QuestionEn })
                {
                    var reply = await service.AskAsync(new ProgramAssistantContext(1, "dashboard", null, null), question);
                    Assert.True(fake.Questions.Contains(question), "لم يصل إلى المحرّك المالي: " + question);
                    Assert.False(string.IsNullOrWhiteSpace(reply.AnswerAr));
                }
            }
        }

        Assert.Equal(expected.Keys.OrderBy(k => k), seen.OrderBy(k => k));
    }

    [Fact]
    public async Task TheGuidePrompts_AnswerFromTheProgramKnowledge_NotFromTheFinancialEngine()
    {
        var fake = new RecordingFinancial();
        var service = new ProgramAssistantService(fake);
        foreach (var group in PageAssistantPrompts.KnownGroups)
        {
            var prompts = PageAssistantPrompts.For("sales_invoices", group, "فواتير المبيعات", "Sales Invoices");
            foreach (var prompt in prompts.Where(p => p.LabelAr is "اشرح هذه الشاشة" or "كيف أرحّل؟" or "الفوترة الإلكترونية" or "صلاحياتي"))
            {
                var before = fake.Questions.Count;
                var reply = await service.AskAsync(new ProgramAssistantContext(1, "sales_invoices", "مدير", "عام"), prompt.QuestionAr);
                Assert.Equal(before, fake.Questions.Count);
                Assert.False(string.IsNullOrWhiteSpace(reply.AnswerAr));
            }
        }
    }
}
