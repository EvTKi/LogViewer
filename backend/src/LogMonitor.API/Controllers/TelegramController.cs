using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogMonitor.Infrastructure.Services;
using LogMonitor.Infrastructure.Data;

namespace LogMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramController : ControllerBase
{
    private readonly TelegramService _telegramService;
    private readonly LogMonitorDbContext _dbContext; // ← теперь компилятор знает, откуда этот тип

    public TelegramController(TelegramService telegramService, LogMonitorDbContext dbContext)
    {
        _telegramService = telegramService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Отправить тестовое сообщение ВСЕМ активным подписчикам
    /// </summary>
    [HttpPost("send-test")]
    public async Task<IActionResult> SendTestMessage([FromBody] TelegramTestMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Сообщение не может быть пустым");

        await _telegramService.SendToAllSubscribersAsync(request.Message);
        return Ok(new { success = true, message = "Тестовое сообщение отправлено" });
    }

    /// <summary>
    /// Получить список активных подписчиков
    /// </summary>
    [HttpGet("subscribers")]
    public async Task<IActionResult> GetSubscribers()
    {
        var subscribers = await _dbContext.TelegramSubscribers
            .Where(s => s.IsActive)
            .Select(s => new
            {
                s.ChatId,
                s.Username,
                s.FirstName,
                s.SubscribedAt
            })
            .ToListAsync();

        return Ok(subscribers);
    }
}

public record TelegramTestMessageRequest(string Message);