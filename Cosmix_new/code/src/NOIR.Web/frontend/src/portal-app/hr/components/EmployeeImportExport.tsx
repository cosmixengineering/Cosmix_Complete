/**
 * EmployeeImportExport — HR-specific import/export using shared ImportExportDropdown.
 *
 * Employee import sends file directly to backend (FormData), unlike Products/Customers
 * which parse CSV on the frontend. The backend returns ImportResultDto which is adapted
 * to the shared ImportResult interface.
 */
import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { ImportExportDropdown, type ImportResult } from '@uikit'
import { exportEmployees, importEmployees } from '@/services/hr'
import { downloadCsv } from '@/lib/csv'
import type { EmployeeStatus, EmploymentType } from '@/types/hr'

export interface EmployeeImportExportProps {
  totalCount?: number
  onImportComplete?: () => void
  /** Current filters to pass to export */
  filters?: {
    departmentId?: string
    status?: EmployeeStatus
    employmentType?: EmploymentType
  }
}

const TEMPLATE_HEADERS = 'FirstName,LastName,Email,Phone,DepartmentCode,Position,JoinDate,EmploymentType'
const TEMPLATE_ROWS = [
  'John,Doe,john@example.com,+84123456789,DEV,Software Engineer,2025-01-15,FullTime',
  'Jane,Smith,jane@example.com,,HR,HR Manager,2025-03-01,FullTime',
].join('\n')

const downloadBlob = (blob: Blob, filename: string) => {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export const EmployeeImportExport = ({
  totalCount,
  onImportComplete,
  filters,
}: EmployeeImportExportProps) => {
  const { t } = useTranslation('common')

  const exportParams = {
    departmentId: filters?.departmentId,
    status: filters?.status,
    employmentType: filters?.employmentType,
  }

  const handleExportCsv = useCallback(async () => {
    const blob = await exportEmployees({ ...exportParams, format: 'CSV' })
    downloadBlob(blob, `employees-export-${new Date().toISOString().split('T')[0]}.csv`)
  }, [exportParams])

  const handleExportExcel = useCallback(async () => {
    const blob = await exportEmployees(exportParams)
    downloadBlob(blob, `employees-export-${new Date().toISOString().split('T')[0]}.xlsx`)
  }, [exportParams])

  const handleImport = useCallback(async (file: File): Promise<ImportResult> => {
    const result = await importEmployees(file)
    return {
      success: result.successCount,
      errors: result.errors.map(e => ({ row: e.rowNumber, message: e.message })),
    }
  }, [])

  const handleDownloadTemplate = useCallback(() => {
    downloadCsv([TEMPLATE_HEADERS, TEMPLATE_ROWS].join('\n'), 'employees-import-template.csv')
  }, [])

  return (
    <ImportExportDropdown
      onExportCsv={handleExportCsv}
      onExportExcel={handleExportExcel}
      onImport={handleImport}
      onDownloadTemplate={handleDownloadTemplate}
      totalCount={totalCount}
      entityLabel={t('hr.employees', 'Employees')}
      onImportComplete={onImportComplete}
      acceptedFileTypes=".csv"
    />
  )
}
