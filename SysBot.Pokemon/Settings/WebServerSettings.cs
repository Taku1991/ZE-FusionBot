using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Settings for the Web Control Panel server
/// </summary>
public sealed class WebServerSettings
{
    private const string WebServer = nameof(WebServer);
    private const string RestAPI = "REST API";

    [Category(WebServer)]
    [Description("The port number for the Bot Control Panel web interface. Default is 8080.")]
    public int ControlPanelPort { get; set; } = 8080;

    [Category(WebServer)]
    [Description("Enable or disable the web control panel. When disabled, the web interface will not be accessible.")]
    public bool EnableWebServer { get; set; } = true;

    [Category(WebServer)]
    [Description("Allow external connections to the web control panel. When false, only localhost connections are allowed.")]
    public bool AllowExternalConnections { get; set; } = false;

    [Category(RestAPI)]
    [Description("Enable or disable the REST API with SignalR. This allows external applications and websites to interact with the bot.")]
    public bool EnableRestAPI { get; set; } = true;

    [Category(RestAPI)]
    [Description("The port number for the REST API server. Default is 9080.")]
    public int RestAPIPort { get; set; } = 9080;

    [Category(RestAPI)]
    [Description("Comma-separated list of allowed CORS origins for the REST API (e.g., https://example.com,http://localhost:3000). Leave empty to allow all origins.")]
    public string CorsOrigins { get; set; } = "http://localhost:3000";
}
