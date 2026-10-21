using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

// Synthetic HTTP contexts against the parent's frozen API only. No host, SQL or private IO.
var assembly = typeof(CurrentAuthentication).Assembly;
var guard = assembly.GetType("Cosmix.Api.Development.HrBusinessTestingGuard")!;
var v1 = assembly.GetType("Cosmix.Api.Development.HrVehicleTestingGuard")!;
var type = assembly.GetType("Cosmix.Api.Development.HrVehicleTestingBoundary")!;
var old = assembly.GetType("Cosmix.Api.Development.HrBusinessTestingBoundary")!;
const string selector = "COSMIX_EXISTING_TESTING_HOST";
var previous = Environment.GetEnvironmentVariable(selector);
const string profile = "HumaHrVehicle20261020V1";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020V1";
const string marker = "TEST-HR-V1-20261020";
const string vehicles = "/api/v1/hr/vehicles";
const string issued = "2026-10-20T00:00:00Z", returned = "2026-10-20T00:01:00Z";
const string issueReference = "TEST ONLY V1 issue condition; synthetic reference, no attachment";
const string returnReference = "TEST ONLY V1 return condition; synthetic reference, no attachment";
var adminAccount = Guid.Parse("94a83b62-5b56-4450-9175-2a7a9e3261bf");
var adminEmployee = Guid.Parse("7c505086-87ea-4fd2-980e-823ecccbe36a");
var hrAccount = Guid.Parse("8475187f-bf3b-4f9c-a3ed-4bac32253005");
var hrEmployee = Guid.Parse("35d19922-70a8-4c80-8578-e1029360e9fc");
var session = Guid.NewGuid();
var admin = new AuthenticatedAccount(adminAccount, adminEmployee, session, 1, SessionClient.Web, true);
var hr = new AuthenticatedAccount(hrAccount, hrEmployee, Guid.NewGuid(), 1, SessionClient.Web, true);
var vehicle = Guid.NewGuid(); var custody = Guid.NewGuid();
var checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
object? Call(Type t, string method, params object?[] args)
{
    try { return t.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args); }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
