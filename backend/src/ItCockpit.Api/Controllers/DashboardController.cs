using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) =>
        Ok(await _dashboard.GetAsync(ct));
}
