import { Search, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Input } from '@uikit'

interface TaskSearchInputProps {
  value: string
  onChange: (value: string) => void
  placeholder?: string
  className?: string
}

export const TaskSearchInput = ({ value, onChange, placeholder, className }: TaskSearchInputProps) => {
  const { t } = useTranslation('common')

  return (
    <div className={`relative min-w-[220px] ${className ?? ''}`}>
      <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
      <Input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? t('pm.searchTasks', { defaultValue: 'Search tasks...' })}
        className="pl-9 pr-8 h-9 rounded-full text-sm"
      />
      {value && (
        <button
          className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground cursor-pointer"
          onClick={() => onChange('')}
          aria-label={t('buttons.clear', { defaultValue: 'Clear' })}
        >
          <X className="h-3.5 w-3.5" />
        </button>
      )}
    </div>
  )
}
