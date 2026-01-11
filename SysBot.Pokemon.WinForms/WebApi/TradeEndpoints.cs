using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon;
using SysBot.Pokemon.Helpers;

namespace SysBot.Pokemon.WinForms.WebApi;

/// <summary>
/// Handles trade-related API endpoints for the WinForms WebAPI
/// </summary>
public static class TradeEndpoints
{
    private static readonly ConcurrentDictionary<string, TradeInfo> _activeTrades = new();
    private static readonly ConcurrentDictionary<string, int> _tradeIdToUniqueId = new(); // Maps external tradeId to uniqueTradeID
    private static int _uniqueTradeCounter = 0;

    // Web API Trade Notifiers for each game type
    private static readonly WebApiTradeNotifier<PB8> _notifierBDSP = new();
    private static readonly WebApiTradeNotifier<PK9> _notifierSV = new();
    private static readonly WebApiTradeNotifier<PK8> _notifierSWSH = new();
    private static readonly WebApiTradeNotifier<PA8> _notifierPLA = new();
    private static readonly WebApiTradeNotifier<PB7> _notifierLGPE = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Submit a new trade request
    /// POST /api/trade/submit
    /// </summary>
    public static async Task<string> SubmitTrade(HttpListenerRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var tradeRequest = JsonSerializer.Deserialize<TradeRequest>(body, JsonOptions);

            if (tradeRequest == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid request body" }, JsonOptions);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(tradeRequest.ShowdownSet) ||
                string.IsNullOrEmpty(tradeRequest.TrainerName) ||
                string.IsNullOrEmpty(tradeRequest.Game))
            {
                return JsonSerializer.Serialize(new { error = "Missing required fields" }, JsonOptions);
            }

            var tradeId = Guid.NewGuid().ToString();
            var tradeCode = string.IsNullOrEmpty(tradeRequest.TradeCode)
                ? GenerateTradeCode()
                : tradeRequest.TradeCode;

            // Parse Showdown set
            ShowdownSet set;
            try
            {
                set = new ShowdownSet(tradeRequest.ShowdownSet);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    tradeId,
                    status = "Failed",
                    errorMessage = $"Invalid Showdown set: {ex.Message}"
                }, JsonOptions);
            }

            // Log the trade request
            LogUtil.LogInfo($"✅ Trade request received for {tradeRequest.Game}: {set.Species}", "TradeAPI");
            LogUtil.LogInfo($"   Trainer: {tradeRequest.TrainerName}, Code: {tradeCode}", "TradeAPI");
            LogUtil.LogInfo($"   Showdown Set:\n{tradeRequest.ShowdownSet}", "TradeAPI");

            // Add to queue based on game
            var result = await AddToQueueAsync(tradeRequest.Game, set, tradeRequest, tradeCode, tradeId);

            // Track the trade
            var tradeInfo = new TradeInfo
            {
                TradeId = tradeId,
                UserId = tradeRequest.UserId,
                Game = tradeRequest.Game,
                TradeCode = tradeCode,
                Status = result.Success ? "Queued" : "Failed",
                QueuePosition = result.QueuePosition,
                ErrorMessage = result.ErrorMessage,
                SubmittedAt = DateTime.UtcNow,
                Messages = new List<string>()
            };

            if (result.Success)
            {
                // Map external tradeId to uniqueTradeID for status tracking
                _tradeIdToUniqueId[tradeId] = result.UniqueTradeID;

                tradeInfo.Messages.Add("✅ Trade successfully added to bot queue!");
                tradeInfo.Messages.Add($"Your trade code is: {tradeCode}");
                tradeInfo.Messages.Add("Please enter this code in your game and start searching.");
                tradeInfo.Messages.Add($"Queue position: {result.QueuePosition}");
            }
            else
            {
                tradeInfo.Messages.Add($"❌ Failed: {result.ErrorMessage}");
            }

            _activeTrades[tradeId] = tradeInfo;

            var response = new
            {
                tradeId,
                userId = tradeRequest.UserId,
                status = tradeInfo.Status,
                tradeCode,
                queuePosition = result.QueuePosition,
                estimatedWaitMinutes = result.QueuePosition * 3,
                errorMessage = result.ErrorMessage,
                submittedAt = tradeInfo.SubmittedAt,
                messages = tradeInfo.Messages
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in SubmitTrade: {ex.Message}\n{ex.StackTrace}", "TradeAPI");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static async Task<(bool Success, int QueuePosition, int UniqueTradeID, string? ErrorMessage)> AddToQueueAsync(
        string game,
        ShowdownSet set,
        TradeRequest request,
        string tradeCode,
        string tradeId)
    {
        try
        {
            var runner = Main.GetBotRunner();
            if (runner == null || Main.Config == null)
            {
                return (false, 0, 0, "Bot not initialized");
            }

            // Get the Hub field via reflection since it's generic
            var hubField = runner.GetType().GetField("Hub");
            if (hubField == null)
            {
                return (false, 0, 0, "Could not access bot hub");
            }

            var hub = hubField.GetValue(runner);
            if (hub == null)
            {
                return (false, 0, 0, "Bot hub is null");
            }

            // Generate unique trade ID
            var uniqueTradeID = System.Threading.Interlocked.Increment(ref _uniqueTradeCounter);

            // Create TrainerInfo
            var trainerInfo = new PokeTradeTrainerInfo(request.TrainerName, ParseUserId(request.UserId));

            // Call the appropriate game method via reflection
            var gameUpper = game.ToUpperInvariant();
            var methodName = $"AddTo{gameUpper}QueueAsync";

            // Get the generic type parameter from the hub
            var hubType = hub.GetType();
            var pkType = hubType.GenericTypeArguments[0];

            // Call the appropriate method dynamically
            var method = typeof(TradeEndpoints).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                return (false, 0, 0, $"Game {game} not supported");
            }

            var task = (Task<(bool, int, string?)>)method.Invoke(null, new object[] { hub, set, trainerInfo, tradeCode, uniqueTradeID })!;
            var (success, queuePos, error) = await task;
            return (success, queuePos, uniqueTradeID, error);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error adding to queue: {ex.Message}\n{ex.StackTrace}", "TradeAPI");
            return (false, 0, 0, ex.Message);
        }
    }

    private static async Task<(bool Success, int QueuePosition, string? ErrorMessage)> AddToBDSPQueueAsync(
        object hubObj,
        ShowdownSet set,
        PokeTradeTrainerInfo trainerInfo,
        string tradeCode,
        int uniqueTradeID)
    {
        try
        {
            var hub = (PokeTradeHub<PB8>)hubObj;

            // Create Pokemon using AutoLegality
            var sav = AutoLegalityWrapper.GetTrainerInfo<PB8>();
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pk = (PB8?)sav.GetLegal(template, out var result);

            if (pk == null)
            {
                return (false, 0, $"Failed to create Pokemon: {result}");
            }

            // Create trade detail with WebApiTradeNotifier
            var code = int.Parse(tradeCode);
            var tradeDetail = new PokeTradeDetail<PB8>(
                pk,
                trainerInfo,
                _notifierBDSP,
                PokeTradeType.Specific,
                code,
                false, null, 0, 0, false, false,
                uniqueTradeID
            );

            // Create trade entry
            var tradeEntry = new TradeEntry<PB8>(
                tradeDetail,
                trainerInfo.ID,
                PokeRoutineType.LinkTrade,
                trainerInfo.TrainerName,
                uniqueTradeID
            );

            // Add to queue
            var queueResult = hub.Queues.Info.AddToTradeQueue(tradeEntry, trainerInfo.ID, false, false);

            if (queueResult == QueueResultAdd.Added)
            {
                var position = hub.Queues.Info.Count;
                LogUtil.LogInfo($"✅ Trade added to BDSP queue: {set.Species} for {trainerInfo.TrainerName} (Code: {tradeCode}, Position: {position})", "TradeAPI");
                return (true, position, null);
            }
            else
            {
                return (false, 0, $"Failed to add to queue: {queueResult}");
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in AddToBDSPQueueAsync: {ex.Message}\n{ex.StackTrace}", "TradeAPI");
            return (false, 0, ex.Message);
        }
    }

    private static async Task<(bool Success, int QueuePosition, string? ErrorMessage)> AddToSVQueueAsync(
        object hubObj,
        ShowdownSet set,
        PokeTradeTrainerInfo trainerInfo,
        string tradeCode,
        int uniqueTradeID)
    {
        try
        {
            var hub = (PokeTradeHub<PK9>)hubObj;
            var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pk = (PK9?)sav.GetLegal(template, out var result);

            if (pk == null)
                return (false, 0, $"Failed to create Pokemon: {result}");

            var code = int.Parse(tradeCode);
            var tradeDetail = new PokeTradeDetail<PK9>(
                pk, trainerInfo, _notifierSV, PokeTradeType.Specific,
                code, false, null, 0, 0, false, false, uniqueTradeID);

            var tradeEntry = new TradeEntry<PK9>(tradeDetail, trainerInfo.ID, PokeRoutineType.LinkTrade, trainerInfo.TrainerName, uniqueTradeID);
            var queueResult = hub.Queues.Info.AddToTradeQueue(tradeEntry, trainerInfo.ID, false, false);

            if (queueResult == QueueResultAdd.Added)
            {
                var position = hub.Queues.Info.Count;
                LogUtil.LogInfo($"✅ Trade added to SV queue: {set.Species} for {trainerInfo.TrainerName}", "TradeAPI");
                return (true, position, null);
            }
            return (false, 0, $"Failed: {queueResult}");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in AddToSVQueueAsync: {ex.Message}", "TradeAPI");
            return (false, 0, ex.Message);
        }
    }

    private static async Task<(bool Success, int QueuePosition, string? ErrorMessage)> AddToSWSHQueueAsync(
        object hubObj,
        ShowdownSet set,
        PokeTradeTrainerInfo trainerInfo,
        string tradeCode,
        int uniqueTradeID)
    {
        try
        {
            var hub = (PokeTradeHub<PK8>)hubObj;
            var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pk = (PK8?)sav.GetLegal(template, out var result);

            if (pk == null)
                return (false, 0, $"Failed to create Pokemon: {result}");

            var code = int.Parse(tradeCode);
            var tradeDetail = new PokeTradeDetail<PK8>(
                pk, trainerInfo, _notifierSWSH, PokeTradeType.Specific,
                code, false, null, 0, 0, false, false, uniqueTradeID);

            var tradeEntry = new TradeEntry<PK8>(tradeDetail, trainerInfo.ID, PokeRoutineType.LinkTrade, trainerInfo.TrainerName, uniqueTradeID);
            var queueResult = hub.Queues.Info.AddToTradeQueue(tradeEntry, trainerInfo.ID, false, false);

            if (queueResult == QueueResultAdd.Added)
            {
                var position = hub.Queues.Info.Count;
                LogUtil.LogInfo($"✅ Trade added to SWSH queue: {set.Species} for {trainerInfo.TrainerName}", "TradeAPI");
                return (true, position, null);
            }
            return (false, 0, $"Failed: {queueResult}");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in AddToSWSHQueueAsync: {ex.Message}", "TradeAPI");
            return (false, 0, ex.Message);
        }
    }

    private static async Task<(bool Success, int QueuePosition, string? ErrorMessage)> AddToPLAQueueAsync(
        object hubObj,
        ShowdownSet set,
        PokeTradeTrainerInfo trainerInfo,
        string tradeCode,
        int uniqueTradeID)
    {
        try
        {
            var hub = (PokeTradeHub<PA8>)hubObj;
            var sav = AutoLegalityWrapper.GetTrainerInfo<PA8>();
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pk = (PA8?)sav.GetLegal(template, out var result);

            if (pk == null)
                return (false, 0, $"Failed to create Pokemon: {result}");

            var code = int.Parse(tradeCode);
            var tradeDetail = new PokeTradeDetail<PA8>(
                pk, trainerInfo, _notifierPLA, PokeTradeType.Specific,
                code, false, null, 0, 0, false, false, uniqueTradeID);

            var tradeEntry = new TradeEntry<PA8>(tradeDetail, trainerInfo.ID, PokeRoutineType.LinkTrade, trainerInfo.TrainerName, uniqueTradeID);
            var queueResult = hub.Queues.Info.AddToTradeQueue(tradeEntry, trainerInfo.ID, false, false);

            if (queueResult == QueueResultAdd.Added)
            {
                var position = hub.Queues.Info.Count;
                LogUtil.LogInfo($"✅ Trade added to PLA queue: {set.Species} for {trainerInfo.TrainerName}", "TradeAPI");
                return (true, position, null);
            }
            return (false, 0, $"Failed: {queueResult}");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in AddToPLAQueueAsync: {ex.Message}", "TradeAPI");
            return (false, 0, ex.Message);
        }
    }

    private static async Task<(bool Success, int QueuePosition, string? ErrorMessage)> AddToLGPEQueueAsync(
        object hubObj,
        ShowdownSet set,
        PokeTradeTrainerInfo trainerInfo,
        string tradeCode,
        int uniqueTradeID)
    {
        try
        {
            var hub = (PokeTradeHub<PB7>)hubObj;
            var sav = AutoLegalityWrapper.GetTrainerInfo<PB7>();
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pk = (PB7?)sav.GetLegal(template, out var result);

            if (pk == null)
                return (false, 0, $"Failed to create Pokemon: {result}");

            var code = int.Parse(tradeCode);
            var tradeDetail = new PokeTradeDetail<PB7>(
                pk, trainerInfo, _notifierLGPE, PokeTradeType.Specific,
                code, false, null, 0, 0, false, false, uniqueTradeID);

            var tradeEntry = new TradeEntry<PB7>(tradeDetail, trainerInfo.ID, PokeRoutineType.LinkTrade, trainerInfo.TrainerName, uniqueTradeID);
            var queueResult = hub.Queues.Info.AddToTradeQueue(tradeEntry, trainerInfo.ID, false, false);

            if (queueResult == QueueResultAdd.Added)
            {
                var position = hub.Queues.Info.Count;
                LogUtil.LogInfo($"✅ Trade added to LGPE queue: {set.Species} for {trainerInfo.TrainerName}", "TradeAPI");
                return (true, position, null);
            }
            return (false, 0, $"Failed: {queueResult}");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in AddToLGPEQueueAsync: {ex.Message}", "TradeAPI");
            return (false, 0, ex.Message);
        }
    }

    /// <summary>
    /// Get trade status by ID
    /// GET /api/trade/status/{tradeId}
    /// </summary>
    public static string GetTradeStatus(string tradeId)
    {
        try
        {
            if (!_activeTrades.TryGetValue(tradeId, out var trade))
            {
                return JsonSerializer.Serialize(new { error = "Trade not found" }, JsonOptions);
            }

            // Check if we have live updates from the bot
            if (_tradeIdToUniqueId.TryGetValue(tradeId, out var uniqueTradeID))
            {
                // Try to get live status from WebApiTradeNotifier (needs type parameter)
                // We'll check all possible notifier types
                var liveStatus = TryGetLiveStatus(uniqueTradeID);
                if (liveStatus != null)
                {
                    // Update our cached trade info with live data
                    trade.Status = liveStatus.Status;
                    if (liveStatus.Messages.Count > trade.Messages.Count)
                    {
                        // Add new messages
                        var newMessages = liveStatus.Messages.Skip(trade.Messages.Count);
                        trade.Messages.AddRange(newMessages);
                    }
                }
            }

            var response = new
            {
                tradeId,
                userId = trade.UserId,
                status = trade.Status,
                tradeCode = trade.TradeCode,
                queuePosition = trade.QueuePosition,
                errorMessage = trade.ErrorMessage,
                submittedAt = trade.SubmittedAt,
                messages = trade.Messages
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in GetTradeStatus: {ex.Message}", "TradeAPI");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static TradeStatusInfo? TryGetLiveStatus(int uniqueTradeID)
    {
        // Try each Pokemon type's notifier
        var statusPB8 = WebApiTradeNotifier<PB8>.GetTradeStatus(uniqueTradeID);
        if (statusPB8 != null) return statusPB8;

        var statusPK9 = WebApiTradeNotifier<PK9>.GetTradeStatus(uniqueTradeID);
        if (statusPK9 != null) return statusPK9;

        var statusPK8 = WebApiTradeNotifier<PK8>.GetTradeStatus(uniqueTradeID);
        if (statusPK8 != null) return statusPK8;

        var statusPA8 = WebApiTradeNotifier<PA8>.GetTradeStatus(uniqueTradeID);
        if (statusPA8 != null) return statusPA8;

        var statusPB7 = WebApiTradeNotifier<PB7>.GetTradeStatus(uniqueTradeID);
        if (statusPB7 != null) return statusPB7;

        return null;
    }

    /// <summary>
    /// Cancel a trade
    /// POST /api/trade/{tradeId}/cancel
    /// </summary>
    public static async Task<string> CancelTrade(HttpListenerRequest request, string tradeId)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var cancelRequest = JsonSerializer.Deserialize<CancelRequest>(body, JsonOptions);

            if (!_activeTrades.TryGetValue(tradeId, out var trade))
            {
                return JsonSerializer.Serialize(new { error = "Trade not found" }, JsonOptions);
            }

            // Verify userId matches
            if (cancelRequest?.UserId != trade.UserId)
            {
                return JsonSerializer.Serialize(new { error = "Unauthorized" }, JsonOptions);
            }

            // Can only cancel if queued or searching
            if (trade.Status != "Queued" && trade.Status != "Searching")
            {
                return JsonSerializer.Serialize(new { error = "Cannot cancel this trade" }, JsonOptions);
            }

            trade.Status = "Cancelled";
            trade.Messages?.Add("Trade cancelled by user");

            LogUtil.LogInfo($"Trade {tradeId} cancelled by user {cancelRequest.UserId}", "TradeAPI");

            return JsonSerializer.Serialize(new { message = "Trade cancelled successfully" }, JsonOptions);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Error in CancelTrade: {ex.Message}", "TradeAPI");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Health check
    /// GET /api/trade/health
    /// </summary>
    public static string GetHealth()
    {
        var runner = Main.GetBotRunner();
        var botRunning = runner != null && Main.Config != null;

        return JsonSerializer.Serialize(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            activeTrades = _activeTrades.Count,
            botInitialized = botRunning,
            mode = Main.Config?.Mode.ToString() ?? "Unknown"
        }, JsonOptions);
    }

    private static ulong ParseUserId(string userId)
    {
        // Try to parse as ulong, otherwise create a hash
        if (ulong.TryParse(userId, out var id))
            return id;

        // Create a consistent hash from the string
        return (ulong)Math.Abs(userId.GetHashCode()) & 0x7FFFFFFFFFFFFFFF;
    }

    private static string GenerateTradeCode()
    {
        var random = new Random();
        return random.Next(10000000, 99999999).ToString();
    }

    // DTOs
    private class TradeRequest
    {
        public string UserId { get; set; } = "";
        public string? UserEmail { get; set; }
        public string TrainerName { get; set; } = "";
        public string Game { get; set; } = "";
        public string ShowdownSet { get; set; } = "";
        public string? TradeCode { get; set; }
    }

    private class CancelRequest
    {
        public string UserId { get; set; } = "";
    }

    private class TradeInfo
    {
        public string TradeId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Game { get; set; } = "";
        public string? TradeCode { get; set; }
        public string Status { get; set; } = "Queued";
        public int QueuePosition { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SubmittedAt { get; set; }
        public List<string>? Messages { get; set; }
    }
}
