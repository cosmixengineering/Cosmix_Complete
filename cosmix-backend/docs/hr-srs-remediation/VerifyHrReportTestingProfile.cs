using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Calls actual internal guard/boundary methods on synthetic contexts only.
// No private-field changes, authority overrides, handler reflection, host, SQL or private-path IO.
var assembly = typeof(CurrentAuthentication).Assembly;
Type Type(string name) => assembly.GetType("Cosmix.Api.Development." + name, throwOnError:true)!;
var guard = Type("HrReportTestingGuard");
var business = Type("HrBusinessTestingGuard");
var host = Type("HrReportTestingHost");
var boundaryType = Type("HrReportTestingBoundary");
var previous = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST");
const string profile = "HumaHrReports20261020R1";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020R1";
const string employee = "35d19922-70a8-4c80-8578-e1029360e9fc";
const string report = "/api/v1/hr/reports/employee-master";
var passed = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); passed++; }
object? Call(Type type, string method, object? instance, params object?[] args)
{
    try { return type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public |
        (instance is null ? BindingFlags.Static : BindingFlags.Instance))!.Invoke(instance,args); }
    catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
}
Dictionary<string,string?> Settings() => new()
{
    ["LocalTesting:Enabled"]="true",["LocalTesting:ExistingAcceptanceProfile"]=profile,
    ["LocalTesting:PrivateDirectory"]=root,["LocalTesting:AllowUnencryptedLocalPipe"]="true",
    ["Urls"]="https://localhost:7481",["AllowedHosts"]="localhost",
    ["ConnectionStrings:CosmixDatabase"]=@"Server=np:\\.\pipe\LOCALDB#9F3001F7\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Cosmix.HrReports.R1",
    ["PrivateFiles:Directory"]=root+@"\files",["PrivateFiles:KeyFile"]=root+@"\keys\files.key",
    ["PrivateFiles:KeyReference"]="hr-reports-r1",["MobileAuthentication:Sms:Provider"]="LocalTesting",
    ["Jwt:Issuer"]="Cosmix.HrReports.R1",["Jwt:Audience"]="Cosmix.HrReports.R1",
    ["MobileAuthentication:Crypto:KeyVersion"]="hr-r1",["AttendanceJobs:Enabled"]="false",
    ["SelfHostedFace:Enabled"]="false",["FaceCapture:Enabled"]="false",["FaceTemplateStorage:Enabled"]="false",
    ["FaceStorageRetention:Enabled"]="false",["FaceStorageRetentionJournal:Enabled"]="false",["EmployeeFileScanner:Enabled"]="false"
};
IConfiguration Config(Dictionary<string,string?>? values=null) => new ConfigurationBuilder().AddInMemoryCollection(values ?? Settings()).Build();
void Validate(Dictionary<string,string?> settings) => Call(business,"ValidateConfiguration",null,Config(settings),root+@"\config\host.json");
void Reject(Action action, string label)
{
    try { action(); } catch (InvalidOperationException) { passed++; return; }
    throw new Exception("Accepted: "+label);
}
object Boundary() => Activator.CreateInstance(boundaryType,BindingFlags.NonPublic|BindingFlags.Instance,null,[Config()],null)!;
async Task<(int Status,bool Reached)> Send(object boundary,string method,string path,string body="",string? query=null,
    Action<DefaultHttpContext>? change=null,string? response=null,int responseStatus=200,bool authorize=false)
{
    var context = new DefaultHttpContext();
    context.Request.Method=method; context.Request.Scheme="https"; context.Request.Host=new HostString("localhost",7481);
    context.Connection.RemoteIpAddress=IPAddress.Loopback; context.Request.Path=path;
    context.Request.QueryString=new QueryString(query);
    context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(body)); context.Request.ContentLength=Encoding.UTF8.GetByteCount(body);
    context.Response.Body=new MemoryStream(); context.RequestServices=new NoServices();
    change?.Invoke(context);
    var reached=false;
    RequestDelegate downstream=async c =>
    {
        reached=true; c.Response.StatusCode=responseStatus;
        if(response is not null) await c.Response.WriteAsync(response);
    };
    RequestDelegate pipeline=authorize ? c => (Task)Call(boundaryType,"AuthorizeActorAsync",boundary,c,downstream)! : downstream;
    await (Task)Call(boundaryType,"InvokeAsync",boundary,context,pipeline)!;
    return (context.Response.StatusCode,reached);
}

