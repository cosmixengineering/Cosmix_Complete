import { useState, useRef, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { Upload, ImageIcon, Loader2 } from 'lucide-react'
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
import { cn } from '@/lib/utils'
import { useUploadMediaFile } from '@/hooks/useMediaFiles'

interface MediaUploadDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/avif']
const MAX_FILE_SIZE = 10 * 1024 * 1024 // 10MB

export const MediaUploadDialog = ({ open, onOpenChange }: MediaUploadDialogProps) => {
  const { t } = useTranslation('common')
  const uploadMutation = useUploadMediaFile()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [isDragOver, setIsDragOver] = useState(false)
  const [validationError, setValidationError] = useState<string | null>(null)

  const validateFile = useCallback((file: File): string | null => {
    if (!ACCEPTED_TYPES.includes(file.type)) {
      return t('media.invalidFileType', 'Invalid file type. Accepted: JPG, PNG, GIF, WebP, AVIF')
    }
    if (file.size > MAX_FILE_SIZE) {
      return t('media.fileTooLarge', 'File too large. Maximum size is 10MB')
    }
    return null
  }, [t])

  const handleUpload = useCallback(async (file: File) => {
    const error = validateFile(file)
    if (error) {
      setValidationError(error)
      return
    }
    setValidationError(null)
    await uploadMutation.mutateAsync({ file, folder: 'content' })
    onOpenChange(false)
  }, [validateFile, uploadMutation, onOpenChange])

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) handleUpload(file)
    // Reset input so same file can be selected again
    if (fileInputRef.current) fileInputRef.current.value = ''
  }, [handleUpload])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
    const file = e.dataTransfer.files[0]
    if (file) handleUpload(file)
  }, [handleUpload])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
  }, [])

  return (
    <Credenza open={open} onOpenChange={onOpenChange}>
      <CredenzaContent>
        <CredenzaHeader>
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-xl bg-primary/10 border border-primary/20">
              <Upload className="h-5 w-5 text-primary" />
            </div>
            <div>
              <CredenzaTitle>{t('media.uploadFile', 'Upload File')}</CredenzaTitle>
              <CredenzaDescription>{t('media.uploadDescription', 'Upload an image to the media library')}</CredenzaDescription>
            </div>
          </div>
        </CredenzaHeader>
        <CredenzaBody>
          <div className="space-y-4">
            {/* Drop zone */}
            <div
              className={cn(
                'border-2 border-dashed rounded-xl p-8 text-center transition-all duration-200 cursor-pointer',
                isDragOver
                  ? 'border-primary bg-primary/5'
                  : 'border-border hover:border-primary/50 hover:bg-muted/30',
                uploadMutation.isPending && 'pointer-events-none opacity-50'
              )}
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onClick={() => fileInputRef.current?.click()}
            >
              {uploadMutation.isPending ? (
                <div className="flex flex-col items-center gap-3">
                  <Loader2 className="h-10 w-10 text-primary animate-spin" />
                  <p className="text-sm text-muted-foreground">{t('media.uploading', 'Uploading...')}</p>
                </div>
              ) : (
                <div className="flex flex-col items-center gap-3">
                  <div className="p-3 rounded-full bg-muted">
                    <ImageIcon className="h-8 w-8 text-muted-foreground" />
                  </div>
                  <div>
                    <p className="text-sm font-medium">{t('media.dragDropOrClick', 'Drag & drop or click to select')}</p>
                    <p className="text-xs text-muted-foreground mt-1">
                      {t('media.acceptedFormats', 'JPG, PNG, GIF, WebP, AVIF (max 10MB)')}
                    </p>
                  </div>
                </div>
              )}
            </div>

            {validationError && (
              <p className="text-sm text-destructive">{validationError}</p>
            )}

            <input
              ref={fileInputRef}
              type="file"
              accept={ACCEPTED_TYPES.join(',')}
              onChange={handleFileSelect}
              className="hidden"
            />
          </div>
        </CredenzaBody>
        <CredenzaFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={uploadMutation.isPending}
            className="cursor-pointer"
          >
            {t('labels.cancel', 'Cancel')}
          </Button>
        </CredenzaFooter>
      </CredenzaContent>
    </Credenza>
  )
}
