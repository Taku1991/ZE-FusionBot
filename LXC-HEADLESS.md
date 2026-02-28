# ZE-FusionBot – LXC Container (Headless, ohne WinForms GUI)

> Basierend auf Analyse vom 28.02.2026  
> Ziel: Bot läuft in einem LXC-Container unter Linux ohne WinForms GUI.  
> Enthalten: Discord, Twitch, YouTube-Integration **und HTTP-Kontrollserver (BotControlPanel)**.  
> Nicht enthalten: WinForms GUI, UpdateChecker (GUI-Teil), Sprite-Vorschau.

---

## TL;DR Machbarkeit

**Ja, vollständig möglich – inklusive HTTP-Webserver.**  
Das Projekt `SysBot.Pokemon.ConsoleApp` existiert bereits als plattformunabhängiger Einstiegspunkt
(`net10.0`, kein WinForms). Der HTTP-Server (`BotServer.cs`) nutzt `System.Net.HttpListener` –
der ist vollständig cross-platform und läuft nativ unter Linux. Nur die WinForms-spezifischen
Callbacks (`Main`-Formreferenz, `Application.Restart()`, `BeginInvoke(MethodInvoker)`) müssen
durch plattformneutrale Entsprechungen ersetzt werden.

---

## Analyse der Projekte

| Projekt | Target | WinForms? | Linux-fähig? |
|---|---|---|---|
| `SysBot.Base` | `net10.0` | Nein | ✅ Ja |
| `SysBot.Pokemon` | `net10.0` | Nein | ✅ Ja |
| `SysBot.Pokemon.Discord` | `net10.0` | Nein | ✅ Ja |
| `SysBot.Pokemon.Twitch` | `net10.0` | Nein | ✅ Ja |
| `SysBot.Pokemon.YouTube` | `net10.0` | Nein | ✅ Ja |
| `SysBot.Pokemon.Z3` | `net10.0` | Nein | ✅ Ja |
| **`SysBot.Pokemon.ConsoleApp`** | `net10.0` | **Nein** | ✅ Erweiterungsbasis |
| `SysBot.Pokemon.WinForms` | `net10.0-windows` | **Ja** | ❌ Nein |
| `SysBot.Pokemon.API` | `net10.0` | Nein | ✅ Ja – **Pflicht** (Homepage-API) |

---

## Detailanalyse: WinForms-Abhängigkeiten im WebApi-Code

Der Webserver (`BotServer.cs`, `WebApiExtensions.cs`, `RestartManager.cs`, `UpdateManager.cs`)
steckt im WinForms-Projekt. Der Code hat folgende WinForms-spezifische Stellen:

| Datei | WinForms-Nutzung | Headless-Ersatz |
|---|---|---|
| `BotServer.cs` | `Main mainForm` im Konstruktor, `_mainForm.Invoke(MethodInvoker)` für Bot-Befehle, `Icon.ExtractAssociatedIcon` für Favicon | `IBotHost`-Interface: Bot-Befehle via `BotRunner.StartAll()` / `StopAll()` u.ä. direkt aufrufen |
| `RestartManager.cs` | `Application.ExecutablePath`, `_mainForm.BeginInvoke(MethodInvoker)` für `SendAll()` | `Environment.ProcessPath`, direkte `BotRunner`-Methoden |
| `WebApiExtensions.cs` | `Application.Restart()`, `Application.Exit()`, `_main.BeginInvoke(MethodInvoker)`, `FlowLayoutPanel`/`BotController`-Reflection | `Process.Start(Environment.ProcessPath)` + `Environment.Exit(0)`, `BotRunner.Bots`-Liste direkt |
| `UpdateManager.cs` | `Application.ExecutablePath` für Statefile-Pfad | `Environment.ProcessPath` |
| `TradeEndpoints.cs` | `Main.GetBotRunner()` static call | Singleton `IPokeBotRunner`-Referenz übergeben |

**Kernpunkt:** Der eigentliche HTTP-Server (`HttpListener`) selbst ist vollständig cross-platform.
Die WinForms-Referenzen betreffen nur die ca. 15–20 Stellen, an denen Bot-Befehle über
Formular-Callbacks ausgelöst werden.

---

## Was geändert werden muss

