# SysBot.Pokemon.API

REST API for Pokemon Trade Bot integration with web applications.

## Features

- ✅ Trade submission endpoint
- ✅ Batch trade support
- ✅ Real-time status tracking
- ✅ WebSocket support via SignalR
- ✅ Queue management
- ✅ Multi-game support (SV, SWSH, BDSP, PLA, LGPE, PLZA)
- ✅ Showdown format support
- ✅ CORS enabled for web integration

## Quick Start

### Prerequisites

- .NET 10.0 SDK
- Running PokeTradeHub instance

### Installation

```bash
# Restore dependencies
dotnet restore

# Build project
dotnet build

# Run API
dotnet run
```

The API will start on `http://localhost:8080`

### Configuration

Edit `appsettings.json`:

```json
{
  "ApiSettings": {
    "Port": 8080,
    "EnableSwagger": true
  }
}
```

## API Documentation

Once running, visit:
- Swagger UI: `http://localhost:8080/swagger`
- Health Check: `http://localhost:8080/api/trade/health`

## Integration with PokeTradeHub

The API needs to be connected to your PokeTradeHub instance:

```csharp
var tradeHubService = app.Services.GetRequiredService<TradeHubService>();
tradeHubService.RegisterHub(yourHub, "SV");
```

## Architecture

```
┌─────────────────┐
│   Next.js Web   │
│   Application   │
└────────┬────────┘
         │ HTTP/WS
         ▼
┌─────────────────┐
│  ASP.NET Core   │
│   Web API       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  TradeHubService│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  PokeTradeHub   │
│   (SysBot.NET)  │
└─────────────────┘
```

## Endpoints

### Trade Management
- `POST /api/trade/submit` - Submit single trade
- `POST /api/trade/submit-batch` - Submit batch trade
- `POST /api/trade/{id}/cancel` - Cancel trade
- `GET /api/trade/status/{id}` - Get trade status

### Queue Information
- `GET /api/queue/{game}` - Get queue for specific game
- `GET /api/queue/all` - Get all queue info

### WebSocket
- `WS /ws/trade` - SignalR hub for real-time updates

# Test endpoint
curl http://localhost:8080/api/trade/health
```

## License

Same as parent SysBot.NET project
