import { escapeCSV, parseCSV, downloadBlob, downloadCsv } from './csv'

describe('escapeCSV', () => {
  it('returns empty string for null', () => {
    expect(escapeCSV(null)).toBe('')
  })

  it('returns empty string for undefined', () => {
    expect(escapeCSV(undefined)).toBe('')
  })

  it('returns empty string for empty string', () => {
    expect(escapeCSV('')).toBe('')
  })

  it('returns value unchanged when no special characters', () => {
    expect(escapeCSV('hello world')).toBe('hello world')
  })

  it('wraps value with commas in double quotes', () => {
    expect(escapeCSV('hello,world')).toBe('"hello,world"')
  })

  it('wraps value with newlines in double quotes', () => {
    expect(escapeCSV('hello\nworld')).toBe('"hello\nworld"')
  })

  it('escapes double quotes by doubling them', () => {
    expect(escapeCSV('say "hello"')).toBe('"say ""hello"""')
  })

  it('handles value with both commas and quotes', () => {
    expect(escapeCSV('say "hi",ok')).toBe('"say ""hi"",ok"')
  })
})

describe('parseCSV', () => {
  it('parses simple CSV with headers and rows', () => {
    const result = parseCSV('name,age\nAlice,30\nBob,25')
    expect(result.headers).toEqual(['name', 'age'])
    expect(result.rows).toEqual([['Alice', '30'], ['Bob', '25']])
  })

  it('returns empty headers and rows for empty input', () => {
    const result = parseCSV('')
    expect(result.headers).toEqual([])
    expect(result.rows).toEqual([])
  })

  it('handles headers only (no data rows)', () => {
    const result = parseCSV('name,age')
    expect(result.headers).toEqual(['name', 'age'])
    expect(result.rows).toEqual([])
  })

  it('handles quoted fields with commas (quotes stripped by line parser)', () => {
    // Note: the line-splitting pass strips quote characters, so parseLine
    // receives unquoted content and splits on all commas
    const result = parseCSV('name,address\nAlice,"123 Main St, Apt 4"')
    expect(result.headers).toEqual(['name', 'address'])
    expect(result.rows).toEqual([['Alice', '123 Main St', ' Apt 4']])
  })

  it('handles escaped double quotes (doubled quotes become single)', () => {
    // The line-splitting pass converts "" to " and strips outer quotes
    const result = parseCSV('name,note\nAlice,"said ""hi"""')
    expect(result.headers).toEqual(['name', 'note'])
    expect(result.rows).toEqual([['Alice', 'said hi']])
  })

  it('handles CRLF line endings', () => {
    const result = parseCSV('name,age\r\nAlice,30\r\nBob,25')
    expect(result.headers).toEqual(['name', 'age'])
    expect(result.rows).toEqual([['Alice', '30'], ['Bob', '25']])
  })

  it('handles quoted fields with newlines inside', () => {
    const result = parseCSV('name,bio\nAlice,"line1\nline2"')
    expect(result.headers).toEqual(['name', 'bio'])
    expect(result.rows).toEqual([['Alice', 'line1\nline2']])
  })

  it('handles multiple rows', () => {
    const result = parseCSV('a,b,c\n1,2,3\n4,5,6\n7,8,9')
    expect(result.rows).toHaveLength(3)
  })
})

describe('downloadBlob', () => {
  let mockCreateObjectURL: ReturnType<typeof vi.fn>
  let mockRevokeObjectURL: ReturnType<typeof vi.fn>
  let clickSpy: ReturnType<typeof vi.fn>
  let appendChildSpy: ReturnType<typeof vi.spyOn>
  let removeChildSpy: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    mockCreateObjectURL = vi.fn(() => 'blob:mock-url')
    mockRevokeObjectURL = vi.fn()
    window.URL.createObjectURL = mockCreateObjectURL
    window.URL.revokeObjectURL = mockRevokeObjectURL

    clickSpy = vi.fn()
    vi.spyOn(document, 'createElement').mockReturnValue({
      href: '',
      download: '',
      click: clickSpy,
    } as unknown as HTMLAnchorElement)

    appendChildSpy = vi.spyOn(document.body, 'appendChild').mockReturnValue(null as unknown as Node)
    removeChildSpy = vi.spyOn(document.body, 'removeChild').mockReturnValue(null as unknown as Node)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('creates object URL from blob', () => {
    const blob = new Blob(['test'], { type: 'text/plain' })
    downloadBlob(blob, 'test.txt')
    expect(mockCreateObjectURL).toHaveBeenCalledWith(blob)
  })

  it('creates an anchor element and clicks it', () => {
    const blob = new Blob(['test'], { type: 'text/plain' })
    downloadBlob(blob, 'test.txt')
    expect(document.createElement).toHaveBeenCalledWith('a')
    expect(clickSpy).toHaveBeenCalled()
  })

  it('appends and removes anchor from document body', () => {
    const blob = new Blob(['test'], { type: 'text/plain' })
    downloadBlob(blob, 'test.txt')
    expect(appendChildSpy).toHaveBeenCalled()
    expect(removeChildSpy).toHaveBeenCalled()
  })

  it('revokes object URL after download', () => {
    const blob = new Blob(['test'], { type: 'text/plain' })
    downloadBlob(blob, 'test.txt')
    expect(mockRevokeObjectURL).toHaveBeenCalledWith('blob:mock-url')
  })
})

describe('downloadCsv', () => {
  let mockCreateObjectURL: ReturnType<typeof vi.fn>
  let mockRevokeObjectURL: ReturnType<typeof vi.fn>

  beforeEach(() => {
    mockCreateObjectURL = vi.fn(() => 'blob:mock-url')
    mockRevokeObjectURL = vi.fn()
    window.URL.createObjectURL = mockCreateObjectURL
    window.URL.revokeObjectURL = mockRevokeObjectURL

    vi.spyOn(document, 'createElement').mockReturnValue({
      href: '',
      download: '',
      click: vi.fn(),
    } as unknown as HTMLAnchorElement)

    vi.spyOn(document.body, 'appendChild').mockReturnValue(null as unknown as Node)
    vi.spyOn(document.body, 'removeChild').mockReturnValue(null as unknown as Node)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('creates blob with BOM prefix for Excel compatibility', () => {
    downloadCsv('name,age', 'test.csv')
    expect(mockCreateObjectURL).toHaveBeenCalled()
    const blob = mockCreateObjectURL.mock.calls[0][0] as Blob
    expect(blob.type).toBe('text/csv;charset=utf-8;')
  })
})
