namespace SysBot.Pokemon.API.Models;

public class TradeResponse
{
    /// <summary>
    /// Unique trade ID for tracking
    /// </summary>
    public required string TradeId { get; set; }

    /// <summary>
    /// User ID who submitted the trade
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Current trade status
    /// </summary>
    public required TradeStatus Status { get; set; }

    /// <summary>
    /// When the trade was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trade code for link trade
    /// </summary>
    public string? TradeCode { get; set; }

    /// <summary>
    /// Position in queue
    /// </summary>
    public int? QueuePosition { get; set; }

    /// <summary>
    /// Estimated wait time in minutes
    /// </summary>
    public int? EstimatedWaitMinutes { get; set; }

    /// <summary>
    /// Bot name handling the trade
    /// </summary>
    public string? BotName { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional messages
    /// </summary>
    public List<string>? Messages { get; set; }

    /// <summary>
    /// Timestamp of last update
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum TradeStatus
{
    Queued,
    Searching,
    Trading,
    Completed,
    Failed,
    Cancelled
}
