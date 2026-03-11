<h1 align="center">
    <a href="https://github.com/Taku1991/ZE-FusionBot#gh-light-mode-only">
    <img src="https://i.imgur.com/0HWW0te.png">
    </a>
    <a href="https://github.com/Taku1991/ZE-FusionBot#gh-dark-mode-only">
    <img src="https://i.imgur.com/0HWW0te.png">
    </a>
</h1>

<p align="center">
  <i>Taku1991's fork of ZE-FusionBot — an independently evolved SysBot.NET project with custom enhancements, supporting LGPE, SWSH, BDSP, PLA, SV, and PLZA!</i>
</p>
<h2 align="center">
  Join our community at <a href="https://discord.gg/pokemonhideout">Pokemon Hideout</a>!
</h2>
<p align="center">
    <img src="https://visitor-badge.laobi.icu/badge?page_id=Taku1991.ZE-FusionBot" alt="Visitors">
</p>
<h4 align="center">
  <a href="https://discord.gg/pokemonhideout">
    <img src="https://i.imgur.com/wbWzI2u.png" alt="discord" style="height: 20px;">
  </a>
  <a href="https://ko-fi.com/pokemonhideout">
    <img src="https://i.imgur.com/nDO4SgL.png" alt="ko-fi" style="height: 20px;">
  </a>
</h4>

---

## 🚀 Introduction