try
{
    Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST",profile);
    Validate(Settings()); Check(true,"valid R1 config");
    foreach (var (key,value) in new (string,string)[] {
        ("AttendanceJobs:Enabled","true"),("Jwt:Issuer","other"),("Jwt:Audience","other"),
        ("MobileAuthentication:Crypto:KeyVersion","other"),("PrivateFiles:KeyReference","other"),
        ("LocalTesting:PrivateDirectory",root+"other"),("Urls","https://localhost:7482"),("AllowedHosts","*"),
        ("SelfHostedFace:Enabled","true"),("FaceCapture:Enabled","true"),("EmployeeFileScanner:Enabled","true"),
        ("MobileAuthentication:Sms:Endpoint","https://external.invalid"),("Unknown:Key","true"),
        ("AttendanceJobs:BatchSize","1") })
    {
        var settings=Settings(); settings[key]=value; Reject(()=>Validate(settings),key);
    }
    var services=new ServiceCollection();
    services.AddSingleton<IHostedService,NoWorker>(); services.AddSingleton(new AttendanceJobOptions());
    services.AddScoped<IDurableJobRepository,DurableJobRepository>();
    Call(host,"ConfigureServices",null,services);
    Check(!services.Any(s=>s.ServiceType==typeof(IHostedService)),"No hosted workers including OTP or DP");
    var options=(AttendanceJobOptions)services.Single(s=>s.ServiceType==typeof(AttendanceJobOptions)).ImplementationInstance!;
    Check(options.Enabled && options.MaximumAttempts==1 && options.BatchSize==1,"Bounded in-memory dispatch");
    Check(services.Single(s=>s.ServiceType==typeof(IDurableJobRepository)).ImplementationType==typeof(DurableJobRepository),
        "Real registered repository unchanged");
    Check(services.Count(s=>s.ServiceType.FullName=="Cosmix.Api.Development.HrReportTestingDispatcher")==1,"Only scoped dispatcher");
    foreach (var suffix in new[] {"","?employeeId="+employee,"?employeeId="+employee+"&page=1&pageSize=2",
        "?employeeId="+Guid.NewGuid()+"&page=1&pageSize=1","?employeeId="+employee+"&page=1&pageSize=1&employeeId="+employee,
        "?employeeId="+employee+"&page=1&pageSize=1&extra=1","?employeeId="+employee+"&page=0&pageSize=1"})
    {
        var response=await Send(Boundary(),"GET",report,query:suffix);
        Check(!response.Reached && response.Status==404,"Reject broad/ambiguous preview");
    }
    foreach (var page in new[]{1,2})
    {
        var response=await Send(Boundary(),"GET",report,query:$"?employeeId={employee}&page={page}&pageSize=1");
        Check(response.Reached,"Exact preview page admitted to downstream authorization");
    }
    foreach (var path in new[]{"/api/v1/hr/employees", "/api/v1/hr/reports/audit", "/api/v1/hr/reports/types",
        "/api/v1/hr/reports/exports/"+Guid.NewGuid(), "/api/v1/hr/vehicles"})
        Check(!(await Send(Boundary(),"GET",path)).Reached,"Unknown/unbound route denied");
    foreach (var change in new Action<DefaultHttpContext>[] {
        c=>c.Request.Scheme="http",c=>c.Request.Host=new("localhost",7482),c=>c.Connection.RemoteIpAddress=IPAddress.Parse("10.0.0.1"),
        c=>c.Request.Headers.Range="bytes=0-1",c=>c.Request.Headers["Content-Encoding"]="gzip",
        c=>c.Request.Headers["Idempotency-Key"]="unwanted" })
        Check(!(await Send(Boundary(),"GET",report,query:$"?employeeId={employee}&page=1&pageSize=1",change:change)).Reached,
            "Transport/header bound");
    foreach (var format in new[]{1,2,3})
    {
        var value=JsonSerializer.SerializeToElement(new { format,reason="TEST ONLY R1 report acceptance",
            filter=new {employeeId=employee,page=1,pageSize=1} });
        Check((bool)Call(boundaryType,"ValidExport",null,value,false)! == (format!=3),"Page formats");
        Check((bool)Call(boundaryType,"ValidExport",null,value,true)! == (format==3),"Durable PDF only");
    }
    foreach (var body in new[]{
        """{"format":3,"reason":"TEST ONLY R1 report acceptance","filter":{}}""",
        """{"format":3,"reason":"TEST ONLY R1 report acceptance","filter":{"employeeId":"35d19922-70a8-4c80-8578-e1029360e9fc","page":1,"pageSize":1,"Page":1}}""",
        """{"format":3,"format":3,"reason":"TEST ONLY R1 report acceptance","filter":{"employeeId":"35d19922-70a8-4c80-8578-e1029360e9fc","page":1,"pageSize":1}}""" })
    {
        using var document=JsonDocument.Parse(body);
        Check(!(bool)Call(boundaryType,"ValidExport",null,document.RootElement,true)!,"Reject ambiguous/broad body");
    }
    var budget=Boundary();
    for(var i=0;i<60;i++) Check((await Send(budget,"GET","/unknown")).Status==404,"Count rejected requests");
    Check((await Send(budget,"GET","/unknown")).Status==503,"Request 61 refused");
    var actor=new AuthenticatedAccount(new("8475187f-bf3b-4f9c-a3ed-4bac32253005"),new(employee),Guid.NewGuid(),1,SessionClient.Web,true);
    Check((bool)Call(guard,"IsActor",null,actor)!,"HR actor allowed");
    foreach(var other in new[]{actor with {AccountId=Guid.NewGuid()},actor with {EmployeeId=Guid.NewGuid()},
        actor with {SessionId=Guid.Empty},actor with {MfaSatisfied=false}})
        Check(!(bool)Call(guard,"IsActor",null,other)!,"Other actor/MFA rejected");
    var anonymous=new DefaultHttpContext(); anonymous.Request.Path=report;
    var reached=false;
    await (Task)Call(boundaryType,"AuthorizeActorAsync",Boundary(),anonymous,
        (RequestDelegate)(_=>{reached=true;return Task.CompletedTask;}))!;
    Check(!reached && anonymous.Response.StatusCode==401,"Anonymous denied before controller or dispatcher");
    string RequestBody() => JsonSerializer.Serialize(new {format=3,reason="TEST ONLY R1 report acceptance",
        filter=new {employeeId=employee,page=1,pageSize=1}});
    string JobResponse(Guid id) => JsonSerializer.Serialize(new {id,reportType="employee-master",state="pending",format=3,
        requestedAt="2026-10-20T08:00:00+00:00",generatedAt=(string?)null,downloadExpiresAt=(string?)null,
        failureCode=(string?)null,totalCount=(int?)null,returnedCount=(int?)null});
    Action<DefaultHttpContext> exportHeader=c=>c.Request.Headers["Idempotency-Key"]="TEST-HR-R1-20261020-export";
    var captured=Boundary(); var capturedId=Guid.NewGuid();
    // Response-boundary contract only: synthetic downstream responses do not claim authentication.
    Check((await Send(captured,"POST",report+"/export",RequestBody(),change:exportHeader,
        response:JobResponse(capturedId),responseStatus:202)).Status==202,"Capture actual lowercase pending202");
    Check((await Send(captured,"POST",report+"/export",RequestBody(),change:exportHeader,
        response:JobResponse(capturedId),responseStatus:202)).Status==202,"Capture identical replay202");
    Check((await Send(captured,"POST",report+"/export",RequestBody(),change:exportHeader,
        response:JobResponse(capturedId),responseStatus:202)).Status==409,"Third export denied");
    var statusPath="/api/v1/hr/reports/exports/"+capturedId.ToString("D");
    var deniedStatus=await Send(captured,"GET",statusPath,authorize:true);
    Check(deniedStatus.Status==401 && !deniedStatus.Reached,"Captured job status without actual MFA cannot dispatch");
    Check((await Send(captured,"GET",statusPath,authorize:true)).Status==401,"Denied status consumes no dispatch latch");
    Check((await Send(captured,"GET",statusPath,response:JobResponse(capturedId))).Status==503,
        "Forged successful downstream status without bound actor fails closed before services");
    Check((await Send(captured,"GET",report,query:$"?employeeId={employee}&page=1&pageSize=1")).Status==503,
        "Uncertain status permanently latches boundary");
    var changedReplay=Boundary();
    await Send(changedReplay,"POST",report+"/export",RequestBody(),change:exportHeader,response:JobResponse(capturedId),responseStatus:202);
    Check((await Send(changedReplay,"POST",report+"/export",RequestBody(),change:exportHeader,
        response:JobResponse(Guid.NewGuid()),responseStatus:202)).Status==503,"Different replay job rejected");
    Check((await Send(changedReplay,"GET",report,query:$"?employeeId={employee}&page=1&pageSize=1")).Status==503,
        "Changed replay latches uncertainty");
    var loginBoundary=Boundary(); var challenge=Guid.NewGuid();
    Action<DefaultHttpContext> browserCookie=c=>c.Request.Headers.Cookie="__Host-Cosmix.Browser="+new string('a',43);
    var login=await Send(loginBoundary,"POST","/api/v1/auth/web/login",
        """{"email":"hr@example.invalid","password":"synthetic-offline-only"}""",change:browserCookie,
        response:JsonSerializer.Serialize(new {challengeId=challenge}),responseStatus:202);
    Check(login.Status==202,"Capture login challenge response");
    Check((await Send(loginBoundary,"POST","/api/v1/auth/web/local-testing/otp",
        JsonSerializer.Serialize(new {challengeId=Guid.NewGuid()}),change:browserCookie)).Status==400,
        "Other OTP challenge never reaches delivery or controller");
    Console.WriteLine($"R1 native profile/boundary: {passed} offline checks passed; no host/SQL/private IO.");
}
finally { Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST",previous); }

sealed class NoServices : IServiceProvider { public object? GetService(Type type) => throw new Exception("Unexpected service resolution: "+type.Name); }
sealed class NoWorker : IHostedService
{
    public Task StartAsync(CancellationToken ct)=>throw new Exception("Host startup forbidden");
    public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
}
