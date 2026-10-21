using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Controllers.HR;

/// <summary>Validate bounded original bytes before MVC can discard ambiguous taxonomy fields.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictStaffTaxonomyJsonAttribute(Type contract, int maximumBytes) : Attribute, IAsyncResourceFilter
{
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
        if (request.ContentLength > maximumBytes) { context.Result = Failure(413); return; }
        // Bound chunked bodies too; retaining this small buffer permits MVC's subsequent typed read.
        request.EnableBuffering(bufferThreshold: maximumBytes + 1, bufferLimit: maximumBytes + 1);
        var bytes = new byte[maximumBytes + 1];
        var length = 0;
        try
        {
            while (length < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (read == 0) break;
                length += read;
            }
            if (length > maximumBytes) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize(bytes.AsSpan(0, length), contract, Options) is null) throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            request.Body.Position = 0;
        }
        await next();
    }

    private static ObjectResult Failure(int status) => new(new ProblemDetails
    {
        Status = status,
        Title = status == 413 ? "Staff taxonomy request exceeds its byte limit." : "Staff taxonomy request requires unambiguous explicit typed JSON."
    }) { StatusCode = status };
}
