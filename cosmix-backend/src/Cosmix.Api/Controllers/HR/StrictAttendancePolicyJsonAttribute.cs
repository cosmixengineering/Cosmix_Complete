using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.HR.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Controllers.HR;

/// <summary>Reject ambiguous policy JSON before MVC discards duplicate members during binding.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictAttendancePolicyJsonAttribute(Type? requestType = null) : Attribute, IAsyncResourceFilter
{
    private readonly Type requestContract = requestType ?? typeof(ValidateAttendancePolicySettingsRequest);
    private const int MaximumBytes = 32 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16
    };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaximumBytes)
        {
            context.Result = Failure(StatusCodes.Status413PayloadTooLarge, "Policy settings exceed the request limit.");
            return;
        }

        // Read only one byte beyond the limit, including for chunked bodies. Keep this small body in memory.
        request.EnableBuffering(bufferThreshold: MaximumBytes + 1, bufferLimit: MaximumBytes + 1);
        var bytes = new byte[MaximumBytes + 1];
        var length = 0;
        try
        {
            while (length < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (read == 0) break;
                length += read;
            }

            if (length > MaximumBytes)
            {
                context.Result = Failure(StatusCodes.Status413PayloadTooLarge, "Policy settings exceed the request limit.");
                return;
            }

            if (JsonSerializer.Deserialize(bytes.AsSpan(0, length), requestContract, Options) is null)
                throw new JsonException();
        }
        catch (JsonException)
        {
            context.Result = Failure(StatusCodes.Status400BadRequest, "Policy settings must contain unambiguous, valid JSON fields.");
            return;
        }
        finally
        {
            request.Body.Position = 0;
        }

        await next();
    }

    private static ObjectResult Failure(int status, string title) =>
        new(new ProblemDetails { Status = status, Title = title }) { StatusCode = status };
}
