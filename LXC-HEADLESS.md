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


---

##  LXC-Container Setup  7 Instanzen + SMB/NFS Share

Diese Sektion beschreibt die vollständige Einrichtung eines Proxmox LXC-Containers für den Betrieb von 7 Bot-Instanzen und der Homepage-API.

---

### 1. Ressourcenplanung

| Ressource | Pro Instanz | 7 Instanzen + API | Empfehlung Container |
|-----------|-------------|-------------------|----------------------|
| RAM | ~350500 MB | ~34 GB | **6 GB** (mit Luft) |
| CPU (vCores) | ~0,51 Core | ~47 Cores | **8 vCores** |
| Disk | ~500 MB | ~4 GB + Logs | **20 GB** |
| Netzwerk | 1 GbE reicht |  | Bridged `vmbr0` |

> **Hinweis**: Der Speicherbedarf hängt stark von der PKM-Datenbank und den Build-Binaries ab. Lieber großzügig planen.

---

### 2. Proxmox LXC-Container erstellen

Entweder per WebUI oder direkt mit `pct`:

```bash
# Ubuntu 24.04 LTS Template (pveam list local)
pct create 112 local:vztmpl/ubuntu-24.04-standard_24.04-2_amd64.tar.zst \
  --hostname sysbot \
  --cores 8 \
  --memory 6144 \
  --rootfs local-lvm:20 \
  --net0 name=eth0,bridge=vmbr0,ip=dhcp \
  --unprivileged 1 \
  --features nesting=1 \
  --ostype ubuntu \
  --start 1
```

Im Container als `root`:

```bash
apt update && apt upgrade -y
apt install -y \
  libgdiplus \
  libicu-dev \
  libssl-dev \
  ca-certificates \
  tzdata \
  cifs-utils \
  nfs-common \
  curl wget git
```

Systemzeit setzen:

```bash
timedatectl set-timezone Europe/Berlin
```

Bot-User anlegen (kein Root-Betrieb):

```bash
useradd -m -s /bin/bash botuser
```

---

### 3. Verzeichnisstruktur (7 Instanzen)

```
/opt/zefusionbot/
 shared/                   SMB/NFS Mount (oder Bind-Mount vom Host)
    deps/                 PKHeX-DLLs & Ressourcen (geteilt)
    pkm/                  PKM-Dateien (geteilt, lesen)
    ports/                ZE_FusionBot_*.port Koordinations-Dateien

 plza-bot/                <- MASTER (startet auch die API)
    config.json
    logs/
    dump/
 swsh-bot/    pla-bot/    bdsp-bot/    lgpe-bot/    sv-bot/
    (jeweils config.json, logs/, dump/)
 felino-bot/
    config.json
    logs/
    dump/

 shared/bin/
     SysBot.Pokemon.ConsoleApp    <- das eigentliche Binary
```

Verzeichnisse anlegen:

```bash
mkdir -p /opt/zefusionbot/shared/{deps,pkm,ports,bin}
for bot in plza-bot swsh-bot pla-bot bdsp-bot lgpe-bot sv-bot felino-bot; do
  mkdir -p /opt/zefusionbot/$bot/{logs,dump}
done
chown -R botuser:botuser /opt/zefusionbot
```

---

### 4. SMB / NFS Share einbinden

#### Option A  Proxmox Bind-Mount (empfohlen, kein Netzwerk-Overhead)

Auf dem **Proxmox-Host** ein Verzeichnis anlegen und im LXC als Bind-Mount einbinden.

Auf dem Host:

```bash
mkdir -p /mnt/pve/sysbot-shared
# uid/gid 100000 = botuser im unprivilegierten Container
chown -R 100000:100000 /mnt/pve/sysbot-shared
```

In `/etc/pve/lxc/112.conf` hinzufügen:

```ini
mp0: /mnt/pve/sysbot-shared,mp=/opt/zefusionbot/shared
```

Danach Container neu starten:

```bash
pct restart 112
```

#### Option B  SMB / CIFS Share (z. B. Windows-NAS oder Synology)

In `/etc/fstab` im Container:

