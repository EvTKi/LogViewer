using Microsoft.AspNetCore.Mvc;
using LogMonitor.Core.Dtos;

namespace LogMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErrorsController : ControllerBase
{
    // Позже внедрим сервис, а пока — заглушка
    [HttpGet]
    public IActionResult GetErrors()
    {
        // Возвращаем пустой список — пока нет логики мониторинга
        return Ok(new List<ErrorDto>());
    }
}