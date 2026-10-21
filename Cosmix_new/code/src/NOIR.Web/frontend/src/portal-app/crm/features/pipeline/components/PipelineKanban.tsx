import { useCallback, useState, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Kanban, Search, Loader2, Trophy, XCircle } from 'lucide-react'
import {
  KanbanBoard, EmptyState, Input, Button,
  Credenza, CredenzaContent, CredenzaHeader, CredenzaTitle, CredenzaDescription, CredenzaBody, CredenzaFooter,
  Textarea,
  type KanbanColumnDef, type KanbanMoveCardParams, type KanbanTerminateCardParams, type CardRenderContext,
} from '@uikit'
import type { PipelineViewDto, LeadCardDto, StageWithLeadsDto } from '@/types/crm'
import { useMoveLeadStage, useWinLead, useLoseLead } from '@/portal-app/crm/queries'
import { LeadCard } from './LeadCard'
import { StageColumnHeader } from './StageColumnHeader'
import { LeadDetailModal } from './LeadDetailModal'

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Map a pipeline stage to a KanbanColumnDef for the generic board. */
const stageToColumn = (stage: StageWithLeadsDto): KanbanColumnDef<LeadCardDto> => ({
  id: stage.id,
  cards: stage.leads,
  isSystem: stage.isSystem,
  systemType: stage.isSystem ? stage.stageType.toLowerCase() : undefined,
})

// ─── PipelineKanban ───────────────────────────────────────────────────────────

interface PipelineKanbanProps {
  pipelineView: PipelineViewDto | undefined
  isLoading: boolean
}

