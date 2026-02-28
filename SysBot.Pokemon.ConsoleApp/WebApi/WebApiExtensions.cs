using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using SysBot.Pokemon.ConsoleApp.API;
using SysBot.Pokemon.ConsoleApp.WebApi;
using static SysBot.Pokemon.ConsoleApp.WebApi.RestartManager;

namespace SysBot.Pokemon.ConsoleApp.WebApi;

public static class WebApiExtensions
{
    private static BotServer? _server;
    private static TcpListener? _tcp;
    private static CancellationTokenSource? _cts;
    private static CancellationTokenSource? _monitorCts;
    private static IBotHost? _host;
    private static ApiHost? _apiHost;

    private static int _webPort = 8080; // Will be set from config
    private static int _tcpPort = 0;
    private static readonly object _portLock = new object();
    private static readonly ConcurrentDictionary<int, DateTime> _portReservations = new();

    /// <summary>
    /// Get the IBotHost for this instance (used by UpdateManager)
    /// </summary>
    public static IBotHost? GetHost() => _host;

    /// <summary>
    /// Get the current TCP port for this bot instance
    /// </summary>
    public static int GetCurrentTcpPort() => _tcpPort;

    /// <summary>
    /// Check if this bot instance is running the REST API server (Master).
    /// </summary>
    public static bool HasRestApiServer() => _apiHost != null;

    public static void InitWebServer(IBotHost host)
    {
        _host = host;
        _cts = new CancellationTokenSource(); // Initialize early for background tasks

        // Get the configured port from settings
        if (host.Config?.Hub?.WebServer != null)
        {
            _webPort = host.Config.Hub.WebServer.ControlPanelPort;

            // Validate port range
            if (_webPort < 1 || _webPort > 65535)
            {
                LogUtil.LogError("WebServer", $"Invalid web server port {_webPort}. Using default port 8080.");
                _webPort = 8080;
            }

            // Update the UpdateManager with the configured port
            UpdateManager.SetConfiguredWebPort(_webPort);

            // Check if web server is enabled
            if (!host.Config.Hub.WebServer.EnableWebServer)
            {
                LogUtil.LogInfo("WebServer", "Web Control Panel is disabled in settings.");
                return;
            }

            LogUtil.LogInfo("WebServer", $"Web Control Panel will be hosted on port {_webPort}");
        }
        else
        {
            // No config available, use default and update UpdateManager
            UpdateManager.SetConfiguredWebPort(_webPort);
        }

        try
        {
            CleanupStalePortFiles();

            CheckPostRestartStartup(host);

            if (IsPortInUse(_webPort))
            {
                lock (_portLock)
                {
                    _tcpPort = FindAvailablePort(8081);
                    ReservePort(_tcpPort);
                }
                StartTcpOnly();

                StartMasterMonitor();
                RestartManager.Initialize(_host, _tcpPort);
                // Check for any pending update state and attempt to resume
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10000); // Wait for system to stabilize
                    var currentState = UpdateManager.GetCurrentState();
                    if (currentState != null && !currentState.IsComplete)
                    {
                        LogUtil.LogInfo("WebServer", $"Found incomplete update session {currentState.SessionId}, attempting to resume");
                        await UpdateManager.StartOrResumeUpdateAsync(_host, _tcpPort);
                    }
                });

                // Slaves don't start REST API - only Master does
                LogUtil.LogInfo("WebServer", "Running as slave instance - REST API will be provided by master on port 8080");