```
//nas.local/sysbot-shared  /opt/zefusionbot/shared  cifs  credentials=/etc/sysbot-smb.creds,uid=1000,gid=1000,iocharset=utf8,_netdev  0  0
```

Credentials-Datei `/etc/sysbot-smb.creds` (chmod 600):

```
username=botuser
password=GEHEIMESPASSWORT
domain=WORKGROUP
```

Mounten testen:

```bash
mount -a && df -h /opt/zefusionbot/shared
```

#### Option C  NFS Share (z. B. TrueNAS / Linux NFS-Server)

In `/etc/fstab` im Container:

```
nfs.local:/volume1/sysbot-shared  /opt/zefusionbot/shared  nfs4  defaults,_netdev,rw  0  0
```

Mounten testen:

```bash
mount -a && df -h /opt/zefusionbot/shared
```

---

### 5. Port-Planung (7 Instanzen)

**WebServer-Ports (HTTP Steuerungs-Panel)** werden vom Bot **automatisch vergeben** – je nachdem welcher Port frei ist. Diese müssen nicht manuell konfiguriert werden.

Manuell konfiguriert werden nur:
- **Switch TCP-Port** – Verbindung zur Nintendo Switch (sys-botbase / USB-Botting), in `config.json` unter `Connection.Port`
- **API-Port** – läuft im PLZA-Bot Prozess auf Port **5000**

| Instanz       | Switch TCP | Beschreibung                      |
|---------------|------------|-----------------------------------|
| `plza-bot`    | 6001       | **MASTER** – startet auch die API |
| `swsh-bot`    | 6002       |                                   |
| `pla-bot`     | 6003       |                                   |
| `bdsp-bot`    | 6004       |                                   |
| `lgpe-bot`    | 6005       |                                   |
| `sv-bot`      | 6006       |                                   |
| `felino-bot`  | 6007       |                                   |
| API           | —          | Port 5000, läuft im PLZA-Bot mit  |

> Um den **tatsächlich vergebenen WebServer-Port** einer Instanz zu sehen:
> ```bash
> journalctl -u zefusionbot@plza-bot | grep -i 'webserver\|listening\|http\|port'
> # oder:
> ss -tlnp | grep SysBot
> ```

---

### 6. `config.json` pro Instanz

Jede Instanz bekommt eine eigene `/opt/zefusionbot/<bot-name>/config.json`.
Das einzige Feld das sich pro Instanz zwingend unterscheiden muss ist die Switch-Verbindung:

```json
{
  "Hub": {
    "WebServer": {
      "Enabled": true
    }
  },
  "Bots": [
    {
      "Connection": {
        "IP": "192.168.1.10",
        "Port": 6001
      }
    }
  ]
}
```

> `WebServer.Port` muss **nicht** gesetzt werden – der Bot wählt automatisch einen freien Port.

Switch-Verbindungen pro Instanz:

| Bot           | Switch-IP      | Switch `Port` |
|---------------|----------------|---------------|
| `plza-bot`    | 192.168.1.10   | 6001          |
| `swsh-bot`    | 192.168.1.11   | 6001          |
| `pla-bot`     | 192.168.1.12   | 6001          |
| `bdsp-bot`    | 192.168.1.13   | 6001          |
| `lgpe-bot`    | 192.168.1.14   | 6001          |
| `sv-bot`      | 192.168.1.15   | 6001          |
| `felino-bot`  | 192.168.1.16   | 6001          |

> IP-Adressen und Ports an die tatsächlichen Switch-Konfigurationen anpassen.

---

### 7. systemd Template-Unit (eine Datei für alle 7 Instanzen)

Template-Datei `/etc/systemd/system/zefusionbot@.service`:

```ini
[Unit]
Description=ZE FusionBot  Instanz %i
After=network-online.target remote-fs.target
Wants=network-online.target

[Service]
Type=simple
User=botuser
WorkingDirectory=/opt/zefusionbot/%i
ExecStart=/opt/zefusionbot/shared/bin/SysBot.Pokemon.ConsoleApp
Restart=on-failure
RestartSec=15
MemoryMax=800M
StandardOutput=journal
StandardError=journal
SyslogIdentifier=zefusionbot-%i

[Install]
WantedBy=multi-user.target
```

