#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Api.Attendance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

// Ephemeral keys and synthetic references only; no SQL, biometric files or production configuration.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Fails(Action action, string code)
{
    try { action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure ex) { Check(ex.Code == code, code); }
}
var clock = new Clock();
var keys = new EphemeralDataProtectionProvider();
var protector = new FaceCaptureProtector(keys, clock);
var binding = new FaceCaptureBinding(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
    Guid.NewGuid(), new('A', 64));
var reference = new PrivateFileReference("synthetic-object", "synthetic-version", new('B', 64), "synthetic-key", 123);
var envelope = new FaceCaptureEnvelope(1, Guid.NewGuid(), binding, reference, "image/png", clock.Now, clock.Now.AddSeconds(60), []);
var token = protector.Protect(envelope);
Check(token.Length < 4096 && !token.Contains("synthetic-object"), "Opaque token bounded and contains no readable private reference");
Check(protector.Unprotect(token, binding) == envelope || protector.Unprotect(token, binding).Binding == binding, "Exact bound round trip");
Fails(() => protector.Unprotect(token, binding with { AccountId = Guid.NewGuid() }), "invalid_face_capture");
Fails(() => protector.Unprotect(token, binding with { SessionId = Guid.NewGuid() }), "invalid_face_capture");
Fails(() => protector.Unprotect(token, binding with { EmployeeId = Guid.NewGuid() }), "invalid_face_capture");
Fails(() => protector.Unprotect(token, binding with { DeviceId = Guid.NewGuid() }), "invalid_face_capture");
Fails(() => protector.Unprotect(token, binding with { AttemptId = Guid.NewGuid() }), "invalid_face_capture");
Fails(() => protector.Unprotect(token, binding with { ChallengeSha256 = new('C',64) }), "invalid_face_capture");
Fails(() => protector.Unprotect(token + "=", binding), "invalid_face_capture");
Fails(() => protector.Unprotect(token[..^3] + "AAA", binding), "invalid_face_capture");
Fails(() => protector.Unprotect(new('A',4097), binding), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { Version = 2 }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { CaptureId = Guid.Empty }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { Binding = binding with { AttemptId = null } }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { ExpiresAtUtc = clock.Now.AddSeconds(301) }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { IssuedAtUtc = clock.Now.AddSeconds(1) }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { MediaType = "image/gif" }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { Image = reference with { SizeBytes = 524289 } }), "invalid_face_capture");
Fails(() => protector.Protect(envelope with { Image = reference with { Sha256 = new('Z',64) } }), "invalid_face_capture");
var enrollment = binding with { AttemptId = null, ChallengeSha256 = null };
Check(protector.Unprotect(protector.Protect(envelope with { Binding = enrollment }), enrollment).Binding == enrollment, "Enrollment audience is distinct from verification");
Fails(() => protector.Unprotect(token, enrollment), "invalid_face_capture");
clock.Now = envelope.ExpiresAtUtc;
Fails(() => protector.Unprotect(token, binding), "face_capture_expired");
Fails(() => new FaceCaptureProtector(new EphemeralDataProtectionProvider(), new Clock()).Unprotect(token, binding), "invalid_face_capture");
async Task JsonCheck(string json, int? expectedStatus)
{
    var http = new DefaultHttpContext();
    http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
    var descriptor = new ControllerActionDescriptor {
        Parameters = new List<ParameterDescriptor> { new ControllerParameterDescriptor { ParameterType = typeof(UploadVerificationFaceCaptureRequest) } }
    };
    var action = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
    var context = new ResourceExecutingContext(action, [], []);
    var called = false;
    await new StrictFaceCaptureJsonAttribute().OnResourceExecutionAsync(context, async () => {
        called = true;
        using var reader = new StreamReader(http.Request.Body, leaveOpen:true);
        Check(await reader.ReadToEndAsync() == json, "Binder sees original capture JSON");
        return new ResourceExecutedContext(action, []);
    });
    Check((context.Result as ObjectResult)?.StatusCode == expectedStatus && called == (expectedStatus is null), "Strict capture JSON result");
}
var validJson = "{\"challenge\":\"synthetic\",\"expectedVersion\":\"AAAAAAAAAAA=\",\"imageBase64\":\"iVBORw0KGgo=\"}";
await JsonCheck(validJson, null);
await JsonCheck(validJson.Replace("\"challenge\":\"synthetic\"", "\"challenge\":\"synthetic\",\"challenge\":\"changed\""), 400);
await JsonCheck(validJson.Replace("\"challenge\":\"synthetic\"", "\"challenge\":\"synthetic\",\"\\u0063hallenge\":\"changed\""), 400);
await JsonCheck(validJson.Replace("\"imageBase64\"", "\"ImageBase64\""), 400);
await JsonCheck(validJson.Replace("}", ",\"livenessPassed\":true}"), 400);
await JsonCheck("{}", 400);
await JsonCheck("null", 400);
await JsonCheck("{", 400);
await JsonCheck(new(' ',710001), 413);
Console.WriteLine($"Face capture protection/JSON: {checks} passed (ephemeral cryptography and real MVC filter; no SQL/provider/device acceptance).");
sealed class Clock : TimeProvider
{
    public DateTimeOffset Now = new(2026,10,12,10,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
