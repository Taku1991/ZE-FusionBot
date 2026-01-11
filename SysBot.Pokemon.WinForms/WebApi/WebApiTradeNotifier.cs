using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.WinForms.WebApi;

/// <summary>
/// Trade status information shared across all trade types
/// </summary>
public class TradeStatusInfo
{
    public string Status { get; set; } = "Queued";
    public List<string> Messages { get; set; } = new();
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    public string? TrainerName { get; set; }
    public string? PokemonName { get; set; }
}

/// <summary>
/// Custom trade notifier that tracks trade status for WebAPI
/// </summary>
public class WebApiTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
{
    // Store trade status updates by uniqueTradeID
    private static readonly ConcurrentDictionary<int, TradeStatusInfo> _tradeStatuses = new();

    public Action<PokeRoutineExecutor<T>>? OnFinish { get; set; }

    public Task SendInitialQueueUpdate()
    {
        return Task.CompletedTask;
    }

    public void UpdateBatchProgress(int currentBatchNumber, T currentPokemon, int uniqueTradeID)
    {
        UpdateStatus(uniqueTradeID, "Trading", $"Batch progress: {currentBatchNumber}");
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        UpdateStatus(info.UniqueTradeID, null, message);
        LogUtil.LogInfo(routine.Connection.Label, message);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));

        UpdateStatus(info.UniqueTradeID, null, msg);
        LogUtil.LogInfo(routine.Connection.Label, msg);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        UpdateStatus(info.UniqueTradeID, null, message);
        LogUtil.LogInfo(routine.Connection.Label, message);
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        UpdateStatus(info.UniqueTradeID, "Cancelled", $"Trade cancelled: {msg}");
        LogUtil.LogInfo(routine.Connection.Label, $"Canceling trade with {info.Trainer.TrainerName}, because {msg}.");
        OnFinish?.Invoke(routine);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        var speciesName = GameInfo.GetStrings("en").Species[result.Species];
        UpdateStatus(info.UniqueTradeID, "Completed", $"Trade completed! Received {speciesName}");
        LogUtil.LogInfo(routine.Connection.Label, $"Finished trading {info.Trainer.TrainerName}");
        OnFinish?.Invoke(routine);
    }

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var speciesName = GameInfo.GetStrings("en").Species[info.TradeData.Species];
        UpdateStatus(info.UniqueTradeID, "Initializing", $"Starting trade for {speciesName}");
        LogUtil.LogInfo(routine.Connection.Label, $"Starting trade loop for {info.Trainer.TrainerName}, sending {speciesName}");
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var speciesName = GameInfo.GetStrings("en").Species[info.TradeData.Species];
        UpdateStatus(info.UniqueTradeID, "Searching", $"Searching for {info.Trainer.TrainerName}...");
        LogUtil.LogInfo(routine.Connection.Label, $"Searching for trade with {info.Trainer.TrainerName}, sending {speciesName}");
    }

    private void UpdateStatus(int uniqueTradeID, string? newStatus, string? message)
    {
        var status = _tradeStatuses.GetOrAdd(uniqueTradeID, _ => new TradeStatusInfo());

        if (newStatus != null)
            status.Status = newStatus;

        if (message != null)
            status.Messages.Add(message);

        status.LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Get trade status by uniqueTradeID
    /// </summary>
    public static TradeStatusInfo? GetTradeStatus(int uniqueTradeID)
    {
        return _tradeStatuses.TryGetValue(uniqueTradeID, out var status) ? status : null;
    }

    /// <summary>
    /// Clean up old trade statuses (older than 1 hour)
    /// </summary>
    public static void CleanupOldTrades()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var oldTrades = _tradeStatuses.Where(x => x.Value.LastUpdate < cutoff).Select(x => x.Key).ToList();
        foreach (var tradeId in oldTrades)
        {
            _tradeStatuses.TryRemove(tradeId, out _);
        }
    }
}
