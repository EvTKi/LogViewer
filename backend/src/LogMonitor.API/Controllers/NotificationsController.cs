using Microsoft.AspNetCore.Mvc;
using LogMonitor.Core.Dtos;

namespace LogMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetNotifications()
    {
        return Ok(new List<NotificationDto>());
    }

    [HttpPatch("{id}/read")]
    public IActionResult MarkAsRead(int id)
    {
        return Ok(new { IsRead = true });
    }
}