`ZE FusionBot (Taku1991 Fork)` is an independently maintained fork of [Secludedly's ZE FusionBot](https://github.com/Secludedly/ZE-FusionBot), featuring custom enhancements, bug fixes, and optimizations developed for the [Pokemon Hideout](https://discord.gg/pokemonhideout) community. This fork shares ideas and improvements back with the original project while maintaining its own development direction.

---

## 🧬 Fork Philosophy & Origins

> **This fork focuses on stability, usability improvements, and custom features tailored for the Pokemon Hideout community.**

This project is built upon the excellent work of many developers in the SysBot.NET ecosystem. Special thanks to:

- **[Secludedly's ZE FusionBot](https://github.com/Secludedly/ZE-FusionBot)** — The original ZE FusionBot that this fork is based on
- **[kwsch's SysBot.NET](https://github.com/kwsch/SysBot.NET)** — The foundation of all SysBot projects
- **[hexbyt3's PokeBot](https://github.com/hexbyt3/PokeBot)** — Major inspiration for features and structure

<details>
<summary><strong>🧬 Click to view full credits & inspirations</strong></summary><br />

### 🧬 Foundational Projects

- **[SysBot.NET](https://github.com/kwsch/SysBot.NET)**
  Created by **[@kwsch](https://github.com/kwsch)** — also the creator of PKHeX.
  *The origin of everything.*

- **[ForkBot.NET](https://github.com/Koi-3088/ForkBot.NET)**
  Developed by **[@Koi-3088](https://github.com/Koi-3088)**.
  One of the earliest and most influential forks.

- **[SysBot.NET (berichan fork)](https://github.com/berichan/SysBot.NET)**
  An insightful fork by **[@berichan](https://github.com/berichan)**.

- **[SysBot.NET (Lusamine fork)](https://github.com/Lusamine/SysBot.NET)**
  Maintained by **[@Lusamine](https://github.com/Lusamine)**.

- **[SysBot.NET (santacrab fork)](https://github.com/santacrab2/SysBot.NET)**
  By **[@santacrab2](https://github.com/santacrab2)**.

---

### 🔧 Evolutionary & Community-Driven Bots

- **[MergeBot](https://github.com/Paschar1/MergeBot)**
  Originally by **[@bakakaito](https://github.com/bakakaito)**, preserved by **[@Paschar1](https://github.com/Paschar1)**.

- **[PokeBot](https://github.com/hexbyt3/PokeBot)**
  Created by **[@hexbyt3](https://github.com/hexbyt3)** — primary inspiration for many features.

- **[ZE-FusionBot](https://github.com/Secludedly/ZE-FusionBot)**
  Created by **[@Secludedly](https://github.com/Secludedly)** — the original FusionBot this fork is based on.

---

### 🚀 Additional Inspirations

- **[SysBot.NET (easyworld fork)](https://github.com/easyworld/SysBot.NET)** — by **[@easyworld](https://github.com/easyworld)**
- **[ManuBot.NET](https://github.com/Manu098vm/ManuBot.NET)** — by **[@Manu098vm](https://github.com/Manu098vm)**
- **[ManuBot.NET (9B1td0 fork)](https://github.com/9B1td0/ManuBot.NET)** — by **[@9B1td0](https://github.com/9B1td0)**
- **[DudeBot.NET](https://github.com/Havokx89/DudeBot.NET)** — by **[@Havokx89](https://github.com/Havokx89)**
- **[ZenBot.NET](https://github.com/Omni-KingZeno/ZenBot.NET)** — by **[@Omni-KingZeno](https://github.com/Omni-KingZeno)**
- **[TradeBot](https://github.com/jonklee99/Tradebot)** — by **[@jonklee99](https://github.com/jonklee99)** with **[@joseph11024](https://github.com/joseph11024)**

</details>

---

## ✨ Highlights

- Support for batch trades via Showdown format or .zip archives.
- Mystery Pokémon and Eggs, Battle-Ready, HOME-Ready, and Event Pokémon trading modules.
- Smart Auto-Correct and Auto-Legalization.
- DM embeds with GIFs, Channel Status notifications, Announcement System, Keyword Response.
- Built-in metrics: Queue tracking, trade counters, medal system.
- Multi-Language request support.
- Live/Real-time log searches.
- Read user DMs sent to the bot.

---

## 🐧 Linux / Headless Mode

ZE FusionBot ships a **ConsoleApp** (`SysBot.Pokemon.ConsoleApp`) for running the bot headless on Linux — no GUI required. Works on any Linux server, VPS, or container (LXC, Docker, etc.).

### ✅ Requirements

- **.NET 10 Runtime** (`linux-x64`, framework-dependent)
- No desktop environment needed — pure CLI
- All six games supported: LGPE, SWSH, BDSP, PLA, SV, PLZA

### 📦 Build & Publish

Publish the ConsoleApp for Linux from your Windows machine:

```bash
dotnet publish SysBot.Pokemon.ConsoleApp \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o "Bot Dateien/"
```

Then upload the contents of `Bot Dateien/` to `/opt/zefusionbot/shared/bin/` on your server.

### 📁 Directory Layout

Each bot gets its own directory with its own `config.json`. All instances share the same binary:

```
/opt/zefusionbot/
├── shared/
│   ├── bin/              ← Published ConsoleApp binaries (shared by all instances)
│   └── mgdb/             ← Shared Mystery Gift DB
├── lgpe/
│   ├── config.json
│   ├── distribute/
│   ├── events/
│   ├── battleready/
│   └── dump/
├── bdsp/
│   └── config.json
├── pla/
│   └── config.json
├── plza/
│   └── config.json
├── sv/
│   └── config.json
└── swsh/
    └── config.json
```

### ⚙️ Configuration

Each bot reads `config.json` from its own working directory. The easiest way to create configs is via the WinForms project, then copy them to the server.

> If no `config.json` is found on first run, a default template is created and the bot exits with instructions.

### 🔧 Systemd Template Service

Create `/etc/systemd/system/zefusionbot@.service`:

```ini
[Unit]
Description=ZE FusionBot - %i
After=network.target

[Service]
Type=simple
User=botuser
WorkingDirectory=/opt/zefusionbot/%i
ExecStart=/usr/bin/dotnet /opt/zefusionbot/shared/bin/SysBot.Pokemon.ConsoleApp.dll
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```

### 🛠️ Managing Bots

The `%i` in the service name is replaced by the instance name (= the bot's directory name):

```bash
# Enable & start a bot instance
systemctl enable zefusionbot@sv
systemctl start zefusionbot@sv

# Stop / Restart
systemctl stop zefusionbot@sv
systemctl restart zefusionbot@sv

# View logs (last 50 lines)
journalctl -u zefusionbot@sv.service -n 50 --no-pager

# Follow logs live
journalctl -u zefusionbot@sv.service -f
```

Replace `sv` with any instance name: `lgpe`, `bdsp`, `pla`, `plza`, `swsh`.

### 🖥️ Web Control Panel

When `EnableWebServer: true` in `config.json`, each bot starts a built-in HTTP panel:

```
http://<server-ip>:<WebServerPort>/
```

The panel lets you:
- View bot status and logs in real time
- Start / Stop / Idle / Resume / Restart all bots
- Toggle the Switch screen on/off remotely
- Trigger a process restart or graceful shutdown

### 🔌 REST Trade API

A JSON REST API runs alongside the web panel. Useful for external integrations:

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET`  | `/api/trade/health` | Health check + bot status |
| `POST` | `/api/trade/submit` | Submit a trade (Showdown set) |
| `GET`  | `/api/trade/status/{id}` | Poll trade status by ID |
| `POST` | `/api/trade/{id}/cancel` | Cancel a queued trade |

**Example: submit a trade**

```bash
curl -X POST http://<server-ip>:<port>/api/trade/submit \
  -H "Content-Type: application/json" \
  -d '{
    "game": "SV",
    "trainerName": "Ash",
    "userId": "123456789",
    "showdownSet": "Pikachu\nAbility: Static\nShiny: Yes\nEVs: 252 Spe\n- Thunderbolt"
  }'
```

### 🖼️ Image Rendering on Linux

All trade embeds (ball overlays, Pokémon sprites, dominant color extraction) use **SkiaSharp** instead of GDI+ — fully compatible with Linux without any additional setup.

---

## 🎥 GIF Previews

<details open>
<summary><strong>🎮 GIFS</strong></summary><br />

<div align="center">

<!-- Row 1 -->
<table>
<!-- Row 2 -->
  <tr>
    <td align="center" width="50%">
      <strong>Batch Trading Archives</strong><br />
      <img src="https://raw.githubusercontent.com/Taku1991/ZE-FusionBot/main/.readme/README_BatchArchive.gif" alt="Batch Trading Archives" width="100%" />
    </td>
    <td align="center" width="50%">
      <strong>Batch Trading Showdown</strong><br />
      <img src="https://raw.githubusercontent.com/Taku1991/ZE-FusionBot/main/.readme/README_BatchShowdown.gif" alt="Batch Trading Showdown" width="100%" />
    </td>
  </tr>

<!-- Row 3 -->
  <tr>
    <td align="center" width="50%">
      <strong>Peek & Video Feature</strong><br />
      <img src="https://raw.githubusercontent.com/Taku1991/ZE-FusionBot/main/.readme/README_Peek+Video.gif" alt="Peek & Video Feature" width="100%" />
    </td>
    <td align="center" width="50%">
      <strong>Mystery Mon</strong><br />
      <img src="https://raw.githubusercontent.com/Taku1991/ZE-FusionBot/main/.readme/README_MysteryMon.gif" alt="Mystery Mon" width="100%" />
    </td>
  </tr>
</table>

</div>

</details>

---

## 🖼️ Image Previews

<details open>
<summary>
 IMAGES
</summary> <br />

<p align="center">
    <img width="49%" src="https://i.imgur.com/hsh43rt.png" alt="img1"/>
&nbsp;
    <img width="49%" src="https://i.imgur.com/lWkBXLi.png" alt="img2"/>
</p>
<p align="center">
    <img width="49%" src="https://i.imgur.com/pMdWfcT.png" alt="img3"/>
&nbsp;
    <img width="49%" src="https://i.imgur.com/rdOq4M7.png" alt="img4"/>
</p>
<p align="center">
    <img width="49%" src="https://i.imgur.com/eWmTGCI.png" alt="img5"/>
&nbsp;
    <img width="49%" src="https://i.imgur.com/SPe1iOa.png" alt="img6"/>
</p>
<p align="center">
    <img width="49%" src="https://i.imgur.com/Xn1IMJ6.png" alt="img7"/>
&nbsp;
	<img width="49%" src="https://i.imgur.com/N9n5jva.jpeg" alt="img8"/>
</p>
</details>

---

# 📖 Command Reference

## ⚡ Basic Commands

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `trade` | t | Trade a Pokémon from Showdown Set or PKM file. | `trade <Showdown Format>` or `<upload pkm>` | Everyone |
| `trade true` | t true | Trade a Pokémon from a PKM file, without AutoOT | `trade true <upload pkm>` | Everyone |
| `tradeUser` | tu, tradeOther | Trade the mentioned user the attached file. | `tradeuser @user` | Everyone |
| `hidetrade` | ht | Same as trade, but hides the embed. | `hidetrade <Showdown Format>` | Everyone |
| `clone` | c | Clone the Pokémon you show via Link Trade. | `clone` | **Premium** 🌟 |
| `dump` | d | Dump the Pokémon you show via Link Trade. | `dump` | Everyone |
| `egg` | Egg | Trade an egg via provided Pokémon set. | `egg <Showdown Format>` | **Premium** 🌟 |
| `seed` | checkMySeed, checkSeed, seedCheck, s, sc | Check a Pokémon seed. | `seedCheck` | Everyone |
| `itemTrade` | it, item | Trade a Pokémon holding a requested item. | `it <Leftovers>` | Everyone |
| `fixOT` | fix, f | Fix OT and Nickname of a Pokémon if an advert is detected. | `fixOT` | **Premium** 🌟 |
| `convert` | showdown | Convert a Showdown Set to RegenTemplate. | `convert <set>` | Everyone |
| `legalize` | alm | Attempt to legalize PKM data. | `legalize <pkm>` | Everyone |
| `validate` | lc, check, verify | Verify PKM legality. | `validate <pkm>` | Everyone |
| `verbose` | lcv | Verify PKM legality with verbose output. | `verbose <pkm>` | Everyone |
| `findFrame` | ff, GetFrameData | Prints next shiny frame from seed. | `findFrame <seed>` | Everyone |
| `deleteTradeCode` | dtc | Deletes the stored Link Trade Code for the user. | `dtc` | Everyone |
| `changeTradeCode` | ctc | Change your stored Link Trade Code. | `ctc 12345678` | Everyone |

## 🎯 Advanced Trade Features

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `textTrade` | tt, text | Upload a .txt/.csv of Showdown sets for batch trading. | `tt <upload .txt/.csv file>` | Everyone |
| `textView` | tv | View a specific Pokémon from your pending TextTrade file. | `tv 2` | **Premium** 🌟 |
| `listEvents` | le | Lists available event files via DM. | `le <species> <page2>` | Everyone |
| `eventRequest` | er | Downloads event attachments and adds to trade queue. | `eventRequest <file>` | **Premium** 🌟 |
| `battleReadyList` | brl | Lists available battle-ready files via DM. | `brl <species> <page2>` | **Premium** 🌟 |
| `battleReadyRequest` | br, brr | Downloads battle-ready attachments and adds to trade queue. | `battleReadyRequest <file>` | **Premium** 🌟 |
| `pokepaste` | pp, Pokepaste, PP | Generates a team from a PokePaste URL. | `pp <URL>` | Everyone |
| `dittoTrade` | dt, ditto | Trade a Ditto with requested stats, language, and nature. | `dt <LinkCode> <IVToBe0> <Lang> <Nature>` | Everyone |
| `mysteryegg` | me | Get a random shiny 6IV egg. | `mysteryegg` | **Premium** 🌟 |
| `mysterymon` | mm, mystery, surprise | Get a fully random Pokémon. | `mysterymon` | **Premium** 🌟 |
| `randomTeam` | rt, RandomTeam, Rt | Generates a random team. | `randomTeam` | Everyone |
| `homeReady` | hr | Displays instructions for HOME-ready trading. | `homeReady` | Everyone |
| `homeReadyRequest` | hrr | Downloads HOME-ready files and adds to trade queue. | `homeReadyRequest <number>` | **Premium** 🌟 |
| `homeReadylist` | hrl | Lists available HOME-ready files. | `homeReadylist` | Everyone |
| `specialRequest` | sr, srp | Lists Wondercard events or requests specific ones. | `srp <game> <page2>` | Everyone |
| `getEvent` | ge, gep | Downloads the requested event as a PKM file. | `getEvent <eventID>` | Everyone |

## 📦 Batch Trading

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `batchTrade` | bt | Trade multiple Pokémon (max 6) from a list. | `bt <Set1> --- <Set2>` | **Premium** 🌟 |
| `batchTradeZip` | btz | Trade multiple Pokémon from a ZIP file. | `btz <file.zip>` | **Premium** 🌟 |
| `batchInfo` | bei | Get info about a batch property. | `batchInfo <prop>` | **Premium** 🌟 |
| `batchValidate` | bev | Validate a batch property. | `batchValidate <prop>` | **Premium** 🌟 |

## 📊 Queue Management

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `queueMode` | qm | Change queue control (manual/threshold/interval). | `qm manual` | Everyone |
| `queueClearAll` | qca, tca | Clear all users from all queues. | `qca` | Sudo, Owner |
| `queueClear` | qc, tc | Remove yourself from the queue. | `qc` | Everyone |
| `queueClearUser` | qcu, tcu | Clear a specified user (sudo required). | `qcu @user` | Sudo, Owner |
| `queueStatus` | qs, ts | Check your position in the queue. | `qs` | Everyone |
| `queueToggle` | qt | Enable/disable queue joining. | `qt` | Sudo, Owner |
| `queueList` | ql | DM the full queue list. | `ql` | Sudo, Owner |
| `tradeList` | tl | Show users currently in trade queue. | `tl` | Sudo, Owner |
| `fixOTList` | fl, fq | Prints the users in the FixOT queue. | `fixOTList` | Sudo, Owner |
| `cloneList` | cl, cq | Prints the users in the Clone queue. | `cloneList` | Sudo, Owner |
| `dumplist` | dl, dq | Prints the users in the Dump queue. | `dumplist` | Sudo, Owner |
| `seedList` | sl, scq, seedCheckQueue, seedQueue, seedList | Show seed check queue users. | `seedList` | Sudo, Owner |

## 🛠 Admin Tools

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `addSudo` | — | Add a user to global sudo. | `addSudo <ID>` | Owner |
| `removeSudo` | — | Remove a user from global sudo. | `removeSudo <ID>` | Owner |
| `blacklistServer` | bls | Adds a server ID to the server blacklist. | `blacklistServer <ID>` | Sudo, Owner |
| `unblacklistServer` | ubls | Removes a server ID from the server blacklist. | `unblacklistServer <ID>` | Sudo, Owner |
| `blacklist` | — | Blacklist a Discord user. | `blacklist @user` | Sudo, Owner |
| `unblacklist` | — | Remove a user from blacklist. | `unblacklist @user` | Sudo, Owner |
| `blacklistId` | — | Blacklist Discord user IDs. | `blacklistId <ID>` | Sudo, Owner |
| `unBlacklistId` | — | Unblacklist Discord user IDs. | `unBlacklistId <ID>` | Sudo, Owner |
| `blacklistComment` | — | Adds comment for blacklisted user. | `blacklistcomment <ID> <msg>` | Sudo, Owner |
| `banTrade` | bant | Ban a user from trading with reason. | `bant @user <reason>` | Sudo, Owner |
| `banID` | — | Ban an online user ID. | `banID <ID>` | Sudo, Owner |
| `unbanID` | — | Unban an online user ID. | `unbanID <ID>` | Sudo, Owner |
| `bannedIDComment` | — | Adds a comment for banned ID. | `bannedIDcomment <ID> <msg>` | Sudo, Owner |
| `bannedIDSummary` | printBannedID, bannedIDPrint | Show list of banned IDs. | `bannedIDSummary` | Sudo, Owner |
| `blacklistSummary` | printBlacklist, blacklistPrint | Show list of blacklisted users. | `blacklistSummary` | Sudo, Owner |

## 🎮 Switch Control

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `click` | — | Click a button on the Switch. | `click <IP> <Button>` | Sudo, Owner |
| `setStick` | — | Move stick to coordinates. | `setStick <IP> <Coords>` | Sudo, Owner |
| `setScreenOn` | screenOn, scrOn | Turn on screen. | `setScreenOn` | Sudo, Owner |
| `setScreenOff` | screenOff, scrOff | Turn off screen. | `setScreenOff` | Sudo, Owner |
| `setScreenOnAll` | screenOnAll, scrOnAll | Turn on screen for all bots. | `setScreenOnAll` | Sudo, Owner |
| `setScreenOffAll` | screenOffAll, scrOffAll | Turn off screen for all bots. | `setScreenOffAll` | Sudo, Owner |
| `peek` | repeek | Take and send a screenshot. | `peek` | Sudo, Owner |
| `video` | Video | Record a GIF from the Switch. | `video` | Sudo, Owner |

## 📡 Bot Management

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `ping` | — | Ping the bot to check if it's running. | `ping` | Sudo, Owner |
| `help` | — | Show all commands. | `help` | Everyone |
| `info` | about, whoami, owner, bot | Show bot information. | `info` | Everyone |
| `botStatus` | — | Get bot status via logs. | `botStatus` | Sudo, Owner |
| `botStart` | — | Start the bot. | `botStart` | Sudo, Owner |
| `botStop` | — | Stop the bot. | `botStop` | Sudo, Owner |
| `botIdle` | botPause, idle | Pause the bot. | `botIdle` | Sudo, Owner |
| `botChange` | — | Change the bot routine. | `botChange <FlexTrade>` | Sudo, Owner |
| `botRestart` | — | Restart the bot(s). | `botRestart` | Sudo, Owner |
| `status` | stats | Get the bot environment status. | `status` | Sudo, Owner |
| `kill` | shutdown | Shutdown the bot. | `kill` | Owner |

## 📢 Echo & Logging

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `announce` | Announce | Send an announcement to Echo channels. | `announce <msg>` | Owner |
| `dm` | - | Send a DM to a user as the bot. | `dm @user <msg>` | Owner |
| `addEmbedChannel` | aec | Assign a channel for bot embeds. | `addEmbedChannel #channel` | Sudo, Owner |
| `echoInfo` | — | Dump echo message settings. | `echoInfo` | Sudo, Owner |
| `echoClear` | rec | Clear echo settings for current channel. | `echoClear` | Sudo, Owner |
| `echoClearAll` | raec | Clear echo settings from all channels. | `echoClearAll` | Sudo, Owner |
| `logHere` | — | Log to current channel. | `logHere` | Sudo, Owner |
| `logClearAll` | — | Clear all log settings. | `logClearAll` | Sudo, Owner |
| `logClear` | — | Clear log settings for current channel. | `logClear` | Sudo, Owner |
| `logInfo` | — | Dump logging settings. | `logInfo` | Sudo, Owner |

## 🔐 Permissions & Guild

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `listGuilds` | lg, servers, listservers | List all guilds the bot is in. | `listGuilds` | Sudo, Owner |
| `leave` | bye | Leave current server. | `leave` | Sudo, Owner |
| `leaveGuild` | lg | Leave a guild by ID. | `leaveGuild <ID>` | Sudo, Owner |
| `leaveAll` | — | Leave all servers. | `leaveAll` | Sudo, Owner |

## 🎲 Misc & Fun

| Command | Aliases | Summary | Example | Permission |
|---------|---------|---------|---------|------------|
| `joke` | lol, insult | Tell a random joke. | `joke` | Everyone |
| `hello` | hi, hey, yo | Say hello to the bot. | `hello` | Everyone |
| `mi` | ml | View personal profile card w/ trainer info. | `myinfo` | Everyone |

## 🧠 Passive Features

- Use filename code like `Great Tusk-Tera(Steel)-03760382.pk9` to auto-set trade code.
- Paste a PKM in chat to receive info + legal formats.
- Thank the bot — it might reply!



## 📝 Batch Commands to Showdown Format

`ZE FusionBot` supports converting batch commands from Discord messages into official Showdown Set formats. This allows easy bulk Pokémon trading with full customization of stats, moves, ribbons, and other metadata.

### Supported Batch Command Mappings

| Batch Command | Showdown Format Equivalent | Notes |
|---------------|---------------------------|-------|
| `.Scale=` | `Scale:` or `Size:` | Accepts keywords (XXXS, XXS, XS, S, AV, L, XL, XXL, XXXL) or numeric values 1-255. |
| `.WeightScalar=` | `Weight:` | Accepts keywords (XS, S, AV, L, XL) or numeric values 1-255. |
| `.HeightScalar=` | `Height:` | Accepts keywords (XS, S, AV, L, XL) or numeric values 1-255. |
| `.OriginalTrainerFriendship=` | `OT Friendship:` | Value 1–255. |
| `.HandlingTrainerFriendship=` | `HT Friendship:` | Value 1–255. |
| `.MetDate=` | `Met Date:` | Supports flexible date formats. |
| `.StatNature=` | `Stat Nature:` | Accepts a Nature keyword. |
| `~=Version=` | `Game:` or `Version:` | Supports full game names or abbreviations. |
| `.MetLocation=` | `Met Location:` | Numeric IDs & Location names supported. |
| `.HyperTrainFlags=` | `HyperTrain:` | True / False. |
| `.HT_[STAT]=` | `HT:` | Supports HP, Atk, Def, SpA, SpD, Spe. |
| `.Moves=` | `Moves:` | "Random" generates random moves. |
| `.RelearnMoves=` | `Relearn Moves:` | "All" or "None" accepted. |
| `.Ribbons=` | `Ribbons:` | "All" or "None" supported. |
| `.RibbonMark[mark]=True` | `Mark:` | Mark names without spaces (e.g., BestFriends). |
| `.Ribbon[name]=True` | `Ribbon:` | Ribbon names without spaces (e.g., BattleChampion). |
| `.SetEVs=` | `Set EVs:` | Accepts `Random`, or `Suggest`. |
| `.SetIVs=` | `Set IVs:` | Accepts `Random`, or presets like `1IV`–`6IV`. |
| `.GV_[STAT]=` | `GVs:` | Supports HP, Atk, Def, SpA, SpD, Spe. |
| `.Marking[type]=` | `Markings:` | Diamond, Heart, Square, Star, Triangle, Circle in Red or Blue `Markings: Diamond=Red / Circle=Blue` etc. |
| `.Characteristic=` | `Characteristic:` | Type out a characteristic. |
| `.Nickname=` | `Nickname:` | Write "Suggest" for a random nickname pulled from code dictionary. |

---

## 🧭 Slash Command Support

ZE FusionBot supports **modern Discord Slash Commands** for fast, clean Pokémon creation across all supported games.
These commands integrate directly with the bot's legality pipeline and AutoOT logic.

### 🎮 Available Slash Commands

| Slash Command | Game |
|--------------|------|
| `/create-sv` | Scarlet / Violet |
| `/create-swsh` | Sword / Shield |
| `/create-bdsp` | Brilliant Diamond / Shining Pearl |
| `/create-pla` | Legends: Arceus |
| `/create-plza` | Legends: Z-A |
| `/create-lgpe` | Let's Go Pikachu / Eevee |

### 🔹 Notes
- Slash commands provide **guided Pokémon creation** without needing manual Showdown formatting.
- Fully compatible with **Auto-Legalization**, **AutoOT**, and **language handling**.
- Ideal for newer users or servers that want a **clean, modern interaction flow**.

> Text commands and batch systems remain fully supported — slash commands simply add another powerful option.

---

### Example Usage

```markdown
Set EVs: Suggest
Set IVs: 5IV
Scale: XL
Weight: 45
Height: AV
OT Friendship: 128
HT Friendship: 128
Met Location: 30024
Game: PLA
Moves: Random
Relearn Moves: All
Mark: BestFriends
Ribbons: All
GVs: 7 HP / 7 Atk / 7 Def / 7 SpA / 7 SpD / 7 Spe
HT: HP / Atk / Def / SpA / SpD / Spe
Characteristic: Quick to flee
Markings: Diamond=Red / Heart=Red / Square=Blue / Star=Blue / Triangle=Red / Circle=Blue
```

## ⚙️ Bot Functions

### 🧑‍🎓 AutoOT
FusionBot automatically applies your **trainer information** based on the save file you're currently using.
- Your **OT / TID / SID / OTGender** are applied automatically.
- To keep the trainer info in your own files, attach them with `t true`.
- For Showdown Sets, simply include the OT/TID/SID you want — AutoOT will then be disabled.

This ensures all trades feel natural and consistent with your game, while still letting you override it if you want custom trainer data.

---

### 🔗 Link Trade Codes
FusionBot assigns you a **personal static Link Trade Code** on your first trade.
- This code is **unique to you** and stays the same for all future trades.
- To reset it: use `dtc` (your next trade gives you a new random code).
- To customize it: use `ctc 12345678` (sets your permanent code to whatever you choose).

This makes trading smoother by removing guesswork — your link code is always ready.

---

### 🏅 Medals & Milestones
Every trade you complete is tracked by FusionBot, and your **trade count** shows up in the footer of the trade embed.
- For every **50 trades**, you earn a new medal 🥇.
- You can check your medals anytime in your profile card with the `mi` command.
- It's just for fun — a little **progression system** to show off your trading dedication.

Think of it like leveling up — the more you trade, the more medals you rack up, proving you're a true master trader.

---

### 🤖 Reading DMs Sent to the Bot
You can now read the DMs a user sends to the bot. This is useful for moderation purposes.
- Visit the **UserDMsToBotForwarder** option in `Hub -> Discord` and insert a Channel ID, then restart the bot.
- The DMs that get logged are only those without a command, so you will not get flooded with user command input.
- You'll also be able to see attachments users send to the bot.

---

## 🔗 Related Projects

- [**ZE-FusionBot (Original)**](https://github.com/Secludedly/ZE-FusionBot) — The original ZE FusionBot by Secludedly
- [**SysBot.NET**](https://github.com/kwsch/SysBot.NET) — The foundation project by kwsch
- [**PKHeX**](https://github.com/kwsch/PKHeX) — Pokémon save file editor

## 🛠️ Tools by Taku1991

- [**Hideout-PK**](https://www.hideout-pk.de/) — Pokemon Hideout Homepage
- [**Showdown Set Builder (DE)**](https://www.hideout-pk.de/setbuilder) — German Showdown Set Builder for easy Pokémon creation
- [**SV Raid Finder (DE)**](https://www.hideout-pk.de/raid-finder) — German Scarlet/Violet Raid Finder

## 🤝 Community

- [**Pokemon Hideout Discord**](https://discord.gg/pokemonhideout) — Join our community for support and trading
