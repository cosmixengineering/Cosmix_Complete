using NOIR.Domain.Entities.Invoicing;
using NOIR.Domain.Entities.Pm;
using NOIR.Domain.Entities.Fsm;
using NOIR.Domain.Entities.Complaints;
using NOIR.Domain.Entities.Helpdesk;
using NOIR.Domain.Entities.Hr;

namespace NOIR.Web.Endpoints;

public static class ExecutiveDashboardEndpoints
{
    public static void MapExecutiveDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard/executive")
            .WithTags("Executive / Management Dashboard")
            .RequireAuthorization();

        // Get CEO Executive KPIs
        group.MapGet("/", async (IApplicationDbContext dbContext) =>
        {
            var totalRevenue = await dbContext.SalesInvoices
                .Where(x => x.Status == InvoiceStatus.Paid || x.Status == InvoiceStatus.Issued)
                .SumAsync(x => x.TotalAmount);

            var activeProjects = await dbContext.Projects
                .CountAsync(x => x.Status == ProjectStatus.Active);

            var pendingFieldJobs = await dbContext.FieldJobs
                .CountAsync(x => x.Status == JobStatus.Assigned || x.Status == JobStatus.InProgress);

            var openComplaints = await dbContext.CustomerComplaints
                .CountAsync(x => x.Status != ComplaintStatus.Closed);

            var openHelpdeskTickets = await dbContext.HelpdeskTickets
                .CountAsync(x => x.Status != TicketStatus.Closed);

            var totalEmployees = await dbContext.Employees
                .CountAsync(x => x.Status == EmployeeStatus.Active);

            var kpis = new ExecutiveKpiSummary(
                TotalRevenue: totalRevenue,
                ActiveProjects: activeProjects,
                PendingFieldJobs: pendingFieldJobs,
                OpenComplaints: openComplaints,
                OpenHelpdeskTickets: openHelpdeskTickets,
                ActiveEmployees: totalEmployees,
                GeneratedAt: DateTimeOffset.UtcNow);

            return Results.Ok(kpis);
        })
        .WithName("GetExecutiveDashboardKpis");
    }
}

public record ExecutiveKpiSummary(
    decimal TotalRevenue,
    int ActiveProjects,
    int PendingFieldJobs,
    int OpenComplaints,
    int OpenHelpdeskTickets,
    int ActiveEmployees,
    DateTimeOffset GeneratedAt);
