using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[Route("api/v1/hr/designations")]
public sealed class DesignationsController(OrganizationMasterService service)
    : OrganizationMasterControllerBase(service, OrganizationMasterKind.Designation);
