using Microsoft.AspNetCore.Mvc;
using LogMonitor.Core.Dtos;
using LogMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly LogMonitorDbContext _dbContext;

    public NotificationsController(LogMonitorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _dbContext.Notifications
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();

        var dtos = notifications.Select(n => new NotificationDto(
            n.Id,
            n.ErrorId,
            n.SentAt,
            n.IsRead,
            n.EmailSent,
            n.TelegramSent
        )).ToList();

        return Ok(dtos);
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notification = await _dbContext.Notifications.FindAsync(id);
        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync();

        return Ok(new { IsRead = true });
    }
}