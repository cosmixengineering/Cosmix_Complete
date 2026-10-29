using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.StatutoryContributions;
using Xunit;

namespace Cosmix.Tests;

public sealed class StatutorySourceIntegrityTests
{
    private static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-statutory-source-test");

    [Fact]
    public async Task LegacyEmployeeItemsCannotCreateStatutoryAuthority()
    {
        var f = await StatutorySourceFixture.CreateAsync();
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service().CreateAsync(f.Request with {
            Entries = null, EmployeeItems = [new(f.Employee, 100, 500)] }, Context, default));
        Assert.Null(f.Row); Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("late")]
    public async Task MissingOrLateRegistrationCannotBeInvented(string kind)
    {
        var f = await StatutorySourceFixture.CreateAsync();
        f.Registration = kind == "missing" ? null : new(f.Employee, StatutoryScheme.EOBI, "LATE", f.Month.AddDays(1), "maker", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service().CreateAsync(f.Request, Context, default));
        Assert.Null(f.Row); Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task MissingApprovedCoverageCannotCreateContribution()
    {
        var f = await StatutorySourceFixture.CreateAsync(); f.Coverages.Clear();
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service().CreateAsync(f.Request, Context, default));
        Assert.Null(f.Row); Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingEvidenceDependenciesCannotCreateOrApprove(bool storage, bool finance)
    {
        var f = await StatutorySourceFixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service(storage: storage, finance: finance).CreateAsync(f.Request, Context, default));
        Assert.Null(f.Row);
        await f.Service().CreateAsync(f.Request, Context, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service(storage: storage, finance: finance).ApproveAsync(f.Row!.Id, Context, default));
        Assert.Equal(ContributionState.Draft, f.Row!.State);
    }

    [Fact]
    public async Task RealEntryProofApprovesAndExportsButChangedProofCannotApproveOrExport()
    {
        var f = await StatutorySourceFixture.CreateAsync();
        var created = await f.Service().CreateAsync(f.Request, Context, default);
        var approved = await f.Service().ApproveAsync(created.Id, Context, default);
        Assert.Equal("Approved", approved.State);
        Assert.Equal(100, approved.EmployeeContributionAmount); Assert.Equal(500, approved.EmployerContributionAmount);
        var schedule = await f.Service().ExportScheduleCsvAsync(created.Id, Context, default);
        Assert.Contains("100.00,500.00,600.00", System.Text.Encoding.UTF8.GetString(schedule.Bytes));
        typeof(StatutoryContributionEntry).GetProperty(nameof(StatutoryContributionEntry.EvidenceSha256))!
            .SetValue(Assert.Single(f.Row!.Entries), new string('B', 64));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service().ExportScheduleCsvAsync(created.Id, Context, default));

        var draft = await StatutorySourceFixture.CreateAsync();
        await draft.Service().CreateAsync(draft.Request, Context, default);
        typeof(StatutoryContributionEntry).GetProperty(nameof(StatutoryContributionEntry.EvidenceSha256))!
            .SetValue(Assert.Single(draft.Row!.Entries), new string('B', 64));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => draft.Service().ApproveAsync(draft.Row.Id, Context, default));
        Assert.Equal(ContributionState.Draft, draft.Row.State);
    }

    [Fact]
    public async Task LegacyPostingAndHeaderOnlyScheduleFailWithoutWrites()
    {
        var f = await StatutorySourceFixture.CreateAsync();
        f.Row = new(StatutoryScheme.EOBI, f.Month, 100, 500, "PKR", "maker", DateTimeOffset.UtcNow, hasEmployeeEntries: true);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service().PostAsync(f.Row.Id, Context, default));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service().ExportScheduleCsvAsync(f.Row.Id, Context, default));
        Assert.Equal(0, f.Saves); Assert.Equal(ContributionState.Draft, f.Row.State);
    }
}

/// <summary>Unit-only repositories with real purpose-bound proof bytes; SQL acceptance uses the production encrypted store.</summary>
internal sealed class StatutorySourceFixture
{
    internal readonly Guid Employee;
    internal readonly DateOnly Month;
    internal EmployeeStatutoryRegistration? Registration;
    internal StatutoryContribution? Row;
    internal readonly List<EmployeeStatutoryCoverage> Coverages = [];
    internal int Saves;
    private readonly TestFiles files = new();
    private readonly IStatutoryContributionRepository repository;
    private readonly IPayrollFinanceRepository financeRepository;
    internal CreateContributionRequest Request => new(StatutoryScheme.EOBI, Month, Currency: "PKR",
        Entries: [new(Employee, 100, 500, "synthetic-source", new string('A', 64))]);

