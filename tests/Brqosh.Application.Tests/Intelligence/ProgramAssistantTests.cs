using Brqosh.Application.Intelligence;
using Brqosh.Infrastructure.Intelligence;

namespace Brqosh.Application.Tests.Intelligence;

public class ProgramAssistantTests
{
    [Fact]
    public async Task Ask_current_page_uses_nav_context()
    {
        var financial = new StubFinancialAssistant();
        var assistant = new ProgramAssistantService(financial);
        var ctx = new ProgramAssistantContext(1, "sales_invoices", "محاسب", "Retail");

        var reply = await assistant.AskAsync(ctx, "ماذا أفعل في هذه الشاشة؟");

        Assert.Equal(ProgramAssistantService.EngineId, reply.Source);
        Assert.Contains("فاتورة", reply.AnswerAr);
        Assert.Contains("ترحيل", reply.AnswerAr);
    }

    [Fact]
    public async Task Ask_zatca_mentions_external_otp()
    {
        var assistant = new ProgramAssistantService(new StubFinancialAssistant());
        var ctx = new ProgramAssistantContext(1, "zatca_settings", "مدير", "Retail");

        var reply = await assistant.AskAsync(ctx, "ZATCA OTP");

        Assert.Contains("OTP", reply.AnswerAr);
        Assert.DoesNotContain("O-01", reply.AnswerAr);
    }

    [Fact]
    public async Task Ask_financial_delegates_to_financial_engine()
    {
        var financial = new StubFinancialAssistant();
        var assistant = new ProgramAssistantService(financial);
        var ctx = new ProgramAssistantContext(1, "dashboard", "محاسب", "Retail");

        var reply = await assistant.AskAsync(ctx, "ما التدفق النقدي؟");

        Assert.True(financial.Called);
        Assert.Contains("تحصيلات", reply.AnswerAr);
    }

