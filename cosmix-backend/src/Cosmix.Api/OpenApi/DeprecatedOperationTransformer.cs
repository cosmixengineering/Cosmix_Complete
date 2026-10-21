using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cosmix.Api.OpenApi;

public sealed class DeprecatedOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor action &&
            action.MethodInfo.GetCustomAttribute<ObsoleteAttribute>() is not null)
            operation.Deprecated = true;
        return Task.CompletedTask;
    }
}
