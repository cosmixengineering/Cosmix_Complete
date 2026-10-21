import { getGravatarUrl, getInitials, getAvatarColor } from './gravatar'

describe('getGravatarUrl', () => {
  it('returns a URL with correct format', async () => {
    const url = await getGravatarUrl('test@example.com')
    expect(url).toMatch(/^https:\/\/www\.gravatar\.com\/avatar\/[a-f0-9]{64}\?s=80&d=404$/)
  })

  it('uses default size of 80', async () => {
    const url = await getGravatarUrl('test@example.com')
    expect(url).toContain('s=80')
  })

  it('accepts custom size', async () => {
    const url = await getGravatarUrl('test@example.com', 200)
    expect(url).toContain('s=200')
  })

  it('normalizes email to lowercase and trimmed', async () => {
    const url1 = await getGravatarUrl('Test@Example.com')
    const url2 = await getGravatarUrl('test@example.com')
    expect(url1).toBe(url2)
  })

  it('trims whitespace from email', async () => {
    const url1 = await getGravatarUrl('  test@example.com  ')
    const url2 = await getGravatarUrl('test@example.com')
    expect(url1).toBe(url2)
  })

  it('produces different hashes for different emails', async () => {
    const url1 = await getGravatarUrl('alice@example.com')
    const url2 = await getGravatarUrl('bob@example.com')
    expect(url1).not.toBe(url2)
  })
})

describe('getInitials', () => {
  it('returns both initials when first and last name provided', () => {
    expect(getInitials('John', 'Doe', 'john@example.com')).toBe('JD')
  })

  it('returns first initial only when last name is null', () => {
    expect(getInitials('John', null, 'john@example.com')).toBe('J')
  })

  it('returns last initial only when first name is null', () => {
    expect(getInitials(null, 'Doe', 'john@example.com')).toBe('D')
  })

  it('falls back to email first character when both names are null', () => {
    expect(getInitials(null, null, 'john@example.com')).toBe('J')
  })

  it('returns uppercase initials', () => {
    expect(getInitials('john', 'doe', 'john@example.com')).toBe('JD')
  })

  it('handles empty strings for names (falls back to email)', () => {
    expect(getInitials('', '', 'alice@test.com')).toBe('A')
  })
})

describe('getAvatarColor', () => {
  it('returns an hsl color string', () => {
    const color = getAvatarColor('test@example.com')
    expect(color).toMatch(/^hsl\(-?\d+, 65%, 40%\)$/)
  })

  it('is deterministic (same input = same output)', () => {
    const color1 = getAvatarColor('test@example.com')
    const color2 = getAvatarColor('test@example.com')
    expect(color1).toBe(color2)
  })

  it('produces different colors for different inputs', () => {
    const color1 = getAvatarColor('alice@example.com')
    const color2 = getAvatarColor('bob@example.com')
    expect(color1).not.toBe(color2)
  })
})
