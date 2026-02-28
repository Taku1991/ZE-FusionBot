using SysBot.Base;
using SysBot.Pokemon;
using System.Collections.Generic;

namespace SysBot.Pokemon.ConsoleApp.WebApi;

/// <summary>
/// Plattformneutrale Abstraktion für Bot-Steuerung (Ersatz für die WinForms Main-Referenz).
/// </summary>
public interface IBotHost
{
    ProgramConfig Config { get; }
    bool IsRunning { get; }
    string InstanceName { get; }
    IReadOnlyList<BotSource<PokeBotState>> GetBotSources();
    void SendAll(BotControlCommand command);
    void PerformRestart();
    void PerformExit();

    /// <summary>
    /// Returns the underlying bot runner (IPokeBotRunner) for direct hub/queue access.
    /// </summary>
    IPokeBotRunner GetBotRunner();
}