export const PipelineKanban = ({ pipelineView, isLoading }: PipelineKanbanProps) => {
  const { t } = useTranslation('common')
  const moveLeadStageMutation = useMoveLeadStage()
  const winLeadMutation = useWinLead()
  const loseLeadMutation = useLoseLead()

  const columns = useMemo(() => pipelineView?.stages.map(stageToColumn) ?? [], [pipelineView?.stages])

  // ── Search ──────────────────────────────────────────────────────────────────
  const [searchInput, setSearchInput] = useState('')

  const filterCards = useCallback((cards: LeadCardDto[]): LeadCardDto[] => {
    if (!searchInput) return cards
    const q = searchInput.toLowerCase()
    return cards.filter(lead =>
      lead.title.toLowerCase().includes(q) ||
      lead.contactName.toLowerCase().includes(q) ||
      (lead.ownerName?.toLowerCase().includes(q) ?? false),
    )
  }, [searchInput])

  // ── Lead detail modal (replaces page navigation) ───────────────────────────
  const [selectedLeadId, setSelectedLeadId] = useState<string | null>(null)
  const leadModalOpen = selectedLeadId !== null

  const handleLeadClick = useCallback((lead: LeadCardDto) => {
    setSelectedLeadId(lead.id)
  }, [])

  // ── System column terminate with confirmation ─────────────────────────────
  const [pendingTerminate, setPendingTerminate] = useState<KanbanTerminateCardParams | null>(null)
  const [lostReason, setLostReason] = useState('')

  const handleTerminateCard = useCallback((params: KanbanTerminateCardParams) => {
    // Show confirm dialog instead of executing immediately
    setPendingTerminate(params)
    setLostReason('')
  }, [])

  const confirmTerminate = useCallback(() => {
    if (!pendingTerminate) return
    const { cardId, systemType } = pendingTerminate
    if (systemType === 'won') {
      winLeadMutation.mutate(cardId, {
        onSuccess: () => { setPendingTerminate(null) },
        onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
      })
    } else if (systemType === 'lost') {
      loseLeadMutation.mutate(
        { id: cardId, reason: lostReason || undefined },
        {
          onSuccess: () => { setPendingTerminate(null); setLostReason('') },
          onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
        },
      )
    }
  }, [pendingTerminate, lostReason, winLeadMutation, loseLeadMutation, t])

  const pendingLeadTitle = useMemo(() => {
    if (!pendingTerminate || !pipelineView) return ''
    for (const stage of pipelineView.stages) {
      const lead = stage.leads.find(l => l.id === pendingTerminate.cardId)
      if (lead) return lead.title
    }
    return ''
  }, [pendingTerminate, pipelineView])

  // ── Card move handler ─────────────────────────────────────────────────────
  const handleMoveCard = useCallback(({ cardId, toColumnId, prevCardId, nextCardId }: KanbanMoveCardParams) => {
    if (!pipelineView) return

    const targetStage = pipelineView.stages.find(s => s.id === toColumnId)
    if (!targetStage) return

    const stageLeads = targetStage.leads.filter(l => l.id !== cardId)
    const prevLead = prevCardId ? stageLeads.find(l => l.id === prevCardId) : null
    const nextLead = nextCardId ? stageLeads.find(l => l.id === nextCardId) : null

    let newSortOrder: number
    if (!prevLead && !nextLead) {
      newSortOrder = 1
    } else if (!prevLead) {
      newSortOrder = nextLead!.sortOrder > 0 ? nextLead!.sortOrder / 2 : nextLead!.sortOrder - 1
    } else if (!nextLead) {
      newSortOrder = prevLead.sortOrder + 1
    } else {
      const prev = prevLead.sortOrder
      const next = nextLead.sortOrder
      newSortOrder = prev < next ? (prev + next) / 2 : prev + 1
    }

    moveLeadStageMutation.mutate(
      { leadId: cardId, newStageId: toColumnId, newSortOrder },
      { onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')) },
    )
  }, [pipelineView, moveLeadStageMutation, t])

  // ── Render: card ──────────────────────────────────────────────────────────
  const renderCard = useCallback((lead: LeadCardDto, _context: CardRenderContext) => (
    <LeadCard lead={lead} onClick={handleLeadClick} />
  ), [handleLeadClick])

  // ── Render: column header ─────────────────────────────────────────────────
  const renderColumnHeader = useCallback((column: KanbanColumnDef<LeadCardDto>, context: { dragHandleProps: Record<string, unknown>; cardCount: number }) => {
    const stage = pipelineView?.stages.find(s => s.id === column.id)
    if (!stage) return null
    return (
      <StageColumnHeader
        name={stage.name}
        color={stage.color}
        totalValue={stage.totalValue}
        leadCount={context.cardCount}
      />
    )
  }, [pipelineView])

  // ── Render: empty column ──────────────────────────────────────────────────
  const renderColumnEmpty = useCallback((_column: KanbanColumnDef<LeadCardDto>, isCardOver: boolean) => (
    <div className={`flex flex-col items-center justify-center py-8 text-center border-2 border-dashed rounded-lg transition-all duration-150 ${
      isCardOver ? 'border-primary/60 bg-primary/5' : 'border-border/50 dark:border-border/60'
    }`}>
      <p className="text-xs text-muted-foreground">
        {t('crm.pipeline.dropHere', { defaultValue: 'Drop deals here' })}
      </p>
    </div>
  ), [t])

  return (
    <div className="space-y-3">
      {/* ── Toolbar ── */}
      <div className="flex flex-wrap items-center gap-2">
        {/* Search */}
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder={t('crm.pipeline.searchDeals', { defaultValue: 'Search deals...' })}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-9 h-9"
          />
        </div>
      </div>

      {/* ── Kanban board (UIKit) ── */}
      <KanbanBoard<LeadCardDto>
        columns={columns}
        getCardId={(lead) => lead.id}
        renderCard={renderCard}
        renderColumnHeader={renderColumnHeader}
        onMoveCard={handleMoveCard}
        onTerminateCard={handleTerminateCard}
        enableColumnReorder={false}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            icon={Kanban}
            title={t('crm.pipeline.noPipelines')}
            description={t('crm.pipeline.noPipelinesDescription')}
          />
        }
        renderColumnEmpty={renderColumnEmpty}
        filterCards={searchInput ? filterCards : undefined}
        renderDragOverlay={(activeItem) => {
          if (activeItem.type === 'card') {
            return (
              <div className="w-[280px] rotate-2 opacity-95 rounded-lg shadow-2xl ring-1 ring-black/5">
                <LeadCard lead={activeItem.card} onClick={() => {}} />
              </div>
            )
          }
          return null
        }}
        columnMinWidth={280}
        columnMaxWidth={320}
        columnClassName="bg-muted/40 dark:bg-muted/50 border-border/50 dark:border-border/80"
        fullHeight
      />

      {/* ── Lead detail modal ── */}
      <LeadDetailModal
        leadId={selectedLeadId}
        open={leadModalOpen}
        onOpenChange={(open) => { if (!open) setSelectedLeadId(null) }}
      />

      {/* ── Win confirmation dialog ── */}
      <Credenza open={pendingTerminate?.systemType === 'won'} onOpenChange={(open) => { if (!open) setPendingTerminate(null) }}>
        <CredenzaContent>
          <CredenzaHeader>
            <CredenzaTitle>{t('crm.leads.confirmWin', { defaultValue: 'Mark this deal as won?' })}</CredenzaTitle>
            <CredenzaDescription>
              {pendingLeadTitle && <span className="font-medium">{pendingLeadTitle}</span>}
              {' '}{t('crm.leads.confirmWinDescription', { defaultValue: 'This deal will be moved to the Won stage.' })}
            </CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody />
          <CredenzaFooter>
            <Button variant="outline" onClick={() => setPendingTerminate(null)} disabled={winLeadMutation.isPending} className="cursor-pointer">
              {t('labels.cancel', { defaultValue: 'Cancel' })}
            </Button>
            <Button onClick={confirmTerminate} disabled={winLeadMutation.isPending} className="cursor-pointer bg-green-600 hover:bg-green-700 text-white">
              {winLeadMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              <Trophy className="h-4 w-4 mr-1.5" />
              {t('crm.leads.win', { defaultValue: 'Won' })}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>

      {/* ── Lose confirmation dialog (with reason) ── */}
      <Credenza open={pendingTerminate?.systemType === 'lost'} onOpenChange={(open) => { if (!open) { setPendingTerminate(null); setLostReason('') } }}>
        <CredenzaContent className="border-destructive/30">
          <CredenzaHeader>
            <CredenzaTitle>{t('crm.leads.confirmLose', { defaultValue: 'Mark this deal as lost?' })}</CredenzaTitle>
            <CredenzaDescription>
              {pendingLeadTitle && <span className="font-medium">{pendingLeadTitle}</span>}
              {' '}{t('crm.leads.confirmLoseDescription', { defaultValue: 'This deal will be moved to the Lost stage.' })}
            </CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody className="space-y-4">
            <Textarea
              value={lostReason}
              onChange={(e) => setLostReason(e.target.value)}
              placeholder={t('crm.leads.lostReasonPlaceholder', { defaultValue: 'Reason for losing (optional)...' })}
              rows={3}
            />
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" onClick={() => { setPendingTerminate(null); setLostReason('') }} disabled={loseLeadMutation.isPending} className="cursor-pointer">
              {t('labels.cancel', { defaultValue: 'Cancel' })}
            </Button>
            <Button
              variant="destructive"
              onClick={confirmTerminate}
              disabled={loseLeadMutation.isPending}
              className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
            >
              {loseLeadMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              <XCircle className="h-4 w-4 mr-1.5" />
              {t('crm.leads.lose', { defaultValue: 'Lost' })}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>
    </div>
  )
}
