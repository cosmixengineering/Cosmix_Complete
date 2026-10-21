import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { FolderOpen, Copy, Check } from 'lucide-react'
import { toast } from 'sonner'
import { Badge, Checkbox, FilePreviewModal, ThumbHashImage } from '@uikit'
import type { MediaFileListItem } from '@/types'
import { formatFileSize, extractFolderName } from './media-utils'

interface MediaGridProps {
  items: MediaFileListItem[]
  selectedIds: Set<string>
  onToggleSelect: (id: string) => void
  onOpenDetail: (item: MediaFileListItem) => void
}

export const MediaGrid = ({ items, selectedIds, onToggleSelect, onOpenDetail }: MediaGridProps) => {
  const { t } = useTranslation('common')
  const [previewFile, setPreviewFile] = useState<MediaFileListItem | null>(null)
  const [copiedId, setCopiedId] = useState<string | null>(null)

  const handleCopyUrl = async (item: MediaFileListItem) => {
    try {
      await navigator.clipboard.writeText(new URL(item.defaultUrl, window.location.origin).href)
      setCopiedId(item.id)
      toast.success(t('media.urlCopied', 'URL copied to clipboard'))
      setTimeout(() => setCopiedId(null), 2000)
    } catch {
      // Clipboard not available
    }
  }

  return (
    <>
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
        {items.map((item) => (
          <div
            key={item.id}
            className={`group relative rounded-xl border transition-all duration-300 hover:shadow-lg cursor-pointer ${
              selectedIds.has(item.id) ? 'border-primary bg-primary/5 ring-1 ring-primary/20' : 'border-border/50 hover:border-primary/30'
            }`}
          >
            {/* Checkbox overlay */}
            <div
              className="absolute top-2 left-2 z-10"
              onClick={(e) => e.stopPropagation()}
            >
              <Checkbox
                checked={selectedIds.has(item.id)}
                onCheckedChange={() => onToggleSelect(item.id)}
                aria-label={t('labels.selectItem', { name: item.originalFileName, defaultValue: `Select ${item.originalFileName}` })}
                className="cursor-pointer bg-background/80 backdrop-blur-sm"
              />
            </div>

            {/* Folder badge overlay */}
            {item.folder && (
              <div className="absolute top-2 right-2 z-10">
                <Badge variant="outline" className="bg-background/80 backdrop-blur-sm text-xs px-1.5 py-0.5 max-w-[120px]">
                  <FolderOpen className="h-3 w-3 mr-1 shrink-0" />
                  <span className="truncate">{extractFolderName(item.folder)}</span>
                </Badge>
              </div>
            )}

            {/* Copy URL button overlay */}
            <div
              className="absolute bottom-[72px] right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity"
              onClick={(e) => e.stopPropagation()}
            >
              <button
                type="button"
                onClick={() => handleCopyUrl(item)}
                className="p-2 rounded-lg bg-background/80 backdrop-blur-sm hover:bg-background border border-border/50 shadow-sm transition-colors"
                aria-label={t('media.copyUrl', 'Copy URL')}
              >
                {copiedId === item.id ? (
                  <Check className="h-4 w-4 text-green-500" />
                ) : (
                  <Copy className="h-4 w-4 text-muted-foreground" />
                )}
              </button>
            </div>

            {/* Thumbnail - click to preview */}
            <div
              className="aspect-square overflow-hidden rounded-t-xl"
              onClick={(e) => {
                e.stopPropagation()
                setPreviewFile(item)
              }}
            >
              <ThumbHashImage
                src={item.defaultUrl}
                thumbHash={item.thumbHash}
                dominantColor={item.dominantColor}
                alt={item.altText || item.originalFileName}
                className="w-full h-full transition-transform duration-300 group-hover:scale-105"
              />
            </div>

            {/* File info - click to open detail */}
            <div
              className="p-2.5 space-y-1"
              onClick={() => onOpenDetail(item)}
            >
              <p className="text-sm font-medium truncate group-hover:text-primary transition-colors">
                {item.originalFileName}
              </p>
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span>{item.format.toUpperCase()}</span>
                <span>{formatFileSize(item.sizeBytes)}</span>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Preview modal */}
      {previewFile && (
        <FilePreviewModal
          open={!!previewFile}
          onOpenChange={(open) => { if (!open) setPreviewFile(null) }}
          files={[{ url: previewFile.defaultUrl, name: previewFile.originalFileName }]}
        />
      )}
    </>
  )
}
