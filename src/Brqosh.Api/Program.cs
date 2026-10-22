using Brqosh.Application.Intelligence;
using Brqosh.Application.Reporting;
using Brqosh.Application.Platform;
using Brqosh.Application.Treasury;
using Brqosh.Infrastructure.Webhooks;
using Brqosh.Api.Idempotency;
using Brqosh.Api.Middleware;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Application.Setup;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Observability;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.WithProperty("EnvironmentName", ctx.HostingEnvironment.EnvironmentName)
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/brqosh-api-.log",
        rollingInterval: RollingInterval.Day));

// ‏(م-٥ · ن-٢٢) تُلتقَط هنا **ويُحكَم عليها بعد قرار الإنتاج** أدناه — انظر الحارسَ هناك.
var configuredConnection = builder.Configuration.GetConnectionString("Brqosh");

var conn = configuredConnection
           ?? $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}";
var usePg = conn.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || string.Equals(builder.Configuration["Database:Provider"], "postgresql", StringComparison.OrdinalIgnoreCase);

builder.Services.AddBrqoshInfrastructure(conn, usePg);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "BRQOSH API",
        Version = "v1",
        Description = "BRQOSH ERP API — accounting, reporting, platform hooks"
    });
});

var app = builder.Build();

// ‏(م-٣ · ن-٠٩) ‏**وصلُ نشرِ أحداث النطاق — صريحاً لا أثراً جانبيّاً في مصنعٍ كسول.**
//
// ‏كان `DomainEventHub.Current` و`OutboxDispatchHook.Wire` يُضبطان داخل مصنعِ
// ‏`IDomainEventPublisher`، و`AddSingleton<T>(factory)` لا يعمل حتى يُطلب النوع.
// ‏**ولم يطلبه أحدٌ في الإنتاج** — فبقي الناشرُ ذاكريّاً، **والصندوقُ الصادر
// ‏والـwebhooks لا يعملان**، ولا شيءَ يشتكي.
app.Services.WireDomainEventPublishing();

// (م-٤ · ن-٣٨) ‏**قرارٌ واحدٌ لـ«هل أنا إنتاج؟» — وكان اثنين مختلفَي الثقة.**
//
// ‏كان هذا السطرُ يقرأ `ASPNETCORE_ENVIRONMENT` وحدَه — **متغيّرَ عمليّةٍ يضبطه أيُّ مُطلِق**.
// ‏بينما `ApiSecurityContextMiddleware` يسأل `AppEnvironmentConfig`، وهو مُحصَّن: يقرأ
// ‏مصدراً رسميّاً على مستوى الجهاز، **ويفشل إلى الإنتاج** عند الغياب، **ولا يُخفَّض
// ‏بإعلان عمليّة**.
//
// ‏**فالعمليّةُ الواحدةُ كانت تحمل جوابين**، وشرطُ المصادقة (`mustAuthenticate` أدناه)
// ‏يركب الأضعف: مضيفٌ أعلن المُنصِّبُ أنّه إنتاج، ويُطلَق بلا `ASPNETCORE_ENVIRONMENT`،
// ‏**يعمل بلا مفتاحٍ ولا مصادقة** بينما طبقةُ الصلاحيّات تعامله إنتاجاً. وذلك بعينه
// ‏الخفضُ الذي أُغلق في حملةٍ سابقة، عائداً من بابٍ آخر.
//
// ‏**والجمعُ بـ«أو» لا باختيار أحدهما:** أيُّ مصدرٍ يقول «إنتاج» يُصدَّق، فلا يُخفَّض
// ‏القرارُ أبداً — والخطأُ في اتجاه التشديد يُكتشف بمفتاحٍ ناقص، وفي اتجاه التساهل
// ‏لا يُكتشف إلّا بعد الاختراق.
var isProduction = app.Environment.IsProduction() || AppEnvironmentConfig.IsProduction;
var apiKey = builder.Configuration[ApiSecuritySettings.ApiKeyEnv]
             ?? Environment.GetEnvironmentVariable(ApiSecuritySettings.ApiKeyEnv);
// ‏(تقويةُ الـAPI · S-4) مفاتيحُ مربوطةٌ بمستأجر — **وإعدادٌ معيبٌ يُسقط الإقلاعَ هنا** لا عند أوّل طلب.
var tenantKeys = TenantApiKeys.Parse(builder.Configuration[TenantApiKeys.Env]
                                     ?? Environment.GetEnvironmentVariable(TenantApiKeys.Env))
    .EnsureDistinctFrom(apiKey);
