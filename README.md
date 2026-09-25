# Fortnite Tracker

A Windows companion app that runs next to Fortnite. It detects you and your party from Fortnite's
own log file and shows each squad member's season stats (K/D, win rate, wins, matches). You can
also look up any player by name.

## How it works (and what it can't do)

The app reads `%LOCALAPPDATA%\FortniteGame\Saved\Logs\FortniteGame.log` in shared read-only mode.
It never touches the game's memory or network traffic and doesn't inject anything.

From that log it gets:

- your Epic account ID and display name
- your party members' account IDs as they join and leave
- when a match starts (`Welcomed by server`) and when it ends (placement)

**Stats for other players in your match aren't available.** Epic redacts their IDs in the log
(`MCP:9f8e7...6d5c4`) and never logs their names. Use the manual lookup for them instead.

The log format is undocumented and can change with any Fortnite update. All patterns are in
`FortniteLogParser.cs`, and each one has a test with a real log line.

## Layout

```
src/FortniteTracker.Core/      log tailer, parser, lobby state, stats client (no UI)
src/FortniteTracker.Desktop/   WPF window hosting the UI in WebView2, global hotkey
ui/                            Vue 3 + Vite + TypeScript, builds into Desktop/wwwroot
tests/FortniteTracker.Core.Tests/
```

The Vue UI and the .NET host talk through WebView2 web messages (see `ui/src/bridge.ts` and
`MainWindow.xaml.cs`).

## Requirements

- .NET 8 SDK
- Node 20 or later
- WebView2 runtime (preinstalled on Windows 11)
- A free API key from https://fortnite-api.com/dashboard. The app asks for it on first run and
  stores it in `%APPDATA%\FortniteTracker\settings.json`.

## Development

```powershell
cd ui; npm install; npm run dev        # Vite on :5173 with hot reload
dotnet run --project src/FortniteTracker.Desktop   # Debug builds load the dev server when it's running
dotnet test
```

With no dev server running, the Debug build loads the last `npm run build` output from `wwwroot`.

## Release build

```powershell
dotnet publish src/FortniteTracker.Desktop -c Release -r win-x64 --self-contained false
```

Release builds run `npm run build` automatically. Pass `-p:SkipUiBuild=true` to skip that step.

## Usage

- Keep Fortnite in **Windowed Fullscreen** so the always-on-top window can sit over it.
- **Ctrl+Shift+F** shows or hides the window.
- Private profiles show "Stats private". The player has to turn on public stats in Fortnite's
  settings (Account and Privacy).
