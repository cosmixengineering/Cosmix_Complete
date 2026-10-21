using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.FaceVerification;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Attendance;

[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictFaceCaptureJsonAttribute : Attribute, IAsyncResourceFilter
{
    private const int MaximumBytes = 710000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, AllowDuplicateProperties = false, MaxDepth = 8 };
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaximumBytes) { context.Result = Failure(413); return; }
        var action = context.ActionDescriptor as ControllerActionDescriptor;
        var type = action?.Parameters.Select(x => x.ParameterType).SingleOrDefault(x =>
            x == typeof(UploadVerificationFaceCaptureRequest) || x == typeof(UploadEnrollmentFaceCaptureRequest));
        if (type is null) throw new InvalidOperationException("Face capture JSON requires its exact request contract.");
        request.EnableBuffering(bufferThreshold: MaximumBytes + 1, bufferLimit: MaximumBytes + 1);
        var bytes = new byte[MaximumBytes + 1];
        try
        {
            var count = 0;
            while (count < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(count), context.HttpContext.RequestAborted);
                if (read == 0) break;
                count += read;
            }
            if (count > MaximumBytes) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize(bytes.AsSpan(0, count), type, Json) is null) throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        finally { CryptographicOperations.ZeroMemory(bytes); request.Body.Position = 0; }
        await next();
    }
    private static ObjectResult Failure(int status) => new(new ProblemDetails
    {
        Status = status, Title = status == 413 ? "Face capture exceeds the request limit." : "Unambiguous face capture JSON is required."
    }) { StatusCode = status };
}
