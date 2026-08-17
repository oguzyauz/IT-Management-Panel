using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItCockpit.Api.Controllers;

/// <summary>
/// Ticket uçları. Müdür tüm kayıtları görür; çalışan yalnızca <b>kendine atanmış</b> olanları —
/// kapsam daraltması <see cref="TicketService"/> içinde sunucu tarafında zorlanır, rol
/// niteliklerine güvenilmez.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController : ControllerBase
{
    private const string ManagerRoles = $"{RoleCodes.Manager},{RoleCodes.Admin}";

    private readonly TicketService _tickets;

    public TicketsController(TicketService tickets) => _tickets = tickets;

    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] TicketStatus[]? status,
        [FromQuery] Guid? assigneeUserId,
        [FromQuery] bool? unassigned,
        [FromQuery] int? priority,
        [FromQuery] string? applicationName,
        [FromQuery] AgingLevel? minAging,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sortBy = "OriginalSentAtUtc",
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var query = new TicketQuery(
            search, status, assigneeUserId, unassigned, priority,
            applicationName, minAging, page, pageSize, sortBy, sortDescending);

        return Ok(await _tickets.SearchAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _tickets.GetAsync(id, ct));

    /// <summary>
    /// Panelden elle ticket oluşturur — maili düşmemiş ama Tixbox'ta var olan kayıtlar için.
    /// Tixbox'a hiçbir şey yazılmaz.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<TicketDetailDto>> Create(
        [FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var created = await _tickets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Ticket'ı bir çalışana atar veya yeniden atar. Tixbox'a hiçbir şey yazılmaz.</summary>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<TicketDetailDto>> Assign(
        Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct) =>
        Ok(await _tickets.AssignAsync(id, request, ct));

    /// <summary>
    /// Panel içi takip durumunu değiştirir.
    /// Bu durum <b>yalnızca yönetim panelindeki</b> takip durumudur; Tixbox durumunu değiştirmez.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<TicketDetailDto>> ChangeStatus(
        Guid id, [FromBody] ChangeStatusRequest request, CancellationToken ct) =>
        Ok(await _tickets.ChangeStatusAsync(id, request, ct));

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<TicketNoteDto>> AddNote(
        Guid id, [FromBody] AddNoteRequest request, CancellationToken ct) =>
        Ok(await _tickets.AddNoteAsync(id, request, ct));

    [HttpGet("warnings")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<ActionResult<IReadOnlyList<ParseWarningDto>>> Warnings(
        [FromQuery] bool onlyUnacknowledged = true, CancellationToken ct = default) =>
        Ok(await _tickets.GetWarningsAsync(onlyUnacknowledged, ct));

    [HttpPost("warnings/{warningId:guid}/acknowledge")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<IActionResult> AcknowledgeWarning(Guid warningId, CancellationToken ct)
    {
        await _tickets.AcknowledgeWarningAsync(warningId, ct);
        return NoContent();
    }
}