    /// <summary>
    /// (أسئلةُ المالك 2026-10-24) «ماذا أفعل في هذه الشاشة؟» على <b>كلِّ</b> شاشةٍ في الفهرس: الردُّ يحمل عنوانَها ودليلَها وسياقَ المستخدم،
    /// ولا يذكر مفتاحَها البرمجيّ — لا على «بعض» الشاشات التي كُتب لها دليلٌ يدويّ.
    /// </summary>
    [Fact]
    public async Task EveryScreenAnswers_WhatToDoHere_WithItsOwnGuide()
    {
        var assistant = new ProgramAssistantService(new StubFinancialAssistant());

        foreach (var key in ProgramNavCatalog.Keys)
        {
            var guide = ProgramKnowledge.TryGetPageGuide(key)!;
            var reply = await assistant.AskAsync(new ProgramAssistantContext(1, key, "محاسب", "Retail"), "ماذا أفعل في هذه الشاشة؟");

            Assert.Contains(guide.TitleAr, reply.AnswerAr);
            Assert.Contains(guide.GuideAr, reply.AnswerAr);
            Assert.Contains(guide.GuideEn, reply.AnswerEn);
            Assert.DoesNotContain(key, reply.AnswerAr, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("projects", "مشروع")]
    [InlineData("pos_session", "وردية")]
    [InlineData("chart_of_accounts", "حساب")]
    public async Task Ask_current_page_covers_construction_and_retail_keys(string navKey, string expectedAr)
    {
        var assistant = new ProgramAssistantService(new StubFinancialAssistant());
        var ctx = new ProgramAssistantContext(1, navKey, "مدير", "Construction");

        var reply = await assistant.AskAsync(ctx, "ماذا أفعل في هذه الشاشة؟");

        Assert.Contains(expectedAr, reply.AnswerAr);
    }

    [Fact]
    public void ProgramNavCatalog_covers_all_navigation_keys()
    {
        Assert.True(ProgramNavCatalog.Count >= 60);
        var contracts = ProgramKnowledge.TryGetPageGuide("contracts");
        Assert.NotNull(contracts);
        Assert.Contains("مستخلص", contracts.GuideAr);
        Assert.NotNull(ProgramKnowledge.TryGetPageGuide("wip"));
    }

    [Fact]
    public void ProgramKnowledge_every_nav_key_has_actionable_guide()
    {
        foreach (var key in ProgramNavCatalog.Keys)
        {
            var guide = ProgramKnowledge.TryGetPageGuide(key);
            Assert.NotNull(guide);
            Assert.True(guide!.GuideAr.Length > 15, $"Guide too short for {key}");
        }
    }

    [Theory]
    [InlineData("كيف أعمل في العقود؟", "contracts")]
    [InlineData("جدول الكميات للمشروع", "boq")]
    [InlineData("مستخلص إنجاز", "progress_billings")]
    [InlineData("سجل تدقيق", "audit_log")]
    public void ProgramQuestionMatcher_matches_expanded_synonyms(string question, string expected)
    {
        var key = ProgramQuestionMatcher.GuessNavKey(question);
        Assert.Equal(expected, key);
    }

    [Fact]
    public void ProgramProfileHints_construction_boq()
    {
        var hint = ProgramProfileHints.TryGet("Construction", "boq");
        Assert.NotNull(hint.Ar);
        Assert.Contains("BOQ", hint.En);
    }

    [Fact]
    public void ComposeExpert_skips_duplicate_inferred_block()
    {
        var ctx = new ProgramAssistantContext(1, "dashboard", "محاسب", "Retail");
        var guide = ProgramKnowledge.TryGetPageGuide("sales_invoices")!;
        var core = new FinancialAssistantReply(guide.GuideAr, guide.GuideEn, "test");
        var workflows = Array.Empty<ProgramWorkflowKnowledge.WorkflowHint>();

        var reply = ProgramReplyComposer.ComposeExpert(ctx, core, "sales_invoices", workflows, "test");

        Assert.DoesNotContain("📌 متعلق بسؤالك", reply.AnswerAr);
    }

    [Fact]
    public void ProgramKnowledge_covers_core_nav_keys()
    {
        foreach (var key in new[]
                 {
                     "dashboard", "sales_invoices", "projects", "pos_session", "purchase_invoices",
                     "customer_support", "zatca_settings",
                 })
        {
            Assert.NotNull(ProgramKnowledge.TryGetPageGuide(key));
        }
    }

    [Fact]
    public async Task Ask_always_includes_screen_context_header()
    {
        var assistant = new ProgramAssistantService(new StubFinancialAssistant());
        var ctx = new ProgramAssistantContext(1, "dashboard", "محاسب", "Retail");

        var reply = await assistant.AskAsync(ctx, "مرحبا");

        Assert.Contains("📍", reply.AnswerAr);
        Assert.Contains("لوحة التحكم", reply.AnswerAr);
        Assert.Contains("محاسب", reply.AnswerAr);
    }

    [Fact]
    public async Task Ask_how_to_invoice_includes_sales_workflow()
    {
        var assistant = new ProgramAssistantService(new StubFinancialAssistant());
        var ctx = new ProgramAssistantContext(1, "dashboard", "محاسب", "Retail");

        var reply = await assistant.AskAsync(ctx, "كيف أنشئ فاتورة مبيعات؟");

        Assert.Contains("فاتورة", reply.AnswerAr);
        Assert.Contains("دورة المبيعات", reply.AnswerAr);
        Assert.Contains("ترحيل", reply.AnswerAr);
    }

    [Fact]
    public async Task Ask_posting_on_invoice_page_includes_reminder()
    {
        var assistant = new ProgramAssistantService(new StubFinancialAssistant());
        var ctx = new ProgramAssistantContext(1, "sales_invoices", "محاسب", "Retail");

        var reply = await assistant.AskAsync(ctx, "كيف أرحّل؟");

        Assert.Contains("مسودة", reply.AnswerAr);
        Assert.Contains("💡", reply.AnswerAr);
    }

    // Audit: the four quick-prompt buttons of the AI panel must route to their topic
    // from any screen (they were routed to the current page guide / financial engine).
    [Theory]
    [InlineData("كيف أرحّل؟", "اضغط ترحيل")]
    [InlineData("How to post?", "اضغط ترحيل")]
    [InlineData("صلاحياتي", "الصلاحيات الحالية")]
    [InlineData("My permissions", "الصلاحيات الحالية")]
    [InlineData("ZATCA / OTP", "OTP")]
    public async Task Quick_prompts_route_to_their_topic_from_dashboard(string prompt, string expectedAr)
    {
        var financial = new StubFinancialAssistant();
        var assistant = new ProgramAssistantService(financial);
        var ctx = new ProgramAssistantContext(1, "dashboard", "محاسب", "Retail");

        var reply = await assistant.AskAsync(ctx, prompt);

        Assert.Contains(expectedAr, reply.AnswerAr);
        Assert.False(financial.Called);
    }

    [Fact]
    public async Task English_vat_keyword_does_not_match_inside_other_words()
    {
        var financial = new StubFinancialAssistant();
        var assistant = new ProgramAssistantService(financial);
        var ctx = new ProgramAssistantContext(1, "dashboard", "Viewer", "Retail");

        await assistant.AskAsync(ctx, "is my data private");

        Assert.False(financial.Called);
    }

    private sealed class StubFinancialAssistant : IFinancialAssistantService
    {
        public bool Called { get; private set; }

        public Task<FinancialAssistantReply> AskAsync(long companyId, string question, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(new FinancialAssistantReply(
                "تحصيلات 1000 ر.س",
                "Collections SAR 1000",
                FinancialIntelligenceService.EngineId));
        }
    }
}
