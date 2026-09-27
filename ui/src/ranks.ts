// Fortnite's ranks by index, as in RankNames.cs. Older seasons have 18; update v40.20 (2026)
// split Elite and Champion into three divisions, so the new "combined" tracks have 22.
const CLASSIC = [
  'Bronze I', 'Bronze II', 'Bronze III', 'Silver I', 'Silver II', 'Silver III',
  'Gold I', 'Gold II', 'Gold III', 'Platinum I', 'Platinum II', 'Platinum III',
  'Diamond I', 'Diamond II', 'Diamond III', 'Elite', 'Champion', 'Unreal',
]
const EXPANDED = [
  'Bronze I', 'Bronze II', 'Bronze III', 'Silver I', 'Silver II', 'Silver III',
  'Gold I', 'Gold II', 'Gold III', 'Platinum I', 'Platinum II', 'Platinum III',
  'Diamond I', 'Diamond II', 'Diamond III', 'Elite I', 'Elite II', 'Elite III',
  'Champion I', 'Champion II', 'Champion III', 'Unreal',
]

/** The rank names a track uses (same rule as RankNames.Ladder in C#). */
export const ladderFor = (track: string, value = 0): string[] =>
  track.toLowerCase().includes('combined') || value >= CLASSIC.length ? EXPANDED : CLASSIC

export const rankName = (index: number, track: string) => ladderFor(track, index)[index] ?? `Rank ${index}`

/** Where each tier starts and ends on a ladder, for charts. */
export function tierBands(ladder: string[]) {
  const bands: { name: string; from: number; to: number }[] = []
  ladder.forEach((n, i) => {
    const tier = n.split(' ')[0]!
    const last = bands[bands.length - 1]
    if (last?.name === tier) last.to = i + 1
    else bands.push({ name: tier, from: i, to: i + 1 })
  })
  return bands
}
