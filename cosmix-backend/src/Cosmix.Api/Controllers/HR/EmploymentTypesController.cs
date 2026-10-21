using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[Route("api/v1/hr/employment-types")]
public sealed class EmploymentTypesController(OrganizationMasterService service)
    : OrganizationMasterControllerBase(service, OrganizationMasterKind.EmploymentType);
