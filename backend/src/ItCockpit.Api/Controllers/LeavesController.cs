using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/leaves")]
[Authorize]
public sealed class LeavesController : ControllerBase
{
    private readonly LeaveService _leaves;

    public LeavesController(LeaveService leaves) => _leaves = leaves;

    /// <summary>Takvim görünümü için tarih aralığındaki izinleri döner.</summary>
    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<LeaveCalendarItemDto>>> Calendar(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct) =>
        Ok(await _leaves.GetCalendarAsync(startDate, endDate, ct));

    /// <summary>Tüm izin talepleri (yönetici görünümü, filtreli).</summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetAll(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] LeaveStatus? status,
        CancellationToken ct) =>
        Ok(await _leaves.GetAllLeavesAsync(startDate, endDate, status, ct));

    /// <summary>Kullanıcının kendi izin geçmişi.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> MyLeaves(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _leaves.GetMyLeavesAsync(userId.Value, ct));
    }

    /// <summary>Yeni izin talebi oluşturur.</summary>
    [HttpPost]
    public async Task<ActionResult<LeaveRequestDto>> Create(
        [FromBody] CreateLeaveRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _leaves.CreateAsync(userId.Value, request, ct));
    }

    /// <summary>Yönetici izin talebini onaylar veya reddeder.</summary>
    [HttpPost("{id:guid}/decision")]
    [Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
    public async Task<ActionResult<LeaveRequestDto>> Decide(
        Guid id, [FromBody] LeaveDecisionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _leaves.DecideAsync(id, userId.Value, request, ct));
    }

    /// <summary>Kullanıcı kendi bekleyen talebini iptal eder.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await _leaves.CancelAsync(id, userId.Value, ct);
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
