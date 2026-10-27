using System.Text.Json;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.Compensation;

public sealed partial class EmployeeCompensationService
{
    // Import callers already hold a current, audited authorization transaction. They must also
    // require compensation request/read authority; neither helper opens a nested transaction.
    internal async Task<CompensationChangeSnapshot> ValidateInTransactionAsync(Guid employeeId,
        RequestCompensationChange input, EmployeeActor actor, CancellationToken ct)
    {
        if (input is null) throw EmployeeOperation.Invalid("invalid_compensation_request");
        var terms = ValidateTerms(input.BaseAmount, input.GrossAmount, input.Currency, input.WageBasis, input.PaymentMethod);
        ValidateReason(input.Reason, true, 1000);
        var employee = await EmployeeAsync(employeeId, ct);
        if (!employee.RowVersion.SequenceEqual(OrganizationOperation.Version(input.ExpectedVersion)))
            throw EmployeeOperation.Conflict("compensation_employee_version_conflict");
        await RequireEligibleAsync(employee, actor.Now, ct);
        var today = Today(actor.Now);
        var window = await WindowAsync(employeeId, today, ct);
        ValidateSchedule(employee, window.Latest, input.ExpectedCompensationId, input.EffectiveFrom, today);
        if (await repository.HasPendingRequestAsync(employeeId, ct))
            throw EmployeeOperation.Conflict("compensation_request_outstanding");
        return new(1, employeeId, window.Latest is null ? null : Map(window.Latest), terms, input.EffectiveFrom, input.Reason.Trim());
    }

    internal async Task<EmployeeWriteResult> StageInTransactionAsync(Guid employeeId,
        RequestCompensationChange input, EmployeeActor actor, CancellationToken ct)
    {
        var snapshot = await ValidateInTransactionAsync(employeeId, input, actor, ct);
        var employee = await EmployeeAsync(employeeId, ct);
        hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
        var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(employeeId), ct);
        var request = new ApprovalRequest(RequestType, employeeId, actor.Identity.AccountId.ToString(),
            JsonSerializer.Serialize(evidence), 1, actor.Now);
        repository.AddRequest(request);
        await hiring.SaveChangesAsync(ct);
        return EmployeeHiringService.Result(employee) with { CompensationRequestId = request.Id };
    }
}