### 1. `SysBot.Pokemon.ConsoleApp.csproj` – Referenzen ergänzen

```xml
<!-- YouTube-Integration -->
<ProjectReference Include="..\SysBot.Pokemon.YouTube\SysBot.Pokemon.YouTube.csproj" />

<!-- AutoMod für Trade-Validierung -->
<Reference Include="PKHeX.Core.AutoMod">
  <HintPath>..\SysBot.Pokemon\deps\PKHeX.Core.AutoMod.dll</HintPath>
</Reference>
```

---

### 2. `ConsoleApp/PokeBotRunnerImpl.cs` – YouTube ergänzen

```csharp
// Aktuell fehlt YouTube. Ergänzen nach dem Twitch-Block:
using SysBot.Pokemon.YouTube;

protected override void AddIntegrations()
{
    AddDiscordBot(Hub.Config.Discord);
    AddTwitchBot(Hub.Config.Twitch);
    AddYouTubeBot(Hub.Config.YouTube);
}

private void AddYouTubeBot(YouTubeSettings config)
{
    if (string.IsNullOrWhiteSpace(config.ClientID))
        return;
    var bot = new YouTubeBot<T>(config, Hub.Config);
    Task.Run(() => bot.StartAsync(CancellationToken.None), CancellationToken.None);
}
```

---

### 3. `ConsoleApp/Program.cs` – `Console.ReadKey()` → Signal-Handler

Im LXC-Container ohne zugewiesenes TTY (z. B. `systemd`-managed) führt `Console.ReadKey()`
zum Sofort-Absturz, weil kein Terminal vorhanden ist.

```csharp
// ERSETZEN:
//   Console.ReadKey();
// DURCH:
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }
env.StopAll();
```

---

### 4. `ConsoleApp/WebApi/` – WebServer headless portieren

Die gesamte WebApi-Logik aus `SysBot.Pokemon.WinForms/WebApi/` wird in den
`SysBot.Pokemon.ConsoleApp/WebApi/`-Ordner kopiert und angepasst.

#### 4a. `IBotHost`-Interface (neues Bindeglied)

Statt der `Main`-Formreferenz wird ein schlankes Interface übergeben:

```csharp
public interface IBotHost
{
    ProgramConfig Config { get; }
    bool IsRunning { get; }
    string InstanceName { get; }
    IReadOnlyList<BotSourceInfo> GetBotSources();
    void SendAll(BotControlCommand command);
    void PerformRestart();   // Process.Start + Environment.Exit
    void PerformExit();      // Environment.Exit(0)
}
```

#### 4b. `HeadlessBotHost` implementiert `IBotHost`

```csharp
public class HeadlessBotHost : IBotHost
{
    private readonly IPokeBotRunner _runner;
    private readonly ProgramConfig _config;

    public bool IsRunning => _runner.IsRunning;
    public ProgramConfig Config => _config;
    public string InstanceName => _config.Hub?.BotName ?? _config.Mode.ToString();

    public void SendAll(BotControlCommand cmd)
    {
        // BotRunner hat StartAll/StopAll/PauseAll/ResumeAll direkt
        switch (cmd)
        {
            case BotControlCommand.Start:   _runner.StartAll();  break;
            case BotControlCommand.Stop:    _runner.StopAll();   break;
            case BotControlCommand.Idle:    _runner.PauseAll();  break;
            case BotControlCommand.Resume:  _runner.ResumeAll(); break;
            // Restart, RebootAndStop etc. → über BotSource.Restart() iterieren
        }
    }

    public void PerformRestart()
    {
        // Unter Linux/LXC: Process neu starten, dann exit
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException();
        Process.Start(exe);
        Environment.Exit(0);
    }

    public void PerformExit() => Environment.Exit(0);
}
```

#### 4c. Konkrete Änderungen je Datei beim Portieren

