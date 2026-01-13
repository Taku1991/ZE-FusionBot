using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SysBot.Pokemon.WinForms.API.Models;
using SysBot.Pokemon.WinForms.API.Services;

namespace SysBot.Pokemon.WinForms.API.Controllers;

[ApiController]
[Route("api/trade")]
public class StatusController : ControllerBase
{
    private readonly TradeHubService _hubService;
    private readonly ILogger<StatusController> _logger;

    public StatusController(TradeHubService hubService, ILogger<StatusController> logger)
    {
        _hubService = hubService;
        _logger = logger;
    }

    /// <summary>
    /// Get status of a specific trade
    /// </summary>
    [HttpGet("status/{tradeId}")]
    public async Task<ActionResult<TradeResponse>> GetTradeStatus(string tradeId)
    {
        try
        {
            var status = await _hubService.GetTradeStatusAsync(tradeId);
            if (status == null)
            {
                return NotFound(new { error = "Trade not found" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for trade {TradeId}", tradeId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all trades for a specific user
    /// </summary>
    [HttpGet("user/{userId}/trades")]
    public async Task<ActionResult<List<TradeResponse>>> GetUserTrades(string userId, [FromQuery] int limit = 10)
    {
        try
        {
            var trades = await _hubService.GetUserTradesAsync(userId);
            return Ok(trades);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trades for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }
}
