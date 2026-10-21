import { useState, useCallback, useRef, useEffect, useMemo, type ReactNode } from 'react'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  KeyboardSensor,
  useSensor,
  useSensors,
  closestCorners,
  pointerWithin,
  rectIntersection,
  useDroppable,
  type DragEndEvent,
  type DragStartEvent,
  type DragOverEvent,
  type CollisionDetection,
} from '@dnd-kit/core'
import {
  SortableContext,
  verticalListSortingStrategy,
  horizontalListSortingStrategy,
  sortableKeyboardCoordinates,
  useSortable,
  arrayMove,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Kanban } from 'lucide-react'
import { Skeleton } from '../skeleton'
import { EmptyState } from '../empty-state'
import type { KanbanBoardProps, KanbanColumnDef, CardRenderContext, ColumnRenderContext } from './types'

// ─── SortableCard ─────────────────────────────────────────────────────────────

const SortableCard = ({
  id,
  columnId,
  disabled,
  children,
}: {
  id: string
  columnId: string
  disabled: boolean
  children: () => ReactNode
}) => {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    disabled,
    data: { type: 'card', columnId },
  })

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  }

  if (isDragging) {
    return (
      <div ref={setNodeRef} style={style} {...attributes} className="relative">
        {/* Invisible card to preserve exact height */}
        <div className="invisible">{children()}</div>
        {/* Dashed placeholder overlay */}
        <div className="absolute inset-0 rounded-lg border-2 border-dashed border-primary/40 bg-primary/5" />
      </div>
    )
  }

  return (
    <div ref={setNodeRef} style={style} {...attributes} {...(disabled ? {} : listeners)}>
      {children()}
    </div>
  )
}

// ─── SortableColumn ───────────────────────────────────────────────────────────

const SortableColumn = ({
  id,
  width,
  minWidth,
  maxWidth,
  fullHeight,
  children,
}: {
  id: string
  width?: number
  minWidth?: number
  maxWidth?: number
  fullHeight?: boolean
  children: (opts: { dragHandleProps: Record<string, unknown>; isDragging: boolean }) => ReactNode
}) => {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    data: { type: 'column' },
  })

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition: isDragging ? undefined : transition,
    flexShrink: 0,
    ...(width != null ? { width, minWidth: width } : {}),
    ...(minWidth != null && width == null ? { minWidth } : {}),
    ...(maxWidth != null ? { maxWidth } : {}),
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`flex flex-col ${fullHeight ? 'h-full' : ''} ${isDragging ? 'opacity-40' : ''}`}
    >
      {children({ dragHandleProps: { ...listeners, ...attributes }, isDragging })}
    </div>
  )
}

// ─── DroppableColumnBody ──────────────────────────────────────────────────────

const DroppableColumnBody = ({
  id,
  isOver,
  fullHeight,
  children,
}: {
  id: string
  isOver: boolean
  fullHeight?: boolean
  children: ReactNode
}) => {
  const { setNodeRef } = useDroppable({ id })
  return (
    <div
      ref={setNodeRef}
      className={`flex-1 space-y-2 p-2 transition-all duration-150 ${
        fullHeight ? 'min-h-0 overflow-y-auto' : 'min-h-[100px]'
      } ${isOver ? 'bg-primary/5 ring-1 ring-inset ring-primary/25 rounded-b-lg' : ''}`}
      style={fullHeight ? { scrollbarWidth: 'thin', scrollbarColor: 'color-mix(in oklch, var(--border) 80%, transparent) transparent' } as React.CSSProperties : undefined}
    >
      {children}
    </div>
  )
}

// ─── KanbanBoard ──────────────────────────────────────────────────────────────

