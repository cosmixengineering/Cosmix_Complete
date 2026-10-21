import {
  validatePassword,
  getPasswordStrength,
  getStrengthColor,
  getStrengthTextColor,
} from './passwordValidation'

describe('validatePassword', () => {
  it('checks minimum length of 12', () => {
    expect(validatePassword('short').length).toBe(false)
    expect(validatePassword('exactlytwelv').length).toBe(true)
    expect(validatePassword('longerthantwelve').length).toBe(true)
  })

  it('checks for lowercase letters', () => {
    expect(validatePassword('NOLOWERCASE123').lowercase).toBe(false)
    expect(validatePassword('hasLowercase').lowercase).toBe(true)
  })

  it('checks for uppercase letters', () => {
    expect(validatePassword('nouppercase123').uppercase).toBe(false)
    expect(validatePassword('HasUppercase').uppercase).toBe(true)
  })

  it('checks for digits', () => {
    expect(validatePassword('NoDigitsHere!').digit).toBe(false)
    expect(validatePassword('Has1Digit').digit).toBe(true)
  })

  it('checks for special characters', () => {
    expect(validatePassword('NoSpecialChars1').special).toBe(false)
    expect(validatePassword('HasSpecial!').special).toBe(true)
  })

  it('checks for at least 4 unique characters', () => {
    expect(validatePassword('aaa').uniqueChars).toBe(false)
    expect(validatePassword('aab').uniqueChars).toBe(false)
    expect(validatePassword('abcd').uniqueChars).toBe(true)
  })

  it('returns all true for a strong password', () => {
    const result = validatePassword('MyStr0ng!Pass')
    expect(result.length).toBe(true)
    expect(result.lowercase).toBe(true)
    expect(result.uppercase).toBe(true)
    expect(result.digit).toBe(true)
    expect(result.special).toBe(true)
    expect(result.uniqueChars).toBe(true)
  })

  it('returns all false for empty string', () => {
    const result = validatePassword('')
    expect(result.length).toBe(false)
    expect(result.lowercase).toBe(false)
    expect(result.uppercase).toBe(false)
    expect(result.digit).toBe(false)
    expect(result.special).toBe(false)
    expect(result.uniqueChars).toBe(false)
  })
})

describe('getPasswordStrength', () => {
  it('returns weak for empty password', () => {
    const result = getPasswordStrength('')
    expect(result.level).toBe('weak')
    expect(result.score).toBe(0)
    expect(result.isValid).toBe(false)
  })

  it('returns weak for score < 40 (0-1 requirements met)', () => {
    // Only lowercase met: 1/6 = 17%
    const result = getPasswordStrength('a')
    expect(result.level).toBe('weak')
    expect(result.score).toBeLessThan(40)
  })

  it('returns fair for score 40-59 (2-3 requirements met)', () => {
    // lowercase + uppercase + uniqueChars = 3/6 = 50%
    const result = getPasswordStrength('abcD')
    expect(result.level).toBe('fair')
    expect(result.score).toBeGreaterThanOrEqual(40)
    expect(result.score).toBeLessThan(60)
  })

  it('returns good for score 60-79 (4 requirements met)', () => {
    // lowercase + uppercase + digit + uniqueChars = 4/6 = 67%
    const result = getPasswordStrength('abcD1')
    expect(result.level).toBe('good')
    expect(result.score).toBeGreaterThanOrEqual(60)
    expect(result.score).toBeLessThan(80)
  })

  it('returns strong for score >= 80 (5-6 requirements met)', () => {
    // All 6 met: 6/6 = 100%
    const result = getPasswordStrength('MyStr0ng!Pass')
    expect(result.level).toBe('strong')
    expect(result.score).toBeGreaterThanOrEqual(80)
  })

  it('isValid is true only when all requirements are met', () => {
    expect(getPasswordStrength('MyStr0ng!Pass').isValid).toBe(true)
    expect(getPasswordStrength('short').isValid).toBe(false)
  })

  it('score is percentage of met requirements', () => {
    // 0/6 = 0%
    expect(getPasswordStrength('').score).toBe(0)
    // 6/6 = 100%
    expect(getPasswordStrength('MyStr0ng!Pass').score).toBe(100)
  })
})

describe('getStrengthColor', () => {
  it('returns bg-red-500 for weak', () => {
    expect(getStrengthColor('weak')).toBe('bg-red-500')
  })

  it('returns bg-orange-500 for fair', () => {
    expect(getStrengthColor('fair')).toBe('bg-orange-500')
  })

  it('returns bg-yellow-500 for good', () => {
    expect(getStrengthColor('good')).toBe('bg-yellow-500')
  })

  it('returns bg-green-500 for strong', () => {
    expect(getStrengthColor('strong')).toBe('bg-green-500')
  })
})

describe('getStrengthTextColor', () => {
  it('returns text-red-700 for weak', () => {
    expect(getStrengthTextColor('weak')).toBe('text-red-700')
  })

  it('returns text-orange-700 for fair', () => {
    expect(getStrengthTextColor('fair')).toBe('text-orange-700')
  })

  it('returns text-yellow-700 for good', () => {
    expect(getStrengthTextColor('good')).toBe('text-yellow-700')
  })

  it('returns text-green-700 for strong', () => {
    expect(getStrengthTextColor('strong')).toBe('text-green-700')
  })
})
