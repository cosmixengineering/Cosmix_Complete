using System.Globalization;
using System.Text;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.StatutoryContributions;

public sealed class StatutoryContributionService(IStatutoryContributionRepository repository, TimeProvider clock)
{
    public async Task<ContributionResponse> CreateAsync(CreateContributionRequest request, string actorUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var contribution = new StatutoryContribution(
            request.Scheme,
            request.ContributionMonth,
            request.EmployeeContributionAmount,
            request.EmployerContributionAmount,
            request.Currency,
            actorUserId,
            now,
            request.PayableAccountCode,
            request.Notes,
            request.EmployerRegistrationNumber);

        await repository.AddContributionAsync(contribution, ct);
        await repository.SaveChangesAsync(ct);

        return Map(contribution);
    }

    public async Task<ContributionResponse> ApproveAsync(Guid id, string actorUserId, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        var now = clock.GetUtcNow();
        // Domain model enforces: actorUserId != CreatedByUserId
        contribution.Approve(actorUserId, now);

        await repository.SaveChangesAsync(ct);

        return Map(contribution);
    }

    public async Task<ContributionResponse> PostAsync(Guid id, string actorUserId, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        var now = clock.GetUtcNow();
        contribution.Post(actorUserId, now);

        await repository.SaveChangesAsync(ct);

        return Map(contribution);
    }

    public async Task<ContributionResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        return Map(contribution);
    }

    public async Task<ContributionListResponse> ListAsync(StatutoryScheme? scheme, int? year, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var items = await repository.ListAsync(scheme, year, skip, pageSize, ct);
        var total = await repository.CountAsync(scheme, year, ct);

        return new ContributionListResponse(items.Select(Map).ToList(), total);
    }

    public async Task<(string FileName, byte[] Bytes)> ExportScheduleCsvAsync(Guid id, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        var registrations = await repository.GetAllRegistrationsForSchemeAsync(contribution.Scheme, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Scheme,ContributionMonth,EmployeeId,RegistrationNumber,EmployeeContribution,EmployerContribution,TotalContribution,Currency,State");

        if (registrations.Count > 0)
        {
            var perEmployeeWorker = contribution.EmployeeContributionAmount / registrations.Count;
            var perEmployeeEmployer = contribution.EmployerContributionAmount / registrations.Count;
            var perEmployeeTotal = perEmployeeWorker + perEmployeeEmployer;

            foreach (var reg in registrations)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:yyyy-MM-dd},{2},{3},{4:F2},{5:F2},{6:F2},{7},{8}",
                    contribution.Scheme,
                    contribution.ContributionMonth,
                    reg.EmployeeId,
                    reg.RegistrationNumber,
                    perEmployeeWorker,
                    perEmployeeEmployer,
                    perEmployeeTotal,
                    contribution.Currency,
                    contribution.State));
            }
        }
        else
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1:yyyy-MM-dd},TOTAL_ONLY,,{2:F2},{3:F2},{4:F2},{5},{6}",
                contribution.Scheme,
                contribution.ContributionMonth,
                contribution.EmployeeContributionAmount,
                contribution.EmployerContributionAmount,
                contribution.TotalContributionAmount,
                contribution.Currency,
                contribution.State));
        }

        var fileName = $"{contribution.Scheme}_Contribution_{contribution.ContributionMonth:yyyy_MM}.csv";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return (fileName, bytes);
    }

    public async Task<RegistrationResponse> AddRegistrationAsync(Guid employeeId, AddRegistrationRequest request, string actorUserId, CancellationToken ct)
    {
        var existing = await repository.GetRegistrationAsync(employeeId, request.Scheme, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Employee already has a registration for scheme {request.Scheme}.");
        }

        var now = clock.GetUtcNow();
        var reg = new EmployeeStatutoryRegistration(
            employeeId,
            request.Scheme,
            request.RegistrationNumber,
            request.RegisteredAt,
            actorUserId,
            now);

        await repository.AddRegistrationAsync(reg, ct);
        await repository.SaveChangesAsync(ct);

        return new RegistrationResponse(reg.Id, reg.EmployeeId, reg.Scheme.ToString(), reg.RegistrationNumber, reg.RegisteredAt, reg.RecordedAt);
    }

    public async Task<IReadOnlyList<RegistrationResponse>> GetRegistrationsAsync(Guid employeeId, CancellationToken ct)
    {
        var list = await repository.GetRegistrationsForEmployeeAsync(employeeId, ct);
        return list.Select(r => new RegistrationResponse(r.Id, r.EmployeeId, r.Scheme.ToString(), r.RegistrationNumber, r.RegisteredAt, r.RecordedAt)).ToList();
    }

    private static ContributionResponse Map(StatutoryContribution c) =>
        new(c.Id, c.Scheme.ToString(), c.ContributionMonth, c.EmployeeContributionAmount, c.EmployerContributionAmount,
            c.TotalContributionAmount, c.Currency, c.State.ToString(), c.PayableAccountCode, c.Notes, c.EmployerRegistrationNumber,
            c.CreatedAt, c.CreatedByUserId, c.ApprovedAt, c.ApprovedByUserId, c.PostedAt, c.PostedByUserId);
}
