using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[Route("api/v1/hr/staff-categories")]
public sealed class StaffCategoriesController(OrganizationMasterService service)
    : OrganizationMasterControllerBase(service, OrganizationMasterKind.StaffCategory);