Aktivieren und starten:

```bash
systemctl daemon-reload

# Alle 7 Bots aktivieren und starten
for bot in plza-bot swsh-bot pla-bot bdsp-bot lgpe-bot sv-bot felino-bot; do
  systemctl enable zefusionbot@$bot
  systemctl start  zefusionbot@$bot
done

# Gesamtstatus
systemctl status 'zefusionbot@*'

# Live-Log eines Bots
journalctl -u zefusionbot@plza-bot -f
journalctl -u zefusionbot@swsh-bot -f
```

> Die API läuft automatisch im PLZA-Bot Prozess mit – kein separater API-Service nötig.

---

### 8. Firewall (ufw)

```bash
apt install -y ufw
ufw default deny incoming
ufw default allow outgoing

# SSH
ufw allow 22/tcp

# Bot WebServer-Ports werden automatisch vergeben – breiten Bereich aus dem LAN erlauben
# (NUR intern / VPN, NICHT öffentlich!)
ufw allow from 192.168.0.0/16 to any port 8000:9000 proto tcp

# Homepage-API (öffentlich oder hinter Reverse Proxy)
ufw allow 5000/tcp

# Switch-Verbindungsports (nur aus dem VLAN der Switches)
ufw allow from 192.168.1.0/24 to any port 6001:6007 proto tcp

ufw enable
ufw status numbered
```

**Empfehlung**: Die WebServer-Ports nicht direkt ins Internet exponieren – stattdessen Nginx mit HTTPS davor.

Zuerst herausfinden welchen Port der Bot automatisch bekommen hat:

```bash
ss -tlnp | grep SysBot
# oder:
journalctl -u zefusionbot@plza-bot | grep -i 'port\|listen\|http'
```

Dann in Nginx eintragen (PORT durch den tatsächlich vergebenen Port ersetzen):

```nginx
# /etc/nginx/sites-available/zefusionbot-plza
server {
    listen 443 ssl;
    server_name plza.example.com;

    ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:PORT;   # <-- auto-vergebenen Port eintragen
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

### 9. Binary deployen

Build auf dem Entwicklungsrechner, dann übertragen:

```bash
# Build (Windows-Entwicklungsrechner):
dotnet publish SysBot.Pokemon.ConsoleApp/SysBot.Pokemon.ConsoleApp.csproj `
  -c Release `
  -r linux-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o ./publish/linux-x64

# Übertragen per scp:
scp ./publish/linux-x64/SysBot.Pokemon.ConsoleApp botuser@lxc-ip:/opt/zefusionbot/shared/bin/
ssh botuser@lxc-ip "chmod +x /opt/zefusionbot/shared/bin/SysBot.Pokemon.ConsoleApp"
```

Alle 7 Instanzen nutzen dann dasselbe Binary über den gemeinsamen Pfad 
kein Kopieren in jede Instanz nötig.

---

### 10. Checkliste vor dem ersten Start

- [ ] Container läuft, `botuser` existiert (`id botuser`)
- [ ] `libgdiplus` installiert (`dpkg -l libgdiplus`)
- [ ] Share gemountet und beschreibbar (`df -h /opt/zefusionbot/shared && touch /opt/zefusionbot/shared/test`)
- [ ] 7× `config.json` vorhanden (plza-bot, swsh-bot, pla-bot, bdsp-bot, lgpe-bot, sv-bot, felino-bot), jeweils mit korrekter Switch-IP/Port
- [ ] Binary vorhanden und `+x` gesetzt (`ls -la /opt/zefusionbot/shared/bin/`)
- [ ] systemd Units geladen (`systemctl daemon-reload`)
- [ ] Alle 7 Services gestartet (`systemctl status 'zefusionbot@*'`)
- [ ] API läuft im PLZA-Bot Prozess (`journalctl -u zefusionbot@plza-bot | grep -i api`)
- [ ] Firewall aktiv (`ufw status`)
- [ ] Logs ohne Fehler (`journalctl -u zefusionbot@plza-bot --since "2 min ago"`)
