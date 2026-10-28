using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using System.Globalization;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من BrqoshSchemaUpdater (مجمَّد) — جداولُ CRM في V14. نُقل كما هو دون تعديل.
public partial class BrqoshSchemaUpdater
{
    private async Task EnsureV14CrmTablesAsync(bool isPg, CancellationToken ct)
    {
        if (isPg)
        {
            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_leads (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Name" character varying(256) NOT NULL,
                    "Phone" character varying(32) NOT NULL DEFAULT '',
                    "Email" character varying(256) NOT NULL DEFAULT '',
                    "Source" character varying(64) NOT NULL DEFAULT '',
                    "Status" character varying(16) NOT NULL,
                    "CustomerId" BIGINT NULL,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true,
                    "DeletedAtUtc" TIMESTAMPTZ NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_crm_leads_company_status
                    ON crm_leads ("CompanyId", "Status");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_opportunities (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NULL,
                    "LeadId" BIGINT NULL,
                    "Title" character varying(256) NOT NULL,
                    "Stage" character varying(16) NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ExpectedCloseDate" DATE NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_crm_opportunities_company_stage
                    ON crm_opportunities ("CompanyId", "Stage");
                """, ct);

            await SafePgIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_activities (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CustomerId" BIGINT NULL,
                    "OpportunityId" BIGINT NULL,
                    "ActivityType" character varying(16) NOT NULL,
                    "Subject" character varying(256) NOT NULL,
                    "Notes" character varying(2048) NOT NULL DEFAULT '',
                    "DueAtUtc" TIMESTAMPTZ NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_crm_activities_company_due
                    ON crm_activities ("CompanyId", "DueAtUtc");
                """, ct);
        }
        else
        {
            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_leads (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Phone TEXT NOT NULL DEFAULT '',
                    Email TEXT NOT NULL DEFAULT '',
                    Source TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL,
                    CustomerId INTEGER NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    DeletedAtUtc TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_crm_leads_company_status
                    ON crm_leads (CompanyId, Status);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_opportunities (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    CustomerId INTEGER NULL,
                    LeadId INTEGER NULL,
                    Title TEXT NOT NULL,
                    Stage TEXT NOT NULL,
                    Amount REAL NOT NULL DEFAULT 0,
                    ExpectedCloseDate TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_crm_opportunities_company_stage
                    ON crm_opportunities (CompanyId, Stage);
                """, ct);

            await SafeSqliteIndexAsync("""
                CREATE TABLE IF NOT EXISTS crm_activities (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    CustomerId INTEGER NULL,
                    OpportunityId INTEGER NULL,
                    ActivityType TEXT NOT NULL,
                    Subject TEXT NOT NULL,
                    Notes TEXT NOT NULL DEFAULT '',
                    DueAtUtc TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ix_crm_activities_company_due
                    ON crm_activities (CompanyId, DueAtUtc);
                """, ct);
        }
    }
}