var provisioningKey = builder.Configuration[ApiSecuritySettings.ProvisioningKeyEnv]
                      ?? Environment.GetEnvironmentVariable(ApiSecuritySettings.ProvisioningKeyEnv);
// (م-٤ · ن-٤٥) ‏**عزلُ المستأجرين كان يفشل مفتوحاً — والآن يفشل مغلقاً حيث يضرّ.**
//
// ‏كان `saasMode` يصير `true` بقيمةٍ صريحةٍ وحدَها، و`BRQOSH_SAAS_MODE` **لا يُضبط في أيّ
// ‏وثيقةٍ ولا سكربتٍ ولا مُنصِّبٍ ولا CI** (مقيسٌ ببحثٍ على المستودع أعاد موضعين: تعليقاً
// ‏وإعلانَ الثابت؛ وشاهدُ ضبطٍ موجب: `BRQOSH_API_KEY` يظهر في خمسة ملفّاتِ نشر). فالعزلُ
// ‏مُطفأٌ في **كلّ** تنصيبٍ قائم، و`TenantGuard` يُعيد `true` لأيّ شركةٍ متى غاب المُعرِّف.
//
// ‏وسردُ الشركات بلا ترويسةٍ كان يُعيد صفوفَ كلّ المستأجرين **ومعها `TenantId` نفسُه** —
// ‏وهو المفتاحُ الذي يفحصه الحارسُ بعدها. **فالتسريبُ يُكمل نفسَه.**
var saasModeSetting = builder.Configuration[ApiSecuritySettings.SaaSModeEnv]
                      ?? Environment.GetEnvironmentVariable(ApiSecuritySettings.SaaSModeEnv);
var saasMode = ApiSecuritySettings.ResolveTenantIsolation(isProduction, saasModeSetting);

ApiSecuritySettings.EnsureProductionApiKey(apiKey, isProduction);

// ‏(م-٥ · ن-٢٢) **ولا يُسقَط إلى دفاترِ المُشغِّل في الإنتاج.**
//
// ‏هذا السطرُ هو السببُ المُسمّى في CEEF-203: «بُرهن تجريبياً أنّ تشغيلةً واحدةً لاختبارات
// ‏API تضيف شركاتٍ إلى قاعدته المحلّيّة، **لأنّ Brqosh.Api يسقط إلى القاعدة الافتراضيّة
// ‏حين لا يجد ConnectionStrings:Brqosh**». **وأُصلح حينها المستدعون ولم يُصلَح السطر.**
// ‏و`appsettings.json` بلا قسم اتّصالاتٍ أصلاً، **فالسقوطُ هو الحالةُ الافتراضيّة لا الحافّة**.
//
// ‏**وموضعُه هنا لا عند البناء — وهو درسٌ دفعتُه:** وضعتُه أوّلاً قبل `AddBrqoshInfrastructure`
// ‏فسبق حارسَ المفتاح، **فصار مضيفٌ إنتاجيٌّ بلا مفتاحٍ يشتكي من قاعدةٍ لا من مفتاح** —
// ‏وأسقط `OneProductionDecisionTests`. **وترتيبُ الحرّاس رسالةٌ لا تفصيل**: أوّلُ ما يُقال
// ‏للمُشغّل يجب أن يكون أهمَّ ما نقص، والمفتاحُ أسبقُ لأنّه قرارُ الإنتاج الواحد.
//
// ‏**والتطويرُ يبقى كما هو**: السقوطُ نافعٌ هناك، والاختباراتُ تعتمده وتوجّه جذرَها إلى
// ‏مجلّدٍ مؤقّت (`TestDataRootInitializer`). فالرفضُ في الإنتاج وحدَه.
if (string.IsNullOrWhiteSpace(configuredConnection) && isProduction)
{
    throw new InvalidOperationException(
        "‏لا سلسلةَ اتّصالٍ مُعرَّفة (ConnectionStrings:Brqosh) والبيئةُ إنتاج. "
        + "‏ولا يُسقَط إلى القاعدة المحلّيّة الافتراضيّة في جذر بيانات المستخدم: "
        + "‏واجهةٌ تعمل على دفاترَ غيرِ المقصودة **تُقلع وتردّ وتُرجع صفراً من كلّ شيء**، "
        + "‏والصفرُ يُقرأ بياناتٍ لا عطلاً. عرِّف ConnectionStrings:Brqosh قبل التشغيل.");
}
ApiSecuritySettings.EnsureProductionProvisioningKey(provisioningKey, isProduction);
TenantContext.EnforceTenantIsolation = saasMode;
if (TenantApiKeys.HeaderOnlyWarning(saasMode, !tenantKeys.IsEmpty) is { } headerOnlyTenant)
    app.Logger.LogWarning("{Warning}", headerOnlyTenant);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BRQOSH API v1");
        c.RoutePrefix = "swagger";
    });
}

