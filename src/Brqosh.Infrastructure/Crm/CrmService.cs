using Brqosh.Domain.Crm;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Crm;

public class CrmService(BrqoshDbContext db)
{
    public async Task<CrmLead> CreateLeadAsync(CrmLead lead, CancellationToken ct = default)
    {
        db.CrmLeads.Add(lead);
        await db.SaveChangesAsync(ct);
        return lead;
    }

    public async Task<CrmOpportunity> CreateOpportunityAsync(CrmOpportunity opportunity, CancellationToken ct = default)
    {
        db.CrmOpportunities.Add(opportunity);
        await db.SaveChangesAsync(ct);
        return opportunity;
    }

    public async Task<CrmActivity> LogActivityAsync(CrmActivity activity, CancellationToken ct = default)
    {
        db.CrmActivities.Add(activity);
        await db.SaveChangesAsync(ct);
        return activity;
    }

    public async Task<Customer> ConvertLeadAsync(long companyId, long leadId, CancellationToken ct = default)
    {
        var lead = await db.CrmLeads
            .SingleAsync(l => l.CompanyId == companyId && l.Id == leadId && l.IsActive, ct);
        if (lead.Status == CrmLeadStatus.Converted && lead.CustomerId is not null)
        {
            return await db.Customers.SingleAsync(c => c.Id == lead.CustomerId.Value, ct);
        }

        var customer = Customer.Create(companyId, lead.Name, phone: lead.Phone, address: lead.Email);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        lead.MarkConverted(customer.Id);
        await db.SaveChangesAsync(ct);
        return customer;
    }
}
