using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/schedule")]
[Authorize]
public sealed class ScheduleController : ControllerBase
{
    private const string ManagerRoles = $"{RoleCodes.Manager},{RoleCodes.Admin}";

    private readonly WorkScheduleService _schedule;

    public ScheduleController(WorkScheduleService schedule) => _schedule = schedule;

    /// <summary>Çalışanın kendi haftalık planı. <c>weekStart</c> verilmezse gelecek hafta.</summary>
    [HttpGet("my-week")]
    public async Task<ActionResult<MyWeekDto>> GetMyWeek([FromQuery] DateOnly? weekStart, CancellationToken ct) =>
        Ok(await _schedule.GetMyWeekAsync(weekStart, ct));

    [HttpPut("my-week")]
    public async Task<ActionResult<MyWeekDto>> SaveMyWeek(
        [FromBody] SaveMyWeekRequest request, CancellationToken ct) =>
        Ok(await _schedule.SaveMyWeekAsync(request, submit: false, ct));

    [HttpPost("my-week/submit")]
    public async Task<ActionResult<MyWeekDto>> SubmitMyWeek(
        [FromBody] SaveMyWeekRequest request, CancellationToken ct) =>
        Ok(await _schedule.SaveMyWeekAsync(request, submit: true, ct));

    [HttpGet("team")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<WeeklyScheduleMatrixDto>> GetTeamMatrix(
        [FromQuery] DateOnly? weekStart, CancellationToken ct) =>
        Ok(await _schedule.GetTeamMatrixAsync(weekStart, ct));

    [HttpGet("today")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<TodayTeamStatusDto>> GetToday(CancellationToken ct) =>
        Ok(await _schedule.GetTodayStatusAsync(ct));

    [HttpGet("user/{userId:guid}")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<MyWeekDto>> GetUserWeek(
        Guid userId, [FromQuery] DateOnly? weekStart, CancellationToken ct) =>
        Ok(await _schedule.GetWeekForUserAsync(userId, weekStart ?? _schedule.DefaultTargetWeek(), ct));

    [HttpPost("{weekId:guid}/decision")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<MyWeekDto>> Decide(
        Guid weekId, [FromBody] ScheduleDecisionRequest request, CancellationToken ct) =>
        Ok(await _schedule.DecideAsync(weekId, request, ct));

    [HttpPost("{weekId:guid}/override")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<MyWeekDto>> Override(
        Guid weekId, [FromBody] ScheduleOverrideRequest request, CancellationToken ct) =>
        Ok(await _schedule.OverrideAsync(weekId, request, ct));
}
