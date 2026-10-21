import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Plus, Trash2, Loader2, GripVertical, Settings2 } from 'lucide-react'
import {
  Button,
  Credenza,
  CredenzaContent,
  CredenzaDescription,
  CredenzaFooter,
  CredenzaHeader,
  CredenzaTitle,
  CredenzaBody,
  CompactColorPicker,
  Input,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@uikit'
import { useCreateColumn, useUpdateColumn, useDeleteColumn } from '@/portal-app/pm/queries'
import { ColumnSettingsDialog } from './ColumnSettingsDialog'
import type { ProjectColumnDto } from '@/types/pm'

interface ColumnManagerProps {
  projectId: string
  columns: ProjectColumnDto[]
}

export const ColumnManager = ({ projectId, columns }: ColumnManagerProps) => {
  const { t } = useTranslation('common')
  const createColumnMutation = useCreateColumn()
  const updateColumnMutation = useUpdateColumn()
  const deleteColumnMutation = useDeleteColumn()

  const [addDialogOpen, setAddDialogOpen] = useState(false)
  const [newName, setNewName] = useState('')
  const [newColor, setNewColor] = useState('#6366f1')
  const [newWipLimit, setNewWipLimit] = useState<number | undefined>()

  const [deleteColumnId, setDeleteColumnId] = useState<string | null>(null)
  const [moveToColumnId, setMoveToColumnId] = useState<string>('')

  const [editingColumnId, setEditingColumnId] = useState<string | null>(null)
  const [editName, setEditName] = useState('')
  const [settingsColumn, setSettingsColumn] = useState<ProjectColumnDto | null>(null)

  const handleCreate = () => {
    if (!newName.trim()) return
    createColumnMutation.mutate(
      { projectId, request: { name: newName, color: newColor, wipLimit: newWipLimit } },
      {
        onSuccess: () => {
          setAddDialogOpen(false)
          setNewName('')
          setNewColor('#6366f1')
          setNewWipLimit(undefined)
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : t('errors.unknown'))
        },
      },
    )
  }

  const handleDelete = () => {
    if (!deleteColumnId || !moveToColumnId) return
    deleteColumnMutation.mutate(
      { projectId, columnId: deleteColumnId, moveToColumnId },
      {
        onSuccess: () => {
          setDeleteColumnId(null)
          setMoveToColumnId('')
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : t('errors.unknown'))
        },
      },
    )
  }

  const handleSaveEdit = (columnId: string) => {
    if (!editName.trim()) return
    updateColumnMutation.mutate(
      { projectId, columnId, request: { name: editName } },
      {
        onSuccess: () => {
          setEditingColumnId(null)
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : t('errors.unknown'))
        },
      },
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">{t('pm.columns')}</h3>
        <Button
          variant="outline"
          size="sm"
          className="cursor-pointer"
          onClick={() => setAddDialogOpen(true)}
        >
          <Plus className="h-4 w-4 mr-2" />
          {t('pm.addColumn')}
        </Button>
      </div>

      <div className="space-y-2">
        {columns
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map((column) => (
            <div key={column.id} className="flex items-center justify-between p-3 rounded-lg border">
              <div className="flex items-center gap-3">
                <GripVertical className="h-4 w-4 text-muted-foreground" />
                {column.color && (
                  <span className="h-3 w-3 rounded-full flex-shrink-0" style={{ backgroundColor: column.color }} />
                )}
                {editingColumnId === column.id ? (
                  <Input
                    value={editName}
                    onChange={(e) => setEditName(e.target.value)}
                    onBlur={() => handleSaveEdit(column.id)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSaveEdit(column.id)}
                    className="h-7 w-40"
                    autoFocus
                  />
                ) : (
                  <span
                    className="text-sm font-medium cursor-pointer"
                    onClick={() => {
                      setEditingColumnId(column.id)
                      setEditName(column.name)
                    }}
                  >
                    {column.name}
                  </span>
                )}
                {column.wipLimit != null && (
                  <span className="text-xs text-muted-foreground">
                    {t('pm.wipLimit')}: {column.wipLimit}
                  </span>
                )}
              </div>
              <div className="flex items-center gap-1">
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-muted-foreground hover:text-foreground cursor-pointer"
                  onClick={() => setSettingsColumn(column)}
                  aria-label={`${t('pm.columnSettings')} ${column.name}`}
                >
                  <Settings2 className="h-4 w-4" />
                </Button>
                {columns.length > 1 && (
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8 text-muted-foreground hover:text-red-500 cursor-pointer"
                    onClick={() => {
                      setDeleteColumnId(column.id)
                      const firstOther = columns.find(c => c.id !== column.id)
                      setMoveToColumnId(firstOther?.id ?? '')
                    }}
                    aria-label={`${t('pm.deleteColumn')} ${column.name}`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </div>
            </div>
          ))}
      </div>

      {/* Add column dialog */}
      <Credenza open={addDialogOpen} onOpenChange={setAddDialogOpen}>
        <CredenzaContent>
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.addColumn')}</CredenzaTitle>
            <CredenzaDescription>{t('pm.addColumn')}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('labels.name')}</label>
              <Input
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder={t('pm.columns')}
                className="mt-1"
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium">{t('pm.color')}</label>
                <div className="mt-1">
                  <CompactColorPicker
                    value={newColor}
                    onChange={setNewColor}
                  />
                </div>
              </div>
              <div>
                <label className="text-sm font-medium">{t('pm.wipLimit')}</label>
                <Input
                  type="number"
                  min={0}
                  value={newWipLimit ?? ''}
                  onChange={(e) => setNewWipLimit(e.target.value ? Number(e.target.value) : undefined)}
                  placeholder={t('pm.wipLimit')}
                  className="mt-1"
                />
              </div>
            </div>
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" onClick={() => setAddDialogOpen(false)} className="cursor-pointer">{t('buttons.cancel')}</Button>
            <Button onClick={handleCreate} disabled={createColumnMutation.isPending} className="cursor-pointer">
              {createColumnMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t('pm.addColumn')}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>

      {/* Column settings dialog */}
      <ColumnSettingsDialog
        open={!!settingsColumn}
        onOpenChange={(open) => !open && setSettingsColumn(null)}
        projectId={projectId}
        column={settingsColumn}
      />

      {/* Delete column confirmation */}
      <Credenza open={!!deleteColumnId} onOpenChange={(open) => !open && setDeleteColumnId(null)}>
        <CredenzaContent className="border-destructive/30">
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.deleteColumn')}</CredenzaTitle>
            <CredenzaDescription>{t('pm.deleteColumnConfirmation')}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('pm.moveTasksTo')}</label>
              <Select value={moveToColumnId} onValueChange={setMoveToColumnId}>
                <SelectTrigger className="mt-1 cursor-pointer">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {columns
                    .filter(c => c.id !== deleteColumnId)
                    .map((col) => (
                      <SelectItem key={col.id} value={col.id} className="cursor-pointer">
                        {col.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" onClick={() => setDeleteColumnId(null)} className="cursor-pointer">{t('buttons.cancel')}</Button>
            <Button
              variant="destructive"
              className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
              onClick={handleDelete}
              disabled={deleteColumnMutation.isPending || !moveToColumnId}
            >
              {deleteColumnMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t('pm.deleteColumn')}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>
    </div>
  )
}
