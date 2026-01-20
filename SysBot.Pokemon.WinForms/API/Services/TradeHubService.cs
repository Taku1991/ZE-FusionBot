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

    // Make _activeTrades static so it can be accessed from TCP handler
    private static readonly ConcurrentDictionary<string, TradeResponse> _activeTrades = new();
    private static int _uniqueTradeCounter = 0;

    // Static mapping: TradeId → Slave Port
    // Master uses this to route status queries to the correct slave
    private static readonly ConcurrentDictionary<string, int> _tradeIdToSlavePort = new();

    public TradeHubService(IHubContext<TradeStatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Static method to process trade directly (called from TCP handler)
    /// </summary>
    public static async Task<TradeResponse> ProcessTradeDirectlyAsync(TradeRequest request)
    {
        // Create a temporary instance to handle the trade
        var tempService = new TradeHubService(null!); // No SignalR for TCP-based trades
        return await tempService.SubmitTradeAsync(request);
    }

    /// <summary>
    /// Static method to get trade status directly (called from TCP handler)
    /// </summary>
    public static async Task<TradeResponse?> GetTradeStatusDirectlyAsync(string tradeId)
    {
        await Task.CompletedTask;
        return _activeTrades.TryGetValue(tradeId, out var trade) ? trade : null;
    }

    /// <summary>
    /// Submits a trade request to the bot system
    /// </summary>
    public async Task<TradeResponse> SubmitTradeAsync(TradeRequest request)
    {
        // Use the TradeId from the request if provided (routed from Master)
        // Otherwise create a new one (direct submission)
        var tradeId = !string.IsNullOrEmpty(request.TradeId)
            ? request.TradeId
            : Guid.NewGuid().ToString();

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
            // Check if this is the Master instance (port 8080)
            // Master routes trades to the correct bot instance
            bool isMaster = IsMasterInstance();

            if (isMaster)
            {
                // Master: Check if this trade is for our own game mode
                var config = Main.Config;
                if (config != null)
                {
                    var normalizedRequestGame = NormalizeGameName(request.Game);
                    var normalizedOwnMode = NormalizeGameName(config.Mode.ToString());

                    if (normalizedRequestGame.Equals(normalizedOwnMode, StringComparison.OrdinalIgnoreCase))
                    {
                        // This is for our game, process locally
                        var result = await EnqueueTradeWithCorrectType(request, tradeCode, tradeId, response);
                        return result;
                    }
                }

                // Different game, route to appropriate slave
                return await RouteTradeToInstance(request, response);
            }

            // Slave instance: Process trade locally
            // Use reflection to call a typed method that handles the entire trade process
            // This ensures the Pokemon is created with the correct type for this bot instance
            var slaveResult = await EnqueueTradeWithCorrectType(request, tradeCode, tradeId, response);
            return slaveResult;
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
        // Check if this trade was routed to a slave bot
        if (_tradeIdToSlavePort.TryGetValue(tradeId, out var slavePort))
        {
            try
            {
                // Query the slave bot for the trade status via TCP
                var result = await SendTcpCommandAsync(slavePort, $"GET_STATUS:{tradeId}");

                if (!result.StartsWith("ERROR"))
                {
                    var slaveStatus = System.Text.Json.JsonSerializer.Deserialize<TradeResponse>(result);
                    if (slaveStatus != null)
                    {
                        return slaveStatus;
                    }
                }

                // Only log errors, not normal status queries
                if (!result.Contains("Trade not found"))
                {
                    LogUtil.LogError($"Failed to get status from slave on port {slavePort}: {result}", "TradeHubService");
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error querying slave for status: {ex.Message}", "TradeHubService");
            }
        }

        // Fallback: Check local trades (for non-routed trades or if slave query failed)
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
        var botRunner = Main.GetBotRunner();
        if (botRunner == null)
        {
            LogUtil.LogError("Bot runner is null", "TradeHubService");
            return null;
        }

        // Cast to dynamic to access Hub property (not exposed on interface but available on implementation)
        dynamic dynamicRunner = botRunner;

        // Get the actual running hub (not config)
        var hub = dynamicRunner.Hub;
        if (hub == null)
        {
            LogUtil.LogError("Hub is null", "TradeHubService");
            return null;
        }

        LogUtil.LogInfo($"Getting bot hub for game: {game}", "TradeHubService");
        LogUtil.LogInfo($"Hub type: {hub.GetType().Name}, Bot count: {hub.Bots.Count}", "TradeHubService");
        LogUtil.LogInfo($"TradeBotsReady: {hub.TradeBotsReady}, CanQueue: {hub.Config.Queues.CanQueue}", "TradeHubService");

        // Return the hub based on the game
        // The hub is generic, so we use dynamic to handle different PKM types
        // All games use the same hub instance, just return it
        return hub;
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

                // Get the correct trainer and create Pokemon for the specific game format
                var template = AutoLegalityWrapper.GetTemplate(set);
                LogUtil.LogInfo($"Generated template for {set.Species} (Game: {game})", "TradeHubService");

                // Get trainer info with preferences
                ITrainerInfo sav;
                if (preferences != null && !string.IsNullOrEmpty(preferences.OriginalTrainerName))
                {
                    var generation = GetGeneration(game);
                    sav = new SimpleTrainerInfo
                    {
                        OT = preferences.OriginalTrainerName,
                        TID16 = (ushort)(preferences.TrainerID ?? 12345),
                        SID16 = (ushort)(preferences.SecretID ?? 54321),
                        Language = ParseLanguage(preferences.Language),
                        Generation = (byte)generation
                    };
                }
                else
                {
                    // Get trainer info specific to the game format
                    sav = GetTrainerInfoForGame(game);
                }

                // Generate legal Pokemon with the correct format
                LogUtil.LogInfo($"Calling GetLegal for {set.Species} with game format {game}", "TradeHubService");
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

        // Convert userId (GUID string) to ulong for PokeTradeTrainerInfo
        ulong userId = ConvertUserIdToUlong(request.UserId);
        var trainerInfo = new PokeTradeTrainerInfo(request.TrainerName, userId);

        // Create trade detail based on game type
        return request.Game.ToUpperInvariant() switch
        {
            "SV" => CreateTradeDetailTyped((PK9)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "PLZA" => CreateTradeDetailTyped((PA9)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "SWSH" => CreateTradeDetailTyped((PK8)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "BDSP" => CreateTradeDetailTyped((PB8)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "PLA" => CreateTradeDetailTyped((PA8)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID),
            "LGPE" => CreateTradeDetailTypedLGPE((PB7)pkm, trainerInfo, tradeCode, response, tradeId, uniqueTradeID, request.LgpeTradeCode),
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

    private PokeTradeDetail<PB7> CreateTradeDetailTypedLGPE(PB7 pkm, PokeTradeTrainerInfo trainerInfo, int tradeCode, TradeResponse response, string tradeId, int uniqueTradeID, string? lgpeCodeString)
    {
        var notifier = new SignalRTradeNotifier<PB7>(_hubContext, tradeId, response);

        // Parse LGPE trade code
        List<Pictocodes>? lgpeCodes = null;
        if (!string.IsNullOrEmpty(lgpeCodeString))
        {
            lgpeCodes = ParseLgpeTradeCode(lgpeCodeString);
        }

        return new PokeTradeDetail<PB7>(
            pkm,
            trainerInfo,
            notifier,
            PokeTradeType.Specific,
            tradeCode,
            lgcode: lgpeCodes,
            uniqueTradeID: uniqueTradeID
        );
    }

    private static List<Pictocodes> ParseLgpeTradeCode(string lgpeCodeString)
    {
        var codes = new List<Pictocodes>();

        // Split by comma and trim
        var parts = lgpeCodeString.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (Enum.TryParse<Pictocodes>(trimmed, true, out var code))
            {
                codes.Add(code);
            }
            else
            {
                LogUtil.LogError($"Invalid LGPE trade code: {trimmed}", "TradeHubService");
            }
        }

        // Validate: Must have exactly 3 codes
        if (codes.Count != 3)
        {
            LogUtil.LogError($"LGPE trade code must have exactly 3 Pokemon, got {codes.Count}. Using default: Pikachu,Pikachu,Pikachu", "TradeHubService");
            return new List<Pictocodes> { Pictocodes.Pikachu, Pictocodes.Pikachu, Pictocodes.Pikachu };
        }

        return codes;
    }

    private static void EnqueueTrade(dynamic hub, dynamic tradeDetail)
    {
        try
        {
            LogUtil.LogInfo($"EnqueueTrade called with trade detail type: {tradeDetail.GetType().Name}", "TradeHubService");

            // Use reflection to call Enqueue since dynamic doesn't work well with generics
            var queuesManager = hub.Queues;
            LogUtil.LogInfo($"Queues manager type: {queuesManager.GetType().Name}", "TradeHubService");

            var enqueueMethod = queuesManager.GetType().GetMethod("Enqueue");

            if (enqueueMethod != null)
            {
                LogUtil.LogInfo($"Calling Enqueue with: RoutineType=LinkTrade, TradeDetail={tradeDetail.GetType().Name}, Priority=0", "TradeHubService");
                enqueueMethod.Invoke(queuesManager, new object[] { PokeRoutineType.LinkTrade, tradeDetail, 0u });
                LogUtil.LogInfo("✅ Trade enqueued successfully via reflection", "TradeHubService");

                // Check bot count using the Count property
                int totalBots = hub.Bots.Count;
                bool allBotsIdle = hub.Bots.All((Func<dynamic, bool>)(bot => bot.Config.CurrentRoutineType == PokeRoutineType.Idle));
                LogUtil.LogInfo($"Bot status: Total bots={totalBots}, All idle={allBotsIdle}, TradeBotsReady={hub.TradeBotsReady}", "TradeHubService");
            }
            else
            {
                LogUtil.LogError("❌ Failed to find Enqueue method on TradeQueueManager", "TradeHubService");
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"❌ Failed to enqueue trade: {ex.Message}\n{ex.StackTrace}", "TradeHubService");
            throw;
        }
    }

    private static QueueInfo GetQueueInfo(dynamic hub, string game)
    {
        try
        {
            // Use reflection to call GetQueue since dynamic doesn't work well with generics
            var queuesManager = hub.Queues;
            var getQueueMethod = queuesManager.GetType().GetMethod("GetQueue");
            var queue = getQueueMethod?.Invoke(queuesManager, new object[] { PokeRoutineType.LinkTrade });

            int queueCount = 0;
            if (queue != null)
            {
                // Get Count property via reflection
                var countProp = queue.GetType().GetProperty("Count");
                queueCount = (int)(countProp?.GetValue(queue) ?? 0);
            }

            // Count bots using Count property (ConcurrentPool doesn't support iteration)
            int totalBots = hub.Bots.Count;

            // Check if any bot is available for trading (not all idle)
            bool botsReady = hub.TradeBotsReady;

            return new QueueInfo
            {
                Game = game,
                TotalInQueue = queueCount,
                AvailableBots = totalBots,
                CurrentlyProcessing = botsReady ? 1 : 0, // Simplified: if bots are ready, at least one is processing
                AverageWaitMinutes = Math.Max(1, queueCount * 2), // Estimate 2 min per trade
                IsOpen = hub.Config.Queues.CanQueue && botsReady
            };
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to get queue info: {ex.Message}", "TradeHubService");
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

    /// <summary>
    /// Checks if this is the Master instance
    /// The Master is the instance that runs the REST API (port 9080)
    /// Slaves only run TCP listeners (ports 8081-8086)
    /// </summary>
    private static bool IsMasterInstance()
    {
        try
        {
            // The key difference: Check if this bot instance has the REST API server
            // Only the Master has the REST API (ApiHost) running on port 9080
            // Slaves only have TCP listeners (no REST API)

            bool hasRestApi = WebApiExtensions.HasRestApiServer();
            return hasRestApi;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"IsMasterInstance check failed: {ex.Message}", "TradeHubService");
            return false;
        }
    }

    /// <summary>
    /// Normalizes game names to match bot mode names
    /// </summary>
    private static string NormalizeGameName(string gameName)
    {
        return gameName.ToUpperInvariant() switch
        {
            "PLA" => "LA",    // Pokémon Legends: Arceus
            "LA" => "LA",
            "PLZA" => "PLZA", // Pokémon Legends: Z-A
            "BDSP" => "BDSP", // Brilliant Diamond/Shining Pearl
            "SWSH" => "SWSH", // Sword/Shield
            "SV" => "SV",     // Scarlet/Violet
            "LGPE" => "LGPE", // Let's Go Pikachu/Eevee
            _ => gameName.ToUpperInvariant()
        };
    }

    /// <summary>
    /// Routes trade request to the correct bot instance via TCP
    /// </summary>
    private async Task<TradeResponse> RouteTradeToInstance(TradeRequest request, TradeResponse response)
    {
        try
        {
            // Find all running instances
            var instances = GetAllBotInstances();

            // Normalize the game name (e.g., "PLA" → "LA")
            string normalizedGame = NormalizeGameName(request.Game);

            // Find the instance that handles this game
            int? targetPort = null;
            foreach (var (port, mode) in instances)
            {
                string normalizedMode = NormalizeGameName(mode);
                if (normalizedMode.Equals(normalizedGame, StringComparison.OrdinalIgnoreCase))
                {
                    targetPort = port;
                    break;
                }
            }

            if (targetPort == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = $"No bot instance found for game: {request.Game}";
                LogUtil.LogError($"No bot instance found for game: {request.Game}", "TradeHubService");
                return response;
            }

            // IMPORTANT: Set the TradeId in the request so the slave uses the same ID
            request.TradeId = response.TradeId;

            // Serialize the request to JSON
            var requestJson = System.Text.Json.JsonSerializer.Serialize(request);

            // Send the trade request via TCP to the target instance
            var result = await SendTcpCommandAsync(targetPort.Value, $"SUBMIT_TRADE:{requestJson}");

            if (result.StartsWith("ERROR"))
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = result;
                return response;
            }

            // Parse the JSON response from the slave instance
            try
            {
                var slaveResponse = System.Text.Json.JsonSerializer.Deserialize<TradeResponse>(result);
                if (slaveResponse != null)
                {
                    // Copy relevant data from slave response
                    response.Status = slaveResponse.Status;
                    response.ErrorMessage = slaveResponse.ErrorMessage;
                    response.QueuePosition = slaveResponse.QueuePosition;
                    response.EstimatedWaitMinutes = slaveResponse.EstimatedWaitMinutes;
                    response.BotName = slaveResponse.BotName;

                    if (slaveResponse.Messages != null)
                    {
                        response.Messages.AddRange(slaveResponse.Messages);
                    }

                    // IMPORTANT: Remember which slave is handling this trade
                    // So we can route status queries to the correct slave
                    _tradeIdToSlavePort[response.TradeId] = targetPort.Value;
                }
                else
                {
                    response.Messages.Add($"Trade routed to {request.Game} bot instance");
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Failed to parse slave response: {ex.Message}", "TradeHubService");
                response.Messages.Add($"Trade routed to {request.Game} bot instance (response parse failed)");
            }

            return response;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to route trade: {ex.Message}", "TradeHubService");
            response.Status = TradeStatus.Failed;
            response.ErrorMessage = $"Failed to route trade: {ex.Message}";
            return response;
        }
    }

    /// <summary>
    /// Gets the shared directory for port files that all bot instances can access
    /// </summary>
    private static string GetSharedPortDirectory()
    {
        // Use a common temp directory that all bot instances can access
        // regardless of which folder they're running from
        var sharedDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZE_FusionBot_Ports");

        // Ensure directory exists
        if (!System.IO.Directory.Exists(sharedDir))
        {
            System.IO.Directory.CreateDirectory(sharedDir);
        }

        return sharedDir;
    }

    /// <summary>
    /// Gets all running bot instances with their ports and game modes
    /// </summary>
    private static List<(int Port, string Mode)> GetAllBotInstances()
    {
        var instances = new List<(int, string)>();

        try
        {
            // Use shared directory so all bot instances can discover each other
            var baseDir = GetSharedPortDirectory();
            var portFiles = System.IO.Directory.GetFiles(baseDir, "ZE_FusionBot_*.port");

            foreach (var portFile in portFiles)
            {
                try
                {
                    var portText = System.IO.File.ReadAllText(portFile).Trim();

                    if (int.TryParse(portText, out int port))
                    {
                        // Query the instance for its INFO
                        var infoJson = QueryRemoteTcp(port, "INFO");

                        if (string.IsNullOrEmpty(infoJson))
                        {
                            LogUtil.LogError($"Empty response from port {port}", "TradeHubService");
                            continue;
                        }

                        if (infoJson.StartsWith("ERROR"))
                        {
                            LogUtil.LogError($"Error response from port {port}: {infoJson}", "TradeHubService");
                            continue;
                        }

                        // Parse the JSON to get the Mode
                        var info = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(infoJson);
                        if (info.TryGetProperty("Mode", out var modeElement))
                        {
                            var mode = modeElement.GetString() ?? "Unknown";
                            instances.Add((port, mode));
                        }
                        else
                        {
                            LogUtil.LogError($"No 'Mode' property found in response from port {port}", "TradeHubService");
                        }
                    }
                    else
                    {
                        LogUtil.LogError($"Failed to parse port from: {portText}", "TradeHubService");
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Failed to query instance from file {portFile}: {ex.Message}", "TradeHubService");
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to get bot instances: {ex.Message}\n{ex.StackTrace}", "TradeHubService");
        }

        return instances;
    }

    /// <summary>
    /// Sends a TCP command to a remote instance
    /// </summary>
    private static string QueryRemoteTcp(int port, string command)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();

            // Set connection timeout
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
            {
                LogUtil.LogError($"Connection to port {port} timed out", "TradeHubService");
                return "ERROR: Connection timeout";
            }

            // Set read/write timeout
            using var stream = client.GetStream();
            stream.ReadTimeout = 5000;
            stream.WriteTimeout = 5000;

            using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);

            writer.WriteLine(command);
            return reader.ReadLine() ?? "";
        }
        catch (System.IO.IOException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            LogUtil.LogError($"Socket error querying port {port}: Connection timeout or refused", "TradeHubService");
            return "ERROR: Connection failed";
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to query remote TCP on port {port}: {ex.Message}", "TradeHubService");
            return $"ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends a TCP command asynchronously
    /// </summary>
    private static async Task<string> SendTcpCommandAsync(int port, string command)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var client = new System.Net.Sockets.TcpClient();

            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var stream = client.GetStream();
            using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);

            await writer.WriteLineAsync(command);

            var response = await reader.ReadLineAsync(cts.Token);
            return response ?? "";
        }
        catch (System.OperationCanceledException)
        {
            LogUtil.LogError($"TCP command to port {port} timed out after 10 seconds", "TradeHubService");
            return "ERROR: Timeout";
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to send TCP command to port {port}: {ex.Message}", "TradeHubService");
            return $"ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Converts a userId (GUID string or number) to ulong
    /// </summary>
    private static ulong ConvertUserIdToUlong(string userId)
    {
        // Try to parse as ulong directly first
        if (ulong.TryParse(userId, out var directValue))
            return directValue;

        // If it's a GUID, convert it to a ulong by hashing
        try
        {
            // Take first 8 bytes of the GUID
            if (Guid.TryParse(userId, out var guid))
            {
                var bytes = guid.ToByteArray();
                // Convert first 8 bytes to ulong
                return BitConverter.ToUInt64(bytes, 0);
            }
        }
        catch
        {
            // Fallback
        }

        // Last resort: hash the string
        return (ulong)userId.GetHashCode();
    }

    /// <summary>
    /// Gets the correct trainer info for the specific game format
    /// This ensures we create the right PKM type (PK8 vs PB8, etc.)
    /// </summary>
    private static ITrainerInfo GetTrainerInfoForGame(string game)
    {
        return game.ToUpperInvariant() switch
        {
            "SV" => AutoLegalityWrapper.GetTrainerInfo<PK9>(),
            "PLZA" => AutoLegalityWrapper.GetTrainerInfo<PA9>(),
            "SWSH" => AutoLegalityWrapper.GetTrainerInfo<PK8>(),
            "BDSP" => AutoLegalityWrapper.GetTrainerInfo<PB8>(),
            "PLA" => AutoLegalityWrapper.GetTrainerInfo<PA8>(),
            "LGPE" => AutoLegalityWrapper.GetTrainerInfo<PB7>(),
            _ => AutoLegalityWrapper.GetFallbackTrainer()
        };
    }

    /// <summary>
    /// Enqueues a trade by calling a typed method based on the current bot's ProgramMode.
    /// This ensures the Pokemon is created with the correct type for this bot instance.
    /// </summary>
    private async Task<TradeResponse> EnqueueTradeWithCorrectType(TradeRequest request, int tradeCode, string tradeId, TradeResponse response)
    {
        try
        {
            var config = Main.Config;
            if (config == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = "Bot configuration not found";
                return response;
            }

            LogUtil.LogInfo($"Current bot mode: {config.Mode}", "TradeHubService");

            // Call the typed method based on the bot's configured mode
            return config.Mode switch
            {
                ProgramMode.SV => await EnqueueTradeTyped<PK9>(request, tradeCode, tradeId, response),
                ProgramMode.PLZA => await EnqueueTradeTyped<PA9>(request, tradeCode, tradeId, response),
                ProgramMode.SWSH => await EnqueueTradeTyped<PK8>(request, tradeCode, tradeId, response),
                ProgramMode.BDSP => await EnqueueTradeTyped<PB8>(request, tradeCode, tradeId, response),
                ProgramMode.LA => await EnqueueTradeTyped<PA8>(request, tradeCode, tradeId, response),
                ProgramMode.LGPE => await EnqueueTradeTyped<PB7>(request, tradeCode, tradeId, response),
                _ => throw new NotSupportedException($"Game mode {config.Mode} is not supported")
            };
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to enqueue trade: {ex.Message}\n{ex.StackTrace}", "TradeHubService");
            response.Status = TradeStatus.Failed;
            response.ErrorMessage = $"Error: {ex.Message}";
            return response;
        }
    }

    /// <summary>
    /// Enqueues a trade with the correct Pokemon type T.
    /// This method is called via the switch statement above to ensure type safety.
    /// </summary>
    private async Task<TradeResponse> EnqueueTradeTyped<T>(TradeRequest request, int tradeCode, string tradeId, TradeResponse response) where T : PKM, new()
    {
        try
        {
            // Get the bot runner
            var botRunner = Main.GetBotRunner();
            if (botRunner == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = "Bot runner not found";
                return response;
            }

            // Cast to typed runner
            var typedRunner = botRunner as PokeBotRunner<T>;
            if (typedRunner == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = $"Bot runner type mismatch. Expected PokeBotRunner<{typeof(T).Name}>";
                return response;
            }

            var hub = typedRunner.Hub;
            if (hub == null)
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = "Hub not found";
                return response;
            }

            LogUtil.LogInfo($"Using typed runner: PokeBotRunner<{typeof(T).Name}>", "TradeHubService");
            LogUtil.LogInfo($"Hub type: {hub.GetType().Name}, Bots: {hub.Bots.Count}, TradeBotsReady: {hub.TradeBotsReady}", "TradeHubService");

            // Create Pokemon with correct type
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var showdownSet = new ShowdownSet(request.ShowdownSet);
            var template = AutoLegalityWrapper.GetTemplate(showdownSet);

            LogUtil.LogInfo($"Creating Pokemon: {showdownSet.Species}", "TradeHubService");
            var pkm = sav.GetLegal(template, out var legality);

            if (pkm == null || !(pkm is T typedPkm))
            {
                response.Status = TradeStatus.Failed;
                response.ErrorMessage = $"Failed to create legal Pokemon. Legality: {legality}";
                return response;
            }

            LogUtil.LogInfo($"Pokemon created successfully: {typedPkm.Species} (Type: {typeof(T).Name})", "TradeHubService");

            // Convert userId to ulong
            ulong userId = ConvertUserIdToUlong(request.UserId);
            var trainerInfo = new PokeTradeTrainerInfo(request.TrainerName, userId);

            // Create trade detail with SignalR notifier
            var notifier = new SignalRTradeNotifier<T>(_hubContext, tradeId, response);

            // Parse LGPE trade code if this is an LGPE trade
            List<Pictocodes>? lgpeCodes = null;
            if (typeof(T) == typeof(PB7) && !string.IsNullOrEmpty(request.LgpeTradeCode))
            {
                lgpeCodes = ParseLgpeTradeCode(request.LgpeTradeCode);
            }

            var tradeDetail = new PokeTradeDetail<T>(
                typedPkm,
                trainerInfo,
                notifier,
                PokeTradeType.Specific,
                tradeCode,
                lgcode: lgpeCodes,
                uniqueTradeID: (int)Interlocked.Increment(ref _uniqueTradeCounter)
            );

            LogUtil.LogInfo($"Trade detail created: {typeof(PokeTradeDetail<T>).Name}", "TradeHubService");

            // Get queue info
            var queue = hub.Queues.GetQueue(PokeRoutineType.LinkTrade);
            int queueCount = queue?.Count ?? 0;

            response.QueuePosition = queueCount + 1;
            response.EstimatedWaitMinutes = Math.Max(1, queueCount * 2);

            LogUtil.LogInfo($"Queue status before enqueue: {queueCount} trades in queue", "TradeHubService");

            // Enqueue the trade - this is now type-safe!
            hub.Queues.Enqueue(PokeRoutineType.LinkTrade, tradeDetail, 0u);

            LogUtil.LogInfo("✅ Trade enqueued successfully (type-safe)", "TradeHubService");

            // Verify enqueue
            var queueAfter = hub.Queues.GetQueue(PokeRoutineType.LinkTrade);
            int queueCountAfter = queueAfter?.Count ?? 0;
            LogUtil.LogInfo($"Queue status after enqueue: {queueCountAfter} trades in queue", "TradeHubService");

            response.Messages.Add("Trade submitted successfully!");
            response.Messages.Add($"Your trade code is: {tradeCode:D8}");
            response.Messages.Add("Please enter this code in your game and start searching.");
            response.Messages.Add($"Queue position: {response.QueuePosition}");

            // Notify via SignalR
            if (_hubContext != null)
            {
                await _hubContext.NotifyTradeUpdate(tradeId, response);
            }

            return response;
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to enqueue typed trade: {ex.Message}\n{ex.StackTrace}", "TradeHubService");
            response.Status = TradeStatus.Failed;
            response.ErrorMessage = $"Error: {ex.Message}";
            return response;
        }
    }
}
