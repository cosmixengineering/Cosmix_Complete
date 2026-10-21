import { Badge } from '@uikit/badge'
import { Button } from '@uikit/button'
import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'

export interface BulkActionToolbarProps {
  selectedCount: number
  onClearSelection: () => void
  children: React.ReactNode
}

export const BulkActionToolbar = ({ selectedCount, onClearSelection, children }: BulkActionToolbarProps) => {
  const { t } = useTranslation('common')

  if (selectedCount === 0) return null

  return (
    <div className="mb-4 p-4 rounded-lg bg-primary/5 border border-primary/20 animate-in fade-in-0 slide-in-from-top-2 duration-200">
      <div className="flex items-center flex-wrap gap-3">
        <Badge variant="secondary" className="text-sm py-1 px-3">
          {selectedCount} {t('labels.selected', 'selected')}
        </Badge>
        {children}
        <div className="flex-1" />
        <Button
          variant="ghost"
          size="sm"
          onClick={onClearSelection}
          className="cursor-pointer"
        >
          <X className="h-4 w-4 mr-2" />
          {t('buttons.clearSelection', 'Clear Selection')}
        </Button>
      </div>
    </div>
  )
}
