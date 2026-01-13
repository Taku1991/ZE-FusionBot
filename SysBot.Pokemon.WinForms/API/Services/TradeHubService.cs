using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SysBot.Base;
using SysBot.Pokemon;
using SysBot.Pokemon.WinForms.API.Models;
using SysBot.Pokemon.WinForms.API.Hubs;
using PKHeX.Core;
using PKHeX.Core.AutoMod;

namespace SysBot.Pokemon.WinForms.API.Services;

/// <summary>
/// Service for managing trades between the web API and the bot system
/// </summary>
public class TradeHubService
{
    private readonly IHubContext<TradeStatusHub> _hubContext;
    private readonly ConcurrentDictionary<string, TradeResponse> _activeTrades = new();
    private static int _uniqueTradeCounter = 0;

    public TradeHubService(IHubContext<TradeStatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Submits a trade request to the bot system
    /// </summary>
    public async Task<TradeResponse> SubmitTradeAsync(TradeRequest request)
    {
        var tradeId = Guid.NewGuid().ToString();
        var tradeCode = ParseTradeCode(request.TradeCode);

        var response = new TradeResponse
        {
            TradeId = tradeId,
            UserId = request.UserId,
            Status = TradeStatus.Queued,
            TradeCode = tradeCode.ToString("D8"),
            SubmittedAt = DateTime.UtcNow,
            Messages = new List<string>()
        };

        _activeTrades[tradeId] = response;

        try
        {
            // Get the bot hub from Main config
            var hub = GetBotHub(request.Game);
            if (hub == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = $"No bot configured for game: {request.Game}";
                return response;
            }

            // Create the Pokemon from the showdown set
            var pkm = await CreatePokemonFromSetAsync(request.Game, request.ShowdownSet, request.Preferences);
            if (pkm == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = "Failed to create legal Pokemon from showdown set. Please check your Showdown format and try again.";
                response.Messages.Add("❌ Unable to generate Pokemon from the provided set.");
                response.Messages.Add("💡 Make sure your Showdown set includes at least:");
                response.Messages.Add("   - Pokemon species name");
                response.Messages.Add("   - Valid moves for this Pokemon");
                response.Messages.Add("   - Correct ability and item names");
                return response;
            }

            // Create trade detail with SignalR notifier
            var tradeDetail = CreateTradeDetail(pkm, request, tradeCode, response, tradeId);

            // Get queue position
            var queueInfo = GetQueueInfo(hub, request.Game);
            response.QueuePosition = queueInfo.TotalInQueue + 1;
            response.EstimatedWaitMinutes = queueInfo.AverageWaitMinutes;

            // Enqueue the trade
            EnqueueTrade(hub, tradeDetail);

            response.Messages.Add("Trade submitted successfully!");
            response.Messages.Add($"Your trade code is: {tradeCode:D8}");
            response.Messages.Add("Please enter this code in your game and start searching.");
            response.Messages.Add($"Queue position: {response.QueuePosition}");

            // Notify via SignalR
            await _hubContext.NotifyTradeUpdate(tradeId, response);

            return response;
        }
        catch (Exception ex)
        {
            response.Status = TradeStatus.Failed;
            response.ErrorMessage = $"Error: {ex.Message}";
            response.Messages.Add($"Failed to submit trade: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Gets the current status of a trade
    /// </summary>
    public async Task<TradeResponse?> GetTradeStatusAsync(string tradeId)
    {
        await Task.CompletedTask;
        return _activeTrades.TryGetValue(tradeId, out var trade) ? trade : null;
    }

    /// <summary>
    /// Gets trade history for a user
    /// </summary>
    public async Task<List<TradeResponse>> GetUserTradesAsync(string userId)
    {
        await Task.CompletedTask;
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
        await Task.CompletedTask;

        if (!_activeTrades.TryGetValue(tradeId, out var trade))
            return false;

        if (trade.UserId != userId)
            return false;

        if (trade.Status != TradeStatus.Queued && trade.Status != TradeStatus.Searching)
            return false;

        trade.Status = TradeStatus.Cancelled;
        trade.Messages?.Add("Trade cancelled by user.");

        _ = _hubContext.NotifyTradeUpdate(tradeId, trade);

        return true;
    }

    /// <summary>
    /// Gets queue information for a specific game
    /// </summary>
    public async Task<QueueInfo> GetQueueInfoAsync(string game)
    {
        await Task.CompletedTask;

        var hub = GetBotHub(game);
        if (hub == null)
        {
            return new QueueInfo
            {
                Game = game,
                TotalInQueue = 0,
                AvailableBots = 0,
                CurrentlyProcessing = 0,
                AverageWaitMinutes = 0,
                IsOpen = false
            };
        }

        return GetQueueInfo(hub, game);
    }

    private static dynamic? GetBotHub(string game)
    {
        var config = Main.Config;
        if (config?.Hub == null)
            return null;

        // Return the hub based on the game
        // The hub is generic, so we use dynamic to handle different PKM types
        return game.ToUpperInvariant() switch
        {
            "SV" => config.Hub,
            "SWSH" => config.Hub,
            "BDSP" => config.Hub,
            "PLA" => config.Hub,
            "LGPE" => config.Hub,
            "PLZA" => config.Hub,
            _ => null
        };
    }

    private static async Task<PKM?> CreatePokemonFromSetAsync(string game, string showdownSet, TrainerPreferences? preferences)
    {
        return await Task.Run(() =>
        {
            try
            {
                LogUtil.LogInfo($"Attempting to create Pokemon from set for game {game}", "TradeHubService");
                LogUtil.LogInfo($"Showdown set: {showdownSet}", "TradeHubService");

                // Normalize the showdown set - handle both single-line and multi-line formats
                ShowdownSet? set = null;

                // Try to parse as-is first (multi-line format)
                try
                {
                    set = new ShowdownSet(showdownSet);
                    LogUtil.LogInfo($"Successfully parsed showdown set: Species={set.Species}, Ability={set.Ability}", "TradeHubService");
                }
                catch (Exception ex)
                {
                    LogUtil.LogInfo($"Failed to parse as multi-line, trying single-line format. Error: {ex.Message}", "TradeHubService");
                    // If that fails, try to convert from single-line format (common from web forms)
                    try
                    {
                        set = ShowdownUtil.ConvertToShowdown(showdownSet);
                        LogUtil.LogInfo($"Successfully converted to showdown set: Species={set?.Species}", "TradeHubService");
                    }
                    catch (Exception ex2)
                    {
                        LogUtil.LogError($"Failed to convert showdown set: {ex2.Message}", "TradeHubService");
                        return null;
                    }
                }

                if (set == null || set.Species == 0)
                {
                    LogUtil.LogError($"Invalid showdown set: set is null or species is 0", "TradeHubService");
                    return null;
                }

                // Get the correct trainer for the game version
                var generation = GetGeneration(game);
                var sav = AutoLegalityWrapper.GetTrainerInfo((byte)generation);

                if (preferences != null && !string.IsNullOrEmpty(preferences.OriginalTrainerName))
                {
                    sav = new SimpleTrainerInfo
                    {
                        OT = preferences.OriginalTrainerName,
                        TID16 = (ushort)(preferences.TrainerID ?? 12345),
                        SID16 = (ushort)(preferences.SecretID ?? 54321),
                        Language = ParseLanguage(preferences.Language),
                        Generation = (byte)generation
                    };
                }

                var template = AutoLegalityWrapper.GetTemplate(set);
                LogUtil.LogInfo($"Generated template for {set.Species} (Generation {generation})", "TradeHubService");

                // Generate legal Pokemon
                LogUtil.LogInfo($"Calling GetLegal for {set.Species}", "TradeHubService");
                var pkm = sav.GetLegal(template, out var result);

                if (pkm == null)
                {
                    LogUtil.LogError($"GetLegal returned null. Result: {result}", "TradeHubService");
                    return null;
                }

                LogUtil.LogInfo($"Successfully created Pokemon: {pkm.Species} (Result: {result})", "TradeHubService");
                return pkm;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Exception in CreatePokemonFromSetAsync: {ex.Message}\n{ex.StackTrace}", "TradeHubService");
                return null;
            }
        });
    }

    private dynamic CreateTradeDetail(PKM pkm, TradeRequest request, int tradeCode, TradeResponse response, string tradeId)
    {
        var uniqueTradeID = (int)Interlocked.Increment(ref _uniqueTradeCounter);
        var trainerInfo = new PokeTradeTrainerInfo(request.TrainerName, ulong.Parse(request.UserId));

        // Create trade detail based on game type
        return request.Game.ToUpperInvariant() switch
        {
            "SV" => CreateTradeDetailTyped((PK9)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "PLZA" => CreateTradeDetailTyped((PA9)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "SWSH" => CreateTradeDetailTyped((PK8)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "BDSP" => CreateTradeDetailTyped((PB8)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "PLA" => CreateTradeDetailTyped((PA8)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "LGPE" => CreateTradeDetailTyped((PB7)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            _ => throw new NotSupportedException($"Game {request.Game} is not supported")
        };
    }

    private PokeTradeDetail<T> CreateTradeDetailTyped<T>(T pkm, PokeTradeTrainerInfo trainerInfo, int tradeCode, TradeResponse response, string tradeId, int uniqueTradeID) where T : PKM, new()
    {
        var notifier = new SignalRTradeNotifier<T>(_hubContext, tradeId, response);
        return new PokeTradeDetail<T>(
            pkm,
            trainerInfo,
            notifier,
            PokeTradeType.Specific,
            tradeCode,
            uniqueTradeID: uniqueTradeID
        );
    }

    private static void EnqueueTrade(dynamic hub, dynamic tradeDetail)
    {
        // Enqueue with normal priority
        hub.Queues.Enqueue(PokeRoutineType.LinkTrade, tradeDetail, 0u);
    }

    private static QueueInfo GetQueueInfo(dynamic hub, string game)
    {
        var queue = hub.Queues.GetQueue(PokeRoutineType.LinkTrade);
        var queueCount = queue.Count;

        // Count currently processing bots
        int processing = 0;
        foreach (var bot in hub.Bots)
        {
            if (bot.Config.CurrentRoutineType == PokeRoutineType.LinkTrade)
                processing++;
        }

        return new QueueInfo
        {
            Game = game,
            TotalInQueue = queueCount,
            AvailableBots = hub.Bots.Count,
            CurrentlyProcessing = processing,
            AverageWaitMinutes = Math.Max(1, queueCount * 2), // Estimate 2 min per trade
            IsOpen = hub.Config.Queues.FlexMode || hub.TradeBotsReady
        };
    }

    private static int ParseTradeCode(string? code)
    {
        if (string.IsNullOrEmpty(code) || !int.TryParse(code, out var parsed))
            return new Random().Next(10000000, 99999999);

        return parsed;
    }

    private static int ParseLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "japanese" or "ja" or "jpn" => 1,
            "english" or "en" or "eng" => 2,
            "french" or "fr" or "fra" => 3,
            "italian" or "it" or "ita" => 4,
            "german" or "de" or "deu" => 5,
            "spanish" or "es" or "spa" => 7,
            "korean" or "ko" or "kor" => 8,
            "chinese" or "zh" or "chs" => 9,
            "taiwanese" or "tw" or "cht" => 10,
            _ => 2 // Default to English
        };
    }

    private static int GetGeneration(string game)
    {
        return game.ToUpperInvariant() switch
        {
            "SV" => 9,
            "PLZA" => 10,
            "SWSH" => 8,
            "BDSP" => 8,
            "PLA" => 8,
            "LGPE" => 7,
            _ => 9
        };
    }
}
