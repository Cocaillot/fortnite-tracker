# Fortnite Tracker

A Windows companion app that runs next to Fortnite. It detects you and your party from Fortnite's
own log file and shows:

- each squad member's season stats (K/D, win rate, wins, matches), plus lookup of any player by name
- your match history (mode, squad size, duration), including sessions from Fortnite's recent logs
- your mode, squad and K/D on your Discord profile (Rich Presence)

It lives in the notification area, has a Ctrl+Shift+F show/hide hotkey, and updates itself.

## How it works (and what it can't do)

The app reads `%LOCALAPPDATA%\FortniteGame\Saved\Logs\FortniteGame.log` in shared read-only mode.
It never touches the game's memory or network traffic and doesn't inject anything.

From that log it gets:

- your Epic account ID and display name
- your party members' account IDs as they join and leave
- when a match starts (`Welcomed by server`) and ends (placement), and the playlist from your own
  presence (`Habanero` = Ranked)

**Stats for other players in your match aren't available.** Epic redacts their IDs in the log
(`MCP:9f8e7...6d5c4`) and never logs their names. Placement and eliminations aren't logged
either, so match history shows mode, squad and duration only.

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
