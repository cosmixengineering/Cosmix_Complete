using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Departments;
using Cosmix.Domain.HR.Departments;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class DepartmentRepository(CosmixDbContext db) : IDepartmentRepository
{
    public async Task<PagedResult<Department>> ListAsync(
        DepartmentListRequest request, CancellationToken cancellationToken)
    {
        var query = db.Departments.AsNoTracking();
        if (request.IsActive.HasValue)
            query = query.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(cancellationToken);
        return new(items, count, request.Page, request.PageSize);
    }

    public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
