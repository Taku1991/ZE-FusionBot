using Microsoft.AspNetCore.Mvc;
using SysBot.Pokemon.API.Models;
using SysBot.Pokemon.API.Services;

namespace SysBot.Pokemon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradeController : ControllerBase
{
    private readonly TradeHubService _hubService;
    private readonly ILogger<TradeController> _logger;

    public TradeController(TradeHubService hubService, ILogger<TradeController> logger)
    {
        _hubService = hubService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a new trade request
    /// </summary>
    [HttpPost("submit")]
    public async Task<ActionResult<TradeResponse>> SubmitTrade([FromBody] TradeRequest request)
    {
        try
        {
            _logger.LogInformation("Trade submission received from user {UserId} for {Game}",
                request.UserId, request.Game);

            // Validate request
            if (string.IsNullOrEmpty(request.ShowdownSet))
            {
                return BadRequest(new { error = "Showdown set is required" });
            }

            if (string.IsNullOrEmpty(request.TrainerName))
            {
                return BadRequest(new { error = "Trainer name is required" });
            }

            var validGames = new[] { "SV", "SWSH", "BDSP", "PLA", "LGPE", "PLZA" };
            if (!validGames.Contains(request.Game.ToUpperInvariant()))
            {
                return BadRequest(new { error = $"Invalid game. Must be one of: {string.Join(", ", validGames)}" });
            }

            // Submit trade
            var response = await _hubService.SubmitTradeAsync(request);

            if (response.Status == TradeStatus.Failed)
            {
                _logger.LogWarning("Trade submission failed for user {UserId}: {Error}",
                    request.UserId, response.ErrorMessage);
                return BadRequest(response);
            }

            _logger.LogInformation("Trade {TradeId} submitted successfully for user {UserId}",
                response.TradeId, request.UserId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting trade for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Submit a batch trade (multiple Pokemon)
    /// </summary>
    [HttpPost("submit-batch")]
    public async Task<ActionResult<List<TradeResponse>>> SubmitBatchTrade([FromBody] TradeRequest request)
    {
        try
        {
            if (request.BatchShowdownSets == null || !request.BatchShowdownSets.Any())
            {
                return BadRequest(new { error = "Batch trades require at least one Pokemon" });
            }

            if (request.BatchShowdownSets.Count > 6)
            {
                return BadRequest(new { error = "Maximum 6 Pokemon per batch trade" });
            }

            _logger.LogInformation("Batch trade submission received from user {UserId} with {Count} Pokemon",
                request.UserId, request.BatchShowdownSets.Count);

            var responses = new List<TradeResponse>();

            foreach (var showdownSet in request.BatchShowdownSets)
            {
                var singleRequest = new TradeRequest
                {
                    UserId = request.UserId,
                    UserEmail = request.UserEmail,
                    DiscordUsername = request.DiscordUsername,
                    TrainerName = request.TrainerName,
                    Game = request.Game,
                    ShowdownSet = showdownSet,
                    TradeCode = request.TradeCode,
                    Preferences = request.Preferences
                };

                var response = await _hubService.SubmitTradeAsync(singleRequest);
                responses.Add(response);

                // If one fails, stop the batch
                if (response.Status == TradeStatus.Failed)
                {
                    _logger.LogWarning("Batch trade failed at Pokemon {Index} for user {UserId}",
                        responses.Count, request.UserId);
                    break;
                }

                // Small delay between submissions
                await Task.Delay(500);
            }

            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting batch trade for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a pending trade
    /// </summary>
    [HttpPost("{tradeId}/cancel")]
    public async Task<ActionResult> CancelTrade(string tradeId, [FromBody] CancelRequest request)
    {
        try
        {
            var success = await _hubService.CancelTradeAsync(tradeId, request.UserId);
            if (!success)
            {
                return NotFound(new { error = "Trade not found or cannot be cancelled" });
            }

            _logger.LogInformation("Trade {TradeId} cancelled by user {UserId}", tradeId, request.UserId);
            return Ok(new { message = "Trade cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling trade {TradeId}", tradeId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class CancelRequest
{
    public required string UserId { get; set; }
}
