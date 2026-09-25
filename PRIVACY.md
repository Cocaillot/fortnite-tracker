# Privacy policy

Fortnite Tracker has no server and no analytics. The developers never receive any information
about you, your PC or how you use the app.

## What stays on your PC

Your settings, match history, ranks, notes, goals and theme are saved in
`%APPDATA%\FortniteTracker` and never leave your PC, except in backups that you create yourself.

The app reads Fortnite's log file (`%LOCALAPPDATA%\FortniteGame\Saved\Logs`). It never reads the
game's memory or network traffic.

## What the app sends, and to whom

The app connects to other services only for the features below:

| Service | What is sent | When |
|---|---|---|
| [fortnite-api.com](https://fortnite-api.com) | Epic account IDs or display names of players, with your own API key | To show stats for you, your party, your eliminator and players you look up |
| GitHub (github.com) | A request for the latest version | To check for updates |
| Discord, on your PC | Your mode, party size and K/D, to the Discord app running on your PC | Only while "Show my stats on Discord" is on (Settings) |
| Discord, via your webhook | A summary of your session (matches, wins, kills, K/D, ranks, eliminator names) | Only if you add a webhook link in Settings, and only to that channel |

These services have their own privacy policies. Links you click in the app open in your browser.

The window is drawn by Microsoft Edge WebView2, a Windows component; it follows Windows' own
diagnostic data settings. The app sends it nothing beyond showing its own pages, which are
stored inside the app.

## Turning features off

- Discord status: Settings → "Show my stats on Discord".
- Session recaps: Settings → "Remove webhook".
- Stats lookups stop if you don't set an API key; the app then only shows what's in Fortnite's log.
- Updates: the update check runs at start; you can block it with your firewall without breaking the app.

## Removing your data

Uninstall the app from Windows Settings → Apps, then delete `%APPDATA%\FortniteTracker` and
`%LOCALAPPDATA%\FortniteTracker.WebView2`.
