using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PKHeX.Core;
using SysBot.Pokemon;
using SysBot.Pokemon.WinForms.API.Hubs;
using SysBot.Pokemon.WinForms.API.Models;

namespace SysBot.Pokemon.WinForms.API.Services;

/// <summary>
/// Notifier that sends trade status updates via SignalR
/// </summary>
public class SignalRTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
{
    private readonly IHubContext<TradeStatusHub> _hubContext;
    private readonly string _tradeId;
    private readonly TradeResponse _response;

    public Action<PokeRoutineExecutor<T>>? OnFinish { get; set; }

    public SignalRTradeNotifier(IHubContext<TradeStatusHub> hubContext, string tradeId, TradeResponse response)
    {
        _hubContext = hubContext;
        _tradeId = tradeId;
        _response = response;
    }

    public async Task SendInitialQueueUpdate()
    {
        _response.Status = TradeStatus.Queued;
        _response.Timestamp = DateTime.UtcNow;
        await _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void UpdateBatchProgress(int currentBatchNumber, T currentPokemon, int uniqueTradeID)
    {
        var message = $"Processing Pokemon {currentBatchNumber} of {_response.QueuePosition}";
        _response.Messages?.Add(message);
        _ = _hubContext.NotifyTradeLog(_tradeId, message);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        _response.Messages?.Add(message);
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, message);
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        var msg = message.Summary;
        _response.Messages?.Add(msg);
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, msg);
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        _response.Messages?.Add(message);
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, message);
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        _response.Status = TradeStatus.Cancelled;
        _response.ErrorMessage = msg.ToString();
        _response.Messages?.Add($"Trade cancelled: {msg}");
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, $"Trade cancelled: {msg}");
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        _response.Status = TradeStatus.Completed;
        _response.Messages?.Add("Trade completed successfully!");
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, "Trade completed successfully!");
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        _response.Status = TradeStatus.Queued;
        _response.Messages?.Add("Trade initialized, waiting in queue...");
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, "Trade initialized");
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        _response.Status = TradeStatus.Searching;
        _response.Messages?.Add($"Bot is searching for you with code: {info.Code}");
        _response.BotName = routine.Connection.Name;
        _response.Timestamp = DateTime.UtcNow;

        _ = _hubContext.NotifyTradeLog(_tradeId, $"Searching with code: {info.Code}");
        _ = _hubContext.NotifyTradeUpdate(_tradeId, _response);
    }
}
