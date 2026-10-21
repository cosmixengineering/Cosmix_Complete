using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Uses the shared AttendanceOperation transaction. Every list/detail is self- or site-filtered in SQL.</summary>
public sealed class SiteReportRepository(CosmixDbContext db) : ISiteReportRepository, ISiteReportReviewRepository
{
    public async Task<SiteReportAuthor?> FindAuthorAsync(Guid accountId, Guid deviceId, CancellationToken ct)
    {
        var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, ct);
        var device = await db.RegisteredDevices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deviceId && x.UserAccountId == accountId, ct);
        return account is null || device is null ? null : new(account, device);
    }
    public async Task<IReadOnlyList<SiteReportAssignment>> AssignmentsAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        await AssignmentQuery(employeeId, date).ToArrayAsync(ct);

    private IQueryable<SiteReportAssignment> AssignmentQuery(Guid employeeId, DateOnly date) =>
        db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
                x.EffectiveFrom <= date && (x.EffectiveTo == null || date < x.EffectiveTo))
            .OrderBy(x => x.Id).Take(2).Select(x => new SiteReportAssignment(x,
                x.Site != null && x.Site.IsActive, x.Site == null ? null : x.Site.StartsOn,
                x.Site == null ? null : x.Site.EndsOn));

    public async Task<SiteReportReadModel?> FindAsync(Guid reportId, Guid? employeeId, Guid? siteId, CancellationToken ct)
    {
        var report = await Scoped(employeeId, siteId).SingleOrDefaultAsync(x => x.Id == reportId, ct);
        return report is null ? null : await ReadModelAsync(report, ct);
    }

    public async Task<SiteReportReadModel?> FindSubmissionAsync(Guid employeeId, Guid accountId, string key, CancellationToken ct)
    {
        var report = await SubmissionKeyQuery(employeeId, accountId, key).SingleOrDefaultAsync(ct);
        return report is null ? null : await ReadModelAsync(report, ct);
    }

    private IQueryable<SiteReport> SubmissionKeyQuery(Guid employeeId, Guid accountId, string key)
    {
        if (employeeId == Guid.Empty || accountId == Guid.Empty)
            throw new InvalidOperationException("Submission reconciliation requires an explicit server-authorized employee/account scope.");
        SiteReportRules.ValidateSubmissionKey(key);
        // The existing BIN2 employee/key index preserves exact case without transforming the indexed column.
        return Scoped(employeeId, null).Where(x => x.UserAccountId == accountId && x.IdempotencyKey == key);
    }

    private async Task<SiteReportReadModel> ReadModelAsync(SiteReport report, CancellationToken ct)
    {
        var reviews = await ReviewQuery(report.Id).ToArrayAsync(ct);
        if (reviews.Length > 256) throw SiteReportRules.Conflict("site_report_review_history_limit");
        var responses = await ResponseQuery(report.Id, report.EmployeeId).ToArrayAsync(ct);
        if (responses.Length > 256) throw SiteReportRules.Conflict("site_report_response_history_limit");
        return new(report, reviews, responses);
    }

    public async Task<PagedResult<SiteReportReadModel>> ListAsync(Guid? employeeId, Guid? siteId,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, SiteReportListRequest request, CancellationToken ct)
    {
        var query = SearchQuery(employeeId, siteId, fromUtc, toUtcExclusive);
        var count = await query.CountAsync(ct);
        var page = await PageQuery(employeeId, siteId, fromUtc, toUtcExclusive, request).ToArrayAsync(ct);
        return new(page.Select(x => new SiteReportReadModel(x.Report, x.Review is null ? [] : [x.Review], x.Response is null ? [] : [x.Response])).ToArray(),
            count, request.Page, request.PageSize);
    }

    public Task<bool> HasSubmissionKeyAsync(Guid employeeId, string key, CancellationToken ct) =>
        db.SiteReports.AnyAsync(x => x.EmployeeId == employeeId && x.IdempotencyKey == key, ct);

    public async Task<PagedResult<SiteReportQueueSiteResponse>> QueueSitesAsync(bool allSites, IReadOnlyList<Guid> siteIds,
        SiteReportQueueListRequest request, CancellationToken ct)
    {
        SiteReportRules.ValidateQueueList(request);
        var count = await QueueSitesQuery(allSites, siteIds).CountAsync(ct);
        var page = await QueueSitesPageQuery(allSites, siteIds, request).ToArrayAsync(ct);
        return new(page, count, request.Page, request.PageSize);
    }

    private IQueryable<Site> QueueSitesQuery(bool allSites, IReadOnlyList<Guid> siteIds)
    {
        if (siteIds is null || siteIds.Any(x => x == Guid.Empty) ||
            (allSites ? siteIds.Count != 0 : siteIds.Count == 0))
            throw new InvalidOperationException("Site-report queue discovery requires an explicit server-authorized scope.");
        var sites = db.Sites.AsNoTracking();
        if (!allSites) sites = sites.Where(x => siteIds.Contains(x.Id));
        return sites;
    }

    private IQueryable<SiteReportQueueSiteResponse> QueueSitesPageQuery(bool allSites, IReadOnlyList<Guid> siteIds,
        SiteReportQueueListRequest request) => QueueSitesQuery(allSites, siteIds).OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip(checked((request.Page - 1) * request.PageSize)).Take(request.PageSize)
            // Inactive sites still hold immutable historical reports. Expose only safe queue labels.
            .Select(x => new SiteReportQueueSiteResponse(x.Id, x.Code, x.Name, x.IsActive));

    public void Add(SiteReport report) => db.SiteReports.Add(report);
    public void Add(SiteReportReview review) => db.SiteReportReviews.Add(review);
    public void Add(SiteReportResponse response) => db.Set<SiteReportResponse>().Add(response);

    private IQueryable<SiteReportReview> ReviewQuery(Guid reportId) => db.SiteReportReviews.AsNoTracking()
        .Where(x => x.SiteReportId == reportId).OrderBy(x => x.Sequence).Take(257);

    private IQueryable<SiteReportResponse> ResponseQuery(Guid reportId, Guid employeeId) => db.Set<SiteReportResponse>().AsNoTracking()
        .Where(x => x.SiteReportId == reportId && x.EmployeeId == employeeId).OrderBy(x => x.Sequence).Take(257);

    private IQueryable<SiteReport> SearchQuery(Guid? employeeId, Guid? siteId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive) =>
        Scoped(employeeId, siteId).Where(x => x.ReceivedAtUtc >= fromUtc && x.ReceivedAtUtc < toUtcExclusive);

    private IQueryable<ReportWithLastReview> PageQuery(Guid? employeeId, Guid? siteId,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, SiteReportListRequest request) =>
        SearchQuery(employeeId, siteId, fromUtc, toUtcExclusive).OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.Id)
            .Skip(checked((request.Page - 1) * request.PageSize)).Take(request.PageSize)
            .Select(x => new ReportWithLastReview(x, x.Reviews.OrderByDescending(r => r.Sequence).FirstOrDefault(),
                db.Set<SiteReportResponse>().Where(r => r.SiteReportId == x.Id && r.EmployeeId == x.EmployeeId)
                    .OrderByDescending(r => r.Sequence).FirstOrDefault()));

    private sealed record ReportWithLastReview(SiteReport Report, SiteReportReview? Review, SiteReportResponse? Response);
    private IQueryable<SiteReport> Scoped(Guid? employeeId, Guid? siteId)
    {
        if (employeeId is null && siteId is null || employeeId == Guid.Empty || siteId == Guid.Empty)
            throw new InvalidOperationException("A site-report query requires an explicit server-authorized scope.");
        return db.SiteReports.AsNoTracking().Where(x => (!employeeId.HasValue || x.EmployeeId == employeeId) &&
            (!siteId.HasValue || x.SiteId == siteId));
    }
}
