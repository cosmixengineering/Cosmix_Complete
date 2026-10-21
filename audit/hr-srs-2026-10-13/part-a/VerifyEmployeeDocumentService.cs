using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
var employee = new Employee(now.AddDays(-3), "synthetic-hr");
var version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
typeof(Employee).GetProperty("RowVersion")!.SetValue(employee, version);
var content = "%PDF-synthetic-only"u8.ToArray();
var reference = new PrivateFileReference("opaque", "version", Convert.ToHexString(SHA256.HashData(content)), "test", content.Length);
var document = new EmployeeDocument(employee.Id, EmployeeDocumentType.Identity, JsonSerializer.Serialize(reference),
    "identity.pdf", "application/pdf", content.Length, reference.Sha256, "synthetic-hr", now.AddDays(-2));
typeof(EmployeeDocument).GetProperty("RowVersion")!.SetValue(document, version);
document.RecordScan(DocumentScanStatus.Clean, true, "synthetic-scanner/v1", now.AddDays(-1));
document.RecordVerification(DocumentVerificationStatus.Verified, "synthetic-verifier", null, now);
var account = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
var allowed = true;
var reads = 0;
var saved = 0;
var grants = typeof(EmployeePermissions).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Select(p => new PermissionGrant((string)p.GetRawConstantValue()!, true, Guid.NewGuid(), AccessScopeKind.All, null)).ToArray();
var current = Proxy.Of<ICurrentAuthentication>((method, _) => method.Name == "Require" ? account : throw new Exception(method.Name));
var access = Proxy.Of<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(allowed ? grants : []));
var authorization = Proxy.Of<IAccessManagementRepository>((method, args) => method.Name switch
{
    "InTransactionAsync" => ((Delegate)args![0]!).DynamicInvoke(args[1]),
    "IsCurrentAdministratorAsync" => Task.FromResult(allowed),
    "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("HR"),
    _ => throw new Exception("Unexpected authorization call: " + method.Name)
});
var repository = Proxy.Of<IEmployeeHiringRepository>((method, args) => method.Name switch
{
    "FindAsync" => Task.FromResult<Employee?>(employee),
    "FindDocumentAsync" => Task.FromResult<EmployeeDocument?>(args![1]!.Equals(document.Id) ? document : null),
    "AddAudit" => null,
    "SaveChangesAsync" => Saved(),
    _ => throw new Exception("Unexpected repository call: " + method.Name)
});
Task Saved() { saved++; return Task.CompletedTask; }
var files = Proxy.Of<IPrivateFileStore>((method, _) => method.Name switch
{
    "ReadAsync" => Read(),
    _ => throw new Exception("Unexpected private store call: " + method.Name)
});
Task<byte[]> Read() { reads++; allowed = false; return Task.FromResult(content.ToArray()); }
var clock = new Clock(now);
var operation = new EmployeeOperation(current, access, authorization, repository, clock, files);
var scanner = Proxy.Of<IEmployeeFileScanner>((_, _) => throw new Exception("Download called scanner"));
var constructor = typeof(EmployeeDocumentService).GetConstructors().Single();
var service = (EmployeeDocumentService)constructor.Invoke(constructor.GetParameters().Select(parameter => parameter.ParameterType.Name switch
{
    nameof(EmployeeOperation) => (object)operation,
    nameof(IEmployeeHiringRepository) => repository,
    nameof(IPrivateFileStore) => files,
    nameof(IEmployeeFileScanner) => scanner,
    nameof(TimeProvider) => clock,
    nameof(MobileAuthenticationOptions) => new MobileAuthenticationOptions { BusinessTimeZoneId = "Asia/Karachi" },
    _ => parameter.ParameterType == typeof(IEmployeeDocumentRepository)
        ? Proxy.Of<IEmployeeDocumentRepository>((method, _) => method.Name == "RefreshAsync" ? Task.CompletedTask : throw new Exception(method.Name))
        : null
}).ToArray());
try
{
    await service.DownloadAsync(employee.Id, document.Id, false, false, new("127.0.0.1", "synthetic-a02"), default);
    Console.WriteLine("FAIL download reauthorizes after private file I/O: revoked actor received private bytes");
    return 1;
}
catch (AuthenticationFailure failure) when (failure.StatusCode == 403)
{
    if (reads != 1) throw new Exception("Authorization check did not straddle actual file I/O");
    Console.WriteLine("PASS download reauthorizes after private file I/O: revoked actor denied");
    return 0;
}

class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler = null!;
    public static T Of<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
}
