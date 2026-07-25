using Brqosh.Application.Intelligence;
using Brqosh.Application.Reporting;
using Brqosh.Application.Platform;
using Brqosh.Application.Treasury;
using Brqosh.Infrastructure.Webhooks;
using Brqosh.Api.Idempotency;
using Brqosh.Api.Middleware;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
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

var conn = builder.Configuration.GetConnectionString("Brqosh")
           ?? $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}";
var usePg = conn.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || string.Equals(builder.Configuration["Database:Provider"], "postgresql", StringComparison.OrdinalIgnoreCase);

builder.Services.AddBrqoshInfrastructure(conn, usePg);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "BRQOSH API",
        Version = "v1",
        Description = "BRQOSH ERP API — accounting, reporting, platform hooks"
    });
});

var app = builder.Build();

var isProduction = app.Environment.IsProduction();
var apiKey = builder.Configuration[ApiSecuritySettings.ApiKeyEnv]
             ?? Environment.GetEnvironmentVariable(ApiSecuritySettings.ApiKeyEnv);
var provisioningKey = builder.Configuration[ApiSecuritySettings.ProvisioningKeyEnv]
                      ?? Environment.GetEnvironmentVariable(ApiSecuritySettings.ProvisioningKeyEnv);
var saasMode = ApiSecuritySettings.IsSaaSMode()
               || string.Equals(builder.Configuration[ApiSecuritySettings.SaaSModeEnv], "1", StringComparison.Ordinal)
               || string.Equals(builder.Configuration[ApiSecuritySettings.SaaSModeEnv], "true", StringComparison.OrdinalIgnoreCase);

ApiSecuritySettings.EnsureProductionApiKey(apiKey, isProduction);
ApiSecuritySettings.EnsureProductionProvisioningKey(provisioningKey, isProduction);
TenantContext.EnforceTenantIsolation = saasMode;

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
    var mustAuthenticate = isProduction || !string.IsNullOrWhiteSpace(apiKey);
    if (!mustAuthenticate || pathOpen)
    {
        await next();
        return;
    }

    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var presented)
        || !SecretComparer.Equals(apiKey, presented.ToString()))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
        return;
    }

    await next();
});

app.UseMiddleware<ApiSecurityContextMiddleware>();

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader)
        && Guid.TryParse(tenantHeader.ToString(), out var tenantId))
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
    var cacheKey = IdempotencyStore.ComposeKey(
        TenantContext.CurrentTenantId, ctx.Request.Path.Value ?? "", key);

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

app.MapGet("/health/ready", async (BrqoshDbContext db, CancellationToken ct) =>
{
    var dbOk = false;
    try
    {
        dbOk = await db.Database.CanConnectAsync(ct);
    }
    catch
    {
        // readiness probe — connectivity failure is reported in payload
    }

    var payload = new
    {
        status = dbOk ? "ok" : "degraded",
        database = dbOk ? "ok" : "unavailable",
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
        return Results.StatusCode(StatusCodes.Status403Forbidden);

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
    var entry = await service.TranslatePeriodAsync(companyId, from, to, "api", ct);
    return entry is null
        ? Results.Ok(new { posted = false, message = "no_translation_required" })
        : Results.Ok(new { posted = true, entryId = entry.Id, entryNumber = entry.EntryNumber });
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
        return Results.StatusCode(StatusCodes.Status403Forbidden);

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

app.MapGet("/api/v1/webhooks/subscriptions", () =>
    Results.Ok(WebhookSubscriptionRegistry.List()));

app.MapPost("/api/v1/webhooks/subscriptions", (WebhookSubscribeRequest body, IWebHostEnvironment env) =>
{
    try
    {
        var sub = WebhookSubscriptionRegistry.Register(
            body.EventType,
            body.TargetUrl,
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
