import type { ReactNode } from 'react'

/** A column in the Kanban board with its cards. */
export interface KanbanColumnDef<TCard> {
  id: string
  cards: TCard[]
  /** System columns (e.g. Won/Lost in CRM) are pinned at the end and not draggable as columns. */
  isSystem?: boolean
  /** Identifier passed to `onTerminateCard` when a card is dropped into this system column. */
  systemType?: string
}

/** Params passed to onMoveCard for a normal card move. */
export interface KanbanMoveCardParams {
  cardId: string
  fromColumnId: string
  toColumnId: string
  /** Card immediately before the moved card in the new position (null if first). */
  prevCardId: string | null
  /** Card immediately after the moved card in the new position (null if last). */
  nextCardId: string | null
}

/** Params passed to onTerminateCard when a card is dropped into a system column. */
export interface KanbanTerminateCardParams {
  cardId: string
  fromColumnId: string
  /** The `systemType` value from the target system column. */
  systemType: string
  /** Card immediately before the dropped card in the system column (null if first). */
  prevCardId: string | null
  /** Card immediately after the dropped card in the system column (null if last). */
  nextCardId: string | null
}

/** Context passed to renderCard for enhanced rendering. */
export interface CardRenderContext {
  /** Whether this card is currently being dragged. */
  isDragging: boolean
  /** Whether a custom drag item (e.g. member pill) is hovering over this card. */
  isCustomDragOver: boolean
  /** Whether this card is selected (in select mode). */
  isSelected: boolean
  /** The column this card belongs to. */
  columnId: string
}

/** Context passed to renderColumnHeader for enhanced rendering. */
export interface ColumnRenderContext {
  /** Props to spread on the drag handle element. */
  dragHandleProps: Record<string, unknown>
  /** Whether this column is currently being dragged. */
  isDragging: boolean
  /** Whether a card is hovering over this column. */
  isCardOver: boolean
  /** Whether this column is collapsed. */
  isCollapsed: boolean
  /** Total card count (unfiltered). */
  cardCount: number
  /** Filtered card count (after filterCards applied). */
  filteredCardCount: number
}

/** Configuration for a custom draggable type (e.g. member pills). */
export interface CustomDragTypeConfig {
  /** Prefix for draggable IDs (e.g. 'member-'). Items with this prefix are treated as this type. */
  prefix: string
  /** Render the drag overlay for this type. Receives the active drag ID. */
  renderOverlay: (activeId: string) => ReactNode
}

/** Params passed to onCustomDrop when a custom drag type is dropped on a card. */
export interface CustomDropParams {
  /** The full draggable ID (e.g. 'member-abc123'). */
  dragId: string
  /** The card ID that was dropped onto. */
  targetCardId: string
  /** The column ID containing the target card. */
  targetColumnId: string
}

export interface KanbanBoardProps<TCard> {
  columns: KanbanColumnDef<TCard>[]
  /** Return the stable unique ID for a card. */
  getCardId: (card: TCard) => string
  /** Render a card. When CardRenderContext overload is used, receives enhanced context. */
  renderCard: ((card: TCard, context: CardRenderContext) => ReactNode) | ((card: TCard) => ReactNode)
  /** Render the column header. When ColumnRenderContext overload is used, receives enhanced context. */
  renderColumnHeader: ((column: KanbanColumnDef<TCard>, context: ColumnRenderContext) => ReactNode) | ((column: KanbanColumnDef<TCard>) => ReactNode)
  /** Called when a card is moved to a normal (non-system) column. */
  onMoveCard: (params: KanbanMoveCardParams) => void
  /** Called when a card is dropped into a system column. Not called for same-column reorder. */
  onTerminateCard?: (params: KanbanTerminateCardParams) => void
  /** Called when the user reorders non-system columns; receives the new ordered IDs. */
  onReorderColumns?: (orderedColumnIds: string[]) => void
  isLoading?: boolean
  /** Column width in px (default: 280). */
  columnWidth?: number
  emptyState?: ReactNode

  // ── Column extension slots ──

  /** Render content at the bottom of a column (e.g. quick-add task). Rendered inside the droppable body. */
  renderColumnFooter?: (column: KanbanColumnDef<TCard>) => ReactNode
  /** Render an empty column placeholder (e.g. "Drop tasks here"). If not provided, shows nothing. */
  renderColumnEmpty?: (column: KanbanColumnDef<TCard>, isCardOver: boolean) => ReactNode
  /** Render a collapsed column strip. When provided + column is collapsed, replaces the full column. */
  renderCollapsedColumn?: (column: KanbanColumnDef<TCard>, context: { cardCount: number; dragHandleProps: Record<string, unknown>; onExpand: () => void }) => ReactNode

  // ── Board-level extension slots ──

  /** Render content after the last column (e.g. "Add column" button). */
  renderBoardFooter?: () => ReactNode
  /** Custom drag overlay renderer. When provided, overrides the default card overlay. */
  renderDragOverlay?: (activeItem: { type: 'card'; card: TCard } | { type: 'column'; column: KanbanColumnDef<TCard> }) => ReactNode

  // ── Behavioral extensions ──

  /** Set of collapsed column IDs (controlled). */
  collapsedColumnIds?: Set<string>
  /** Called when a column collapse is toggled (if not controlled externally). */
  onToggleCollapse?: (columnId: string) => void

  /** Custom drag types (e.g. member assignment pills). */
  customDragTypes?: CustomDragTypeConfig[]
  /** Called when a custom draggable is dropped onto a card. */
  onCustomDrop?: (params: CustomDropParams) => void

  /** Filter cards for display only. DnD operates on unfiltered columns. */
  filterCards?: (cards: TCard[], columnId: string) => TCard[]
  /** Sort cards for display only. Applied after filterCards. */
  sortCards?: (cards: TCard[], columnId: string) => TCard[]

  /** Board interaction mode. 'drag' = normal DnD (default), 'pan' = scroll only, 'select' = lasso selection. */
  boardMode?: 'drag' | 'pan' | 'select'
  /** Selected card IDs (for select mode). */
  selectedCardIds?: Set<string>
  /** Called when lasso selection completes with the newly selected card IDs. */
  onLassoSelect?: (cardIds: string[]) => void

  /** Whether a card can be dragged. Defaults to true for all cards. */
  isCardDraggable?: (card: TCard) => boolean

  /** Whether columns can be dragged to reorder. Defaults to true. Set false for fixed column layouts (e.g. CRM pipeline stages). */
  enableColumnReorder?: boolean

  /** Board container className override. */
  boardClassName?: string
  /** Column container className override. */
  columnClassName?: string
  /** Column min/max width (default: columnWidth for both, making it fixed). PM uses min-w-[280px] max-w-[320px]. */
  columnMinWidth?: number
  columnMaxWidth?: number
  /** Full-height mode: board fills available height with scrollable columns (default: false). */
  fullHeight?: boolean
}
