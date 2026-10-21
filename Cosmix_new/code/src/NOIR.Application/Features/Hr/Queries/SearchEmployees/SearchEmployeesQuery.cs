namespace NOIR.Application.Features.Hr.Queries.SearchEmployees;

public sealed record SearchEmployeesQuery(string SearchText, int Take = 10);
