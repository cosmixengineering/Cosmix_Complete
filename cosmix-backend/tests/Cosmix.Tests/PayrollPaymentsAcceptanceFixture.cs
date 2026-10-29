using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Payslips;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

/// <summary>Strict clone-only continuation. Real SQL, repositories, transactions and protected
/// evidence; current session/role authority is synthetic and never claims real login acceptance.</summary>
internal sealed class PayrollPaymentsAcceptanceFixture : IAsyncDisposable
{
    internal const string EvidenceRoot = "D:/CosmixPayrollPayments20261028";
    internal static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-payments-acceptance-20261028");
    internal readonly CosmixDbContext Db;
    internal readonly PaymentClock Clock = new();
    internal readonly IPrivateFileStore Files;
    internal readonly ICurrentAuthentication Current;
    internal readonly IAccountAccessReader Access;
    internal readonly IAccessManagementRepository Authorization;
    internal readonly ISchedulingJournalRepository Journal;
    internal readonly PayrollFinanceSetupService Setup;
    internal readonly PayrollAccountingService Accounting;
    internal readonly PayrollSettlementReportService Reports;
    internal readonly EmployeeSelfService Own;
    internal readonly PayrollFrozenBatchReader Frozen;
    internal Guid ActorId = Guid.NewGuid(), ActorEmployeeId = Guid.NewGuid();
    internal readonly Guid Maker = Guid.NewGuid(), Reviewer = Guid.NewGuid(), ReversalReviewer = Guid.NewGuid(), Payee = Guid.NewGuid();
    internal Guid EmployeeId;
    internal PayrollObligation Obligation = null!;
    internal PayrollJournalEntry Accrual = null!;
    private readonly HashSet<string> permissions = ["payroll.accounting.configure", "payroll.accounting.configure.approve",
        "payroll.accounting.accrue", "payroll.accounting.payment", "payroll.accounting.payment.confirm",
        "payroll.reports.read", "payroll.reports.export", EmployeeSelfServicePermissions.FinancesRead];
    private readonly Guid session = Guid.NewGuid(), assignment = Guid.NewGuid();