export const KanbanBoard = <TCard,>({
  columns,
  getCardId,
  renderCard,
  renderColumnHeader,
  onMoveCard,
  onTerminateCard,
  onReorderColumns,
  isLoading,
  emptyState,
  columnWidth = 280,
  // Extension slots
  renderColumnFooter,
  renderColumnEmpty,
  renderCollapsedColumn,
  renderBoardFooter,
  renderDragOverlay,
  // Behavioral extensions
  collapsedColumnIds: externalCollapsedIds,
  onToggleCollapse,
  customDragTypes,
  onCustomDrop,
  filterCards,
  sortCards,
  boardMode = 'drag',
  selectedCardIds,
  onLassoSelect,
  isCardDraggable,
  enableColumnReorder = true,
  // Styling
  boardClassName,
  columnClassName,
  columnMinWidth,
  columnMaxWidth,
  fullHeight,
}: KanbanBoardProps<TCard>) => {
  // ── Local optimistic state ─────────────────────────────────────────────────
  const [localColumns, setLocalColumns] = useState<KanbanColumnDef<TCard>[]>(columns)
  const localColumnsRef = useRef<KanbanColumnDef<TCard>[]>(localColumns)
  const isDraggingRef = useRef(false)

  useEffect(() => { localColumnsRef.current = localColumns }, [localColumns])

  // Sync when new server data arrives — always apply, then unlock dragging.
  // This ensures optimistic state persists until the API response arrives.
  useEffect(() => {
    setLocalColumns(columns)
    isDraggingRef.current = false
  }, [columns])

  // ── Collapsed columns (internal state if not controlled) ──────────────────
  const [internalCollapsedIds, setInternalCollapsedIds] = useState<Set<string>>(new Set())
  const collapsedColumnIds = externalCollapsedIds ?? internalCollapsedIds

  const handleToggleCollapse = useCallback((columnId: string) => {
    if (onToggleCollapse) {
      onToggleCollapse(columnId)
    } else {
      setInternalCollapsedIds(prev => {
        const next = new Set(prev)
        if (next.has(columnId)) next.delete(columnId)
        else next.add(columnId)
        return next
      })
    }
  }, [onToggleCollapse])

  // ── Drag state ─────────────────────────────────────────────────────────────
  const [activeId, setActiveId] = useState<string | null>(null)
  const [dragType, setDragType] = useState<'card' | 'column' | 'custom' | null>(null)
  const [overColumnId, setOverColumnId] = useState<string | null>(null)
  const [overCardId, setOverCardId] = useState<string | null>(null)

  const activeCard = activeId && dragType === 'card'
    ? localColumns.find(col => col.cards.some(c => getCardId(c) === activeId))?.cards.find(c => getCardId(c) === activeId) ?? null
    : null

  const activeColumn = activeId && dragType === 'column'
    ? localColumns.find(c => c.id === activeId) ?? null
    : null

  // ── Filtered/sorted view (display only — DnD operates on localColumns) ────
  const displayColumns = useMemo(() => {
    if (!filterCards && !sortCards) return localColumns
    return localColumns.map(col => {
      let cards = col.cards
      if (filterCards) cards = filterCards(cards, col.id)
      if (sortCards) cards = sortCards(cards, col.id)
      return { ...col, cards }
    })
  }, [localColumns, filterCards, sortCards])

  // ── Sensors ────────────────────────────────────────────────────────────────
  // In 'drag' mode, DnD is active (distance: 8). Board panning coexists — the pan handler
  // skips interactive elements and drag handles, so DnD and pan don't conflict.
  // In 'select' mode, DnD is disabled (huge distance) so lasso can work.
  // In 'pan' mode, DnD is also disabled — pure pan only.
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: boardMode === 'drag' ? 8 : 999999 },
    }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  // Non-system column IDs for horizontal sortable context
  const activeColumnIds = localColumns.filter(c => !c.isSystem).map(c => c.id)

  // Custom drag type prefix lookup
  const getCustomDragType = useCallback((id: string) => {
    return customDragTypes?.find(dt => id.startsWith(dt.prefix))
  }, [customDragTypes])

  // ── Collision detection ────────────────────────────────────────────────────
  const collisionDetection: CollisionDetection = useCallback((args) => {
    const dragId = String(args.active.id)
    const cols = localColumnsRef.current
    const colIdSet = new Set(cols.map(c => c.id))
    const isActiveColumnDrag = colIdSet.has(dragId) && !cols.find(c => c.id === dragId)?.isSystem

    if (isActiveColumnDrag) {
      return closestCorners({
        ...args,
        droppableContainers: args.droppableContainers.filter(c => {
          const id = String(c.id)
          return colIdSet.has(id) && !cols.find(col => col.id === id)?.isSystem
        }),
      })
    }

    // Custom drag type: only collide with cards
    if (getCustomDragType(dragId)) {
      return closestCorners({
        ...args,
        droppableContainers: args.droppableContainers.filter(c => !colIdSet.has(String(c.id))),
      })
    }

    // Card drag: pointer-within for precise targeting, fallback to rect intersection
    const pointerHits = pointerWithin(args)
    if (pointerHits.length > 0) {
      const cardHit = pointerHits.find(({ id }) => !colIdSet.has(String(id)))
      return cardHit ? [cardHit] : [pointerHits[0]]
    }
    return rectIntersection(args)
  }, [getCustomDragType])

  // ── handleDragStart ────────────────────────────────────────────────────────
  const handleDragStart = useCallback((event: DragStartEvent) => {
    const id = String(event.active.id)
    setActiveId(id)
    isDraggingRef.current = true
    if (getCustomDragType(id)) {
      setDragType('custom')
    } else {
      setDragType(localColumnsRef.current.some(c => c.id === id) ? 'column' : 'card')
    }
  }, [getCustomDragType])

  // ── handleDragOver: optimistic reorder ────────────────────────────────────
  const handleDragOver = useCallback((event: DragOverEvent) => {
    const { active, over } = event
    if (!over) { setOverColumnId(null); setOverCardId(null); return }

    const activeId = String(active.id)
    const overId = String(over.id)
    if (activeId === overId) return

    // Custom drag: track which card we're over for visual highlight
    if (getCustomDragType(activeId)) {
      const cols = localColumnsRef.current
      const isCard = cols.some(col => col.cards.some(c => getCardId(c) === overId))
      setOverCardId(isCard ? overId : null)
      return
    }

    setLocalColumns(prev => {
      const isColDrag = enableColumnReorder && prev.some(c => c.id === activeId && !c.isSystem)

      if (isColDrag) {
        const fromIdx = prev.findIndex(c => c.id === activeId)
        const toIdx = prev.findIndex(c => c.id === overId && !c.isSystem)
        if (fromIdx === -1 || toIdx === -1 || fromIdx === toIdx) return prev
        return arrayMove(prev, fromIdx, toIdx)
      }

      // Card drag — find source and target columns
      const srcColIdx = prev.findIndex(col => col.cards.some(c => getCardId(c) === activeId))
      if (srcColIdx === -1) return prev

      let tgtColIdx = prev.findIndex(c => c.id === overId)
      if (tgtColIdx === -1) {
        tgtColIdx = prev.findIndex(col => col.cards.some(c => getCardId(c) === overId))
      }
      if (tgtColIdx === -1) return prev

      setOverColumnId(prev[tgtColIdx].id)

      const cols = prev.map(c => ({ ...c, cards: [...c.cards] }))
      const srcCards = cols[srcColIdx].cards
      const tgtCards = cols[tgtColIdx].cards
      const activeCardIdx = srcCards.findIndex(c => getCardId(c) === activeId)
      if (activeCardIdx === -1) return prev

      if (srcColIdx === tgtColIdx) {
        // Same-column reorder
        const overCardIdx = srcCards.findIndex(c => getCardId(c) === overId)
        if (overCardIdx === -1) return prev
        cols[srcColIdx].cards = arrayMove(srcCards, activeCardIdx, overCardIdx)
      } else {
        // Cross-column move (optimistic — shows drop position placeholder)
        const [moved] = srcCards.splice(activeCardIdx, 1)
        const overCardIdx = tgtCards.findIndex(c => getCardId(c) === overId)
        if (overCardIdx === -1) {
          tgtCards.push(moved)
        } else {
          tgtCards.splice(overCardIdx, 0, moved)
        }
      }
      return cols
    })
  }, [getCardId, getCustomDragType])

  // ── handleDragEnd: commit ──────────────────────────────────────────────────
  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event
    // Don't set isDraggingRef.current = false here for card/column moves.
    // The sync effect will set it false when new server data arrives.
    // This prevents the optimistic state from reverting before the API responds.
    setActiveId(null)
    setOverColumnId(null)
    setOverCardId(null)
    const currentDragType = dragType
    setDragType(null)

    if (!over) {
      isDraggingRef.current = false
      setLocalColumns(columns)
      return
    }

    const activeId = String(active.id)
    const overId = String(over.id)

    // Custom drag drop
    if (currentDragType === 'custom') {
      const cols = localColumnsRef.current
      const targetCard = cols.flatMap(col => col.cards.map(c => ({ card: c, colId: col.id }))).find(x => getCardId(x.card) === overId)
      if (targetCard && onCustomDrop) {
        onCustomDrop({
          dragId: activeId,
          targetCardId: overId,
          targetColumnId: targetCard.colId,
        })
      }
      return
    }

    if (currentDragType === 'column') {
      const latestCols = localColumnsRef.current
      const newNonSystemIds = latestCols.filter(c => !c.isSystem).map(c => c.id)
      const originalNonSystemIds = columns.filter(c => !c.isSystem).map(c => c.id).join(',')
      if (newNonSystemIds.join(',') === originalNonSystemIds) return
      onReorderColumns?.(newNonSystemIds)
      return
    }

    if (currentDragType === 'card') {
      // Find source column from ORIGINAL server data
      const originalSrcCol = columns.find(col => col.cards.some(c => getCardId(c) === activeId))
      if (!originalSrcCol) return

      const latestCols = localColumnsRef.current

      // Determine target column from the drop target (over.id)
      let targetCol: KanbanColumnDef<TCard> | undefined
      targetCol = latestCols.find(c => c.id === overId)
      if (!targetCol) {
        targetCol = latestCols.find(col => col.cards.some(c => getCardId(c) === overId))
      }
      if (!targetCol) return

      // System column: terminate the card (with position info from optimistic state)
      if (targetCol.isSystem && targetCol.systemType) {
        const sysCards = targetCol.cards
        const sysIdx = sysCards.findIndex(c => getCardId(c) === activeId)
        const sysPrev = sysIdx > 0 ? sysCards[sysIdx - 1] : null
        const sysNext = sysIdx < sysCards.length - 1 ? sysCards[sysIdx + 1] : null
        onTerminateCard?.({
          cardId: activeId,
          fromColumnId: originalSrcCol.id,
          systemType: targetCol.systemType,
          prevCardId: sysPrev ? getCardId(sysPrev) : null,
          nextCardId: sysNext ? getCardId(sysNext) : null,
        })
        isDraggingRef.current = false
        setLocalColumns(columns) // revert for system columns — confirm dialog will handle the actual move
        return
      }

      // Normal column: find neighbors from local (optimistic) position
      const tgtColCards = targetCol.cards
      const cardIdx = tgtColCards.findIndex(c => getCardId(c) === activeId)

      // No-op if position unchanged
      const origIdx = originalSrcCol.cards.findIndex(c => getCardId(c) === activeId)
      if (originalSrcCol.id === targetCol.id && origIdx === cardIdx) return

      const prevCard = cardIdx > 0 ? tgtColCards[cardIdx - 1] : null
      const nextCard = cardIdx < tgtColCards.length - 1 ? tgtColCards[cardIdx + 1] : null

      onMoveCard({
        cardId: activeId,
        fromColumnId: originalSrcCol.id,
        toColumnId: targetCol.id,
        prevCardId: prevCard ? getCardId(prevCard) : null,
        nextCardId: nextCard ? getCardId(nextCard) : null,
      })
    }
  }, [dragType, columns, getCardId, onMoveCard, onTerminateCard, onReorderColumns, onCustomDrop])

  // ── Board pan / lasso selection ────────────────────────────────────────────
  // Pan is available in BOTH 'drag' and 'pan' modes (DnD and pan coexist in drag mode —
  // the guards skip interactive elements / drag handles so they don't conflict).
  // Lasso is only available in 'select' mode.
  const boardScrollRef = useRef<HTMLDivElement>(null)
  const panRef = useRef<{ pointerId: number; startX: number; scrollLeft: number } | null>(null)
  const [lassoRect, setLassoRect] = useState<{ startX: number; startY: number; endX: number; endY: number } | null>(null)
  const lassoRef = useRef<{ pointerId: number; startX: number; startY: number } | null>(null)

  const getIntersectingCardIds = useCallback((screenRect: { left: number; top: number; right: number; bottom: number }): string[] => {
    const cards = boardScrollRef.current?.querySelectorAll('[data-kanban-card-id]')
    if (!cards) return []
    const ids: string[] = []
    cards.forEach(card => {
      const cr = card.getBoundingClientRect()
      if (!(cr.right < screenRect.left || cr.left > screenRect.right ||
            cr.bottom < screenRect.top || cr.top > screenRect.bottom)) {
        const id = card.getAttribute('data-kanban-card-id')
        if (id) ids.push(id)
      }
    })
    return ids
  }, [])

  const handleBoardPointerDown = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    if (e.button !== 0) return
    const target = e.target as HTMLElement
    if (!boardScrollRef.current?.contains(target)) return
    // Skip interactive elements and drag handles — prevents conflict with DnD
    if (target.closest('button, input, textarea, a, select, [role="button"], [role="option"], [role="menuitem"]')) return
    if (target.closest('.cursor-grab')) return

    if (boardMode === 'select') {
      // Lasso mode: don't start if clicking on a card
      if (target.closest('[data-kanban-card-id]')) return
      const board = boardScrollRef.current!
      const boardRect = board.getBoundingClientRect()
      const x = e.clientX - boardRect.left + board.scrollLeft
      const y = e.clientY - boardRect.top + board.scrollTop
      lassoRef.current = { pointerId: e.pointerId, startX: x, startY: y }
      setLassoRect({ startX: x, startY: y, endX: x, endY: y })
      ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
    } else {
      // 'drag' or 'pan' mode: enable board panning on empty space
      panRef.current = { pointerId: e.pointerId, startX: e.clientX, scrollLeft: boardScrollRef.current?.scrollLeft ?? 0 }
      ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
      ;(e.currentTarget as HTMLElement).style.cursor = 'grabbing'
    }
  }, [boardMode])

  const handleBoardPointerMove = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    if (panRef.current && boardScrollRef.current) {
      const dx = e.clientX - panRef.current.startX
      boardScrollRef.current.scrollLeft = panRef.current.scrollLeft - dx
      return
    }
    if (lassoRef.current && boardScrollRef.current) {
      const board = boardScrollRef.current
      const boardRect = board.getBoundingClientRect()
      const x = e.clientX - boardRect.left + board.scrollLeft
      const y = e.clientY - boardRect.top + board.scrollTop
      setLassoRect({ startX: lassoRef.current.startX, startY: lassoRef.current.startY, endX: x, endY: y })
    }
  }, [])

  const handleBoardPointerUp = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    if (panRef.current) {
      panRef.current = null
      ;(e.currentTarget as HTMLElement).style.cursor = ''
      return
    }
    if (lassoRef.current && lassoRect && boardScrollRef.current) {
      const board = boardScrollRef.current
      const boardRect = board.getBoundingClientRect()
      const screenLeft = Math.min(lassoRect.startX, lassoRect.endX) - board.scrollLeft + boardRect.left
      const screenTop = Math.min(lassoRect.startY, lassoRect.endY) - board.scrollTop + boardRect.top
      const screenRight = Math.max(lassoRect.startX, lassoRect.endX) - board.scrollLeft + boardRect.left
      const screenBottom = Math.max(lassoRect.startY, lassoRect.endY) - board.scrollTop + boardRect.top
      if (Math.abs(lassoRect.endX - lassoRect.startX) > 5 || Math.abs(lassoRect.endY - lassoRect.startY) > 5) {
        const ids = getIntersectingCardIds({ left: screenLeft, top: screenTop, right: screenRight, bottom: screenBottom })
        if (ids.length > 0) onLassoSelect?.(ids)
      }
      lassoRef.current = null
      setLassoRect(null)
    }
  }, [lassoRect, getIntersectingCardIds, onLassoSelect])

  // ── Render helpers ─────────────────────────────────────────────────────────

  const renderCardWithContext = useCallback((card: TCard, columnId: string) => {
    const cardId = getCardId(card)
    const context: CardRenderContext = {
      isDragging: activeId === cardId && dragType === 'card',
      isCustomDragOver: overCardId === cardId,
      isSelected: selectedCardIds?.has(cardId) ?? false,
      columnId,
    }
    // Support both old (card) => ReactNode and new (card, context) => ReactNode signatures
    return (renderCard as (card: TCard, context: CardRenderContext) => ReactNode)(card, context)
  }, [getCardId, activeId, dragType, overCardId, selectedCardIds, renderCard])

  const renderColumnHeaderWithContext = useCallback((column: KanbanColumnDef<TCard>, extras: { dragHandleProps: Record<string, unknown>; isDragging: boolean; isCardOver: boolean }) => {
    const originalCol = columns.find(c => c.id === column.id)
    const context: ColumnRenderContext = {
      dragHandleProps: extras.dragHandleProps,
      isDragging: extras.isDragging,
      isCardOver: extras.isCardOver,
      isCollapsed: collapsedColumnIds.has(column.id),
      cardCount: originalCol?.cards.length ?? column.cards.length,
      filteredCardCount: column.cards.length,
    }
    return (renderColumnHeader as (column: KanbanColumnDef<TCard>, context: ColumnRenderContext) => ReactNode)(column, context)
  }, [columns, collapsedColumnIds, renderColumnHeader])

  // ── Column size props ──────────────────────────────────────────────────────
  const hasFlexWidth = columnMinWidth != null || columnMaxWidth != null
  const colWidth = hasFlexWidth ? undefined : columnWidth
  const colMinWidth = columnMinWidth ?? (hasFlexWidth ? undefined : undefined)
  const colMaxWidth = columnMaxWidth

  // ── Loading state ──────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="flex gap-4 overflow-x-auto pb-4">
        {[...Array(4)].map((_, i) => (
          <div key={i} style={{ minWidth: columnWidth }} className="space-y-3">
            <Skeleton className="h-12 w-full rounded-lg" />
            <Skeleton className="h-24 w-full rounded-lg" />
            <Skeleton className="h-24 w-full rounded-lg" />
          </div>
        ))}
      </div>
    )
  }

  // ── Empty state ────────────────────────────────────────────────────────────
  if (localColumns.length === 0) {
    return (
      <div>
        {emptyState ?? (
          <EmptyState icon={Kanban} title="No columns" description="Add columns to get started." />
        )}
        {renderBoardFooter?.()}
      </div>
    )
  }

  const nonSystemColumns = displayColumns.filter(c => !c.isSystem)
  const systemColumns = displayColumns.filter(c => c.isSystem)

  const boardClasses = fullHeight
    ? `relative flex gap-4 overflow-x-auto pb-2 cursor-default scrollbar-none select-none h-[calc(100vh-9.5rem)] min-h-[400px] ${boardClassName ?? ''}`
    : `flex gap-4 overflow-x-auto pb-4 ${boardClassName ?? ''}`

  const boardStyleProps = fullHeight
    ? { scrollbarWidth: 'none' as const, msOverflowStyle: 'none' as const }
    : undefined

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
    >
      <SortableContext items={activeColumnIds} strategy={horizontalListSortingStrategy}>
        <div
          ref={boardScrollRef}
          className={boardClasses}
          style={boardStyleProps}
          onPointerDown={handleBoardPointerDown}
          onPointerMove={handleBoardPointerMove}
          onPointerUp={handleBoardPointerUp}
          onLostPointerCapture={handleBoardPointerUp}
        >
          {/* Sortable (active) columns */}
          {nonSystemColumns.map((column) => {
            const isCollapsed = collapsedColumnIds.has(column.id)
            const isCardOver = overColumnId === column.id && dragType === 'card'
            const originalCol = columns.find(c => c.id === column.id)
            const originalCardCount = originalCol?.cards.length ?? column.cards.length

            return (
              <SortableColumn
                key={column.id}
                id={column.id}
                width={isCollapsed ? undefined : colWidth}
                minWidth={isCollapsed ? undefined : colMinWidth}
                maxWidth={isCollapsed ? undefined : colMaxWidth}
                fullHeight={fullHeight}
              >
                {({ dragHandleProps, isDragging }) => {
                  // Collapsed column rendering
                  if (isCollapsed && renderCollapsedColumn) {
                    return renderCollapsedColumn(column, {
                      cardCount: originalCardCount,
                      dragHandleProps,
                      onExpand: () => handleToggleCollapse(column.id),
                    })
                  }

                  return (
                    <div
                      className={`rounded-lg border flex flex-col h-full transition-all duration-150 ${
                        isDragging ? 'shadow-lg border-primary/40' : ''
                      } ${columnClassName ?? 'bg-muted/30 border-border/50'}`}
                    >
                      <div {...(enableColumnReorder ? dragHandleProps : {})} className={`rounded-t-lg ${enableColumnReorder ? 'cursor-grab active:cursor-grabbing' : ''}`}>
                        {renderColumnHeaderWithContext(column, { dragHandleProps, isDragging, isCardOver })}
                      </div>
                      <DroppableColumnBody id={column.id} isOver={isCardOver} fullHeight={fullHeight}>
                        <SortableContext
                          items={column.cards.map(getCardId)}
                          strategy={verticalListSortingStrategy}
                        >
                          {column.cards.map((card) => {
                            const cardId = getCardId(card)
                            const draggable = isCardDraggable ? isCardDraggable(card) : true
                            return (
                              <SortableCard
                                key={cardId}
                                id={cardId}
                                columnId={column.id}
                                disabled={!draggable || boardMode !== 'drag'}
                              >
                                {() => (
                                  <div data-kanban-card-id={cardId}>
                                    {renderCardWithContext(card, column.id)}
                                  </div>
                                )}
                              </SortableCard>
                            )
                          })}
                        </SortableContext>
                        {column.cards.length === 0 && renderColumnEmpty?.(column, isCardOver)}
                        {renderColumnFooter?.(column)}
                      </DroppableColumnBody>
                    </div>
                  )
                }}
              </SortableColumn>
            )
          })}

          {/* System columns (pinned, not draggable as columns) */}
          {systemColumns.map((column) => {
            const isCardOver = overColumnId === column.id
            return (
              <div
                key={column.id}
                className={`flex flex-col flex-shrink-0 ${fullHeight ? 'h-full' : ''}`}
                style={{ width: columnWidth, minWidth: columnWidth }}
              >
                <div className={`rounded-lg border flex flex-col h-full transition-all duration-150 ${columnClassName ?? 'bg-muted/30 border-border/50'}`}>
                  <div className="rounded-t-lg">
                    {renderColumnHeaderWithContext(column, { dragHandleProps: {}, isDragging: false, isCardOver })}
                  </div>
                  <DroppableColumnBody id={column.id} isOver={isCardOver} fullHeight={fullHeight}>
                    <SortableContext
                      items={column.cards.map(getCardId)}
                      strategy={verticalListSortingStrategy}
                    >
                      {column.cards.map((card) => {
                        const cardId = getCardId(card)
                        return (
                          <SortableCard
                            key={cardId}
                            id={cardId}
                            columnId={column.id}
                            disabled={true}
                          >
                            {() => (
                              <div data-kanban-card-id={cardId}>
                                {renderCardWithContext(card, column.id)}
                              </div>
                            )}
                          </SortableCard>
                        )
                      })}
                    </SortableContext>
                    {/* Explicit ghost card for system columns too */}
                    {column.cards.length === 0 && renderColumnEmpty?.(column, isCardOver)}
                  </DroppableColumnBody>
                </div>
              </div>
            )
          })}

          {/* Board footer (e.g. "Add column" button) */}
          {renderBoardFooter?.()}

          {/* Lasso selection overlay */}
          {lassoRect && (
            <div
              className="absolute bg-primary/10 border border-primary/40 rounded-sm pointer-events-none z-50"
              style={{
                left: Math.min(lassoRect.startX, lassoRect.endX),
                top: Math.min(lassoRect.startY, lassoRect.endY),
                width: Math.abs(lassoRect.endX - lassoRect.startX),
                height: Math.abs(lassoRect.endY - lassoRect.startY),
              }}
            />
          )}
        </div>
      </SortableContext>

      <DragOverlay dropAnimation={null}>
        {renderDragOverlay ? (
          <>
            {activeCard && renderDragOverlay({ type: 'card', card: activeCard })}
            {activeColumn && renderDragOverlay({ type: 'column', column: activeColumn })}
          </>
        ) : (
          activeCard && (
            <div style={{ width: columnWidth }}>
              {renderCardWithContext(activeCard, '')}
            </div>
          )
        )}
        {/* Custom drag overlays */}
        {dragType === 'custom' && activeId && customDragTypes?.map(dt => {
          if (activeId.startsWith(dt.prefix)) {
            return <div key={dt.prefix}>{dt.renderOverlay(activeId)}</div>
          }
          return null
        })}
      </DragOverlay>
    </DndContext>
  )
}