async Task<IResult?> RejectTenantCompanyAsync(BrqoshDbContext db, long companyId, CancellationToken ct)
{
    if (await TenantGuard.IsCompanyAllowedAsync(db, companyId, ct))
        return null;
    return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
}

// (م-٤ · ن-١٦) ‏**خمسةُ مساراتِ كتابةٍ كانت بلا حارسِ دورٍ إطلاقاً.**
//
// ‏عرفُ المستودع أن تفحص خدماتُ البنية الصلاحيّةَ بنفسها — ويُطبَّق في
// ‏`FxClosingTranslationService` فعلاً. **لكنّ أربعاً من خمسِ خدماتٍ على مسارات الكتابة
// ‏لا تذكر `SecurityContext` إطلاقاً**، واثنتان تكتبان عبر `DbContext` مباشرةً في هذا
// ‏الملفّ بلا خدمةٍ أصلاً. فيُكتب سعرُ صرفٍ، وتُحذف خريطةُ استبعادٍ بينيّة — **وكلتاهما
// ‏تُغيّر كلَّ تقريرٍ موحَّدٍ بعدها** — بمفتاحٍ دورُه `Viewer`.
//
// ‏**ويُرفض هنا بـ403 لا برميةٍ من الخدمة**: لا ربطَ لـ`AccountingException` برمزِ حالةٍ
// ‏في هذه الواجهة، فرميةُ حارسٍ تخرج **500** — وهي تقول «عطلٌ عندنا» لا «ممنوع».
// ‏والحارسُ في الخدمة يبقى **طبقةً ثانية** تُغطّي سطحَ المكتب ومن يأتي بعدُ.
IResult? RejectAccountingWrite()
{
    if (SecurityContext.CurrentRole is not UserRole.Administrator and not UserRole.Accountant)
        return Results.Json(
            new { error = "accounting_write_forbidden" },
            statusCode: StatusCodes.Status403Forbidden);

    return null;
}

app.UseSerilogRequestLogging();

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    finally
    {
        AppMetrics.IncRequest();
        if (ctx.Response.StatusCode >= 500)
            AppMetrics.IncError();
    }
});

// (CEEF-210) حدّ المعدل **قبل** فحص المفتاح: كان بعده، فكل محاولة مفتاح خاطئ تُردّ 401
// دون أن تمرّ على العدّاد إطلاقاً — تخمين المفاتيح بالقوة الغاشمة بلا أي حدّ، والعقوبة
// تقع على أصحاب المفاتيح الصحيحة وحدهم.
app.UseMiddleware<ApiRateLimitMiddleware>();

// مفتاح API — إلزامي في Production · اختياري في Development للتجريب المحلي
app.Use(async (ctx, next) =>
{
    var pathOpen = ctx.Request.Path.StartsWithSegments("/health");
    var mustAuthenticate = isProduction || !string.IsNullOrWhiteSpace(apiKey) || !tenantKeys.IsEmpty;
    if (!mustAuthenticate || pathOpen)
    {
        await next();
        return;
    }

    var hasKey = ctx.Request.Headers.TryGetValue("X-Api-Key", out var presented);
    // ‏(S-4) مفتاحٌ مربوطٌ بمستأجرٍ يُعرِّف المستأجرَ نفسَه — تحمله الطبقةُ التالية بدل الترويسة.
    var boundTenant = hasKey ? tenantKeys.TenantFor(presented.ToString()) : null;
    if (!hasKey || (boundTenant is null && !SecretComparer.Equals(apiKey, presented.ToString())))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
        return;
    }

    if (boundTenant is { } tenantOfKey)
        ctx.Items[TenantApiKeys.Env] = tenantOfKey;

    await next();
});

