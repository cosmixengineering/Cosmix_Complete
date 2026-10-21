namespace NOIR.Application.Features.Crm.Queries.GetCrmDashboard;

public class GetCrmDashboardQueryHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetCrmDashboardQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Features.Crm.DTOs.CrmDashboardDto>> Handle(
        GetCrmDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var totalContacts = await _dbContext.CrmContacts
            .Where(c => !c.IsDeleted)
            .TagWith("CrmDashboard_TotalContacts")
            .CountAsync(cancellationToken);

        var totalCompanies = await _dbContext.CrmCompanies
            .Where(c => !c.IsDeleted)
            .TagWith("CrmDashboard_TotalCompanies")
            .CountAsync(cancellationToken);

        var activeLeads = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Active)
            .TagWith("CrmDashboard_ActiveLeads")
            .CountAsync(cancellationToken);

        var wonLeads = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Won)
            .TagWith("CrmDashboard_WonLeads")
            .CountAsync(cancellationToken);

        var lostLeads = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Lost)
            .TagWith("CrmDashboard_LostLeads")
            .CountAsync(cancellationToken);

        var totalPipelineValue = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Active)
            .TagWith("CrmDashboard_TotalPipelineValue")
            .SumAsync(l => l.Value, cancellationToken);

        var wonDealValue = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Won)
            .TagWith("CrmDashboard_WonDealValue")
            .SumAsync(l => l.Value, cancellationToken);

        // Materialize projected data first, then group in-memory to avoid EF Core GroupBy translation issues
        var stageProjections = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Active && l.Stage != null)
            .Select(l => new { l.Value, StageName = l.Stage!.Name, StageColor = l.Stage.Color })
            .TagWith("CrmDashboard_LeadsByStage")
            .ToListAsync(cancellationToken);

        var leadsByStage = stageProjections
            .GroupBy(x => new { x.StageName, x.StageColor })
            .Select(g => new Features.Crm.DTOs.LeadsByStageDto(
                g.Key.StageName, g.Key.StageColor, g.Count(), g.Sum(x => x.Value)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var ownerProjections = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Active && l.OwnerId != null)
            .Select(l => new { l.Value, OwnerFirstName = l.Owner!.FirstName, OwnerLastName = l.Owner.LastName })
            .TagWith("CrmDashboard_LeadsByOwner")
            .ToListAsync(cancellationToken);

        var leadsByOwner = ownerProjections
            .GroupBy(x => new { x.OwnerFirstName, x.OwnerLastName })
            .Select(g => new Features.Crm.DTOs.LeadsByOwnerDto(
                g.Key.OwnerFirstName + " " + g.Key.OwnerLastName, g.Count(), g.Sum(x => x.Value)))
            .OrderByDescending(x => x.TotalValue)
            .ToList();

        // "This month" metrics
        var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var wonDealsThisMonth = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Won && l.WonAt >= monthStart)
            .TagWith("CrmDashboard_WonDealsThisMonth")
            .CountAsync(cancellationToken);

        var wonValueThisMonth = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Won && l.WonAt >= monthStart)
            .TagWith("CrmDashboard_WonValueThisMonth")
            .SumAsync(l => l.Value, cancellationToken);

        var lostDealsThisMonth = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Lost && l.LostAt >= monthStart)
            .TagWith("CrmDashboard_LostDealsThisMonth")
            .CountAsync(cancellationToken);

        // 90-day conversion rate: Won / (Won + Lost) * 100
        var ninetyDaysAgo = DateTimeOffset.UtcNow.AddDays(-90);

        var wonLast90 = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Won && l.WonAt >= ninetyDaysAgo)
            .TagWith("CrmDashboard_WonLast90")
            .CountAsync(cancellationToken);

        var lostLast90 = await _dbContext.Leads
            .Where(l => !l.IsDeleted && l.Status == LeadStatus.Lost && l.LostAt >= ninetyDaysAgo)
            .TagWith("CrmDashboard_LostLast90")
            .CountAsync(cancellationToken);

        var totalClosed90 = wonLast90 + lostLast90;
        var conversionRate = totalClosed90 > 0
            ? Math.Round((decimal)wonLast90 / totalClosed90 * 100, 2)
            : 0m;

        return Result.Success(new Features.Crm.DTOs.CrmDashboardDto(
            totalContacts, totalCompanies, activeLeads, wonLeads, lostLeads,
            totalPipelineValue, wonDealValue,
            wonDealsThisMonth, wonValueThisMonth, lostDealsThisMonth, conversionRate,
            leadsByStage, leadsByOwner));
    }
}
