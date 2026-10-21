import { useTranslation } from 'react-i18next'
import { Trash2, Loader2 } from 'lucide-react'
import {
  Button,
  Credenza,
  CredenzaContent,
  CredenzaHeader,
  CredenzaTitle,
  CredenzaDescription,
  CredenzaBody,
  CredenzaFooter,
} from '@uikit'
import type { MediaFileListItem } from '@/types'

interface DeleteMediaDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Single file to delete (when not bulk) */
  file?: MediaFileListItem | null
  /** Filenames for bulk delete */
  bulkFileNames?: string[]
  /** Count of items being deleted */
  bulkCount?: number
  onConfirm: () => void
  isPending: boolean
}

export const DeleteMediaDialog = ({
  open,
  onOpenChange,
  file,
  bulkFileNames,
  bulkCount,
  onConfirm,
  isPending,
}: DeleteMediaDialogProps) => {
  const { t } = useTranslation('common')
  const isBulk = (bulkCount ?? 0) > 0

  return (
    <Credenza open={open} onOpenChange={onOpenChange}>
      <CredenzaContent className="border-destructive/30">
        <CredenzaHeader>
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-xl bg-destructive/10 border border-destructive/20">
              <Trash2 className="h-5 w-5 text-destructive" />
            </div>
            <div>
              <CredenzaTitle>
                {isBulk
                  ? t('media.bulkDeleteTitle', { count: bulkCount, defaultValue: `Delete ${bulkCount} Files` })
                  : t('media.deleteTitle', 'Delete File')}
              </CredenzaTitle>
              <CredenzaDescription>
                {isBulk
                  ? t('media.bulkDeleteConfirmation', {
                      count: bulkCount,
                      defaultValue: `Are you sure you want to delete ${bulkCount} selected files? This action cannot be undone.`,
                    })
                  : t('media.deleteConfirmation', {
                      name: file?.originalFileName,
                      defaultValue: `Are you sure you want to delete "${file?.originalFileName}"? This action cannot be undone.`,
                    })}
              </CredenzaDescription>
            </div>
          </div>
        </CredenzaHeader>
        <CredenzaBody>
          {isBulk && bulkFileNames && bulkFileNames.length > 0 && (
            <div className="max-h-32 overflow-y-auto rounded-lg bg-muted/50 p-3 space-y-1">
              {bulkFileNames.map((name, i) => (
                <p key={i} className="text-sm text-muted-foreground truncate">{name}</p>
              ))}
            </div>
          )}
        </CredenzaBody>
        <CredenzaFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isPending}
            className="cursor-pointer"
          >
            {t('labels.cancel', 'Cancel')}
          </Button>
          <Button
            variant="destructive"
            onClick={onConfirm}
            disabled={isPending}
            className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
          >
            {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isPending
              ? t('labels.deleting', 'Deleting...')
              : t('labels.delete', 'Delete')}
          </Button>
        </CredenzaFooter>
      </CredenzaContent>
    </Credenza>
  )
}