    internal PayrollPaymentsAcceptanceFixture()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_PAYMENTS_CONNECTION")
            ?? throw new InvalidOperationException("Explicit cloned payment acceptance target required.");
        var target = new SqlConnectionStringBuilder(connection);
        if (target.DataSource != "(localdb)\\CosmixUat_d893653346" || target.InitialCatalog != "Cosmix_Payroll_Payments_20261028" ||
            !target.IntegratedSecurity || !string.IsNullOrEmpty(target.AttachDBFilename) || !string.IsNullOrEmpty(target.UserID) || !string.IsNullOrEmpty(target.Password))
            throw new InvalidOperationException("Only the exact isolated payment clone with Windows authentication is allowed.");
        var objects = Path.Combine(EvidenceRoot, "private", "objects");
        var key = Path.Combine(EvidenceRoot, "private", "storage.key");
        if (!Directory.Exists(objects) || !File.Exists(key) || new FileInfo(key).Length != 32 ||
            File.Exists(Path.Combine(EvidenceRoot, "payment-acceptance-result.json")))
            throw new InvalidOperationException("Require the prepared private clone and a fresh evidence run; never overwrite an accepted run.");
        Db = new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
        Files = new EncryptedPrivateFileStore(new() { Directory = objects, KeyFile = key, KeyReference = "synthetic-month-v1" });
        Current = Proxy<ICurrentAuthentication>((_, _) => new AuthenticatedAccount(ActorId, ActorEmployeeId, session, 1, SessionClient.Web, true));
        Access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(permissions.Select(code =>
            new PermissionGrant(code, true, assignment, AccessScopeKind.All, null) { AuthorityTimingKnown = true,
                ValidFromUtc = new(2026,1,1,0,0,0,TimeSpan.Zero) }).ToArray()));
        Authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => typeof(PayrollPaymentsAcceptanceFixture).GetMethod(nameof(Transaction), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"),
            "IsCurrentAdministratorAsync" or "IsCurrentSystemRoleAssignmentAsync" or "IsEmployeeActiveForAccessAsync" => Task.FromResult(true),
            _ => throw new InvalidOperationException("Unsupported synthetic authorization boundary.")
        });
        var journal = new SchedulingJournalRepository(Db, null!, null!);
        Journal = Proxy<ISchedulingJournalRepository>((method, args) => method.Name == "IsCurrentSessionAsync"
            ? Task.FromResult(true) : method.Invoke(journal, args));
        var sensitive = new SensitiveComplianceOperation(Current, Access, Authorization, Journal, Clock);
        var writes = new PayrollFinanceOperation(sensitive, Journal, Files, Clock);
        var finance = new PayrollFinanceRepository(Db);
        Frozen = new(new PayrollHandoffRepository(Db), Files);
        Setup = new(finance, sensitive, writes, Clock);
        Accounting = new(new PayrollAccountingRepository(Db), Clock, sensitive, Setup, Frozen, finance, Files, writes);
        var reader = new PayrollSettlementReader(Frozen, new PayslipRepository(Db), new PayrollAccountingRepository(Db), Clock, Accounting);
        Reports = new(sensitive, reader);
        var attendance = new AttendanceOperation(Current, Access, Authorization, Journal, null!, Clock, Files);
        var payslips = new PayslipService(attendance, Frozen, new PayslipRepository(Db), Clock, settlements: reader);
        Own = new(new EmployeeSelfServiceRepository(Db, Clock), Current, Access, Clock, Authorization, Journal,
            payslips: payslips, settlements: reader);
    }

    internal async Task LoadAsync()
    {
        Assert.Empty(await Db.Database.GetPendingMigrationsAsync());
        Assert.False(Db.Database.HasPendingModelChanges());
        Assert.Empty(await Db.SalaryPayments.AsNoTracking().ToArrayAsync());
        var employee = Assert.Single(await Db.Employees.AsNoTracking().ToArrayAsync());
        Assert.StartsWith("SYNTHETIC", employee.FullLegalName ?? ""); EmployeeId = employee.Id;
        Obligation = Assert.Single(await Db.PayrollObligations.AsNoTracking().ToArrayAsync());
        Assert.Equal(PayrollObligationState.Finalized, Obligation.State);
        Assert.Equal(33000m, Obligation.TotalGrossAmount); Assert.Equal(32200m, Obligation.TotalNetAmount);
        Assert.Equal(new DateOnly(2026,9,1), Obligation.PeriodStart);
        Assert.Equal("9E21158AEA524CFC40F514BA2C7084A66E10E35393CB8144D15285A5AE5535BA", Obligation.SourceFingerprint);
        Accrual = await Db.PayrollJournalEntries.AsNoTracking().SingleAsync(x => x.ObligationId == Obligation.Id && x.SourceKey == $"ACCRUAL-SALARY-{Obligation.Id:N}");
        Assert.Equal(32200m, Accrual.Amount);
        Assert.Equal(4, await Db.PayrollJournalEntries.CountAsync(x => x.ObligationId == Obligation.Id));
        ActorEmployeeId = EmployeeId;
    }

    internal void Actor(Guid actor) { ActorId = actor; Clock.Now = Clock.Now.AddSeconds(1); }
    internal void RemovePermission(string permission) => permissions.Remove(permission);
    internal void AddPermission(string permission) => permissions.Add(permission);
    internal async Task SaveEvidenceAsync<T>(string name, T value)
    {
        await using var stream = new FileStream(Path.Combine(EvidenceRoot, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }
    internal static async Task SaveArtifactAsync(string name, byte[] bytes)
    {
        await using var stream = new FileStream(Path.Combine(EvidenceRoot, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes);
    }
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Db.Database.CurrentTransaction is not null || Db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Mutations must be inside the audited transaction.");
        Db.ChangeTracker.Clear();
        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try { var result = await action(ct); await transaction.CommitAsync(ct); return result; }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        finally { Db.ChangeTracker.Clear(); }
    }
    public ValueTask DisposeAsync() => Db.DisposeAsync();
    internal sealed class PaymentClock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026,10,28,10,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    {
        var value = DispatchProxy.Create<T, PayrollPaymentsCallbackProxy>();
        ((PayrollPaymentsCallbackProxy)(object)value).Handler = callback; return value;
    }
}

public class PayrollPaymentsCallbackProxy : DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
}
