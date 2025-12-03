using Microsoft.AspNetCore.Mvc;
using LogMonitor.Core.Dtos;

namespace LogMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    [HttpPost]
    public IActionResult Configure([FromBody] ConfigureRequest request)
    {
        // Позже обработаем настройку
        return Ok(new { Success = true });
    }
}