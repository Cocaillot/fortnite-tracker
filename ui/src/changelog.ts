// "What's new" for each version, newest first. Titles and texts are English keys (see i18n-fr.ts).
export interface Release {
  version: string
  items: { title: string; text: string }[]
}

export const changelog: Release[] = [
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
