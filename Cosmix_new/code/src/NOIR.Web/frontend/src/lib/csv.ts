/**
 * Shared CSV utilities for import/export operations.
 * Extracted from ProductImportExport for reuse across modules.
 */

/**
 * Escape a value for CSV output.
 * Wraps values containing commas, quotes, or newlines in double-quotes.
 */
export const escapeCSV = (value: string | null | undefined): string => {
  if (!value) return ''
  if (value.includes(',') || value.includes('"') || value.includes('\n')) {
    return `"${value.replace(/"/g, '""')}"`
  }
  return value
}

/**
 * Parse CSV text into headers and rows.
 * Handles quoted fields with commas and newlines.
 */
export const parseCSV = (text: string): { headers: string[]; rows: string[][] } => {
  const lines: string[] = []
  let currentLine = ''
  let inQuotes = false

  for (let i = 0; i < text.length; i++) {
    const char = text[i]
    if (char === '"') {
      if (inQuotes && text[i + 1] === '"') {
        currentLine += '"'
        i++
      } else {
        inQuotes = !inQuotes
      }
    } else if (char === '\n' && !inQuotes) {
      if (currentLine.trim()) {
        lines.push(currentLine)
      }
      currentLine = ''
    } else if (char === '\r' && !inQuotes) {
      // Skip carriage return
    } else {
      currentLine += char
    }
  }

  if (currentLine.trim()) {
    lines.push(currentLine)
  }

  if (lines.length === 0) {
    return { headers: [], rows: [] }
  }

  const parseLine = (line: string): string[] => {
    const values: string[] = []
    let currentValue = ''
    let inQuotes = false

    for (let i = 0; i < line.length; i++) {
      const char = line[i]
      if (char === '"') {
        if (inQuotes && line[i + 1] === '"') {
          currentValue += '"'
          i++
        } else {
          inQuotes = !inQuotes
        }
      } else if (char === ',' && !inQuotes) {
        values.push(currentValue)
        currentValue = ''
      } else {
        currentValue += char
      }
    }
    values.push(currentValue)

    return values
  }

  const headers = parseLine(lines[0])
  const rows = lines.slice(1).map(parseLine)

  return { headers, rows }
}

/**
 * Download a string as a CSV file with BOM for Excel compatibility.
 */
export const downloadCsv = (content: string, filename: string): void => {
  const blob = new Blob(['\uFEFF' + content], { type: 'text/csv;charset=utf-8;' })
  downloadBlob(blob, filename)
}

/**
 * Download a Blob as a file.
 */
export const downloadBlob = (blob: Blob, filename: string): void => {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  window.URL.revokeObjectURL(url)
}