| Originaldatei (WinForms) | Änderung beim Portieren |
|---|---|
| `BotServer.cs` | Konstruktor: `Main mainForm` → `IBotHost host`; alle `_mainForm.Invoke(MethodInvoker)` → `_host.SendAll(cmd)` direkt |
| `RestartManager.cs` | `Main? _mainForm` → `IBotHost? _host`; `Application.ExecutablePath` → `Environment.ProcessPath`; `_mainForm.BeginInvoke(MethodInvoker(() => SendAll()))` → `_host.SendAll()` direkt |
| `WebApiExtensions.cs` | `static Main? _main` → `static IBotHost? _host`; alle `_main.BeginInvoke(MethodInvoker(() => Application.Restart()))` → `_host.PerformRestart()`; `Application.Exit()` → `_host.PerformExit()`; `GetBotControllers()` via Reflection-auf-WinForms → `_host.GetBotSources()` |
| `UpdateManager.cs` | `Application.ExecutablePath` → `Environment.ProcessPath`; WinForms-Callbacks für Update-Install durch Shell-Script-Logik ersetzen (funktioniert auch unter Linux) |
| `TradeEndpoints.cs` | `Main.GetBotRunner()` → `IBotHost.Config` + direkte Runner-Referenz |

#### 4d. Was vom Update-System bleibt / wegfällt

| Feature | Headless-Version |
|---|---|
| HTTP-Kontrollpanel (HTML/JS/CSS) | ✅ Bleibt – wird als Embedded Resource ins ConsoleApp eingebettet |
| Restart-Scheduler (Zeitplan) | ✅ Bleibt – `System.Threading.Timer` ohne WinForms |
| Trade-API (`/api/trade/submit`, `/api/trade/status/`) | ✅ Bleibt |
| Remote-Button/Macro über TCP | ✅ Bleibt – nutzt `ISwitchConnectionAsync` direkt |
| Auto-Update (Download + Batch-Script) | ⚠️ Batch-Script (`.bat`) → Shell-Script (`.sh`) – für Linux anpassen |
| Master/Slave Multi-Instanz TCP-Koordination | ✅ Bleibt – reines TCP, kein WinForms |
| `netsh http add urlacl` (URL-Reservierung) | ❌ Entfernen – unter Linux nicht nötig/möglich |
| FontAwesome / GUI-Fonts | ❌ Entfernen |
| `Icon.ExtractAssociatedIcon` (Favicon) | ⚠️ Icon-Datei direkt lesen falls vorhanden, sonst leer |

---

## Systempakete für den LXC Container

Das Discord-Projekt referenziert `PKHeX.Drawing.PokeSprite.dll`, welche GDI+ nutzt.
Unter Linux ist dafür `libgdiplus` nötig:

```bash
# Debian/Ubuntu LXC Template:
apt-get install -y libgdiplus libc6-dev

# .NET 10 Runtime:
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --runtime dotnet
```

> **HTTP-Webserver:** `System.Net.HttpListener` läuft unter Linux nativ ohne Admin-Rechte,
> solange auf `http://+:PORT/` (alle Interfaces) gebunden wird. Unter Linux benötigt das
> **keine** `netsh`-Reservierung (Windows-only) – einfach starten.

---

## Build-Befehl für LXC (Linux x64)

```bash
dotnet publish SysBot.Pokemon.ConsoleApp/SysBot.Pokemon.ConsoleApp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o ./publish/linux-x64
```

Für eine vollständig selbstständige Binary (kein .NET auf dem Host nötig):

```bash
dotnet publish SysBot.Pokemon.ConsoleApp/SysBot.Pokemon.ConsoleApp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/linux-x64-standalone
```

---

## Empfohlene `systemd`-Unit für den LXC Container

