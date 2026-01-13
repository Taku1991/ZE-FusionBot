using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SysBot.Pokemon.WinForms.API.Models;
using SysBot.Pokemon.WinForms.API.Services;

namespace SysBot.Pokemon.WinForms.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueueController : ControllerBase
{
    private readonly TradeHubService _hubService;
    private readonly ILogger<QueueController> _logger;

    public QueueController(TradeHubService hubService, ILogger<QueueController> logger)
    {
        _hubService = hubService;
        _logger = logger;
    }

    /// <summary>
    /// Get queue information for a specific game
    /// </summary>
    [HttpGet("{game}")]
    public async Task<ActionResult<QueueInfo>> GetQueueInfo(string game)
    {
        try
        {
            var validGames = new[] { "SV", "SWSH", "BDSP", "PLA", "LGPE", "PLZA" };
            if (!validGames.Contains(game.ToUpperInvariant()))
            {
                return BadRequest(new { error = $"Invalid game. Must be one of: {string.Join(", ", validGames)}" });
            }

            var queueInfo = await _hubService.GetQueueInfoAsync(game.ToUpperInvariant());
            return Ok(queueInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue info for {Game}", game);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get queue information for all games
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<Dictionary<string, QueueInfo>>> GetAllQueueInfo()
    {
        try
        {
            var games = new[] { "SV", "SWSH", "BDSP", "PLA", "LGPE", "PLZA" };
            var result = new Dictionary<string, QueueInfo>();

            foreach (var game in games)
            {
                result[game] = await _hubService.GetQueueInfoAsync(game);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all queue info");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