app.UseMiddleware<ApiSecurityContextMiddleware>();

app.Use(async (ctx, next) =>
{
    var hasTenantHeader = ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader);
    if (ctx.Items.TryGetValue(TenantApiKeys.Env, out var bound) && bound is Guid boundTenant)
    {
        // ‏(S-4) **المفتاحُ يحكم لا الترويسة**: ترويسةٌ تخالف مستأجرَ المفتاح محاولةُ دخولِ مستأجرٍ آخر — تُرفض.
        if (hasTenantHeader && !(Guid.TryParse(tenantHeader.ToString(), out var claimed) && claimed == boundTenant))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_mismatch" });
            return;
        }

        TenantContext.CurrentTenantId = boundTenant;
    }
    else if (hasTenantHeader && Guid.TryParse(tenantHeader.ToString(), out var tenantId))
    {
        TenantContext.CurrentTenantId = tenantId;
    }

    try
    {
        await next();
    }
    finally
    {
        TenantContext.CurrentTenantId = null;
    }
});

app.Use(async (ctx, next) =>
{
    if (!HttpMethods.IsPost(ctx.Request.Method) ||
        !ctx.Request.Path.StartsWithSegments("/api/v1") ||
        !ctx.Request.Headers.TryGetValue("Idempotency-Key", out var keyHeader))
    {
        await next();
        return;
    }

    var key = keyHeader.ToString().Trim();
    if (string.IsNullOrEmpty(key))
    {
        await next();
        return;
    }

    // (CEEF-210) مفتاح مركّب: المستأجر + المسار + الترويسة — لا تسريب عابر للمستأجرين.
    // ‏(S-6) **والمستدعي**: الدورُ والاسمُ كما حسمهما `ApiSecurityContextMiddleware` قبل هذه الطبقة — فلا يُعاد على
    // ‏مستدعٍ آخرَ ردُّ غيره قبل أن يبلغ فحصَ صلاحيّته.
    var cacheKey = IdempotencyStore.ComposeKey(
        TenantContext.CurrentTenantId, SecurityContext.CurrentRole, SecurityContext.CurrentUsername,
        ctx.Request.Path.Value ?? "", key);

    if (IdempotencyStore.TryGet(cacheKey, out var cached))
    {
        ctx.Response.StatusCode = cached.StatusCode;
        ctx.Response.ContentType = cached.ContentType;
        await ctx.Response.WriteAsync(cached.Body);
        return;
    }

    var originalBody = ctx.Response.Body;
    await using var buffer = new MemoryStream();
    ctx.Response.Body = buffer;

    await next();

    buffer.Position = 0;
    var bodyText = await new StreamReader(buffer).ReadToEndAsync();
    var contentType = ctx.Response.ContentType ?? "application/json";

    if (ctx.Response.StatusCode is >= 200 and < 300)
        IdempotencyStore.Save(cacheKey, new IdempotentResponse(ctx.Response.StatusCode, contentType, bodyText));

    buffer.Position = 0;
    ctx.Response.Body = originalBody;
    await buffer.CopyToAsync(originalBody);
});

const string ServiceName = "Brqosh.Api";
const string ServiceVersion = "v1";

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = ServiceName, version = ServiceVersion }));

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    probe = "liveness",
    utc = DateTime.UtcNow,
    service = ServiceName,
}));

