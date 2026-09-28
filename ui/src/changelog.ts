// "What's new" for each version, newest first. Titles and texts are English keys (see i18n-fr.ts).
export interface Release {
  version: string
  items: { title: string; text: string }[]
}

export const changelog: Release[] = [
  {
    version: '0.9.11',
    items: [
      {
        title: 'No more in-game overlay',
        text: 'The bar over the game is gone: everything is in the app window, which you can still bring up in game with your shortcut.',
      },
    ],
  },
  {
    version: '0.9.7',
    items: [
      {
        title: 'Your real eliminations, from replays',
        text: 'With Record Replays on in Fortnite, each match gets your real eliminations, your placement, your true eliminator and the players your team eliminated (click a match). Replays are deleted once read.',
      },
    ],
  },
  {
    version: '0.9.6',
    items: [
      {
        title: 'The new ranks',
        text: 'Elite and Champion now have three divisions each, as in Fortnite since this season, and Unreal is shown correctly again.',
      },
      {
        title: 'Honest kill counts in ranked',
        text: "Fortnite's public stats leave out many ranked eliminations, so ranked matches show no kill count rather than a wrong one.",
      },
    ],
  },
  {
    version: '0.9.5',
    items: [
      {
        title: 'Update in one click',
        text: 'A banner appears as soon as a new version is out. "Update now" installs it and restarts the app. You can also check yourself in Settings → Updates.',
      },
      {
        title: 'Filters and rank points in History',
        text: 'Filter matches by type, game and building, and see how many rank points each ranked match earned or cost.',
      },
      {
        title: 'Clearer match names',
        text: 'Each match says which game it was (Battle Royale, Reload, OG) and whether building was on.',
      },
      {
        title: 'Reliable kill counts',
        text: 'Kills per match are no longer counted twice when you play quick matches back to back.',
      },
    ],
  },
  {
    version: '0.9.0',
    items: [
      {
        title: 'Opens with Fortnite',
        text: 'Turn on "Open with Fortnite" in Settings: the app waits quietly in the tray and opens by itself when the game starts.',
      },
      {
        title: 'Your own shortcuts',
        text: 'Pick the keys that show the app and toggle the overlay, in Settings → Shortcuts.',
      },
      {
        title: 'Export and backup',
        text: 'Export your matches to Excel, back up all your data, and restore it on another PC.',
      },
      {
        title: 'A guide for new players',
        text: 'Friends installing the app are now walked through the setup, including a check of their stats key.',
      },
    ],
  },
]

/** Releases newer than the last one seen, up to the running version. */
export function unseen(current: string, lastSeen: string | null): Release[] {
  const newer = (a: string, b: string) => {
    const pa = a.split('.').map(Number)
    const pb = b.split('.').map(Number)
    for (let i = 0; i < 3; i++) if ((pa[i] ?? 0) !== (pb[i] ?? 0)) return (pa[i] ?? 0) > (pb[i] ?? 0)
    return false
  }
  return changelog.filter((r) => !newer(r.version, current) && (!lastSeen || newer(r.version, lastSeen)))
}
