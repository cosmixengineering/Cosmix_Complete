using System.ComponentModel.DataAnnotations;

namespace Cosmix.Application.HR.Departments;

public sealed class DepartmentListRequest
{
    [Range(1, 1000000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(100)]
    public string? Search { get; init; }

    public bool? IsActive { get; init; }
}
