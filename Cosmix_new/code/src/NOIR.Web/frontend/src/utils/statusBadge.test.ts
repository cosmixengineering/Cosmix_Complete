import { getStatusBadgeClasses } from './statusBadge'

describe('getStatusBadgeClasses', () => {
  const allColors = [
    'green', 'red', 'yellow', 'blue', 'gray', 'purple', 'orange',
    'emerald', 'cyan', 'pink', 'amber', 'indigo', 'rose', 'slate', 'violet',
  ] as const

  it.each(allColors)('returns correct classes for %s', (color) => {
    const result = getStatusBadgeClasses(color)
    expect(result).toContain(`bg-${color}-100`)
    expect(result).toContain(`text-${color}-800`)
    expect(result).toContain(`border-${color}-200`)
    expect(result).toContain(`dark:bg-${color}-900/30`)
    expect(result).toContain(`dark:text-${color}-400`)
    expect(result).toContain(`dark:border-${color}-800`)
  })

  it('returns gray classes for unknown color', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = getStatusBadgeClasses('nonexistent' as any)
    expect(result).toBe(getStatusBadgeClasses('gray'))
  })

  it('returns a non-empty string for every valid color', () => {
    for (const color of allColors) {
      expect(getStatusBadgeClasses(color)).toBeTruthy()
    }
  })
})
