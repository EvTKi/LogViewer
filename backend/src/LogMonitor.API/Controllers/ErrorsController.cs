using Microsoft.AspNetCore.Mvc;
using LogMonitor.Core.Dtos;
using LogMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErrorsController : ControllerBase
{
    private readonly LogMonitorDbContext _dbContext;

    public ErrorsController(LogMonitorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetErrors()
    {
        var errors = await _dbContext.Errors
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var dtos = errors.Select(e => new ErrorDto(
            e.Id,
            e.FileName,
            e.Content,
            e.LinePosition,
            e.CreatedAt
        )).ToList();

        return Ok(dtos);
    }
}