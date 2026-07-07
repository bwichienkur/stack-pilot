using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    private bool IsPlatformSuperAdmin() =>
        User.Claims
            .Where(c => c.Type == StackPilotClaimTypes.OrgRole)
            .Any(c => c.Value.Contains("PlatformSuperAdmin", StringComparison.OrdinalIgnoreCase));

    [HttpGet("organizations")]
    public async Task<ActionResult<ApiResponse<List<AdminOrganizationSummaryDto>>>> ListOrganizations(CancellationToken ct)
    {
        if (!IsPlatformSuperAdmin())
            return Forbid();

        return Ok(ApiResponse<List<AdminOrganizationSummaryDto>>.Ok(await _admin.ListOrganizationsAsync(ct)));
    }

    [HttpGet("organizations/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminOrganizationDetailDto>>> GetOrganization(Guid id, CancellationToken ct)
    {
        if (!IsPlatformSuperAdmin())
            return Forbid();

        var org = await _admin.GetOrganizationDetailAsync(id, ct);
        return org is null ? NotFound() : Ok(ApiResponse<AdminOrganizationDetailDto>.Ok(org));
    }

    [HttpPut("organizations/{id:guid}/plan")]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> OverridePlan(Guid id, [FromBody] OverridePlanRequest request, CancellationToken ct)
    {
        if (!IsPlatformSuperAdmin())
            return Forbid();

        return Ok(ApiResponse<OrganizationDto>.Ok(await _admin.OverridePlanAsync(id, request, ct)));
    }
}
