using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[Route("api/v1/hr/cities")]
public sealed class CitiesController(OrganizationMasterService service)
    : OrganizationMasterControllerBase(service, OrganizationMasterKind.City);