    private StatutorySourceFixture(Guid employee, DateOnly month)
    {
        Employee = employee; Month = month;
        repository = Proxy<IStatutoryContributionRepository>((name, args) => name switch {
            "GetByIdAsync" or "GetBySchemeAndMonthAsync" => Task.FromResult(Row),
            "GetRegistrationAsync" => Task.FromResult(Registration),
            "AddContributionAsync" => Add((StatutoryContribution)args![0]!),
            "SaveChangesAsync" => Save(),
            _ => throw new InvalidOperationException("Unexpected repository call: " + name)
        });
        financeRepository = Proxy<IPayrollFinanceRepository>((name, _) => name switch {
            "EmployeeExistsAsync" => Task.FromResult(true),
            "CoverageAsync" => Task.FromResult<IReadOnlyList<EmployeeStatutoryCoverage>>(Coverages),
            _ => throw new InvalidOperationException("Unexpected finance call: " + name)
        });
    }
    internal StatutoryContributionService Service(SensitiveComplianceOperation? operation = null, bool storage = true, bool finance = true) =>
        new(repository, TimeProvider.System, operation ?? new PermissionsTests.Fixture().Operation,
            finance ? financeRepository : null, storage ? files : null);

    internal static async Task<StatutorySourceFixture> CreateAsync()
    {
        var f = new StatutorySourceFixture(Guid.NewGuid(), new(2026, 9, 1));
        f.Registration = new(f.Employee, StatutoryScheme.EOBI, "SYNTHETIC-REG", f.Month, "maker", DateTimeOffset.UtcNow);
        await f.AddCoverageAsync(StatutoryScheme.EOBI);
        return f;
    }

    internal static async Task<StatutorySourceFixture> RetainAsync(StatutoryContribution row, EmployeeStatutoryRegistration registration)
    {
        var f = new StatutorySourceFixture(registration.EmployeeId, row.ContributionMonth) { Row = row, Registration = registration };
        await f.AddCoverageAsync(row.Scheme);
        foreach (var entry in row.Entries)
        {
            var proof = new StatutoryEntryProof(row.Id, entry.EmployeeId, entry.RegistrationId, entry.RegistrationNumber, row.Scheme,
                row.ContributionMonth, row.Currency, entry.EmployeeContributionAmount, entry.EmployerContributionAmount, entry.EvidenceReference, entry.EvidenceSha256);
            var reference = await f.files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), StatutoryContributionService.EntryPurpose(row.Id, entry.EmployeeId), default);
            typeof(StatutoryContributionEntry).GetProperty(nameof(StatutoryContributionEntry.PrivateProofReference))!.SetValue(entry, JsonSerializer.Serialize(reference));
        }
        return f;
    }

    private async Task AddCoverageAsync(StatutoryScheme scheme)
    {
        var proof = new StatutoryCoverageProof(Employee, Month, Month.AddMonths(1), [scheme], "synthetic-coverage", new string('A', 64));
        var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), StatutoryContributionService.CoveragePurpose(Employee), default);
        var row = new EmployeeStatutoryCoverage(Employee, Month, Month.AddMonths(1), [scheme], proof.EvidenceReference, proof.EvidenceSha256,
            JsonSerializer.Serialize(reference), "coverage-maker", DateTimeOffset.UtcNow);
        row.Approve("coverage-reviewer", DateTimeOffset.UtcNow); Coverages.Add(row);
    }
    private Task Add(StatutoryContribution row) { Row = row; return Task.CompletedTask; }
    private Task Save() { Saves++; return Task.CompletedTask; }
    private static T Proxy<T>(Func<string, object?[]?, object?> callback) where T : class
    {
        var result = DispatchProxy.Create<T, PayrollFinanceRepositoryProxy>();
        ((PayrollFinanceRepositoryProxy)(object)result).Handler = callback; return result;
    }
    private sealed class TestFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> values = [];
        public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N"); values.Add(key, (purpose, bytes.ToArray()));
            return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        {
            var value = values[reference.StorageKey]; Assert.Equal(purpose, value.Purpose);
            Assert.Equal(reference.Sha256, Convert.ToHexString(SHA256.HashData(value.Bytes))); return Task.FromResult(value.Bytes.ToArray());
        }
    }
}
