# Fortnite Tracker

## ⬇️ [Download for Windows (FortniteTracker-win-Setup.exe)](https://github.com/Cocaillot/fortnite-tracker/releases/latest/download/FortniteTracker-win-Setup.exe)

Run the downloaded file: it installs, adds a desktop shortcut and opens the app. Windows may say
"Windows protected your PC" because the app isn't code-signed: click **More info → Run anyway**.
On first launch, paste a free API key from [fortnite-api.com/dashboard](https://fortnite-api.com/dashboard).
The app updates itself after that.

A Windows companion app that runs next to Fortnite. It detects you and your party from Fortnite's
own log file and shows:

- each party member's season stats (K/D, win rate, wins, matches) for the mode you're playing,
  coloured by Fortnite item rarity, plus lookup of any player by name
- the player who eliminated your team, with a threat level (Casual → Sweat) and how their K/D
  compares to yours, as a card, a Windows notification and in the in-game overlay
- a small click-through in-game overlay with your squad's K/D (Ctrl+Shift+O)
- match history with a session dashboard: time played, most-played mode, your nemesis, and
  (experimental) kills and wins per match
- ranks for you, your party and your friends (current season, best rank, past seasons)
- player profiles: season and lifetime stats per mode, ranks, and your history with them
- a leaderboard of you, your party, friends with a rank this season and players you follow
- sessions: what you did in each play session, from Epic's own before/after numbers
- your mode, party and K/D on your Discord profile (Rich Presence)

It lives in the notification area, has a Ctrl+Shift+F show/hide hotkey, and updates itself.

**Per-match kills and wins** aren't in the log. The app reads your season stats when a match
starts and again after it ends; the difference is that match. If two matches land in your stats
at once, the result is left blank rather than guessed.

**Stats for the current mode:** fortnite-api has solo, duo, squad and `ltm` buckets. Ranked
counts as `ltm`, and there is no trio bucket, so Ranked shows "Ranked & LTMs" stats and trios
show all modes.

## How it works (and what it can't do)

The app reads `%LOCALAPPDATA%\FortniteGame\Saved\Logs\FortniteGame.log` in shared read-only mode.
It never touches the game's memory or network traffic and doesn't inject anything.

From that log it gets:

- your Epic account ID and display name
- your party members' account IDs as they join and leave
- when a match starts (`Welcomed by server`) and ends (placement), and the playlist from your own
  presence (`Habanero` = Ranked)
- the display names of players the camera follows (`LogFortViewTarget`). Once your team is
  eliminated, the first of them is the player who eliminated it; earlier targets are your own
  teammates and are ignored. Those names are looked up as Epic, then PSN, then Xbox accounts.
  Streamer Mode players appear as `Anonyme[272]` and are shown as hidden.

**Ranks** come from the log too: Fortnite fetches ranked progress (`HabaneroProgress`) for you,
your party and friends shown in its social panel, never for opponents. A rank updated in the last
120 days counts as this season. Ranks run from 0 (Bronze I) to 17 (Unreal); 2026 "combined"
tracks also show values above 17, which are displayed as "Rank N" because their names aren't known.
Ranked modes are named from Epic's codenames (BlastBerry = Reload, FeralCorgi = Ballistic,
SquareClub = Arenas Boxfights, Pimlico = Crown Jam…); unconfirmed ones (e.g. `bling`,
`RadiantToothpick`) get a readable name derived from the codename and are marked as such.
Friends whose stats are private show as "Private profile": the log has their rank but not their name.

**Stats for the rest of the lobby, and opponents' ranks, aren't available.** Epic redacts other players' IDs
in the log (`MCP:9f8e7...6d5c4`) and doesn't log their names unless you spectate them. Getting the
full lobby would need memory reading or packet capture, which anti-cheat bans. No public service
provides competitive ranks. Placement and elimination counts aren't logged either.

