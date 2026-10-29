using System.Reflection;
using System.Text;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.PayrollImports;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.PayrollImports;
using Cosmix.Domain.HR.PayrollSalary;
using Xunit;

namespace Cosmix.Tests;
public sealed class PayrollImportWorkflowTests
{
    [Fact]
    public async Task Upload_preview_submit_retains_source_and_creates_pending_canonical_requests()
    {
        var f = new Fixture();
        var source = await f.Upload(); var preview = await f.Preview(source.SourceId);
        Assert.True(preview.CanApply); Assert.Single(preview.Rows);
        var result = await f.Apply(source.SourceId, preview.PreviewFingerprint, "submit-key-001");
        Assert.Equal("Submitted", result.State);
        Assert.Equal(PayrollSalaryService.ComponentRequestType, Assert.Single(result.Requests).RequestType);
        Assert.Equal(source.SourceSha256, result.SourceSha256);
        Assert.Equal(ApprovalState.Pending, Assert.Single(f.Components).ApprovalRequest.State);
        var read = await f.Service.SubmissionAsync(source.SourceId, Fixture.Context, default);
        Assert.Equal(result.Requests[0], read.Requests[0]);
        var retry = await f.Apply(source.SourceId, preview.PreviewFingerprint, "submit-key-001");
        Assert.Equal(result.Requests[0], retry.Requests[0]); Assert.Single(f.Components);
    }

    [Fact]
    public async Task Duplicate_rows_are_reported_and_entire_submission_is_rejected()
    {
        var f = new Fixture(); var source = await f.Upload(duplicate: true);
        var preview = await f.Preview(source.SourceId);
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows.SelectMany(x => x.Issues), x => x.Code == "salary_import_duplicate_employee_component");
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Apply(source.SourceId, preview.PreviewFingerprint));
        Assert.Equal(409, failure.StatusCode); Assert.Empty(f.Components); Assert.Null(f.Source!.SubmittedAt);
    }

    [Fact]
    public async Task Changed_employee_version_rejects_stale_preview_before_any_salary_write()
    {
        var f = new Fixture(); var source = await f.Upload(); var preview = await f.Preview(source.SourceId);
        f.Version = "AAAAAAAAAAI=";
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Apply(source.SourceId, preview.PreviewFingerprint));
        Assert.Equal("salary_import_preview_changed", failure.Code); Assert.Empty(f.Components);
    }

    [Fact]
    public async Task Source_permissions_are_required_even_with_import_permission()
    {
        var f = new Fixture(); var source = await f.Upload(); f.AllowSalarySource = false;
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Preview(source.SourceId));
        Assert.Equal(403, failure.StatusCode); Assert.Empty(f.Components);
    }

    [Fact]
    public async Task Same_raw_file_cannot_create_a_second_import_with_a_new_key()
    {
        var f = new Fixture(); await f.Upload();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Upload());
        Assert.Equal("salary_import_source_already_uploaded", failure.Code);
    }

    private sealed class Fixture
    {
        public static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-import-test");
        public readonly Guid Employee = Guid.NewGuid();
        public PayrollSalaryImport? Source;
        public readonly List<PayrollSalaryComponent> Components = [];
        public string Version = "AAAAAAAAAAE=";
        public bool AllowSalarySource = true;
        public PayrollImportService Service { get; }
        private readonly Dictionary<string, string> mapping;
        public Fixture()
        {
            var identity = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            var grants = new[] { PayrollImportPermissions.Read, PayrollImportPermissions.Submit, PayrollSalaryPermissions.Read,
                PayrollSalaryPermissions.Request, EmployeeCompensationPermissions.Read, EmployeeCompensationPermissions.Request }
                .Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }).ToArray();
            var current = Proxy<ICurrentAuthentication>((_, _) => identity);
            var access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
                grants.Where(x => AllowSalarySource || x.Permission != PayrollSalaryPermissions.Read).ToArray()));
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args),
                "IsCurrentAdministratorAsync" => Task.FromResult(true),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticHr"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var receipts = new List<IdempotencyReceipt>();
            var journal = Proxy<ISchedulingJournalRepository>((method, args) => method.Name switch
            {
                "IsCurrentSessionAsync" => Task.FromResult(true),
                "FindReceiptAsync" => Task.FromResult(receipts.SingleOrDefault(x => x.Key == (string)args[2]!)),
                "AddReceipt" => Add(receipts, (IdempotencyReceipt)args[0]!),
                "AddAudit" => null,
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            var files = new PayrollSalaryFrozenEvidenceTests.MemoryFiles();
            var operation = new AttendanceOperation(current, access, authorization, journal, null!, new Clock(), files);
            var salaryRepo = Proxy<IPayrollSalaryRepository>((method, args) => method.Name switch
            {
                "ComponentsAsync" => Task.FromResult<IReadOnlyList<PayrollSalaryComponent>>(Components.ToArray()),
                "EmployeeApprovedAsync" => Task.FromResult(true),
                "HasPreparedPeriodAsync" or "HasPreparedEmployeePeriodAsync" => Task.FromResult(false),
                "Add" => AddComponent(args),
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            var repo = Proxy<IPayrollImportRepository>((method, args) => method.Name switch
            {
                "FindAsync" => Task.FromResult(Source?.Id == (Guid)args[0]! ? Source : null),
                "SourceExistsAsync" => Task.FromResult(Source?.SourceSha256 == (string)args[0]!),
                "EmployeeAsync" => Task.FromResult<PayrollImportEmployee?>(new(Employee, 101, Version)),
                "Add" => SetSource((PayrollSalaryImport)args[0]!),
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            Service = new(operation, repo, files, null!, null!, new PayrollSalaryService(operation, salaryRepo, files),
                salaryRepo, access, authorization, new Clock());
            mapping = new[] { "rowType", "employeeNumber", "effectiveFrom", "currency", "componentCode", "componentKind", "componentAmount", "prorateWithBase" }
                .ToDictionary(x => x, x => x);
        }
        public Task<PayrollImportInspection> Upload(bool duplicate = false)
        {
            var row = "Component,101,2030-01-01,PKR,TRAVEL,FixedEarning,1000.00,true\n";
            var csv = string.Join(',', mapping.Keys) + "\n" + row + (duplicate ? row : "");
            return Service.InspectAsync(new("salary.csv", "text/csv", Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)), "Synthetic source"), Guid.NewGuid().ToString(), Context, default);
        }
        public Task<PayrollImportPreview> Preview(Guid id) => Service.PreviewAsync(new(id, "CSV", mapping), Context, default);
        public Task<PayrollImportApplied> Apply(Guid id, string fingerprint, string? key = null) =>
            Service.ApplyAsync(new(id, "CSV", mapping, fingerprint, "Synthetic submission"), key ?? Guid.NewGuid().ToString(), Context, default);
        private object? AddComponent(object?[] args)
        {
            var component = (PayrollSalaryComponent)args[0]!;
            typeof(PayrollSalaryComponent).GetProperty(nameof(PayrollSalaryComponent.ApprovalRequest))!.SetValue(component, args[1]);
            Components.Add(component); return null;
        }
        private object? SetSource(PayrollSalaryImport source) { Source = source; return null; }
        private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero); }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = DispatchProxy.Create<T, CallbackProxy>(); ((CallbackProxy)(object)value).Handler = handler; return value; }
    public class CallbackProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    }
}
