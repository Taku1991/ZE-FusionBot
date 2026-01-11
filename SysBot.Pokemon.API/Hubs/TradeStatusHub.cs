using Microsoft.AspNetCore.SignalR;
using SysBot.Pokemon.API.Models;

namespace SysBot.Pokemon.API.Hubs;

/// <summary>
/// SignalR Hub for real-time trade status updates
/// </summary>
public class TradeStatusHub : Hub
{
    private readonly ILogger<TradeStatusHub> _logger;
    private static readonly Dictionary<string, string> _tradeConnections = new();

    public TradeStatusHub(ILogger<TradeStatusHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client subscribes to trade updates
    /// </summary>
    public async Task SubscribeToTrade(string tradeId)
    {
        _tradeConnections[Context.ConnectionId] = tradeId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"trade-{tradeId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to trade {TradeId}",
            Context.ConnectionId, tradeId);
    }

    /// <summary>
    /// Client unsubscribes from trade updates
    /// </summary>
    public async Task UnsubscribeFromTrade(string tradeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"trade-{tradeId}");
        _tradeConnections.Remove(Context.ConnectionId);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from trade {TradeId}",
            Context.ConnectionId, tradeId);
    }

    /// <summary>
    /// Send trade status update to all subscribers
    /// </summary>
    public async Task SendTradeUpdate(string tradeId, TradeResponse status)
    {
        await Clients.Group($"trade-{tradeId}").SendAsync("TradeStatusUpdate", status);
    }

    /// <summary>
    /// Send trade log message to subscribers
    /// </summary>
    public async Task SendTradeLog(string tradeId, string message)
    {
        await Clients.Group($"trade-{tradeId}").SendAsync("TradeLog", new
        {
            tradeId,
            message,
            timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_tradeConnections.TryGetValue(Context.ConnectionId, out var tradeId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"trade-{tradeId}");
            _tradeConnections.Remove(Context.ConnectionId);
            _logger.LogInformation("Client {ConnectionId} disconnected from trade {TradeId}",
                Context.ConnectionId, tradeId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Extension class to send updates from outside the Hub
/// </summary>
public static class TradeStatusHubExtensions
{
    public static async Task NotifyTradeUpdate(this IHubContext<TradeStatusHub> hubContext,
        string tradeId, TradeResponse status)
    {
        await hubContext.Clients.Group($"trade-{tradeId}").SendAsync("TradeStatusUpdate", status);
    }

    public static async Task NotifyTradeLog(this IHubContext<TradeStatusHub> hubContext,
        string tradeId, string message)
    {
        await hubContext.Clients.Group($"trade-{tradeId}").SendAsync("TradeLog", new
        {
            tradeId,
            message,
            timestamp = DateTime.UtcNow
        });
    }
}
