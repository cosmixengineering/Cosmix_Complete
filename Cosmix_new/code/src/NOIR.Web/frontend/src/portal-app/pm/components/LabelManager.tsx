import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Plus, Trash2, Loader2, Tag } from 'lucide-react'
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
  EmptyState,
  Input,
  Skeleton,
} from '@uikit'
import { useProjectLabelsQuery, useCreateLabel, useDeleteLabel } from '@/portal-app/pm/queries'

interface LabelManagerProps {
  projectId: string
}

export const LabelManager = ({ projectId }: LabelManagerProps) => {
  const { t } = useTranslation('common')
  const { data: labels, isLoading } = useProjectLabelsQuery(projectId)
  const createLabelMutation = useCreateLabel()
  const deleteLabelMutation = useDeleteLabel()

  const [addDialogOpen, setAddDialogOpen] = useState(false)
  const [newName, setNewName] = useState('')
  const [newColor, setNewColor] = useState('#6366f1')
  const [deleteLabelId, setDeleteLabelId] = useState<string | null>(null)

  const handleCreate = () => {
    if (!newName.trim()) return
    createLabelMutation.mutate(
      { projectId, request: { name: newName, color: newColor } },
      {
        onSuccess: () => {
          setAddDialogOpen(false)
          setNewName('')
          setNewColor('#6366f1')
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : t('errors.unknown'))
        },
      },
    )
  }

  const handleDelete = () => {
    if (!deleteLabelId) return
    deleteLabelMutation.mutate(
      { projectId, labelId: deleteLabelId },
      {
        onSuccess: () => {
          setDeleteLabelId(null)
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : t('errors.unknown'))
        },
      },
    )
  }

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-6 w-32" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">{t('pm.labels')}</h3>
        <Button
          variant="outline"
          size="sm"
          className="cursor-pointer"
          onClick={() => setAddDialogOpen(true)}
        >
          <Plus className="h-4 w-4 mr-2" />
          {t('pm.addLabel')}
        </Button>
      </div>

      {!labels || labels.length === 0 ? (
        <EmptyState
          icon={Tag}
          title={t('pm.noLabels')}
          description={t('pm.addLabel')}
          size="sm"
        />
      ) : (
        <div className="space-y-2">
          {labels.map((label) => (
            <div key={label.id} className="flex items-center justify-between p-3 rounded-lg border">
              <div className="flex items-center gap-3">
                <span
                  className="h-3 w-3 rounded-full flex-shrink-0"
                  style={{ backgroundColor: label.color }}
                />
                <span className="text-sm font-medium">{label.name}</span>
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-muted-foreground hover:text-red-500 cursor-pointer"
                onClick={() => setDeleteLabelId(label.id)}
                aria-label={`${t('pm.deleteLabel')} ${label.name}`}
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>
      )}

      {/* Add label dialog */}
      <Credenza open={addDialogOpen} onOpenChange={setAddDialogOpen}>
        <CredenzaContent>
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.addLabel')}</CredenzaTitle>
            <CredenzaDescription>{t('pm.createLabel')}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('pm.labelName')}</label>
              <Input
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder={t('pm.labelName')}
                className="mt-1"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('pm.labelColor')}</label>
              <div className="mt-1">
                <CompactColorPicker
                  value={newColor}
                  onChange={setNewColor}
                />
              </div>
            </div>
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" onClick={() => setAddDialogOpen(false)} className="cursor-pointer">
              {t('buttons.cancel')}
            </Button>
            <Button onClick={handleCreate} disabled={createLabelMutation.isPending} className="cursor-pointer">
              {createLabelMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t('pm.addLabel')}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>

      {/* Delete label confirmation */}
      <Credenza open={!!deleteLabelId} onOpenChange={(open) => !open && setDeleteLabelId(null)}>
        <CredenzaContent className="border-destructive/30">
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.deleteLabel')}</CredenzaTitle>
            <CredenzaDescription>{t('pm.deleteLabelConfirm')}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaFooter>
            <Button variant="outline" onClick={() => setDeleteLabelId(null)} className="cursor-pointer">
              {t('buttons.cancel')}
            </Button>
            <Button
              variant="destructive"
              className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
              onClick={handleDelete}
              disabled={deleteLabelMutation.isPending}
            >
              {deleteLabelMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t('pm.deleteLabel')}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>
    </div>
  )
}