```ini
# /etc/systemd/system/zefusionbot.service

[Unit]
Description=ZE FusionBot (Headless)
After=network.target

[Service]
Type=simple
User=botuser
WorkingDirectory=/opt/zefusionbot
ExecStart=/opt/zefusionbot/ZE_FusionBot
Restart=on-failure
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Damit übernimmt `systemd` Neustart-Logik und Logging. Der **Bot-interne Restart-Scheduler**
(täglich zu einer konfigurierten Uhrzeit) kann trotzdem parallel aktiv bleiben – er ruft dann
intern `Process.Start()` + `Environment.Exit()` statt `Application.Restart()` auf. Beide
Varianten funktionieren nebeneinander, `systemd` startet den Prozess danach automatisch neu.

---

## Zusammenfassung der Code-Änderungen

| Datei / Schritt | Beschreibung | Aufwand |
|---|---|---|
| `ConsoleApp.csproj` | YouTube + AutoMod Referenz ergänzen | ~2 Min |
| `ConsoleApp/PokeBotRunnerImpl.cs` | YouTube-Integration ergänzen | ~5 Min |
| `ConsoleApp/Program.cs` | `Console.ReadKey()` → Signal-Handler; WebServer starten | ~15 Min |
| **Neu: `IBotHost`-Interface** | Plattformneutrale Bot-Steuerung (ersetzt `Main`-Referenz) | ~30 Min |
| **Neu: `HeadlessBotHost`** | Implementiert `IBotHost` via `BotRunner`-Methoden | ~1 h |
| `BotServer.cs` portieren | `Main mainForm` → `IBotHost`; `MethodInvoker` → direkte Calls | ~1–2 h |
| `RestartManager.cs` portieren | `Application.*` → `Environment.*` / `IBotHost` | ~1 h |
| `WebApiExtensions.cs` portieren | WinForms-Reflection → `IBotHost.GetBotSources()` | ~1–2 h |
| `UpdateManager.cs` portieren | `Application.ExecutablePath` → `Environment.ProcessPath`; `.bat` → `.sh` | ~30 Min |
| `TradeEndpoints.cs` portieren | `Main.GetBotRunner()` → Singleton-Runner übergeben | ~30 Min |
| HTML/CSS/JS Embedded Resources | In `ConsoleApp.csproj` als `EmbeddedResource` eintragen | ~5 Min |
| **Gesamt** | | **~5–7 h** |

---

## Was vom WebApi komplett übernommen werden kann (ohne Änderung)

- `BotControlCommand.cs` – reines Enum, kein WinForms
- `WebApiTradeNotifier.cs` – kein WinForms
- `TradeEndpoints.cs` (Großteil) – nur `Main.GetBotRunner()` anpassen
- `Models/ApiModels.cs` – nur DTOs, kein WinForms
- Alle `BotControlPanel.*`-Ressourcen (HTML/CSS/JS)
- `BotServer.QueryRemote()` (Static TCP-Helper) – plattformneutral

---

## Nicht benötigte Projekte (können aus dem Build ausgeschlossen werden)

- `SysBot.Pokemon.WinForms` (Windows-only)

---

## `SysBot.Pokemon.API` – Pflichtkomponente (Homepage-API)

Dieses Projekt ist **kein optionales Extra**, sondern die ASP.NET-API zur Homepage und muss
im LXC-Container mitlaufen.

**Gute Nachricht:** Das Projekt hat **keine WinForms-Abhängigkeiten** – Target ist `net10.0`,
kein `net10.0-windows`, kein `UseWindowsForms`. Es läuft bereits headless.

| Eigenschaft | Wert |
|---|---|
| Target Framework | `net10.0` |
| Typ | ASP.NET Core Web API (`Microsoft.NET.Sdk.Web`) |
| WinForms | ❌ Keine |
| Linux-fähig | ✅ Ja, sofort |
| Abhängigkeiten | `SysBot.Pokemon`, `SysBot.Base`, `PKHeX.Core`, `PKHeX.Core.AutoMod` |

### Im LXC starten

Das Projekt kann entweder **separat** (eigener Port) oder **gemeinsam** mit dem ConsoleApp
laufen. Im `ConsoleApp/Program.cs` einfach parallel starten:

```csharp
// API-Host parallel starten (falls aktiviert)
if (!string.IsNullOrEmpty(cfg.Hub.WebServer.RestAPIPort.ToString()))
{
    var apiHost = new ApiHost(cfg.Hub.WebServer.RestAPIPort, origins);
    apiHost.Start();
}
```

Oder als eigenständiger Prozess per `systemd`:

```ini
# /etc/systemd/system/zefusionbot-api.service

[Unit]
Description=ZE FusionBot Homepage API
After=network.target zefusionbot.service

[Service]
Type=simple
User=botuser
WorkingDirectory=/opt/zefusionbot-api
ExecStart=/opt/zefusionbot-api/SysBot.Pokemon.API
Restart=on-failure
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Build-Befehl:

```bash
dotnet publish SysBot.Pokemon.API/SysBot.Pokemon.API.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/linux-x64-api
```
