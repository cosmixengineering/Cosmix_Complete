/**
 * ImportExportDropdown — Shared dropdown for import/export operations.
 *
 * Standard UI pattern for all list pages with data import/export capability.
 * Renders a dropdown button with: Export CSV, Export Excel, Import CSV, Download Template.
 *
 * Export-only mode: omit `onImport` and `onDownloadTemplate`.
 */
import { useState, useRef, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Download,
  Upload,
  FileSpreadsheet,
  Loader2,
} from 'lucide-react'
import {
  Badge,
  Button,
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@uikit'
import { toast } from 'sonner'
import { ImportProgressDialog, type ImportResult } from './ImportProgressDialog'

export interface ImportExportDropdownProps {
  /** Export handler — receives format, should trigger file download */
  onExportCsv?: () => Promise<void>
  /** Export Excel handler */
  onExportExcel?: () => Promise<void>
  /** Import handler — receives file, returns success/error counts */
  onImport?: (file: File) => Promise<ImportResult>
  /** Download template handler */
  onDownloadTemplate?: () => void
  /** Total item count shown as badge on Export CSV */
  totalCount?: number
  /** Entity label for dialog titles, e.g. "Products" */
  entityLabel?: string
  /** Callback when import completes successfully (for page refresh) */
  onImportComplete?: () => void
  /** Accepted file types for import (default: ".csv") */
  acceptedFileTypes?: string
  /** Disable the entire dropdown */
  disabled?: boolean
}

export const ImportExportDropdown = ({
  onExportCsv,
  onExportExcel,
  onImport,
  onDownloadTemplate,
  totalCount,
  entityLabel,
  onImportComplete,
  acceptedFileTypes = '.csv',
  disabled,
}: ImportExportDropdownProps) => {
  const { t } = useTranslation('common')
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [isExporting, setIsExporting] = useState(false)
  const [isImporting, setIsImporting] = useState(false)
  const [importProgress, setImportProgress] = useState(0)
  const [importResult, setImportResult] = useState<ImportResult | null>(null)
  const [showImportDialog, setShowImportDialog] = useState(false)

  const hasImport = !!onImport
  const hasExport = !!onExportCsv || !!onExportExcel
  const label = hasImport && hasExport
    ? t('importExport.importExport', 'Import/Export')
    : hasImport
      ? t('importExport.import', 'Import')
      : t('buttons.export', 'Export')

  const handleExportCsv = useCallback(async () => {
    if (!onExportCsv) return
    setIsExporting(true)
    try {
      await onExportCsv()
      toast.success(t('buttons.exportSuccess', 'Export completed'))
    } catch {
      toast.error(t('buttons.exportFailed', 'Export failed'))
    } finally {
      setIsExporting(false)
    }
  }, [onExportCsv, t])

  const handleExportExcel = useCallback(async () => {
    if (!onExportExcel) return
    setIsExporting(true)
    try {
      await onExportExcel()
      toast.success(t('buttons.exportSuccess', 'Export completed'))
    } catch {
      toast.error(t('buttons.exportFailed', 'Export failed'))
    } finally {
      setIsExporting(false)
    }
  }, [onExportExcel, t])

  const handleFileSelect = useCallback(() => {
    fileInputRef.current?.click()
  }, [])

  const handleFileChange = useCallback(async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file || !onImport) return

    // Reset input so same file can be re-selected
    event.target.value = ''

    setIsImporting(true)
    setImportProgress(0)
    setImportResult(null)
    setShowImportDialog(true)

    try {
      setImportProgress(50)
      const result = await onImport(file)
      setImportProgress(100)
      setImportResult(result)

      if (result.success > 0) {
        toast.success(
          t('importExport.importSuccess', {
            count: result.success,
            defaultValue: '{{count}} items imported successfully',
          }),
        )
        onImportComplete?.()
      }

      if (result.errors.length > 0) {
        toast.warning(
          t('importExport.importPartialErrors', {
            count: result.errors.length,
            defaultValue: '{{count}} rows had errors',
          }),
        )
      }
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : t('importExport.importFailed', 'Import failed')
      toast.error(message)
      setShowImportDialog(false)
    } finally {
      setIsImporting(false)
    }
  }, [onImport, onImportComplete, t])

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="secondary"
            disabled={disabled || isExporting}
            className="h-9 ring-1 ring-border cursor-pointer"
          >
            {isExporting ? (
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <FileSpreadsheet className="h-4 w-4 mr-2" />
            )}
            {label}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-48">
          {onExportCsv && (
            <DropdownMenuItem
              className="cursor-pointer"
              onClick={handleExportCsv}
              disabled={isExporting}
            >
              {isExporting ? (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              ) : (
                <Download className="h-4 w-4 mr-2" />
              )}
              {t('importExport.exportCsv', 'Export CSV')}
              {totalCount != null && (
                <Badge variant="secondary" className="ml-auto text-xs">
                  {totalCount}
                </Badge>
              )}
            </DropdownMenuItem>
          )}
          {onExportExcel && (
            <DropdownMenuItem
              className="cursor-pointer"
              onClick={handleExportExcel}
              disabled={isExporting}
            >
              {isExporting ? (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              ) : (
                <FileSpreadsheet className="h-4 w-4 mr-2" />
              )}
              {t('importExport.exportExcel', 'Export Excel')}
            </DropdownMenuItem>
          )}
          {hasImport && hasExport && <DropdownMenuSeparator />}
          {onImport && (
            <DropdownMenuItem className="cursor-pointer" onClick={handleFileSelect}>
              <Upload className="h-4 w-4 mr-2" />
              {t('importExport.importCsv', 'Import CSV')}
            </DropdownMenuItem>
          )}
          {onDownloadTemplate && (
            <DropdownMenuItem className="cursor-pointer" onClick={onDownloadTemplate}>
              <FileSpreadsheet className="h-4 w-4 mr-2 text-muted-foreground" />
              {t('importExport.downloadTemplate', 'Download Template')}
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      {/* Hidden file input for import */}
      {hasImport && (
        <input
          ref={fileInputRef}
          type="file"
          accept={acceptedFileTypes}
          onChange={handleFileChange}
          className="hidden"
        />
      )}

      {/* Import Progress Dialog */}
      <ImportProgressDialog
        open={showImportDialog}
        onOpenChange={setShowImportDialog}
        isImporting={isImporting}
        progress={importProgress}
        result={importResult}
        entityLabel={entityLabel}
      />
    </>
  )
}
