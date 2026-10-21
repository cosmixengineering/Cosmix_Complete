using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[Route("api/v1/hr/branches")]
public sealed class BranchesController(OrganizationMasterService service)
    : OrganizationMasterControllerBase(service, OrganizationMasterKind.Branch);