void Reject(Action action)
{
    try { action(); } catch (InvalidOperationException) { checks++; return; }
    throw new Exception("Guard accepted changed configuration.");
}
Dictionary<string, string?> Config() => new()
{
    ["LocalTesting:Enabled"] = "true", ["LocalTesting:ExistingAcceptanceProfile"] = profile,
    ["LocalTesting:PrivateDirectory"] = root, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
    ["LocalTesting:V1:IssuedAt"] = issued, ["LocalTesting:V1:ReturnedAt"] = returned,
    ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
    ["ConnectionStrings:CosmixDatabase"] = @"Server=np:\\.\pipe\LOCALDB#9F3001F7\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Cosmix.HrBusiness.V1",
    ["PrivateFiles:Directory"] = root + @"\files", ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["PrivateFiles:KeyReference"] = "hr-business-v1", ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
    ["SelfHostedFace:Enabled"] = "false", ["FaceCapture:Enabled"] = "false",
    ["FaceTemplateStorage:Enabled"] = "false", ["FaceStorageRetention:Enabled"] = "false",
    ["FaceStorageRetentionJournal:Enabled"] = "false", ["EmployeeFileScanner:Enabled"] = "false"
};
IConfiguration Configuration(Dictionary<string,string?>? c = null) => new ConfigurationBuilder().AddInMemoryCollection(c ?? Config()).Build();
void Validate(Dictionary<string,string?> c) => Call(guard, "ValidateConfiguration", Configuration(c), root + @"\config\host.json");
object Boundary() => Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance, null, [Configuration()], null)!;
string Create(string model = "TEST ONLY custody acceptance") => JsonSerializer.Serialize(new { registrationNumber = marker, vehicleType = "Motorcycle", makeModel = model, assetReference = marker });
string VehicleResponse(Guid? id = null) => JsonSerializer.Serialize(new { id = id ?? vehicle, registrationNumber = marker, vehicleType = "Motorcycle", makeModel = "TEST ONLY custody acceptance", assetReference = marker, isActive = true });
string Issue() => JsonSerializer.Serialize(new { vehicleId = vehicle, employeeId = hrEmployee, issuedAt = issued, issueOdometerKm = 1000, issueConditionReference = issueReference });
string Return() => JsonSerializer.Serialize(new { returnedAt = returned, returnOdometerKm = 1000, returnConditionReference = returnReference });
string CustodyResponse(bool done) => JsonSerializer.Serialize(new
{
    id = custody, vehicleId = vehicle, employeeId = hrEmployee, registrationNumber = marker,
    employeeName = "TEST ONLY", issuedAt = issued, issueOdometerKm = 1000, issueConditionReference = issueReference,
    returnedAt = done ? returned : null, returnOdometerKm = done ? (decimal?)1000 : null,
    returnConditionReference = done ? returnReference : null, isActive = !done
});
async Task<(int Status, bool Reached)> Send(object boundary, string method, string path,
    string body = "{}", string key = "", int status = 200, string response = "[]",
    AuthenticatedAccount? actor = null, Action<HttpContext>? setup = null, bool authenticate = true,
    Func<Task>? dispatchBarrier = null)
{
    var c = new DefaultHttpContext();
    c.Request.Method = method; c.Request.Scheme = "https"; c.Request.Host = new("localhost:7481");
    c.Connection.RemoteIpAddress = IPAddress.Loopback;
    var split = path.Split('?', 2); c.Request.Path = split[0];
    if (split.Length == 2) c.Request.QueryString = new("?" + split[1]);
    c.Request.ContentType = "application/json"; c.Request.Headers["Idempotency-Key"] = key;
    using var input = new MemoryStream(Encoding.UTF8.GetBytes(body));
    using var output = new MemoryStream();
    c.Request.Body = input; c.Request.ContentLength = input.Length; c.Response.Body = output;
    if (authenticate) c.Items[CurrentAuthentication.ItemKey] = actor ?? admin;
    setup?.Invoke(c);
    var reached = false;
    RequestDelegate endpoint = async ctx =>
    {
        reached = true;
        if (dispatchBarrier is not null) await dispatchBarrier();
        ctx.Response.StatusCode = status; await ctx.Response.WriteAsync(response);
    };
    RequestDelegate auth = ctx => (Task)type.GetMethod("AuthorizeActorAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(boundary, [ctx, endpoint])!;
    await (Task)type.GetMethod("InvokeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(boundary, [c, auth])!;
    Check(ReferenceEquals(c.Request.Body, input) && ReferenceEquals(c.Response.Body, output), "Streams not restored.");
    return (c.Response.StatusCode, reached);
}
async Task Seed(object b)
{
    var r = await Send(b, "POST", vehicles, Create(), marker + "-create", response: VehicleResponse());
    Check(r == (200, true), "Create blocked.");
}
try
{
    Environment.SetEnvironmentVariable(selector, profile);
    Check(!(bool)old.GetProperty("IsSelected", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!, "C04 boundary selected for V1.");
    Validate(Config()); checks++;
    foreach (var pair in new Dictionary<string,string?>
    {
        ["LocalTesting:ExistingAcceptanceProfile"] = "HumaHrBusiness20261019C04",
        ["LocalTesting:PrivateDirectory"] = root + "-other", ["Urls"] = "http://localhost:7481",
        ["PrivateFiles:KeyReference"] = "hr-business-c04", ["AttendanceJobs:Enabled"] = "true",
        ["SelfHostedFace:Enabled"] = "true", ["MobileAuthentication:Sms:Provider"] = "real",
        ["LocalTesting:V1:IssuedAt"] = returned, ["LocalTesting:V1:ReturnedAt"] = "2099-10-20T00:00:00Z",
        ["LocalTesting:V1:EmployeeId"] = Guid.NewGuid().ToString(), ["ConnectionStrings:Other"] = "server=remote"
    }) { var c = Config(); c[pair.Key] = pair.Value; Reject(() => Validate(c)); }
    foreach (var field in new[] { "LocalTesting:V1:IssuedAt", "LocalTesting:V1:ReturnedAt" })
    { var c = Config(); c.Remove(field); Reject(() => Validate(c)); }
    var connection = Config()["ConnectionStrings:CosmixDatabase"]!;
    Call(guard, "ValidateConnection", connection); checks++;
    foreach (var bad in new[] { connection.Replace("9F3001F7","F9CAED57"), connection + ";User ID=x", connection + ";AttachDBFilename=C:\\x.mdf", connection.Replace("Business.V1","Business.C04") })
        Reject(() => Call(guard, "ValidateConnection", bad));
    object?[] Identity() => [@"Software-PC1\LOCALDB#9F3001F7", "Cosmix_Testing", @"COSMIXRND\Huma.Khan", @"COSMIXRND\Huma.Khan",
        Guid.Parse("D9C34627-18A2-41CF-A804-821E39516303"), 25768, false, 0, 0,
        new[] { @"C:\Users\Huma.Khan\Cosmix_Testing.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf" }];
    Call(guard,"ValidateIdentity",Identity()); checks++;
    foreach (var index in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
    {
        var x=Identity(); x[index]=index switch { 4 => Guid.NewGuid(), 5 => 1, 6 => true, 7 or 8 => 1, 9 => Array.Empty<string>(), _ => "wrong" };
        Reject(() => Call(guard,"ValidateIdentity",x));
    }
    Call(guard,"ValidateTransport","Named pipe","FALSE",true); checks++;
    Reject(() => Call(guard,"ValidateTransport","TCP","TRUE",true));
    Reject(() => Call(guard,"ValidateTransport","Named pipe","FALSE",false));
    foreach(var method in new[]{"GET","POST","PUT","PATCH","DELETE"})
        foreach(var path in new[]{"/api/v1/hr/departments","/api/v1/hr/vehicles/policies","/api/v1/hr/vehicles/trips","/api/v1/auth/web/refresh","/health/live"})
            Check(!(await Send(Boundary(),method,path)).Reached,"Disallowed route reached endpoint.");
    foreach(var setup in new Action<HttpContext>[] { c=>c.Request.Scheme="http",c=>c.Request.Host=new("localhost:7482"),
        c=>c.Connection.RemoteIpAddress=IPAddress.Parse("192.0.2.1"),c=>c.Request.PathBase="/prefix",c=>c.Request.QueryString=new("?extra=1") })
        Check(!(await Send(Boundary(),"GET",vehicles,setup:setup)).Reached,"Transport/query bypass.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create(),marker+"-other")).Reached,"Unexpected key.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create("TEST ONLY custody changed payload"),marker+"-create")).Reached,"Changed payload before create.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create().Replace("\"Motorcycle\"","\"Car\""),marker+"-create")).Reached,"Changed vehicle type.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create()[..^1]+",\"extra\":1}",marker+"-create")).Reached,"Extra body field.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create()[..^1]+",\"vehicleType\":\"Motorcycle\"}",marker+"-create")).Reached,"Duplicate body field.");
    Check(!(await Send(Boundary(),"POST",vehicles,new string(' ',16385),marker+"-create",setup:c=>c.Request.ContentLength=null)).Reached,"Chunked oversized body.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create(),marker+"-create",setup:c=>c.Request.Headers["Content-Encoding"]="gzip")).Reached,"Encoded body.");
    Check(!(await Send(Boundary(),"POST",vehicles,Create(),marker+"-create",actor:hr)).Reached,"HR mutation.");
    Check(!(await Send(Boundary(),"GET",vehicles,authenticate:false)).Reached,"Anonymous business read.");
    Check(!(await Send(Boundary(),"GET",vehicles,actor:admin with { MfaSatisfied=false })).Reached,"Pre-MFA business read.");
    Check(!(await Send(Boundary(),"GET",vehicles,actor:admin with { EmployeeId=hrEmployee })).Reached,"Wrong actor binding.");
    Check((await Send(Boundary(),"GET","/api/v1/me/vehicles/custody",actor:hr)).Reached,"HR own read blocked.");
    Check((await Send(Boundary(),"GET","/api/v1/me/vehicles/custody",actor:admin)).Reached,"Admin own read blocked.");
    var concurrent=Boundary();
    var arrived=0;
    var rendezvous=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    async Task Overlap()
    {
        if (Interlocked.Increment(ref arrived)==2) rendezvous.SetResult();
        await rendezvous.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    var parallel=await Task.WhenAll(
        Send(concurrent,"POST",vehicles,Create(),marker+"-create",response:VehicleResponse(),dispatchBarrier:Overlap),
        Send(concurrent,"POST",vehicles,Create(),marker+"-create",response:VehicleResponse(),dispatchBarrier:Overlap));
    Check(arrived==2 && parallel.All(r=>r==(200,true)),"V1 same-key dispatch did not overlap.");
    Check((await Send(concurrent,"POST",vehicles,Create(),marker+"-create",response:VehicleResponse(Guid.NewGuid()))).Status==502,
        "Second create response could adopt another vehicle.");
    Check((await Send(concurrent,"GET",vehicles)).Status==503,"Contradictory create did not stop host.");
    var b=Boundary();
    Check((await Send(b,"POST",vehicles,Create(),marker+"-create",status:400)).Status==400,"Missing CSRF status swallowed.");
    await Seed(b);
    Check((await Send(b,"POST",vehicles,Create(),marker+"-create",response:VehicleResponse())).Reached,"Exact create replay.");
    Check((await Send(b,"POST",vehicles,Create("TEST ONLY custody changed payload"),marker+"-create",status:409)).Status==409,"Changed-payload conflict blocked.");
    Check(!(await Send(b,"POST",vehicles,Create(),marker+"-create",actor:admin with { SessionId=Guid.NewGuid() })).Reached,"Actor session changed.");
    Check(!(await Send(b,"POST",vehicles+"/custody/issue",Issue().Replace(hrEmployee.ToString(),adminEmployee.ToString()),marker+"-issue")).Reached,"Wrong beneficiary.");
    Check((await Send(b,"POST",vehicles+"/custody/issue",Issue(),marker+"-issue",response:CustodyResponse(false))).Reached,"Issue blocked.");
    Check((await Send(b,"POST",vehicles+"/custody/issue",Issue(),marker+"-issue",response:CustodyResponse(false))).Reached,"Issue replay blocked.");
    Check((await Send(b,"POST",vehicles+"/custody/issue",Issue(),marker+"-issue-conflict",status:409)).Status==409,"Issue negative blocked.");
    var returnPath=vehicles+"/custody/"+custody+"/return";
    Check((await Send(b,"POST",returnPath,Return(),marker+"-return",response:CustodyResponse(true))).Reached,"Return blocked.");
    Check((await Send(b,"POST",returnPath,Return(),marker+"-return",response:CustodyResponse(true))).Reached,"Return replay blocked.");
    Check((await Send(b,"POST",returnPath,Return(),marker+"-return-conflict",status:409)).Status==409,"Repeat return negative blocked.");
    Check((await Send(b,"GET",vehicles+"/custody/history?vehicleId="+vehicle+"&employeeId="+hrEmployee)).Reached,"Bound history blocked.");
    Check(!(await Send(Boundary(),"GET",vehicles+"/custody/history?employeeId="+adminEmployee)).Reached,"Foreign history allowed.");
    foreach(var badResponse in new[]{"{}",VehicleResponse(Guid.Empty),new string('x',65537)})
    {
        var poisoned=Boundary();
        Check((await Send(poisoned,"POST",vehicles,Create(),marker+"-create",response:badResponse)).Status==502,"Uncertain success not poisoned.");
        Check((await Send(poisoned,"GET",vehicles)).Status==503,"Uncertain state reusable.");
    }
    var failed=Boundary();
    await Send(failed,"POST",vehicles,Create(),marker+"-create",status:503);
    Check((await Send(failed,"GET",vehicles)).Status==503,"Failed write did not stop host.");
    var bounded=Boundary();
    for(var i=0;i<18;i++) Check((await Send(bounded,"GET",vehicles)).Reached,"Business cap early.");
    Check((await Send(bounded,"GET",vehicles)).Status==503,"Business cap missing.");
    bounded=Boundary();
    for(var i=0;i<100;i++) await Send(bounded,"GET","/forbidden");
    Check((await Send(bounded,"GET","/api/v1/me/permissions")).Status==503,"Denied requests not counted.");
    bounded=Boundary();
    foreach(var email in new[]{"hr@example.invalid","admin@example.invalid"})
    {
        var login=JsonSerializer.Serialize(new{email,password="synthetic"});
        Check((await Send(bounded,"POST","/api/v1/auth/web/login",login,status:202,response:"{}")).Reached,"Synthetic login blocked.");
        Check(!(await Send(bounded,"POST","/api/v1/auth/web/login",login)).Reached,"Repeated login allowed.");
    }
    Environment.SetEnvironmentVariable(selector,"HumaHrBusiness20261019C04");
    Check((bool)old.GetProperty("IsSelected",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!,"C04 no longer selected.");
    Check(!(bool)v1.GetProperty("IsSelected",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!,"V1 leaks into C04.");
    Console.WriteLine($"PASS {checks} V1 profile/boundary checks; no host, SQL, private IO or product build.");
}
finally { Environment.SetEnvironmentVariable(selector,previous); }