                return;
            }

            // No netsh needed on Linux - HttpListener handles permissions differently
            // On Linux, binding to http://+:port/ may require root or authbind

            lock (_portLock)
            {
                _tcpPort = FindAvailablePort(8081);
                ReservePort(_tcpPort);
            }
            StartFullServer();

            RestartManager.Initialize(_host, _tcpPort);
            // Check for any pending update state and attempt to resume
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000); // Wait for system to stabilize
                var currentState = UpdateManager.GetCurrentState();
                if (currentState != null && !currentState.IsComplete)
                {
                    LogUtil.LogInfo("WebServer", $"Found incomplete update session {currentState.SessionId}, attempting to resume");
                    await UpdateManager.StartOrResumeUpdateAsync(_host, _tcpPort);
                }
            });

            // Periodically clean up completed update sessions
            _ = Task.Run(async () =>
            {
                while (_cts != null && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), _cts.Token);
                        UpdateManager.ClearState();
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Exit gracefully when cancelled
                    }
                }
            });
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebServer", $"Failed to initialize web server: {ex.Message}");
        }
    }

    private static void ReservePort(int port)
    {
        _portReservations[port] = DateTime.Now;
    }

    private static void ReleasePort(int port)
    {
        _portReservations.TryRemove(port, out _);
    }

    private static void CleanupStalePortFiles()
    {
        try
        {
            // Use shared directory so all bot instances can discover each other
            var portDir = GetSharedPortDirectory();

            // Also clean up stale port reservations (older than 5 minutes)
            var now = DateTime.Now;
            var staleReservations = _portReservations
                .Where(kvp => (now - kvp.Value).TotalMinutes > 5)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var port in staleReservations)
            {
                _portReservations.TryRemove(port, out _);
            }

            var portFiles = Directory.GetFiles(portDir, "ZE_FusionBot_*.port");

            foreach (var portFile in portFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(portFile);
                    var pidStr = fileName.Substring("ZE_FusionBot_".Length);

                    if (int.TryParse(pidStr, out int pid))
                    {
                        if (pid == Environment.ProcessId)
                            continue;

                        try
                        {
                            var process = Process.GetProcessById(pid);
                            if (process.ProcessName.Contains("SysBot", StringComparison.OrdinalIgnoreCase) ||
                                process.ProcessName.Contains("ZE_FusionBot", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                        catch (ArgumentException)
                        {
                        }

                        File.Delete(portFile);
                        LogUtil.LogInfo("WebServer", $"Cleaned up stale port file: {Path.GetFileName(portFile)}");
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError("WebServer", $"Error processing port file {portFile}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebServer", $"Failed to cleanup stale port files: {ex.Message}");
        }
    }

    private static void StartMasterMonitor()
    {
        _monitorCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            var random = new Random();

            while (!_monitorCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000 + random.Next(5000), _monitorCts.Token);

                    if (UpdateManager.IsUpdateInProgress() || RestartManager.IsRestartInProgress)
                    {
                        continue;
                    }

                    if (!IsPortInUse(_webPort))
                    {
                        LogUtil.LogInfo("WebServer", "Master web server is down. Attempting to take over...");

                        await Task.Delay(random.Next(1000, 3000));

                        if (!IsPortInUse(_webPort) && !UpdateManager.IsUpdateInProgress() && !RestartManager.IsRestartInProgress)
                        {
                            TryTakeOverAsMaster();
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogUtil.LogError("WebServer", $"Error in master monitor: {ex.Message}");
                }
            }
        }, _monitorCts.Token);
    }

    private static void TryTakeOverAsMaster()
    {
        try
        {
            _server = new BotServer(_host!, _webPort, _tcpPort);
            _server.Start();

            _monitorCts?.Cancel();
            _monitorCts = null;

            LogUtil.LogInfo("WebServer", $"Successfully took over as master web server on port {_webPort}");
            LogUtil.LogInfo("WebServer", $"Web interface is now available at http://localhost:{_webPort}");
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebServer", $"Failed to take over as master: {ex.Message}");
            StartMasterMonitor();
        }
    }

    private static void StartTcpOnly()
    {
        // Initialize TradeHubService so TCP-routed trades can be processed
        if (_host != null)
            SysBot.Pokemon.ConsoleApp.API.Services.TradeHubService.Initialize(_host);

        StartTcp();

        // Slaves no longer need their own web server - logs are read directly from file by master

        CreatePortFile();
    }

    private static void StartRestAPI()
    {
        try
        {
            if (_host?.Config?.Hub?.WebServer == null || !_host.Config.Hub.WebServer.EnableRestAPI)
            {
                LogUtil.LogInfo("REST API is disabled in settings.", "ApiHost");
                return;
            }

            var apiPort = _host.Config.Hub.WebServer.RestAPIPort;
            var corsOrigins = _host.Config.Hub.WebServer.CorsOrigins;

            var origins = string.IsNullOrWhiteSpace(corsOrigins)
                ? new[] { "http://localhost:3000" }
                : corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .ToArray();

            _apiHost = new ApiHost(apiPort, origins);
            _apiHost.Start();
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to start REST API: {ex.Message}", "ApiHost");
        }
    }

    private static void StartFullServer()
    {
        try
        {
            _server = new BotServer(_host!, _webPort, _tcpPort);
            _server.Start();
            StartTcp();
            CreatePortFile();

            // Initialize TradeEndpoints and TradeHubService with the host
            TradeEndpoints.Initialize(_host!);
            SysBot.Pokemon.ConsoleApp.API.Services.TradeHubService.Initialize(_host!);

            // Start REST API with SignalR
            StartRestAPI();
        }
        catch (Exception ex) when (ex.Message.Contains("conflicts with an existing registration"))
        {
            // Another instance became master first - gracefully become a slave
            LogUtil.LogInfo("WebServer", $"Port {_webPort} conflict during startup, starting as slave");
            StartTcpOnly();  // This will create the port file as a slave
        }
    }

    private static void StartTcp()
    {
        _cts ??= new CancellationTokenSource(); // Only create if not already created
        Task.Run(() => StartTcpListenerAsync(_cts.Token));
    }

    private static async Task StartTcpListenerAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        var random = new Random();

        for (int retry = 0; retry < maxRetries && !cancellationToken.IsCancellationRequested; retry++)
        {
            try
            {
                _tcp = new TcpListener(System.Net.IPAddress.Loopback, _tcpPort);
                _tcp.Start();

                LogUtil.LogInfo("TCP", $"TCP listener started successfully on port {_tcpPort}");

                await AcceptClientsAsync(cancellationToken);
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse && retry < maxRetries - 1)
            {
                LogUtil.LogInfo("TCP", $"TCP port {_tcpPort} in use, finding new port (attempt {retry + 1}/{maxRetries})");
                await Task.Delay(random.Next(500, 1500), cancellationToken);

                lock (_portLock)
                {
                    ReleasePort(_tcpPort);
                    _tcpPort = FindAvailablePort(_tcpPort + 1);
                    ReservePort(_tcpPort);
                }

                CreatePortFile();
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogUtil.LogError("TCP", $"TCP listener error: {ex.Message}");

                if (retry == maxRetries - 1)
                {
                    LogUtil.LogError("TCP", $"Failed to start TCP listener after {maxRetries} attempts");
                    throw new InvalidOperationException("Unable to find available TCP port");
                }
            }
        }
    }

    private static async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _tcp!.AcceptTcpClientAsync(token);
                _ = HandleClientSafelyAsync(client);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown - don't scream about it
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                // Also normal during shutdown
                break;
            }
            catch (ObjectDisposedException)
            {
                // Listener got cleaned up during exit
                break;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"TCP Listener Error: {ex}", "TCP");
            }
        }
    }


    private static async Task HandleClientSafelyAsync(TcpClient client)
    {
        try
        {
            await HandleClient(client);
        }
        catch (Exception ex)
        {
            LogUtil.LogError("TCP", $"Unhandled error in HandleClient: {ex.Message}");
        }
    }

    private static async Task HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                var command = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(command))
                {
                    var response = await ProcessCommandAsync(command);
                    await writer.WriteLineAsync(response);
                    await writer.FlushAsync();
                }
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // Normal disconnection - don't log as error
        }
        catch (ObjectDisposedException)
        {
            // Normal during shutdown
        }
        catch (Exception ex)
        {
            LogUtil.LogError("TCP", $"Error handling TCP client: {ex.Message}");
        }
    }

    private static async Task<string> ProcessCommandAsync(string command)
    {
        return await Task.Run(() => ProcessCommand(command));
    }

    private static string ProcessCommand(string command)
    {
        if (_host == null)
            return "ERROR: Bot host not initialized";

        var parts = command.Split(':');
        var cmd = parts[0].ToUpperInvariant();
        var botId = parts.Length > 1 ? parts[1] : null;

        return cmd switch
        {
            "STARTALL" => ExecuteGlobalCommand(BotControlCommand.Start),
            "STOPALL" => ExecuteGlobalCommand(BotControlCommand.Stop),
            "IDLEALL" => ExecuteGlobalCommand(BotControlCommand.Idle),
            "RESUMEALL" => ExecuteGlobalCommand(BotControlCommand.Resume),
            "RESTARTALL" => ExecuteGlobalCommand(BotControlCommand.Restart),
            "REBOOTALL" => ExecuteGlobalCommand(BotControlCommand.RebootAndStop),
            "SCREENONALL" => ExecuteGlobalCommand(BotControlCommand.ScreenOnAll),
            "SCREENOFFALL" => ExecuteGlobalCommand(BotControlCommand.ScreenOffAll),
            "LISTBOTS" => GetBotsList(),
            "STATUS" => GetBotStatuses(botId),
            "ISREADY" => CheckReady(),
            "INFO" => GetInstanceInfo(),
            "VERSION" => SysBot.Pokemon.PokeBot.Version,
            "UPDATE" => TriggerUpdate(),
            "SELFRESTARTALL" => TriggerSelfRestart(),
            "RESTARTSCHEDULE" => GetRestartSchedule(),
            "REMOTE_BUTTON" => HandleRemoteButton(parts),
            "REMOTE_MACRO" => HandleRemoteMacro(parts),
            "SUBMIT_TRADE" => HandleSubmitTrade(command),
            "GET_STATUS" => HandleGetStatus(command),
            _ => $"ERROR: Unknown command '{cmd}'"
        };
    }

    private static string HandleSubmitTrade(string command)
    {
        try
        {
            var colonIndex = command.IndexOf(':');
            if (colonIndex == -1 || colonIndex == command.Length - 1)
            {
                LogUtil.LogError("Invalid SUBMIT_TRADE format", "WebApiExtensions");
                return "ERROR: Invalid SUBMIT_TRADE format. Expected SUBMIT_TRADE:{JSON}";
            }

            var json = command.Substring(colonIndex + 1);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var request = System.Text.Json.JsonSerializer.Deserialize<SysBot.Pokemon.ConsoleApp.API.Models.TradeRequest>(json, options);
            if (request == null)
                return "ERROR: Failed to deserialize TradeRequest";

            var response = SysBot.Pokemon.ConsoleApp.API.Services.TradeHubService.ProcessTradeDirectlyAsync(request).Result;
            return System.Text.Json.JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to handle SUBMIT_TRADE: {ex.Message}", "WebApiExtensions");
            return $"ERROR: {ex.Message}";
        }
    }

    private static string HandleGetStatus(string command)
    {
        try
        {
            var colonIndex = command.IndexOf(':');
            if (colonIndex == -1 || colonIndex == command.Length - 1)
            {
                LogUtil.LogError("Invalid GET_STATUS format", "WebApiExtensions");
                return "ERROR: Invalid GET_STATUS format. Expected GET_STATUS:{tradeId}";
            }

            var tradeId = command.Substring(colonIndex + 1);
            var status = SysBot.Pokemon.ConsoleApp.API.Services.TradeHubService.GetTradeStatusDirectlyAsync(tradeId).Result;

            if (status == null)
                return "ERROR: Trade not found";

            return System.Text.Json.JsonSerializer.Serialize(status);
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to handle GET_STATUS: {ex.Message}", "WebApiExtensions");
            return $"ERROR: {ex.Message}";
        }
    }

    private static volatile bool _updateInProgress = false;
    private static readonly object _updateLock = new();

    private static string TriggerUpdate()
    {
        try
        {
            lock (_updateLock)
            {
                if (_updateInProgress)
                {
                    LogUtil.LogInfo("WebApiExtensions", "Update already in progress - ignoring duplicate request");
                    return "Update already in progress";
                }
                _updateInProgress = true;
            }

            if (_host == null)
            {
                lock (_updateLock) { _updateInProgress = false; }
                return "ERROR: Bot host not initialized";
            }

            LogUtil.LogInfo("WebApiExtensions", $"Update triggered for slave instance on port {_tcpPort}");

            // Perform the actual update: download, install, and restart (same as master)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Check for updates and get download URL
                    var (updateAvailable, _, newVersion, downloadUrl) = await HeadlessUpdateChecker.CheckForUpdatesAsync(false);

                    if (!updateAvailable && string.IsNullOrEmpty(downloadUrl))
                    {
                        LogUtil.LogInfo("WebApiExtensions", "No update available, but proceeding with restart for sync");
                        _host.PerformRestart();
                        return;
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        LogUtil.LogError("WebApiExtensions", "No download URL available for update");
                        lock (_updateLock) { _updateInProgress = false; }
                        return;
                    }

                    LogUtil.LogInfo("WebApiExtensions", $"Downloading update from: {downloadUrl}");

                    // Download the update
                    string tempPath = await DownloadUpdateForSlaveAsync(downloadUrl);

                    if (string.IsNullOrEmpty(tempPath))
                    {
                        LogUtil.LogError("WebApiExtensions", "Failed to download update");
                        lock (_updateLock) { _updateInProgress = false; }
                        return;
                    }

                    LogUtil.LogInfo("WebApiExtensions", $"Update downloaded to: {tempPath}");

                    // Install the update via shell script (Linux-compatible)
                    InstallUpdateAndRestartSlave(tempPath);
                }
                catch (Exception ex)
                {
                    LogUtil.LogError("WebApiExtensions", $"Error during slave update: {ex.Message}");
                    lock (_updateLock) { _updateInProgress = false; }
                }
            });

            return "OK: Update triggered";
        }
        catch (Exception ex)
        {
            lock (_updateLock) { _updateInProgress = false; }
            return $"ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Download update file for slave instance
    /// </summary>
    private static async Task<string> DownloadUpdateForSlaveAsync(string downloadUrl)
    {
        try
        {
            var uri = new Uri(downloadUrl);
            var originalFileName = Path.GetFileName(uri.LocalPath);
            string tempPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(originalFileName)}_{Guid.NewGuid()}.exe");

            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "ZE-FusionBot");

            var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            var fileBytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempPath, fileBytes);

            LogUtil.LogInfo("WebApiExtensions", $"Downloaded {fileBytes.Length} bytes to {tempPath}");
            return tempPath;
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebApiExtensions", $"Failed to download update: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Install update and restart slave instance (Linux shell script, replaces Windows batch script)
    /// </summary>
    private static void InstallUpdateAndRestartSlave(string downloadedFilePath)
    {
        try
        {
            string currentExePath = Environment.ProcessPath ?? Environment.CurrentDirectory;
            string applicationDirectory = Path.GetDirectoryName(currentExePath) ?? Environment.CurrentDirectory;
            string executableName = Path.GetFileName(currentExePath);

            // Use ZE_FusionBot as the target name (standardized name, no .exe on Linux)
            string targetExeName = "ZE_FusionBot";
            string targetExePath = Path.Combine(applicationDirectory, targetExeName);
            string backupPath = Path.Combine(applicationDirectory, $"{executableName}.backup");

            // Create shell script file for update process
            string scriptPath = Path.Combine(Path.GetTempPath(), $"UpdateSysBot_Slave_{Environment.ProcessId}.sh");
            string scriptContent = $@"#!/bin/sh
sleep 2
echo 'Updating ZE-FusionBot (Slave Instance)...'
# Backup current version
if [ -f '{currentExePath}' ]; then
    [ -f '{backupPath}' ] && rm -f '{backupPath}'
    mv '{currentExePath}' '{backupPath}'
fi
# Install new version with standardized name
mv '{downloadedFilePath}' '{targetExePath}'
chmod +x '{targetExePath}'
# Start new version
'{targetExePath}' &
# Clean up this script
rm -f '$0'
";

            File.WriteAllText(scriptPath, scriptContent);

            // Make the script executable
            using var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x {scriptPath}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            chmod?.WaitForExit(5000);

            LogUtil.LogInfo("WebApiExtensions", "Starting slave update shell script");

            // Start the update shell script
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = scriptPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);

            // Exit the current instance
            LogUtil.LogInfo("WebApiExtensions", "Exiting slave instance for update");
            _host?.PerformExit();
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebApiExtensions", $"Failed to install slave update: {ex.Message}");
            lock (_updateLock) { _updateInProgress = false; }
        }
    }

    private static string TriggerSelfRestart()
    {
        try
        {
            if (_host == null)
                return "ERROR: Bot host not initialized";

            Task.Run(async () =>
            {
                await Task.Delay(2000);
                _host.PerformRestart();
            });

            return "OK: Restart triggered";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string GetRestartSchedule()
    {
        try
        {
            var config = RestartManager.GetScheduleConfig();
            var nextRestart = RestartManager.NextScheduledRestart;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                config.Enabled,
                config.Time,
                NextRestart = nextRestart?.ToString("yyyy-MM-dd HH:mm:ss"),
                IsRestartInProgress = RestartManager.IsRestartInProgress,
                CurrentState = RestartManager.CurrentState.ToString()
            });
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string ExecuteGlobalCommand(BotControlCommand command)
    {
        try
        {
            _host!.SendAll(command);
            return $"OK: {command} command sent to all bots";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to execute {command} - {ex.Message}";
        }
    }

    private static string GetBotsList()
    {
        try
        {
            if (_host == null)
                return "ERROR: Bot host not initialized";

            var botList = new List<object>();
            var botSources = _host.GetBotSources();

            foreach (var source in botSources)
            {
                var bot = source.Bot;
                if (bot == null) continue;

                var connCfg = bot.Config.Connection;
                botList.Add(new
                {
                    Id = $"{connCfg.IP}:{connCfg.Port}",
                    Name = bot.Connection.Label ?? connCfg.IP,
                    RoutineType = bot.Config.InitialRoutine.ToString(),
                    Status = source.IsRunning ? (source.IsPaused ? "Paused" : "Running") : "Stopped",
                    ConnectionType = bot.Connection.GetType().Name,
                    IP = connCfg.IP,
                    Port = connCfg.Port
                });
            }

            return System.Text.Json.JsonSerializer.Serialize(new { Bots = botList });
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebAPI", $"GetBotsList error: {ex.Message}");
            return $"ERROR: Failed to get bots list - {ex.Message}";
        }
    }

    private static string GetBotStatuses(string? botId)
    {
        try
        {
            if (_host == null)
                return "ERROR: Bot host not initialized";

            var botSources = _host.GetBotSources();

            if (string.IsNullOrEmpty(botId))
            {
                var statuses = botSources
                    .Where(s => s.Bot != null)
                    .Select(s => new
                    {
                        Id = $"{s.Bot!.Config.Connection.IP}:{s.Bot.Config.Connection.Port}",
                        Name = s.Bot.Connection.Label ?? s.Bot.Config.Connection.IP,
                        Status = s.IsRunning ? (s.IsPaused ? "Paused" : "Running") : "Stopped"
                    }).ToList();

                return System.Text.Json.JsonSerializer.Serialize(statuses);
            }

            var target = botSources.FirstOrDefault(s =>
                s.Bot != null &&
                $"{s.Bot.Config.Connection.IP}:{s.Bot.Config.Connection.Port}" == botId);

            if (target == null)
                return "ERROR: Bot not found";

            return target.IsRunning ? (target.IsPaused ? "Paused" : "Running") : "Stopped";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get status - {ex.Message}";
        }
    }

    private static string CheckReady()
    {
        try
        {
            if (_host == null)
                return "NOT_READY";

            var hasRunningBots = _host.GetBotSources().Any(s => s.IsRunning && !s.IsPaused);
            return hasRunningBots ? "READY" : "NOT_READY";
        }
        catch
        {
            return "NOT_READY";
        }
    }

    private static string GetInstanceInfo()
    {
        try
        {
            var config = _host?.Config;
            var version = SysBot.Pokemon.PokeBot.Version;
            var mode = config?.Mode.ToString() ?? "Unknown";
            var name = GetInstanceName(config, mode);

            var info = new
            {
                Version = version,
                Mode = mode,
                Name = name,
                Environment.ProcessId,
                Port = _tcpPort,
                ProcessPath = Environment.ProcessPath
            };

            return System.Text.Json.JsonSerializer.Serialize(info);
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get instance info - {ex.Message}";
        }
    }

    private static string HandleRemoteButton(string[] parts)
    {
        try
        {
            if (parts.Length < 3)
                return "ERROR: Invalid command format. Expected REMOTE_BUTTON:button:botIndex";

            var button = parts[1];
            if (!int.TryParse(parts[2], out var botIndex))
                return "ERROR: Invalid bot index";

            if (_host == null)
                return "ERROR: Bot host not initialized";

            var botSources = _host.GetBotSources();
            if (botIndex < 0 || botIndex >= botSources.Count)
                return $"ERROR: Bot index {botIndex} out of range";

            var botSource = botSources[botIndex];

            if (botSource?.Bot == null)
                return $"ERROR: Bot at index {botIndex} not available";

            if (!botSource.IsRunning)
                return $"ERROR: Bot at index {botIndex} is not running";

            var bot = botSource.Bot;
            if (bot.Connection is not SysBot.Base.ISwitchConnectionAsync connection)
                return "ERROR: Bot connection not available";

            var switchButton = MapButtonToSwitch(button);
            if (switchButton == null)
                return $"ERROR: Invalid button: {button}";

            var cmd = SwitchCommand.Click(switchButton.Value);

            // Execute the command synchronously since we're already in a background thread
            Task.Run(async () => await connection.SendAsync(cmd, CancellationToken.None)).Wait();

            return $"OK: Button {button} pressed on bot {botIndex}";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string HandleRemoteMacro(string[] parts)
    {
        try
        {
            if (parts.Length < 3)
                return "ERROR: Invalid command format. Expected REMOTE_MACRO:macro:botIndex";

            var macro = parts[1];
            if (!int.TryParse(parts[2], out var botIndex))
                return "ERROR: Invalid bot index";

            if (_host == null)
                return "ERROR: Bot host not initialized";

            var botSources = _host.GetBotSources();
            if (botIndex < 0 || botIndex >= botSources.Count)
                return $"ERROR: Bot index {botIndex} out of range";

            var botSource = botSources[botIndex];

            if (botSource?.Bot == null)
                return $"ERROR: Bot at index {botIndex} not available";

            if (!botSource.IsRunning)
                return $"ERROR: Bot at index {botIndex} is not running";

            // For now, just return success - macro implementation can be added later
            return $"OK: Macro {macro} executed on bot {botIndex}";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static SysBot.Base.SwitchButton? MapButtonToSwitch(string button)
    {
        return button.ToUpperInvariant() switch
        {
            "A" => SysBot.Base.SwitchButton.A,
            "B" => SysBot.Base.SwitchButton.B,
            "X" => SysBot.Base.SwitchButton.X,
            "Y" => SysBot.Base.SwitchButton.Y,
            "UP" => SysBot.Base.SwitchButton.DUP,
            "DOWN" => SysBot.Base.SwitchButton.DDOWN,
            "LEFT" => SysBot.Base.SwitchButton.DLEFT,
            "RIGHT" => SysBot.Base.SwitchButton.DRIGHT,
            "L" => SysBot.Base.SwitchButton.L,
            "R" => SysBot.Base.SwitchButton.R,
            "ZL" => SysBot.Base.SwitchButton.ZL,
            "ZR" => SysBot.Base.SwitchButton.ZR,
            "LSTICK" => SysBot.Base.SwitchButton.LSTICK,
            "RSTICK" => SysBot.Base.SwitchButton.RSTICK,
            "HOME" => SysBot.Base.SwitchButton.HOME,
            "CAPTURE" => SysBot.Base.SwitchButton.CAPTURE,
            "PLUS" => SysBot.Base.SwitchButton.PLUS,
            "MINUS" => SysBot.Base.SwitchButton.MINUS,
            _ => null
        };
    }

    private static string GetInstanceName(ProgramConfig? config, string mode)
    {
        if (!string.IsNullOrEmpty(config?.Hub?.BotName))
            return config.Hub.BotName;

        return mode switch
        {
            "LGPE" => "LGPE",
            "BDSP" => "BDSP",
            "SWSH" => "SWSH",
            "SV" => "SV",
            "LA" => "LA",
            _ => "PokeBot"
        };
    }

    /// <summary>
    /// Gets the shared directory for port files that all bot instances can access
    /// </summary>
    private static string GetSharedPortDirectory()
    {
        // Use a common temp directory that all bot instances can access
        // regardless of which folder they're running from
        var sharedDir = Path.Combine(Path.GetTempPath(), "ZE_FusionBot_Ports");

        // Ensure directory exists
        if (!Directory.Exists(sharedDir))
        {
            Directory.CreateDirectory(sharedDir);
        }

        return sharedDir;
    }

    private static void CreatePortFile()
    {
        try
        {
            // Use shared directory so all bot instances can discover each other
            var portDir = GetSharedPortDirectory();
            var portFile = Path.Combine(portDir, $"ZE_FusionBot_{Environment.ProcessId}.port");
            var tempFile = portFile + ".tmp";

            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs))
            {
                writer.WriteLine(_tcpPort);
                // No longer writing web port - slaves don't have web servers
                writer.Flush();
                fs.Flush(true);
            }

            File.Move(tempFile, portFile, true);
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebServer", $"Failed to create port file: {ex.Message}");
        }
    }

    private static void CleanupPortFile()
    {
        try
        {
            // Use shared directory so all bot instances can discover each other
            var portDir = GetSharedPortDirectory();
            var portFile = Path.Combine(portDir, $"ZE_FusionBot_{Environment.ProcessId}.port");

            if (File.Exists(portFile))
                File.Delete(portFile);
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebServer", $"Failed to cleanup port file: {ex.Message}");
        }
    }

    private static int FindAvailablePort(int startPort)
    {
        // Use shared directory so all bot instances can discover each other
        var portDir = GetSharedPortDirectory();

        // Use a lock to prevent race conditions
        lock (_portLock)
        {
            for (int port = startPort; port < startPort + 100; port++)
            {
                // Check if port is reserved by another instance
                if (_portReservations.ContainsKey(port))
                    continue;

                if (!IsPortInUse(port))
                {
                    // Check if any port file claims this port
                    var portFiles = Directory.GetFiles(portDir, "ZE_FusionBot_*.port");
                    bool portClaimed = false;

                    foreach (var file in portFiles)
                    {
                        try
                        {
                            // Lock the file before reading to prevent race conditions
                            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var reader = new StreamReader(fs);
                            var content = reader.ReadToEnd().Trim();
                            if (content == port.ToString() || content.Contains($"\"Port\":{port}"))
                            {
                                portClaimed = true;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (!portClaimed)
                    {
                        // Double-check the port is still available
                        if (!IsPortInUse(port))
                        {
                            return port;
                        }
                    }
                }
            }
        }
        throw new InvalidOperationException("No available ports found");
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(200) };
            var response = client.GetAsync($"http://localhost:{port}/api/bot/instances").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            try
            {
                using var tcpClient = new TcpClient();
                var result = tcpClient.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
                if (success)
                {
                    tcpClient.EndConnect(result);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void StopWebServer(IBotHost host)
    {
        try
        {
            _monitorCts?.Cancel();
            _cts?.Cancel();
            _tcp?.Stop();
            _server?.Dispose();
            _apiHost?.Dispose();
            _apiHost = null;
            RestartManager.Shutdown();

            // Release the port reservations
            lock (_portLock)
            {
                ReleasePort(_tcpPort);
            }

            CleanupPortFile();
        }
        catch (Exception ex)
        {
            LogUtil.LogError("WebServer", $"Error stopping web server: {ex.Message}");
        }
    }

    private static void CheckPostRestartStartup(IBotHost host)
    {
        try
        {
            var workingDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            var restartFlagPath = Path.Combine(workingDir, "restart_in_progress.flag");
            var updateFlagPath = Path.Combine(workingDir, "update_in_progress.flag");

            bool isPostRestart = File.Exists(restartFlagPath);
            bool isPostUpdate = File.Exists(updateFlagPath);

            if (!isPostRestart && !isPostUpdate)
                return;

            string operation = isPostRestart ? "restart" : "update";
            string logSource = isPostRestart ? "RestartManager" : "UpdateManager";

            LogUtil.LogInfo($"Post-{operation} startup detected. Waiting for all instances to come online...", logSource);

            if (isPostRestart) File.Delete(restartFlagPath);
            if (isPostUpdate) File.Delete(updateFlagPath);

            Task.Run(() => HandlePostOperationStartupAsync(host, operation, logSource));
        }
        catch (Exception ex)
        {
            LogUtil.LogError("StartupManager", $"Error checking post-restart/update startup: {ex.Message}");
        }
    }

    private static async Task HandlePostOperationStartupAsync(IBotHost host, string operation, string logSource)
    {
        await Task.Delay(5000);

        const int maxAttempts = 12;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                LogUtil.LogInfo($"Post-{operation} check attempt {attempt + 1}/{maxAttempts}", logSource);

                // Start local bots
                host.SendAll(BotControlCommand.Start);
                LogUtil.LogInfo("Start All command sent to local bots", logSource);

                // Start remote instances
                var instances = GetAllRunningInstances(0);
                if (instances.Count > 0)
                {
                    LogUtil.LogInfo($"Found {instances.Count} remote instances online. Sending Start All command...", logSource);
                    await SendStartCommandsToRemoteInstancesAsync(instances, logSource);
                }

                LogUtil.LogInfo($"Post-{operation} Start All commands completed successfully", logSource);
                break;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error during post-{operation} startup attempt {attempt + 1}: {ex.Message}", logSource);
                if (attempt < maxAttempts - 1)
                    await Task.Delay(5000);
            }
        }
    }

    private static async Task SendStartCommandsToRemoteInstancesAsync(List<(int Port, int ProcessId)> instances, string logSource)
    {
        var tasks = instances.Select(instance => Task.Run(() =>
        {
            try
            {
                var response = BotServer.QueryRemote(instance.Port, "STARTALL");
                LogUtil.LogInfo($"Start command sent to port {instance.Port}: {response}", logSource);
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Failed to send start command to port {instance.Port}: {ex.Message}", logSource);
            }
        }));

        await Task.WhenAll(tasks);
    }

    private static List<(int Port, int ProcessId)> GetAllRunningInstances(int currentPort)
    {
        var instances = new List<(int, int)>();

        try
        {
            var processes = Process.GetProcessesByName("ZE_FusionBot")
                .Where(p => p.Id != Environment.ProcessId);

            foreach (var process in processes)
            {
                try
                {
                    var exePath = process.MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath))
                        continue;

                    // Use shared directory so all bot instances can discover each other
                    var portFile = Path.Combine(GetSharedPortDirectory(), $"ZE_FusionBot_{process.Id}.port");
                    if (!File.Exists(portFile))
                        continue;

                    var portText = File.ReadAllText(portFile).Trim();
                    // Port file now contains TCP port on first line
                    var lines = portText.Split('\n', '\r').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                    if (lines.Length == 0 || !int.TryParse(lines[0], out var port))
                        continue;

                    if (IsPortInUse(port))
                    {
                        instances.Add((port, process.Id));
                    }
                }
                catch { }
            }
        }
        catch { }

        return instances;
    }
}
