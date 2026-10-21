using Cosmix.Api.Controllers.HR;
using Cosmix.Api.Controllers.Attendance;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cosmix.Api.OpenApi;

public sealed class OrganizationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor action &&
            (typeof(OrganizationMasterControllerBase).IsAssignableFrom(action.ControllerTypeInfo) ||
             action.ControllerTypeInfo == typeof(HolidayCalendarsController) ||
             action.ControllerTypeInfo == typeof(SitesController) ||
             action.ControllerTypeInfo == typeof(SiteGeofencesController) ||
             action.ControllerTypeInfo == typeof(OfficeBoundariesController) ||
             action.ControllerTypeInfo == typeof(ShiftsController)) &&
            operation.Responses?.TryGetValue("201", out var created) == true && created is OpenApiResponse response)
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers["Location"] = new OpenApiHeader
            {
                Description = "Detail URL of the created master or shift, exact calendar revision or exact geofence version.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri-reference" }
            };
        }
        return Task.CompletedTask;
    }
}
