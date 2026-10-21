/**
 * ExportButton — Report-specific export using shared ImportExportDropdown.
 */
import { useCallback } from 'react'
import { ImportExportDropdown } from '@uikit'
import { exportReport } from '@/services/reports'
import type { ReportType } from '@/types/report'

export interface ExportButtonProps {
  reportType: ReportType
  startDate?: string
  endDate?: string
  disabled?: boolean
}

export const ExportButton = ({ reportType, startDate, endDate, disabled }: ExportButtonProps) => {
  const handleExportCsv = useCallback(async () => {
    await exportReport({ reportType, format: 'CSV', startDate, endDate })
  }, [reportType, startDate, endDate])

  const handleExportExcel = useCallback(async () => {
    await exportReport({ reportType, format: 'Excel', startDate, endDate })
  }, [reportType, startDate, endDate])

  return (
    <ImportExportDropdown
      onExportCsv={handleExportCsv}
      onExportExcel={handleExportExcel}
      disabled={disabled}
    />
  )
}