app.MapGet("/health/ready", async (BrqoshDbContext db, ILogger<Program> logger, CancellationToken ct) =>
{
    var dbOk = false;
    string? dbError = null;
    try
    {
        dbOk = await db.Database.CanConnectAsync(ct);
    }
    catch (Exception ex)
    {
        // (CEEF-231 · O-36) ‏**مسبارُ الجاهزية لا يسقط بسقوط القاعدة** — وهذا التسامحُ
        // ‏يبقى. **لكنّ «unavailable» بلا سببٍ يُرسل المشغّلَ إلى السجلّات وهو يقرأ
        // ‏الردَّ نفسَه**: كلمةُ مرورٍ خاطئة، أو شهادةٌ منتهية، أو مضيفٌ لا يُحلّ — ثلاثتُها
        // ‏تُقرأ سواءً، وأوّلُ دقيقةٍ في العطل هي أثمنُها.
        //
        // ‏**لكنّ المسارَ مفتوحٌ بلا مفتاح** — فالنصُّ الكامل إلى السجلّ، وفي الإنتاج نوعُ الخطأ وحدَه (`ReadinessProbe`).
        logger.LogWarning(ex, "Readiness probe: database unavailable: {Reason}", ex.GetBaseException().Message);
        dbError = ReadinessProbe.DatabaseErrorText(ex, isProduction);
    }

    var payload = new
    {
        status = dbOk ? "ok" : "degraded",
        database = dbOk ? "ok" : "unavailable",
        databaseError = dbError,
        utc = DateTime.UtcNow,
        service = ServiceName,
        version = ServiceVersion,
    };

    return dbOk
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/api/v1/ops/metrics", () => Results.Ok(AppMetrics.Snapshot()));

app.MapGet("/api/v1/reports/trial-balance", async (
    long companyId, DateOnly from, DateOnly to, ITrialBalanceQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await query.ExecuteAsync(companyId, from, to, ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/company-groups", async (BrqoshDbContext db, CancellationToken ct) =>
{
    // أمن (ع-A1، CEEF-200): في وضع SaaS، ارفض السرد بلا هوية مستأجر.
    if (TenantContext.EnforceTenantIsolation && TenantContext.CurrentTenantId is null)
        return Results.Json(new { error = "tenant_header_required" }, statusCode: StatusCodes.Status403Forbidden);

    var query = db.CompanyGroups.AsQueryable();
    if (TenantContext.CurrentTenantId is not null)
    {
        var tenantId = TenantContext.CurrentTenantId.Value;
        query = query.Where(g => db.Companies.Any(c => c.CompanyGroupId == g.Id && c.TenantId == tenantId));
    }

    var rows = await query
        .OrderBy(g => g.Code)
        .Select(g => new
        {
            g.Id,
            g.Code,
            g.NameAr,
            g.ReportingCurrencyCode,
            CompanyCount = db.Companies.Count(c => c.CompanyGroupId == g.Id),
        })
        .Take(100)
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/company-groups/{groupId:long}/reports/consolidated-trial-balance", async (
    long groupId, DateOnly from, DateOnly to,
    IConsolidatedTrialBalanceQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    var rows = await query.ExecuteAsync(groupId, from, to, null, ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/company-groups/{groupId:long}/reports/consolidated-income-statement", async (
    long groupId, DateOnly from, DateOnly to,
    IConsolidatedIncomeStatementQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    var result = await query.ExecuteAsync(groupId, from, to, null, ct);
    return Results.Ok(result);
});

app.MapGet("/api/v1/company-groups/{groupId:long}/reports/consolidated-balance-sheet", async (
    long groupId, DateOnly asOf,
    IConsolidatedBalanceSheetQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    var result = await query.ExecuteAsync(groupId, asOf, null, ct);
    return Results.Ok(result);
});

app.MapGet("/api/v1/company-groups/{groupId:long}/reports/intercompany-eliminations", async (
    long groupId, DateOnly from, DateOnly to,
    IIntercompanyEliminationQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    var rows = await query.ExecuteAsync(groupId, from, to, ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/company-groups/{groupId:long}/intercompany-account-maps", async (
    long groupId, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    var rows = await db.IntercompanyAccountMaps.AsNoTracking()
        .Where(m => m.CompanyGroupId == groupId)
        .OrderBy(m => m.AccountCode)
        .Select(m => new { m.Id, m.AccountCode })
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapPost("/api/v1/company-groups/{groupId:long}/intercompany-account-maps", async (
    long groupId, string accountCode, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    if (RejectAccountingWrite() is { } roleReject) return roleReject;

    var map = IntercompanyAccountMap.Create(groupId, accountCode);
    db.IntercompanyAccountMaps.Add(map);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { map.Id, map.CompanyGroupId, map.AccountCode });
});

app.MapDelete("/api/v1/company-groups/{groupId:long}/intercompany-account-maps/{accountCode}", async (
    long groupId, string accountCode, BrqoshDbContext db, CancellationToken ct) =>
{
    if (groupId <= 0) return Results.BadRequest(new { error = "groupId required" });
    if (!await TenantGuard.IsGroupAllowedAsync(db, groupId, ct))
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);
    if (RejectAccountingWrite() is { } roleReject) return roleReject;

    var code = accountCode.Trim();
    var map = await db.IntercompanyAccountMaps
        .SingleOrDefaultAsync(m => m.CompanyGroupId == groupId && m.AccountCode == code, ct);
    if (map is null) return Results.NotFound();
    db.IntercompanyAccountMaps.Remove(map);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

app.MapPost("/api/v1/companies/{companyId:long}/fx-closing-translation", async (
    long companyId, DateOnly from, DateOnly to,
    FxClosingTranslationService service, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    // (جاهزيّةُ الإطلاق · ح-٤٦) ‏فرقُ الترجمة يُحسب ويُعاد ولا يُرحَّل — **IAS 21 يجعله في
    // ‏القوائم المُترجَمة لا في دفاتر المنشأة**. و`posted` باقٍ في الردّ `false` دائماً كي لا
    // ‏يقرأ عميلٌ قديمٌ غيابَه ترحيلاً.
    var figure = await service.TranslatePeriodAsync(companyId, from, to, "api", ct);
    return figure is null
        ? Results.Ok(new { posted = false, message = "no_translation_required" })
        : Results.Ok(new
        {
            posted = false,
            functionalCurrency = figure.FunctionalCurrency,
            reportingCurrency = figure.ReportingCurrency,
            netIncome = figure.NetIncome,
            averageRate = figure.AverageRate,
            closingRate = figure.ClosingRate,
            cta = figure.Cta,
        });
});

app.MapGet("/api/v1/billing-plans", async (IBillingPlanCatalog catalog, CancellationToken ct) =>
{
    var plans = await catalog.ListActiveAsync(ct);
    return Results.Ok(plans);
});

app.MapPost("/api/v1/tenants/provision", async (
    HttpContext http,
    TenantProvisionRequest request,
    ITenantProvisioningService provisioning,
    CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(provisioningKey))
    {
        if (!http.Request.Headers.TryGetValue("X-Provisioning-Key", out var presented)
            || !SecretComparer.Equals(provisioningKey, presented.ToString()))
            return Results.Json(new { error = "provisioning_unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
    }
    else if (isProduction)
        return Results.Json(new { error = "provisioning_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        var result = await provisioning.ProvisionAsync(request, ct);
        return Results.Ok(new
        {
            result.TenantId,
            result.CompanyId,
            planCode = result.PlanCode,
        });
    }
    catch (AccountingException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/tenants/subscription", async (IBillingPlanCatalog catalog, CancellationToken ct) =>
{
    if (TenantContext.CurrentTenantId is not Guid tenantId)
        return Results.BadRequest(new { error = "X-Tenant-Id header required" });

    var sub = await catalog.GetSubscriptionAsync(tenantId, ct);
    return sub is null ? Results.NotFound() : Results.Ok(sub);
});

app.MapGet("/api/v1/companies", async (BrqoshDbContext db, CancellationToken ct) =>
{
    // أمن (ع-A1، CEEF-200): في وضع SaaS، ارفض السرد بلا هوية مستأجر —
    // يمنع حامل المفتاح المشترك من سرد كل الشركات بحذف ترويسة X-Tenant-Id.
    if (TenantContext.EnforceTenantIsolation && TenantContext.CurrentTenantId is null)
        return Results.Json(new { error = "tenant_header_required" }, statusCode: StatusCodes.Status403Forbidden);

    var query = db.Companies.AsQueryable();
    if (TenantContext.CurrentTenantId is not null)
        query = query.Where(c => c.TenantId == TenantContext.CurrentTenantId.Value);

    var rows = await query
        .OrderBy(c => c.NameAr)
        .Select(c => new
        {
            c.Id,
            c.NameAr,
            c.VatRegistrationNumber,
            c.Profile,
            c.BaseCurrencyCode,
            c.FunctionalCurrencyCode,
            c.CompanyGroupId,
            c.TenantId,
        })
        .Take(200)
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapPost("/api/v1/exchange-rates", async (
    long companyId,
    string fromCurrency,
    string toCurrency,
    DateOnly effectiveDate,
    decimal rate,
    IExchangeRateService service,
    BrqoshDbContext db,
    CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    if (RejectAccountingWrite() is { } roleReject) return roleReject;
    await service.RecordRateAsync(companyId, fromCurrency, toCurrency, effectiveDate, rate, "api", ct);
    return Results.Ok(new { companyId, fromCurrency, toCurrency, effectiveDate, rate });
});

app.MapGet("/api/v1/exchange-rates/lookup", async (
    long companyId,
    string fromCurrency,
    string toCurrency,
    DateOnly asOf,
    IExchangeRateService service,
    BrqoshDbContext db,
    CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var resolved = await service.GetRateAsync(companyId, fromCurrency, toCurrency, asOf, ct);
    return Results.Ok(new { companyId, fromCurrency, toCurrency, asOf, rate = resolved });
});

app.MapGet("/api/v1/companies/{companyId:long}/exchange-rates", async (
    long companyId,
    BrqoshDbContext db,
    CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;

    var rows = await db.ExchangeRates.AsNoTracking()
        .Where(r => r.CompanyId == companyId)
        .OrderByDescending(r => r.EffectiveDate)
        .ThenByDescending(r => r.Id)
        .Take(500)
        .Select(r => new
        {
            r.Id,
            r.FromCurrencyCode,
            r.ToCurrencyCode,
            r.EffectiveDate,
            r.Rate,
            r.Source,
            r.CreatedAtUtc,
        })
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapPost("/api/v1/companies/{companyId:long}/exchange-rates/sync", async (
    long companyId,
    DateOnly? asOf,
    string? currencies,
    IFxRateSyncService sync,
    BrqoshDbContext db,
    CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    if (RejectAccountingWrite() is { } roleReject) return roleReject;

    IReadOnlyList<string>? targets = null;
    if (!string.IsNullOrWhiteSpace(currencies))
    {
        targets = currencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    var result = await sync.SyncCompanyRatesAsync(companyId, asOf, targets, ct);
    return Results.Ok(new
    {
        result.ProviderId,
        result.AsOfDate,
        result.RecordedCount,
        quotes = result.Quotes.Select(q => new
        {
            q.FromCurrency,
            q.ToCurrency,
            q.EffectiveDate,
            q.Rate,
            q.Source,
        }),
    });
});

app.MapGet("/api/v1/reports/project-profitability", async (
    long companyId, IProjectProfitabilityQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await query.ExecuteAsync(companyId, ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/customers", async (long companyId, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await db.Customers
        .Where(c => c.CompanyId == companyId && c.IsActive)
        .OrderBy(c => c.NameAr)
        .Select(c => new { c.Id, c.NameAr, c.VatRegistrationNumber, c.Phone })
        .Take(500)
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/products", async (long companyId, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await db.Products
        .Where(p => p.CompanyId == companyId && p.IsActive)
        .OrderBy(p => p.Code)
        .Select(p => new { p.Id, p.Code, p.NameAr, p.Unit, Type = p.Type.ToString(), p.SalePrice, p.WeightedAverageCost })
        .Take(500)
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/branches", async (long companyId, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await db.Branches
        .Where(b => b.CompanyId == companyId && b.IsActive)
        .OrderByDescending(b => b.IsHeadquarters).ThenBy(b => b.Code)
        .Select(b => new { b.Id, b.Code, b.NameAr, b.IsHeadquarters, b.IsActive })
        .Take(200)
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/domain-events", async (
    long companyId, int? take, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var limit = Math.Clamp(take ?? 100, 1, 500);
    var rows = await db.StoredDomainEvents
        .Where(e => e.CompanyId == companyId)
        .OrderByDescending(e => e.OccurredAtUtc)
        .Take(limit)
        .Select(e => new { e.EventId, e.EventType, e.OccurredAtUtc, e.PayloadJson })
        .ToListAsync(ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/data-quality", async (
    long companyId, IDataQualityService service, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var report = await service.RunAsync(companyId, ct);
    return Results.Ok(report);
});

app.MapGet("/api/v1/event-tracking", async (
    long companyId, DateTime? fromUtc, DateTime? toUtc, IEventTrackingQuery query, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await query.ExecuteAsync(companyId, fromUtc, toUtc, ct);
    return Results.Ok(rows);
});

app.MapPost("/api/v1/report-snapshots/trial-balance", async (
    long companyId, DateOnly from, DateOnly to, IReportSnapshotService service, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    if (RejectAccountingWrite() is { } roleReject) return roleReject;
    var snapshot = await service.CaptureTrialBalanceAsync(companyId, from, to, ct);
    return Results.Ok(snapshot);
});

app.MapGet("/api/v1/report-snapshots", async (
    long companyId, string? reportType, IReportSnapshotService service, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var rows = await service.ListAsync(companyId, reportType, ct);
    return Results.Ok(rows);
});

app.MapGet("/api/v1/intelligence/financial", async (
    long companyId, DateOnly? asOf, IFinancialIntelligenceService service, BrqoshDbContext db, CancellationToken ct) =>
{
    if (companyId <= 0) return Results.BadRequest(new { error = "companyId required" });
    var tenantReject = await RejectTenantCompanyAsync(db, companyId, ct);
    if (tenantReject is not null) return tenantReject;
    var report = await service.AnalyzeAsync(companyId, asOf, ct);
    return Results.Ok(report);
});

// (م-٣ · ن-٢٧) ‏**المساران كانا بلا حارسِ صلاحيّةٍ ولا مستأجر — وسائرُ الملفّ يحرس.**
//
// ‏‏`MapGet` كان `() => Results.Ok(WebhookSubscriptionRegistry.List())`: لا وسيطَ ولا سياق،
// ‏فيُعيد **قائمةَ العمليّة كلَّها** لأيّ حاملِ مفتاح — أي **عناوينَ وجهةٍ سجّلها مستأجرون
// ‏آخرون**، وهي عناوينُ أنظمتِهم الداخليّة. وهذا كشفٌ **بالغٌ اليوم**.
//
// ‏و`MapPost` معاملاه `(WebhookSubscribeRequest, IWebHostEnvironment)` — لا `SecurityContext`
// ‏ولا `TenantContext`. فيُزرع **هدفُ تسليمٍ خارجيٌّ** بلا صلاحيّة. وأثرُه مؤجَّلٌ لا معدوم:
// ‏التسليمُ لا يقع اليوم لأنّ `OutboxDispatchHook` غيرُ موصول (بند ن-٠٩)، **فالاشتراكُ
// ‏المزروعُ يصير حيّاً في اللحظة التي يُوصَل فيها**.
//
// ‏**وتصحيحٌ لصياغة البند:** «حاملُ مفتاحٍ بدور Viewer يشترك» ليس تصعيداً — `NarrowByRequest`
// ‏تُضيّق ولا تُوسّع، فصاحبُ الترويسة يملك المفتاحَ الأقوى ويُقيّد نفسَه طوعاً. **والعطلُ
// ‏الدائمُ عكسُه**: مضيفٌ يُضبَط `BRQOSH_API_ROLE=Viewer`، أو يُترك في الإنتاج فيسقط إلى
// ‏`Viewer` انغلاقاً، **يزرع الهدفَ رغم ذلك** — لأنّ المسارين لا يسألان `SecurityContext` أصلاً.
IResult? RejectWebhookAdministration()
{
    // ‏(١) عزلُ المستأجر — العرفُ نفسُه المستعمَل في عشرة مسارات في هذا الملفّ.
    if (TenantContext.EnforceTenantIsolation && TenantContext.CurrentTenantId is null)
        return Results.Json(new { error = "tenant_forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    // ‏(٢) ثمّ الدور — **قائمةُ سماحٍ لا قائمةُ منع**، وهو الدرسُ المقيسُ في ن-٢٦: قائمةُ
    // ‏المنع تنسى ما لم يخطر ببالها، وقائمةُ السماح لا تستطيع.
    if (SecurityContext.CurrentRole != UserRole.Administrator)
        return Results.Json(new { error = "webhook_admin_required" }, statusCode: StatusCodes.Status403Forbidden);

    return null;
}

app.MapGet("/api/v1/webhooks/subscriptions", () =>
    RejectWebhookAdministration()
        ?? Results.Ok(WebhookSubscriptionRegistry.ListForTenant(TenantContext.CurrentTenantId)));

app.MapPost("/api/v1/webhooks/subscriptions", (WebhookSubscribeRequest body, IWebHostEnvironment env) =>
{
    if (RejectWebhookAdministration() is { } reject) return reject;

    try
    {
        var sub = WebhookSubscriptionRegistry.Register(
            body.EventType,
            body.TargetUrl,
            tenantId: TenantContext.CurrentTenantId,
            allowInsecureLocal: env.IsDevelopment());
        return Results.Created($"/api/v1/webhooks/subscriptions/{sub.Id}", sub);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
    try
    {
        await new DatabaseSchemaService(db).EnsureSchemaAsync();
    }
    catch (Exception ex) when (DatabaseSchemaService.IsIgnorableSchemaConflict(ex))
    {
        await new BrqoshSchemaUpdater(db).ApplyAsync();
    }
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
