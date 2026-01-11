using SysBot.Pokemon;
using SysBot.Pokemon.API.Models;
using PKHeX.Core;
using System.Collections.Concurrent;

namespace SysBot.Pokemon.API.Services;

/// <summary>
/// Service for managing trades between the web API and the bot system.
/// Currently in MOCK MODE - not connected to real bots yet.
/// </summary>
public class TradeHubService
{
    // Mock trade storage
    private readonly ConcurrentDictionary<string, TradeResponse> _activeTrades = new();
    private readonly ConcurrentDictionary<string, List<string>> _tradeMessages = new();

    /// <summary>
    /// Submits a trade request (MOCK VERSION - for testing)
    /// </summary>
    public async Task<TradeResponse> SubmitTradeAsync(TradeRequest request)
    {
        await Task.Delay(100); // Simulate processing

        var tradeId = Guid.NewGuid().ToString();
        var tradeCode = !string.IsNullOrEmpty(request.TradeCode)
            ? request.TradeCode
            : GenerateTradeCode();

        var response = new TradeResponse
        {
            TradeId = tradeId,
            UserId = request.UserId,
            Status = TradeStatus.Queued,
            TradeCode = tradeCode,
            QueuePosition = 1,
            EstimatedWaitMinutes = 2,
            SubmittedAt = DateTime.UtcNow,
            Messages = new List<string>
            {
                "Trade submitted successfully!",
                $"Your trade code is: {tradeCode}",
                "Please enter this code in your game and start searching.",
                "⚠️ MOCK MODE: This is a test response. Not connected to real bots yet."
            }
        };

        _activeTrades[tradeId] = response;
        _tradeMessages[tradeId] = response.Messages.ToList();

        return response;
    }

    /// <summary>
    /// Gets the current status of a trade
    /// </summary>
    public async Task<TradeResponse?> GetTradeStatusAsync(string tradeId)
    {
        await Task.Delay(50); // Simulate processing

        if (_activeTrades.TryGetValue(tradeId, out var trade))
        {
            // Simulate status progression for testing
            if (trade.Status == TradeStatus.Queued)
            {
                var elapsed = DateTime.UtcNow - trade.SubmittedAt;
                if (elapsed.TotalSeconds > 10)
                {
                    trade.Status = TradeStatus.Searching;
                    trade.Messages.Add("Bot is now searching for you...");
                }
            }
            else if (trade.Status == TradeStatus.Searching)
            {
                var elapsed = DateTime.UtcNow - trade.SubmittedAt;
                if (elapsed.TotalSeconds > 20)
                {
                    trade.Status = TradeStatus.Trading;
                    trade.Messages.Add("Trainer found! Trading now...");
                }
            }

            return trade;
        }

        return null;
    }

    /// <summary>
    /// Gets trade history for a user
    /// </summary>
    public async Task<List<TradeResponse>> GetUserTradesAsync(string userId)
    {
        await Task.Delay(50);

        return _activeTrades.Values
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.SubmittedAt)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Cancels a pending trade
    /// </summary>
    public async Task<bool> CancelTradeAsync(string tradeId, string userId)
    {
        await Task.Delay(50);

        if (_activeTrades.TryGetValue(tradeId, out var trade))
        {
            if (trade.UserId != userId)
                return false; // Not your trade

            if (trade.Status == TradeStatus.Queued || trade.Status == TradeStatus.Searching)
            {
                trade.Status = TradeStatus.Cancelled;
                trade.Messages.Add("Trade cancelled by user.");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets queue information for a specific game
    /// </summary>
    public async Task<QueueInfo> GetQueueInfoAsync(string game)
    {
        await Task.Delay(50);

        // Mock data for testing
        return new QueueInfo
        {
            Game = game,
            TotalInQueue = 3,
            AverageWaitMinutes = 5,
            IsOpen = true,
            AvailableBots = 2,
            CurrentlyProcessing = 1
        };
    }

    /// <summary>
    /// Generates a random 8-digit trade code
    /// </summary>
    private string GenerateTradeCode()
    {
        var random = new Random();
        return random.Next(10000000, 99999999).ToString();
    }

    // ====== REAL BOT INTEGRATION (Not implemented yet) ======
    // These methods will be implemented when connecting to actual bots:

    // public void RegisterHub<T>(PokeTradeHub<T> hub) where T : PKM, new()
    // {
    //     // Register a bot hub instance
    // }

    // private async Task<PKM> CreatePokemonFromSet(string game, ShowdownSet set)
    // {
    //     // Use AutoLegalityWrapper to create legal Pokemon
    // }

    // private TradeEntry<T> CreateTradeEntry<T>(T pokemon, TradeRequest request, string tradeCode)
    //     where T : PKM, new()
    // {
    //     // Create TradeEntry for bot queue
    // }
}
