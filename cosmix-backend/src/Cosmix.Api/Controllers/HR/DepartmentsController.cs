using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[Route("api/v1/hr/departments")]
public sealed class DepartmentsController(OrganizationMasterService service)
    : OrganizationMasterControllerBase(service, OrganizationMasterKind.Department);
