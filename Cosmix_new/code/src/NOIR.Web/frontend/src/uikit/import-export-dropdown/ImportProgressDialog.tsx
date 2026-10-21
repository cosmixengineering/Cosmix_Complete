/**
 * ImportProgressDialog — Shared import progress + result dialog.
 *
 * Shows a progress bar while importing, then a success/error summary.
 * Used by ImportExportDropdown and can be used standalone.
 */
import { useTranslation } from 'react-i18next'
import { Check, AlertTriangle } from 'lucide-react'
import {
  Button,
  Credenza,
  CredenzaBody,
  CredenzaContent,
  CredenzaDescription,
  CredenzaFooter,
  CredenzaHeader,
  CredenzaTitle,
  Progress,
  ScrollArea,
} from '@uikit'

export interface ImportError {
  row: number
  message: string
}

export interface ImportResult {
  success: number
  errors: ImportError[]
}

export interface ImportProgressDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  isImporting: boolean
  progress: number
  result: ImportResult | null
  /** Entity label for dialog title, e.g. "Products", "Customers" */
  entityLabel?: string
}

export const ImportProgressDialog = ({
  open,
  onOpenChange,
  isImporting,
  progress,
  result,
  entityLabel,
}: ImportProgressDialogProps) => {
  const { t } = useTranslation('common')

  const importingTitle = entityLabel
    ? t('importExport.importing', { entity: entityLabel, defaultValue: `Importing ${entityLabel}...` })
    : t('importExport.importingGeneric', 'Importing...')

  return (
    <Credenza open={open} onOpenChange={onOpenChange}>
      <CredenzaContent className="sm:max-w-[500px]">
        <CredenzaHeader>
          <CredenzaTitle>
            {isImporting ? importingTitle : t('importExport.importComplete', 'Import Complete')}
          </CredenzaTitle>
          <CredenzaDescription>
            {isImporting
              ? t('importExport.pleaseWait', 'Please wait while we process your file.')
              : t('importExport.summary', 'Here is a summary of the import.')}
          </CredenzaDescription>
        </CredenzaHeader>

        <CredenzaBody>
          <div className="space-y-4">
            {isImporting ? (
              <div className="space-y-2">
                <Progress value={progress} />
                <p className="text-sm text-center text-muted-foreground">{progress}%</p>
              </div>
            ) : result ? (
              <>
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-2 p-3 rounded-lg bg-emerald-500/10 flex-1">
                    <Check className="h-5 w-5 text-emerald-600" />
                    <div>
                      <p className="font-medium text-emerald-600">{result.success}</p>
                      <p className="text-xs text-emerald-600/80">
                        {t('importExport.successLabel', 'Imported')}
                      </p>
                    </div>
                  </div>
                  {result.errors.length > 0 && (
                    <div className="flex items-center gap-2 p-3 rounded-lg bg-destructive/10 flex-1">
                      <AlertTriangle className="h-5 w-5 text-destructive" />
                      <div>
                        <p className="font-medium text-destructive">{result.errors.length}</p>
                        <p className="text-xs text-destructive/80">
                          {t('importExport.errorsLabel', 'Errors')}
                        </p>
                      </div>
                    </div>
                  )}
                </div>

                {result.errors.length > 0 && (
                  <div className="space-y-2">
                    <p className="text-sm font-medium">
                      {t('importExport.errorDetails', 'Error Details:')}
                    </p>
                    <ScrollArea className="h-[150px] rounded-md border p-2">
                      {result.errors.map((error, index) => (
                        <div key={index} className="text-sm py-1 border-b last:border-0">
                          <span className="text-muted-foreground">
                            {t('importExport.row', 'Row')} {error.row}:
                          </span>{' '}
                          <span className="text-destructive">{error.message}</span>
                        </div>
                      ))}
                    </ScrollArea>
                  </div>
                )}
              </>
            ) : null}
          </div>
        </CredenzaBody>

        <CredenzaFooter>
          <Button
            onClick={() => onOpenChange(false)}
            disabled={isImporting}
            className="cursor-pointer"
          >
            {isImporting ? t('buttons.cancel', 'Cancel') : t('buttons.close', 'Close')}
          </Button>
        </CredenzaFooter>
      </CredenzaContent>
    </Credenza>
  )
}
