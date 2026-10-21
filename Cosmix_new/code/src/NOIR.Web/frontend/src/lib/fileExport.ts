/**
 * Shared file export helper for downloading files from API endpoints.
 * Handles authentication, content-disposition parsing, and blob download.
 */
import { getAccessToken } from '@/services/tokenStorage'
import { getPageContext } from '@/services/pageContext'
import { i18n } from '@/i18n'
import { downloadBlob } from '@/lib/csv'

/**
 * Download a file export from an API endpoint.
 * Adds auth token, page context, and language headers automatically.
 */
export const downloadFileExport = async (url: string, defaultFilename: string): Promise<void> => {
  const token = getAccessToken()
  const pageContext = getPageContext()
  const headers: Record<string, string> = {
    'Accept-Language': i18n.language,
  }
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (pageContext) headers['X-Page-Context'] = pageContext

  const response = await fetch(url, { credentials: 'include', headers })
  if (!response.ok) throw new Error(`Export failed: ${response.status}`)

  const disposition = response.headers.get('Content-Disposition')
  let filename = defaultFilename
  if (disposition) {
    const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
    if (match?.[1]) filename = match[1].replace(/['"]/g, '')
  }

  const blob = await response.blob()
  downloadBlob(blob, filename)
}
