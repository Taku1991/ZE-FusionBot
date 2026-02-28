using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon;
using SysBot.Pokemon.Discord;
using SysBot.Pokemon.ConsoleApp.WebApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.ConsoleApp.WebApi;

/// <summary>
/// Headless-Implementierung von IBotHost für LXC/Linux ohne WinForms.
/// Ersetzt Main-Formreferenzen durch direkte BotRunner-Aufrufe.
/// </summary>
public class HeadlessBotHost : IBotHost
{
    private readonly IPokeBotRunner _runner;
    private readonly ProgramConfig _config;

    public HeadlessBotHost(IPokeBotRunner runner, ProgramConfig config)
    {
        _runner = runner;
        _config = config;
    }

    public ProgramConfig Config => _config;
    public bool IsRunning => _runner.IsRunning;
    public string InstanceName => _config.Hub?.BotName is { Length: > 0 } name ? name : _config.Mode.ToString();

    public IReadOnlyList<BotSource<PokeBotState>> GetBotSources() =>
        _runner.Bots.ToList().AsReadOnly();

    public IPokeBotRunner GetBotRunner() => _runner;

    public void SendAll(BotControlCommand cmd)
    {
        _runner.InitializeStart();

        switch (cmd)
        {
            case BotControlCommand.Start:
                _runner.StartAll();
                break;

            case BotControlCommand.Stop:
                _runner.StopAll();
                break;

            case BotControlCommand.Idle:
                foreach (var b in _runner.Bots) b.Pause();
                break;

            case BotControlCommand.Resume:
                foreach (var b in _runner.Bots) b.Resume();
                break;

            case BotControlCommand.Restart:
                foreach (var b in _runner.Bots)
                    b.Restart();
                break;

            case BotControlCommand.RebootAndStop:
                foreach (var b in _runner.Bots)
                    b.RebootAndStop();
                break;

            case BotControlCommand.ScreenOnAll:
                _ = Task.Run(() => SendScreenStateToAll(true));
                break;

            case BotControlCommand.ScreenOffAll:
                _ = Task.Run(() => SendScreenStateToAll(false));
                break;
        }
    }

    public void PerformRestart()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            LogUtil.LogError("HeadlessBotHost", "Cannot restart: ProcessPath is null.");
            return;
        }

        LogUtil.LogInfo("HeadlessBotHost", "Restarting process...");
        Process.Start(exe);
        Environment.Exit(0);
    }

    public void PerformExit() => Environment.Exit(0);

    private static async Task SendScreenStateToAll(bool turnOn)
    {
        var sent = false;
        var runners = new Func<Task>[]
        {
            () => TrySendScreenState<PA9>(turnOn),
            () => TrySendScreenState<PK9>(turnOn),
            () => TrySendScreenState<PK8>(turnOn),
            () => TrySendScreenState<PA8>(turnOn),
            () => TrySendScreenState<PB8>(turnOn),
            () => TrySendScreenState<PB7>(turnOn),
        };

        foreach (var runner in runners)
        {
            try
            {
                await runner();
                sent = true;
                break;
            }
            catch (BotNotFoundException) { /* kein Bot dieses Typs, weiter */ }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, "[ScreenToggle]");
            }
        }

        if (!sent)
            LogUtil.LogError("[ScreenToggle] Kein passender Bot gefunden.", "RemoteControl");
    }

    private static async Task TrySendScreenState<T>(bool turnOn) where T : PKM, new()
    {
        if (SysCord<T>.Runner == null)
            throw new BotNotFoundException();

        foreach (var botSource in SysCord<T>.Runner.Bots)
        {
            var bot = botSource.Bot;
            var connection = bot.Connection;
            if (connection == null)
                continue;

            var isCRLF = bot is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
            var cmd = SwitchCommand.SetScreen(turnOn ? ScreenState.On : ScreenState.Off, isCRLF);
            await connection.SendAsync(cmd, CancellationToken.None).ConfigureAwait(false);
            LogUtil.LogInfo($"[ScreenToggle] Screen {(turnOn ? "on" : "off")} für {connection.Name}", "RemoteControl");
        }
    }

    private sealed class BotNotFoundException : Exception { }
}
