import { cn, formatDisplayName } from './utils'

describe('cn', () => {
  it('merges class names', () => {
    expect(cn('foo', 'bar')).toBe('foo bar')
  })

  it('handles conditional classes', () => {
    expect(cn('base', false && 'hidden', 'visible')).toBe('base visible')
  })

  it('deduplicates conflicting tailwind classes', () => {
    expect(cn('p-4', 'p-2')).toBe('p-2')
  })

  it('merges tailwind variants correctly', () => {
    expect(cn('text-red-500', 'text-blue-500')).toBe('text-blue-500')
  })

  it('handles undefined and null inputs', () => {
    expect(cn('foo', undefined, null, 'bar')).toBe('foo bar')
  })

  it('handles empty call', () => {
    expect(cn()).toBe('')
  })

  it('handles array inputs', () => {
    expect(cn(['foo', 'bar'])).toBe('foo bar')
  })
})

describe('formatDisplayName', () => {
  it('converts PascalCase to spaced words', () => {
    expect(formatDisplayName('WelcomeEmail')).toBe('Welcome Email')
  })

  it('converts acronym OTP to uppercase', () => {
    expect(formatDisplayName('PasswordResetOtp')).toBe('Password Reset OTP')
  })

  it('converts acronym API to uppercase', () => {
    expect(formatDisplayName('ApiSettings')).toBe('API Settings')
  })

  it('converts acronym URL to uppercase', () => {
    expect(formatDisplayName('UrlValidation')).toBe('URL Validation')
  })

  it('converts acronym SMTP to uppercase', () => {
    expect(formatDisplayName('SmtpConfig')).toBe('SMTP Config')
  })

  it('handles single word', () => {
    expect(formatDisplayName('Dashboard')).toBe('Dashboard')
  })

  it('handles multiple acronyms', () => {
    expect(formatDisplayName('ApiUrlConfig')).toBe('API URL Config')
  })

  it('converts ID acronym', () => {
    expect(formatDisplayName('UserId')).toBe('User ID')
  })

  it('converts SSO acronym', () => {
    expect(formatDisplayName('SsoSettings')).toBe('SSO Settings')
  })

  it('converts HTML acronym', () => {
    expect(formatDisplayName('HtmlTemplate')).toBe('HTML Template')
  })

  it('converts CSS acronym', () => {
    expect(formatDisplayName('CssStyles')).toBe('CSS Styles')
  })
})
