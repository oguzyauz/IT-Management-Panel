using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
public sealed class RemindersController : ControllerBase
{
    private readonly ReminderService _reminders;

    public RemindersController(ReminderService reminders) => _reminders = reminders;

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<ReminderTemplateDto>>> Templates(CancellationToken ct) =>
        Ok(await _reminders.GetTemplatesAsync(ct));

    /// <summary>Gönderim yapmaz — yalnızca müdürün düzenleyeceği taslağı üretir.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ReminderPreviewDto>> Preview(
        [FromBody] ReminderPreviewRequest request, CancellationToken ct) =>
        Ok(await _reminders.BuildPreviewAsync(request, ct));

    /// <summary>
    /// Hatırlatmayı gönderir. <c>Confirmed = true</c> olmadan gönderim yapılmaz —
    /// müdürün açık onayı zorunludur.
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<ReminderHistoryItemDto>> Send(
        [FromBody] ReminderSendCommand command, CancellationToken ct) =>
        Ok(await _reminders.SendAsync(command, ct));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ReminderHistoryItemDto>>> History(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? recipientUserId,
        [FromQuery] ReminderStatus? status,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _reminders.GetHistoryAsync(
            new ReminderHistoryQuery(startDate, endDate, recipientUserId, status, take), ct));
}
