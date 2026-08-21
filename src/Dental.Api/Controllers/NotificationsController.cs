using Dental.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// Uygulama içi bildirimler (topbar zili). İzin gerektirmez — kullanıcı yalnız kendisine
/// ya da kiracı geneline düşen bildirimleri görür.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await notifications.ListAsync(unreadOnly, page, pageSize, ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<object>> MarkAllRead(CancellationToken ct)
        => Ok(new { updated = await notifications.MarkAllReadAsync(ct) });
}
