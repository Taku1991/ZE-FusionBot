namespace SysBot.Pokemon.API.Models;

public class QueueInfo
{
    /// <summary>
    /// Game version
    /// </summary>
    public required string Game { get; set; }

    /// <summary>
    /// Total trades in queue
    /// </summary>
    public int TotalInQueue { get; set; }

    /// <summary>
    /// Currently processing trades
    /// </summary>
    public int CurrentlyProcessing { get; set; }

    /// <summary>
    /// Available bot count for this game
    /// </summary>
    public int AvailableBots { get; set; }

    /// <summary>
    /// Average wait time in minutes
    /// </summary>
    public int AverageWaitMinutes { get; set; }

    /// <summary>
    /// Queue is accepting new trades
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
