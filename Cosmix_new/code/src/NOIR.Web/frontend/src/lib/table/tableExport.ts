import type { Table, RowData } from '@tanstack/react-table'

export interface ExportOptions {
  /** File name without extension */
  filename?: string
  /** Only export visible columns (default: true) */
  visibleColumnsOnly?: boolean
}

/**
 * Get column header text from a TanStack column definition
 */
const getColumnHeader = (column: ReturnType<Table<unknown>['getVisibleLeafColumns']>[number]): string => {
  // Prefer meta.label (set on all NOIR columns)
  const meta = column.columnDef.meta as { label?: string } | undefined
  if (meta?.label) return meta.label
  if (typeof column.columnDef.header === 'string') return column.columnDef.header
  return column.id
}

/**
 * Serialize a cell value to a plain string for export
 */
const serializeValue = (value: unknown): string => {
  if (value === null || value === undefined) return ''
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (value instanceof Date) return value.toISOString()
  return String(value)
}

/**
 * Export TanStack table data to CSV and trigger a download.
 * Exports all rows in the current row model (respects filters + sorting).
 */
export const exportTableToCSV = <TData extends RowData>(
  table: Table<TData>,
  { filename = 'export', visibleColumnsOnly = true }: ExportOptions = {}
): void => {
  const columns = visibleColumnsOnly
    ? table.getVisibleLeafColumns().filter(col => col.getCanHide() !== false)
    : table.getAllLeafColumns().filter(col => col.getCanHide() !== false)

  // Header row
  const headers = columns.map(getColumnHeader as (col: typeof columns[number]) => string)

  // Data rows
  const rows = table.getRowModel().rows.map(row =>
    columns.map(col => {
      const cell = row.getAllCells().find(c => c.column.id === col.id)
      return serializeValue(cell?.getValue())
    })
  )

  // Build CSV string
  const escapeCsv = (val: string) => {
    if (val.includes(',') || val.includes('"') || val.includes('\n')) {
      return `"${val.replace(/"/g, '""')}"`
    }
    return val
  }

  const csvContent = [
    headers.map(escapeCsv).join(','),
    ...rows.map(row => row.map(escapeCsv).join(','))
  ].join('\n')

  // Trigger download
  const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${filename}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

/**
 * Export TanStack table data to Excel (.xlsx) via dynamic import of xlsx library.
 * Falls back to CSV if xlsx is not available.
 */
export const exportTableToExcel = async <TData extends RowData>(
  table: Table<TData>,
  { filename = 'export', visibleColumnsOnly = true }: ExportOptions = {}
): Promise<void> => {
  const columns = visibleColumnsOnly
    ? table.getVisibleLeafColumns().filter(col => col.getCanHide() !== false)
    : table.getAllLeafColumns().filter(col => col.getCanHide() !== false)

  const headers = columns.map(getColumnHeader as (col: typeof columns[number]) => string)

  const rows = table.getRowModel().rows.map(row =>
    columns.reduce<Record<string, unknown>>((acc, col) => {
      const cell = row.getAllCells().find(c => c.column.id === col.id)
      const header = getColumnHeader(col as Parameters<typeof getColumnHeader>[0])
      acc[header] = cell?.getValue() ?? ''
      return acc
    }, {})
  )

  try {
    // Dynamic import of xlsx (optional peer dep) — use variable to bypass Vite import analysis
    const mod = 'xlsx'
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const XLSX = await (Function(`return import("${mod}")`)() as Promise<any>)
    const ws = XLSX.utils.json_to_sheet(rows, { header: headers })
    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Data')

    // Auto-column widths
    ws['!cols'] = columns.map(col => ({
      wch: Math.min(Math.max((getColumnHeader(col as Parameters<typeof getColumnHeader>[0])).length + 4, 12), 40),
    }))

    XLSX.writeFile(wb, `${filename}.xlsx`)
  } catch {
    // xlsx not installed — fall back to CSV
    exportTableToCSV(table, { filename, visibleColumnsOnly })
  }
}
