using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SysBot.Pokemon.WinForms.API.Hubs;
using SysBot.Pokemon.WinForms.API.Services;
using SysBot.Base;

namespace SysBot.Pokemon.WinForms.API;

/// <summary>
/// Hosts the ASP.NET Core API with SignalR in the WinForms application
/// </summary>
public class ApiHost : IDisposable
{
    private WebApplication? _app;
    private Task? _runTask;
    private readonly int _port;
    private readonly string[] _corsOrigins;

    public ApiHost(int port, string[] corsOrigins)
    {
        _port = port;
        _corsOrigins = corsOrigins;
    }

    public void Start()
    {
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = AppContext.BaseDirectory
            });

            // Configure Kestrel to listen on the specified port
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(_port);
            });

            // Suppress console logging output (we use our own logging)
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new CustomLoggerProvider());

            // Add services
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });
            builder.Services.AddSignalR();

            // Register TradeHubService as singleton
            builder.Services.AddSingleton<TradeHubService>();

            // Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowHomepage", policy =>
                {
                    policy.WithOrigins(_corsOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            _app = builder.Build();

            // Configure middleware
            _app.UseCors("AllowHomepage");
            _app.UseRouting();

            // Map controllers and SignalR hub
            _app.MapControllers();
            _app.MapHub<TradeStatusHub>("/ws/trade");

            // Start the web application in a background task
            _runTask = Task.Run(async () =>
            {
                try
                {
                    await _app.RunAsync();
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"API host error: {ex.Message}", "ApiHost");
                }
            });

            LogUtil.LogInfo($"REST API with SignalR started on http://0.0.0.0:{_port}", "ApiHost");
            LogUtil.LogInfo($"SignalR hub available at ws://0.0.0.0:{_port}/ws/trade", "ApiHost");
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"Failed to start API host: {ex.Message}", "ApiHost");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            try
            {
                await _app.StopAsync();
                LogUtil.LogInfo("API host stopped", "ApiHost");
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error stopping API host: {ex.Message}", "ApiHost");
            }
        }
    }

    public void Dispose()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
            _app?.DisposeAsync().AsTask().Wait(5000);
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}

/// <summary>
/// Custom logger provider that redirects to LogUtil
/// </summary>
internal class CustomLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new CustomLogger(categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Custom logger that redirects to LogUtil
/// </summary>
internal class CustomLogger : ILogger
{
    private readonly string _categoryName;

    public CustomLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var category = _categoryName.Split('.').LastOrDefault() ?? "API";

        switch (logLevel)
        {
            case LogLevel.Error:
            case LogLevel.Critical:
                LogUtil.LogError($"{message}", category);
                if (exception != null)
                    LogUtil.LogError($"{exception}", category);
                break;
        }
    }
}
