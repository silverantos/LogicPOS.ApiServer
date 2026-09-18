using LogicPOS.ApiServer.Data.Entities;
using LogicPOS.ApiServer.DTOs;
using LogicPOS.ApiServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogicPOS.ApiServer.Controllers;

// The client's session/cash-drawer commands (OpenTerminalSession, CloseTerminalSession, cash-drawer
// in/out) carry no explicit TerminalId — the server is expected to derive it from the caller's JWT
// ("terminalId" claim, set at login by JwtTokenGenerator). A "?terminalId=" query fallback is also
// accepted so these can be exercised without a full login flow.
//
// The client is also inconsistent about the route root: most calls use "worksessions" (plural), but
// OpenDay/CloseDay/GetLastWorkSessionByTerminalId use "worksession" (singular) — and the singular form
// also uses "period" instead of "periods" for one of them. Both prefixes are registered so every call
// works regardless.
[AllowAnonymous]
[Route("worksessions")]
[Route("worksession")]
[Route("api/worksessions")]
[Route("api/worksession")]
public sealed class WorkSessionsController : ApiControllerBase
{
    private readonly WorkSessionService _workSessionService;

    public WorkSessionsController(WorkSessionService workSessionService)
    {
        _workSessionService = workSessionService;
    }

    private Guid? ResolveTerminalId(Guid? queryTerminalId)
    {
        var claim = User.FindFirst("terminalId")?.Value;
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var claimTerminalId))
        {
            return claimTerminalId;
        }

        return queryTerminalId;
    }

    // --- Day period ---

    [HttpGet("periods/day-is-open")]
    public async Task<IActionResult> DayIsOpen(CancellationToken cancellationToken) => Ok(await _workSessionService.DayIsOpenAsync(cancellationToken));

    [HttpPost("periods/open-day")]
    public async Task<IActionResult> OpenDay([FromBody] OpenWorkSessionDayRequest request, CancellationToken cancellationToken)
    {
        var (success, error, response) = await _workSessionService.OpenDayAsync(request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { errors = new[] { new { name = "day", reason = error } } });
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("periods/close-day")]
    public async Task<IActionResult> CloseDay([FromBody] CloseWorkSessionDayRequest request, CancellationToken cancellationToken)
    {
        var (success, error) = await _workSessionService.CloseDayAsync(request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { errors = new[] { new { name = "day", reason = error } } });
        }

        return NoContent();
    }

    [HttpPut("periods/close-all-sessions")]
    public async Task<IActionResult> CloseAllSessions(CancellationToken cancellationToken)
    {
        await _workSessionService.CloseAllSessionsAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("periods/lastday")]
    public async Task<IActionResult> GetLastClosedDay(CancellationToken cancellationToken)
    {
        var day = await _workSessionService.GetLastClosedDayAsync(cancellationToken);
        return day is null ? NotFound() : Ok(day);
    }

    [HttpGet("periods/alldays")]
    public async Task<IActionResult> GetAllClosedDays(CancellationToken cancellationToken) => Ok(await _workSessionService.GetAllClosedDaysAsync(cancellationToken));

    // --- Terminal session ---

    [HttpGet("periods/terminal-is-open")]
    public async Task<IActionResult> TerminalIsOpen([FromQuery] Guid terminalId, CancellationToken cancellationToken)
    {
        return Ok(await _workSessionService.TerminalIsOpenAsync(terminalId, cancellationToken));
    }

    [HttpPost("periods/open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenTerminalSessionRequest request, [FromQuery] Guid? terminalId, CancellationToken cancellationToken)
    {
        var resolvedTerminalId = ResolveTerminalId(terminalId);
        if (resolvedTerminalId is null)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = "Sem TerminalId (autentique-se ou indique ?terminalId=)." } } });
        }

        var (success, error, response) = await _workSessionService.OpenTerminalSessionAsync(resolvedTerminalId.Value, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = error } } });
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("periods/close-session")]
    public async Task<IActionResult> CloseSession([FromBody] CloseTerminalSessionRequest request, [FromQuery] Guid? terminalId, CancellationToken cancellationToken)
    {
        var resolvedTerminalId = ResolveTerminalId(terminalId);
        if (resolvedTerminalId is null)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = "Sem TerminalId (autentique-se ou indique ?terminalId=)." } } });
        }

        var (success, error) = await _workSessionService.CloseTerminalSessionAsync(resolvedTerminalId.Value, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = error } } });
        }

        return NoContent();
    }

    [HttpGet("periods/open-terminal-sessions")]
    public async Task<IActionResult> GetOpenTerminalSessions(CancellationToken cancellationToken) => Ok(await _workSessionService.GetOpenTerminalSessionsAsync(cancellationToken));

    [HttpGet("period/terminal/{terminalId:guid}")]
    public async Task<IActionResult> GetLastWorkSessionByTerminalId(Guid terminalId, CancellationToken cancellationToken)
    {
        var session = await _workSessionService.GetLastWorkSessionByTerminalIdAsync(terminalId, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    // --- Cash drawer movements ---

    [HttpPost("movements/cash-drawer-in")]
    public async Task<IActionResult> CashDrawerIn([FromBody] CashDrawerMovementRequest request, [FromQuery] Guid? terminalId, CancellationToken cancellationToken)
    {
        var resolvedTerminalId = ResolveTerminalId(terminalId);
        if (resolvedTerminalId is null)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = "Sem TerminalId (autentique-se ou indique ?terminalId=)." } } });
        }

        var (success, error, response) = await _workSessionService.AddCashDrawerMovementAsync(resolvedTerminalId.Value, ApiWorkSessionMovementType.CashDrawerIn, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = error } } });
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("movements/cash-drawer-out")]
    public async Task<IActionResult> CashDrawerOut([FromBody] CashDrawerMovementRequest request, [FromQuery] Guid? terminalId, CancellationToken cancellationToken)
    {
        var resolvedTerminalId = ResolveTerminalId(terminalId);
        if (resolvedTerminalId is null)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = "Sem TerminalId (autentique-se ou indique ?terminalId=)." } } });
        }

        var (success, error, response) = await _workSessionService.AddCashDrawerMovementAsync(resolvedTerminalId.Value, ApiWorkSessionMovementType.CashDrawerOut, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { errors = new[] { new { name = "terminalId", reason = error } } });
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("movements/total-cash-in-drawer/{terminalId:guid}")]
    public async Task<IActionResult> GetTotalCashInDrawer(Guid terminalId, CancellationToken cancellationToken)
    {
        var total = await _workSessionService.GetTotalCashInDrawerAsync(terminalId, cancellationToken);
        return Ok(total);
    }
}