The log format is undocumented and can change with any Fortnite update. All patterns are in
`FortniteLogParser.cs`, and each one has a test with a real (anonymized) log line.

## Layout

```
src/FortniteTracker.Core/      log tailer, parser, session state, history, stats client (no UI)
src/FortniteTracker.Desktop/   WPF + WebView2 window, tray icon, hotkey, updates, Discord presence
ui/                            Vue 3 + Vite + TypeScript, builds into Desktop/wwwroot
tests/FortniteTracker.Core.Tests/
scripts/release.ps1            builds the installer and publishes GitHub releases
```

The Vue UI and the .NET host talk through WebView2 web messages (see `ui/src/bridge.ts` and
`UiBridge.cs`).

## Data on the user's PC

| Path | Contents |
|---|---|
| `%APPDATA%\FortniteTracker\settings.json` | fortnite-api.com key, Rich Presence on/off |
| `%APPDATA%\FortniteTracker\history.json` | match history |
| `%LOCALAPPDATA%\FortniteTracker\` | installed app (managed by Velopack) |
| `%LOCALAPPDATA%\FortniteTracker.WebView2\` | UI browser profile |

## Development

For testing without touching your real data, the app accepts overrides:
`--Fortnite:LogPath=<log>` (read another log), `--Fortnite:AssumeRunning=true` (don't check
for the game process) and `--Storage:Directory=<folder>` (settings and history location).


Requirements: .NET 8 SDK, Node 20+, WebView2 runtime (preinstalled on Windows 11).

```powershell
cd ui; npm install; npm run dev                    # Vite on :5173 with hot reload
dotnet run --project src/FortniteTracker.Desktop   # Debug builds use the dev server when it's running
dotnet test
```

Each user needs a free key from https://fortnite-api.com/dashboard. The app asks for it on first run.

## One-time setup

### Discord Rich Presence

1. In the [Discord Developer Portal](https://discord.com/developers/applications), create an
   application named **Fortnite Tracker**. Discord shows this name as "Playing Fortnite Tracker".
2. Copy its **Application ID**.
3. Under **Rich Presence → Art Assets**, upload `src/FortniteTracker.Desktop/Assets/app-256.png`
   with the key `logo`.
4. Pass the ID to the release script as `-DiscordClientId`. For local testing, put it in
   `Discord:ClientId` in `appsettings.json`.

Users can turn Rich Presence off in the app's Settings tab.

### Installer and auto-updates (Velopack + GitHub Releases)

1. Push this repo to GitHub. A public repo lets installed copies check for updates without a token.
2. Create a fine-grained token with **Contents: read and write** on the repo.
3. Release:

   ```powershell
   $env:GITHUB_TOKEN = "<token>"
   .\scripts\release.ps1 -Version 0.1.0 -RepoUrl https://github.com/<you>/<repo> -DiscordClientId <id> -Upload
   ```

4. Share this link in your Discord server. It always points at the newest installer:

   `https://github.com/<you>/<repo>/releases/latest/download/FortniteTracker-win-Setup.exe`

For each new version, run the script again with a higher `-Version`. Installed copies check every
4 hours, download the update in the background, and install it on the next start. Users can also
pick "Restart to update" from the tray or the in-app banner.

Without `-Upload`, the script only builds `releases\FortniteTracker-win-Setup.exe` locally.
Velopack refuses to package a version that isn't higher than one already in `releases\`. To
rebuild the same version locally, delete that folder first.

The installer isn't code-signed, so Windows SmartScreen shows "Windows protected your PC" the
first time. Users click **More info → Run anyway**. Signing needs a code-signing certificate
(`vpk pack --signParams`).

## Usage

- Keep Fortnite in **Windowed Fullscreen** so the always-on-top window can sit over it.
- **Ctrl+Shift+F** shows or hides the window. Closing it keeps the app running in the tray;
  right-click the tray icon to exit.
- Private profiles show "Stats private". The player has to turn on public stats in Fortnite's
  settings (Account and Privacy).
