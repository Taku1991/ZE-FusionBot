using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SysBot.Pokemon.API.Services;
using SysBot.Pokemon.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
// Swagger temporarily disabled due to version incompatibility with .NET 10
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

// Add SignalR for WebSocket support
builder.Services.AddSignalR();

// Add CORS for Next.js integration
// Load CORS origins from appsettings.json
var corsOrigins = new List<string>();
var localDev = builder.Configuration.GetSection("CorsOrigins:LocalDevelopment").Get<string[]>();
var tailscaleVMs = builder.Configuration.GetSection("CorsOrigins:TailscaleVMs").Get<string[]>();
var productionDomains = builder.Configuration.GetSection("CorsOrigins:ProductionDomains").Get<string[]>();

if (localDev != null) corsOrigins.AddRange(localDev);
if (tailscaleVMs != null) corsOrigins.AddRange(tailscaleVMs);
if (productionDomains != null) corsOrigins.AddRange(productionDomains);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJS", policy =>
    {
        policy.WithOrigins(corsOrigins.ToArray())
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Register TradeHub service as singleton
builder.Services.AddSingleton<TradeHubService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Swagger temporarily disabled
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseHttpsRedirection();
app.UseCors("AllowNextJS");
app.UseAuthorization();

app.MapControllers();
app.MapHub<TradeStatusHub>("/ws/trade");

app.Run();
