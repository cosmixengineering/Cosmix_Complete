import { useCallback, useState, useEffect, useRef, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { useDraggable } from '@dnd-kit/core'
import { toast } from 'sonner'
import {
  Plus, Kanban, X, ArrowDown, Minus, ArrowUp, AlertTriangle, Loader2, EllipsisVertical, Pencil, Trash2,
  UserCheck, UserX, ChevronDown, Layers, ArrowUpDown, Check, ChevronsUpDown, Archive, Copy,
  Hand, MousePointer2, Square, CheckSquare,
} from 'lucide-react'
import {
  Avatar, Button, EmptyState, KanbanBoard,
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger, DropdownMenuCheckboxItem,
  DropdownMenuSub, DropdownMenuSubContent, DropdownMenuSubTrigger,
  Credenza, CredenzaContent, CredenzaHeader, CredenzaTitle, CredenzaDescription, CredenzaFooter, CredenzaBody,
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
  Tooltip, TooltipContent, TooltipTrigger,
} from '@uikit'
import type { KanbanColumnDef, KanbanMoveCardParams, CardRenderContext, CustomDropParams } from '@uikit'
import {
  useKanbanBoardQuery,
  useMoveTask,
  useCreateTask,
  useUpdateTask,
  useCreateColumn,
  useUpdateColumn,
  useDeleteColumn,
  useChangeTaskStatus,
  useReorderColumns,
  useMoveAllColumnTasks,
  useDuplicateColumn,
  useArchiveTask,
  useBulkArchiveTasks,
  useBulkChangeTaskStatus,
} from '@/portal-app/pm/queries'
import type { TaskCardDto, KanbanColumnDto, ProjectMemberDto, TaskLabelBriefDto, TaskPriority } from '@/types/pm'
import { TaskCard } from './TaskCard'
import { TaskDetailModal } from './TaskDetailModal'
import { ColumnSettingsDialog } from './ColumnSettingsDialog'
import { TaskFilterPopover, matchDueDate, matchCompletion, type DueDateFilter, type CompletionFilter } from './TaskFilterPopover'
import { TaskSearchInput } from './TaskSearchInput'

// ─── Column ↔ Status mapping + default colors ──────────────────────────────

const COLUMN_STATUS_MAP: Record<string, string> = {
  'todo': 'Todo', 'in progress': 'InProgress', 'in review': 'InReview', 'done': 'Done',
}

const DEFAULT_COLUMN_COLORS: Record<string, string> = {
  'todo': '#94a3b8', 'in progress': '#3b82f6', 'in review': '#8b5cf6', 'done': '#22c55e',
}

const getColumnColor = (column: { name: string; color: string | null }): string =>
  column.color ?? DEFAULT_COLUMN_COLORS[column.name.toLowerCase()] ?? '#94a3b8'

// ─── Draggable member pill ────────────────────────────────────────────────────

const DraggableMemberPill = ({
  member,
  active,
  onFilter,
}: {
  member: ProjectMemberDto
  active: boolean
  onFilter: () => void
}) => {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `member-${member.employeeId}`,
    data: { type: 'member', employeeId: member.employeeId, memberName: member.employeeName, avatarUrl: member.avatarUrl },
  })
  const firstName = member.employeeName.split(' ')[0]

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={{ opacity: isDragging ? 0.4 : 1, touchAction: 'none' }}
      className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium border cursor-grab active:cursor-grabbing transition-all select-none ${
        active ? 'bg-primary text-primary-foreground border-primary' : 'bg-background border-border hover:bg-muted'
      }`}
      aria-label={`${member.employeeName}`}
      onClick={(e) => { e.stopPropagation(); onFilter() }}
    >
      <Avatar src={member.avatarUrl ?? undefined} alt={member.employeeName} fallback={member.employeeName} size="sm" className="h-5 w-5 text-[9px]" />
      {firstName}
    </div>
  )
}

// ─── ProjectKanbanBoard ─────────────────────────────────────────────────────

interface ProjectKanbanBoardProps {
  projectId: string
  members?: ProjectMemberDto[]
  onCreateTask?: (columnId: string) => void
}

export const ProjectKanbanBoard = ({ projectId, members, onCreateTask }: ProjectKanbanBoardProps) => {
  const { t } = useTranslation('common')
  const { data: board, isLoading } = useKanbanBoardQuery(projectId)
  const moveTaskMutation = useMoveTask()
  const createTaskMutation = useCreateTask()
  const updateTaskMutation = useUpdateTask()
  const createColumnMutation = useCreateColumn()
  const updateColumnMutation = useUpdateColumn()
  const deleteColumnMutation = useDeleteColumn()
  const changeStatusMutation = useChangeTaskStatus()
  const reorderColumnsMutation = useReorderColumns()
  const moveAllColumnTasksMutation = useMoveAllColumnTasks()
  const duplicateColumnMutation = useDuplicateColumn()
  const archiveTaskMutation = useArchiveTask()
  const bulkArchiveTasksMutation = useBulkArchiveTasks()
  const bulkChangeStatusMutation = useBulkChangeTaskStatus()

  // ── Pan / Select mode ─────────────────────────────────────────────────────
  const [boardMode, setBoardMode] = useState<'drag' | 'pan' | 'select'>('drag')
  const [selectedTaskIds, setSelectedTaskIds] = useState<Set<string>>(new Set())

  const toggleTaskSelection = useCallback((taskId: string) => {
    setSelectedTaskIds(prev => {
      const next = new Set(prev)
      if (next.has(taskId)) next.delete(taskId)
      else next.add(taskId)
      return next
    })
  }, [])

  const clearSelection = useCallback(() => setSelectedTaskIds(new Set()), [])

  const switchMode = useCallback((mode: 'drag' | 'pan' | 'select') => {
    setBoardMode(mode)
    if (mode !== 'select') setSelectedTaskIds(new Set())
  }, [])

  // ── Collapsed columns ───────────────────────────────────────────────────────
  const [collapsedColumns, setCollapsedColumns] = useState<Set<string>>(new Set())

  const toggleColumnCollapse = useCallback((columnId: string) => {
    setCollapsedColumns(prev => {
      const next = new Set(prev)
      if (next.has(columnId)) next.delete(columnId)
      else next.add(columnId)
      return next
    })
  }, [])

  // ── UI state ───────────────────────────────────────────────────────────────
  const [quickAddColumnId, setQuickAddColumnId] = useState<string | null>(null)
  const [quickAddTitle, setQuickAddTitle] = useState('')
  const [isAddingColumn, setIsAddingColumn] = useState(false)
  const [newColumnName, setNewColumnName] = useState('')
  const [multiPasteLines, setMultiPasteLines] = useState<string[]>([])
  const [multiPasteColumnId, setMultiPasteColumnId] = useState<string | null>(null)
  const [editingColumnId, setEditingColumnId] = useState<string | null>(null)
  const [editingColumnName, setEditingColumnName] = useState('')
  const [columnSettingsId, setColumnSettingsId] = useState<string | null>(null)
  const [deleteColumnId, setDeleteColumnId] = useState<string | null>(null)
  const [deleteColumnMoveToId, setDeleteColumnMoveToId] = useState<string>('')
  const [moveAllColumnId, setMoveAllColumnId] = useState<string | null>(null)
  const [moveAllTargetColumnId, setMoveAllTargetColumnId] = useState<string>('')
  const [archiveAllColumnId, setArchiveAllColumnId] = useState<string | null>(null)

  // Keep a ref to board columns for handlers
  const boardColumnsRef = useRef<KanbanColumnDto[]>([])
  useEffect(() => { if (board?.columns) boardColumnsRef.current = board.columns }, [board?.columns])

  // ── URL-synced state ───────────────────────────────────────────────────────
  const [searchParams, setSearchParams] = useSearchParams()

  const taskNumber = searchParams.get('task')
  const taskModalOpen = Boolean(taskNumber)
  const selectedTaskId = useMemo(() => {
    if (!taskNumber || !board?.columns) return null
    return board.columns.flatMap(c => c.tasks).find(t => t.taskNumber === taskNumber)?.id ?? null
  }, [taskNumber, board?.columns])
  const setSelectedTaskId = (taskNum: string | null) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      if (taskNum) next.set('task', taskNum)
      else next.delete('task')
      return next
    }, { replace: true })
  }

  const boardTaskType = (searchParams.get('board-task-type') ?? 'all') as 'all' | 'tasks' | 'subtasks'
  const boardSearch = searchParams.get('board-search') ?? ''
  const boardAssignees = searchParams.get('board-assignees')?.split(',').filter(Boolean) ?? []
  const boardReporters = searchParams.get('board-reporters')?.split(',').filter(Boolean) ?? []
  const boardPriorities = searchParams.get('board-priorities')?.split(',').filter(Boolean) ?? []
  const boardLabels = searchParams.get('board-labels')?.split(',').filter(Boolean) ?? []
  const boardDue = (searchParams.get('board-due') ?? '') as DueDateFilter
  const boardDueStart = searchParams.get('board-due-start') ?? ''
  const boardDueEnd = searchParams.get('board-due-end') ?? ''
  const boardCompletion = (searchParams.get('board-completion') ?? '') as CompletionFilter

  const advancedFilterCount = boardLabels.length + (boardDue ? 1 : 0) + boardReporters.length + (boardCompletion ? 1 : 0)
  const hasActiveFilters = Boolean(boardSearch) || boardAssignees.length > 0 || boardPriorities.length > 0 || boardTaskType !== 'all' || advancedFilterCount > 0

  const setFilter = (key: string, value: string) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      if (value) next.set(key, value)
      else next.delete(key)
      return next
    }, { replace: true })
  }

  const clearAllFilters = () => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      ;['board-search', 'board-assignees', 'board-reporters', 'board-priorities', 'board-task-type', 'board-labels', 'board-due', 'board-due-start', 'board-due-end', 'board-completion'].forEach(k => next.delete(k))
      return next
    }, { replace: true })
  }

  // ── Available labels ───────────────────────────────────────────────────────
  const availableLabels = useMemo((): TaskLabelBriefDto[] => {
    const seen = new Set<string>()
    const labels: TaskLabelBriefDto[] = []
    if (!board?.columns) return labels
    for (const col of board.columns) {
      for (const task of col.tasks) {
        for (const label of task.labels) {
          if (!seen.has(label.id)) { seen.add(label.id); labels.push(label) }
        }
      }
    }
    return labels
  }, [board?.columns])

  // ── Column sort overrides ──────────────────────────────────────────────────
  type SortOption = 'default' | 'priority' | 'dueDate' | 'alpha'
  const priorityOrder: Record<string, number> = { Urgent: 4, High: 3, Medium: 2, Low: 1 }
  const [columnSortOverrides, setColumnSortOverrides] = useState<Record<string, SortOption>>({})

  // ── Map board data → KanbanColumnDef ──────────────────────────────────────
  const kanbanColumns = useMemo((): KanbanColumnDef<TaskCardDto>[] => {
    return (board?.columns ?? []).map(col => ({
      id: col.id,
      cards: col.tasks,
    }))
  }, [board?.columns])

  // ── Filter + Sort callbacks for UIKit KanbanBoard ─────────────────────────
  const filterCards = useCallback((cards: TaskCardDto[], _columnId: string): TaskCardDto[] => {
    return cards.filter(task => {
      const matchSearch = !boardSearch || task.title.toLowerCase().includes(boardSearch.toLowerCase())
      const matchAssignee = boardAssignees.length === 0 ||
        (boardAssignees.includes('__unassigned__') && !task.assigneeName) ||
        (task.assigneeName != null && boardAssignees.some(a => a !== '__unassigned__' && task.assigneeName!.toLowerCase().includes(a.toLowerCase())))
      const matchReporter = boardReporters.length === 0 ||
        (boardReporters.includes('__no-reporter__') && !task.reporterName) ||
        (task.reporterName != null && boardReporters.some(r => r !== '__no-reporter__' && task.reporterName!.toLowerCase().includes(r.toLowerCase())))
      const matchPriority = boardPriorities.length === 0 || boardPriorities.includes(task.priority)
      const matchTaskType = boardTaskType === 'all' || (boardTaskType === 'tasks' && !task.parentTaskId) || (boardTaskType === 'subtasks' && Boolean(task.parentTaskId))
      const matchLabel = boardLabels.length === 0 || (boardLabels.includes('__no-label__') && task.labels.length === 0) || task.labels.some(l => boardLabels.includes(l.id))
      const matchDue = matchDueDate(task.dueDate, boardDue, boardDueStart || undefined, boardDueEnd || undefined)
      const matchComp = matchCompletion(task.completedAt, boardCompletion)
      return matchSearch && matchAssignee && matchReporter && matchPriority && matchTaskType && matchLabel && matchDue && matchComp
    })
  }, [boardSearch, boardAssignees, boardReporters, boardPriorities, boardTaskType, boardLabels, boardDue, boardDueStart, boardDueEnd, boardCompletion])

  const sortCards = useCallback((cards: TaskCardDto[], columnId: string): TaskCardDto[] => {
    const sort = columnSortOverrides[columnId] ?? 'default'
    if (sort === 'default') return cards
    return [...cards].sort((a, b) => {
      if (sort === 'priority') return (priorityOrder[b.priority] ?? 0) - (priorityOrder[a.priority] ?? 0)
      if (sort === 'dueDate') {
        if (!a.dueDate && !b.dueDate) return 0
        if (!a.dueDate) return 1
        if (!b.dueDate) return -1
        return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime()
      }
      if (sort === 'alpha') return a.title.localeCompare(b.title)
      return 0
    })
  }, [columnSortOverrides])

  // ── Card move handler ─────────────────────────────────────────────────────
  const handleMoveCard = useCallback(({ cardId, toColumnId, prevCardId, nextCardId }: KanbanMoveCardParams) => {
    if (!board) return
    const targetCol = board.columns.find(c => c.id === toColumnId)
    if (!targetCol) return

    const colTasks = targetCol.tasks.filter(t => t.id !== cardId)
    const prevTask = prevCardId ? colTasks.find(t => t.id === prevCardId) : null
    const nextTask = nextCardId ? colTasks.find(t => t.id === nextCardId) : null

    let newSortOrder: number
    if (!prevTask && !nextTask) {
      newSortOrder = 1
    } else if (!prevTask) {
      newSortOrder = nextTask!.sortOrder > 0 ? nextTask!.sortOrder / 2 : nextTask!.sortOrder - 1
    } else if (!nextTask) {
      newSortOrder = prevTask.sortOrder + 1
    } else {
      const prev = prevTask.sortOrder
      const next = nextTask.sortOrder
      newSortOrder = prev < next ? (prev + next) / 2 : prev + 1
    }

    moveTaskMutation.mutate(
      { id: cardId, request: { columnId: toColumnId, sortOrder: newSortOrder } },
      {
        onSuccess: () => {
          const matchedStatus = COLUMN_STATUS_MAP[targetCol.name.toLowerCase()]
          const origCol = board.columns.find(col => col.tasks.some(t => t.id === cardId))
          const movedTask = origCol?.tasks.find(t => t.id === cardId)
          if (matchedStatus && movedTask && movedTask.status !== matchedStatus) {
            changeStatusMutation.mutate({ id: cardId, status: matchedStatus })
          }
        },
        onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
      },
    )
  }, [board, moveTaskMutation, changeStatusMutation, t])

  // ── Column reorder handler ────────────────────────────────────────────────
  const handleReorderColumns = useCallback((orderedIds: string[]) => {
    reorderColumnsMutation.mutate(
      { projectId, request: { columnIds: orderedIds } },
      { onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')) },
    )
  }, [reorderColumnsMutation, projectId, t])

  // ── Member assignment via custom drag ─────────────────────────────────────
  const handleCustomDrop = useCallback(({ dragId, targetCardId }: CustomDropParams) => {
    const employeeId = dragId.replace('member-', '')
    updateTaskMutation.mutate(
      { id: targetCardId, request: { assigneeId: employeeId } },
      { onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')) },
    )
  }, [updateTaskMutation, t])

  // ── Lasso selection handler ───────────────────────────────────────────────
  const handleLassoSelect = useCallback((cardIds: string[]) => {
    setSelectedTaskIds(prev => {
      const next = new Set(prev)
      cardIds.forEach(id => next.add(id))
      return next
    })
  }, [])

  // ── Task action handlers ──────────────────────────────────────────────────
  const handleTaskClick = useCallback((task: TaskCardDto) => {
    setSelectedTaskId(task.taskNumber)
  }, [])

  const handleQuickAddTask = useCallback((columnId: string, title: string) => {
    const lines = title.split('\n').map(l => l.trim()).filter(Boolean)
    if (lines.length > 1) {
      setMultiPasteLines(lines)
      setMultiPasteColumnId(columnId)
      setQuickAddColumnId(null)
      setQuickAddTitle('')
      return
    }
    createTaskMutation.mutate(
      { projectId, title: lines[0] ?? title, columnId },
      {
        onSuccess: () => { setQuickAddColumnId(null); setQuickAddTitle('') },
        onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
      },
    )
  }, [createTaskMutation, projectId, t])

  const handleAddColumn = useCallback(() => {
    if (!newColumnName.trim()) return
    createColumnMutation.mutate(
      { projectId, request: { name: newColumnName.trim() } },
      {
        onSuccess: () => { setIsAddingColumn(false); setNewColumnName('') },
        onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
      },
    )
  }, [createColumnMutation, newColumnName, projectId, t])

  const handleQuickComplete = useCallback((task: TaskCardDto) => {
    const newStatus = task.status === 'Done' ? 'Todo' : 'Done'
    changeStatusMutation.mutate(
      { id: task.id, status: newStatus },
      { onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')) },
    )
  }, [changeStatusMutation, t])

  const handleUnassignTask = useCallback((task: TaskCardDto) => {
    updateTaskMutation.mutate(
      { id: task.id, request: { assigneeId: undefined } },
      { onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')) },
    )
  }, [updateTaskMutation, t])

  const handleArchiveTask = useCallback((task: TaskCardDto) => {
    archiveTaskMutation.mutate(task.id, {
      onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
    })
  }, [archiveTaskMutation, t])

  const handleMoveToColumn = useCallback((task: TaskCardDto, columnId: string) => {
    const targetColumn = board?.columns.find(c => c.id === columnId)
    if (!targetColumn) return
    moveTaskMutation.mutate(
      { id: task.id, request: { columnId, sortOrder: targetColumn.tasks.length } },
      {
        onSuccess: () => {
          const matchedStatus = COLUMN_STATUS_MAP[targetColumn.name.toLowerCase()]
          if (matchedStatus && task.status !== matchedStatus) {
            changeStatusMutation.mutate({ id: task.id, status: matchedStatus })
          }
        },
        onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
      },
    )
  }, [board?.columns, moveTaskMutation, changeStatusMutation, t])

  const handleModalMoveToColumn = useCallback((taskId: string, columnId: string) => {
    if (!board?.columns) return
    const targetColumn = board.columns.find(c => c.id === columnId)
    if (!targetColumn) return
    const currentColumn = board.columns.find(c => c.tasks.some(t => t.id === taskId))
    if (currentColumn?.id === columnId) return
    moveTaskMutation.mutate(
      { id: taskId, request: { columnId, sortOrder: targetColumn.tasks.length } },
      {
        onSuccess: () => {
          const matchedStatus = COLUMN_STATUS_MAP[targetColumn.name.toLowerCase()]
          const task = currentColumn?.tasks.find(t => t.id === taskId)
          if (matchedStatus && task && task.status !== matchedStatus) {
            changeStatusMutation.mutate({ id: taskId, status: matchedStatus })
          }
        },
      },
    )
  }, [board?.columns, moveTaskMutation, changeStatusMutation])

  const handleContextMenuChangePriority = useCallback((task: TaskCardDto, priority: string) => {
    updateTaskMutation.mutate(
      { id: task.id, request: { priority: priority as TaskPriority } },
      { onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')) },
    )
  }, [updateTaskMutation, t])

  const handleColumnRenameStart = useCallback((column: KanbanColumnDto) => {
    setEditingColumnId(column.id)
    setEditingColumnName(column.name)
  }, [])

  const handleColumnRenameSave = useCallback((columnId: string) => {
    const original = board?.columns.find(c => c.id === columnId)
    if (!editingColumnName.trim() || original?.name === editingColumnName.trim()) {
      setEditingColumnId(null)
      return
    }
    updateColumnMutation.mutate(
      { projectId, columnId, request: { name: editingColumnName.trim() } },
      {
        onSuccess: () => setEditingColumnId(null),
        onError: (err) => { toast.error(err instanceof Error ? err.message : t('errors.unknown')); setEditingColumnId(null) },
      },
    )
  }, [editingColumnName, board?.columns, updateColumnMutation, projectId, t])

  // ── Resolve board column by ID (for rendering) ───────────────────────────
  const getColumn = useCallback((columnId: string): KanbanColumnDto | undefined => {
    return board?.columns.find(c => c.id === columnId)
  }, [board?.columns])

  // ── Filtered columns for bulk ops and menus ───────────────────────────────
  const filteredBoardColumns = board?.columns ?? []

  // ── Custom drag type config for member pills ──────────────────────────────
  const customDragTypes = useMemo(() => {
    if (!members?.length) return undefined
    return [{
      prefix: 'member-',
      renderOverlay: (activeId: string) => {
        const member = members.find(m => `member-${m.employeeId}` === activeId)
        if (!member) return null
        return (
          <div className="flex items-center gap-2 bg-primary text-primary-foreground rounded-full px-3 py-1.5 text-xs font-medium shadow-lg border border-primary/50">
            <Avatar src={member.avatarUrl ?? undefined} alt={member.employeeName} fallback={member.employeeName} size="sm" className="h-5 w-5 text-[9px]" />
            {member.employeeName.split(' ')[0]}
            <UserCheck className="h-3.5 w-3.5 ml-0.5" />
          </div>
        )
      },
    }]
  }, [members])

  // ── Render card ───────────────────────────────────────────────────────────
  const renderCard = useCallback((task: TaskCardDto, context: CardRenderContext) => {
    return (
      <TaskCard
        task={task}
        onClick={handleTaskClick}
        isDraggable={boardMode === 'drag'}
        onComplete={boardMode === 'drag' ? handleQuickComplete : undefined}
        onUnassign={boardMode === 'drag' ? handleUnassignTask : undefined}
        onArchive={boardMode === 'drag' ? handleArchiveTask : undefined}
        onChangePriority={boardMode === 'drag' ? handleContextMenuChangePriority : undefined}
        onMoveToColumn={boardMode === 'drag' ? handleMoveToColumn : undefined}
        columns={filteredBoardColumns}
        currentColumnId={context.columnId}
        isMemberDragTarget={context.isCustomDragOver}
        isSelected={context.isSelected}
        onSelect={boardMode === 'select' ? toggleTaskSelection : undefined}
      />
    )
  }, [boardMode, getColumn, handleTaskClick, handleQuickComplete, handleUnassignTask, handleArchiveTask, handleContextMenuChangePriority, handleMoveToColumn, filteredBoardColumns, toggleTaskSelection])

  // ── Render column header ──────────────────────────────────────────────────
  const renderColumnHeader = useCallback((column: KanbanColumnDef<TaskCardDto>, context: { dragHandleProps: Record<string, unknown>; isDragging: boolean; isCardOver: boolean; cardCount: number }) => {
    const boardCol = getColumn(column.id)
    if (!boardCol) return null
    const originalTaskCount = context.cardCount
    const wipExceeded = boardCol.wipLimit != null && originalTaskCount > boardCol.wipLimit

    return (
      <>
        {/* Column color stripe */}
        <div className="h-1 rounded-t-lg" style={{ backgroundColor: getColumnColor(boardCol) }} />
        {/* Column header */}
        <div className={`flex items-center justify-between px-2 py-2.5 border-b border-border/50 dark:border-border/70 ${wipExceeded ? 'bg-red-50 dark:bg-red-950/30' : ''}`}>
          <div
            {...context.dragHandleProps}
            className="flex items-center gap-1.5 min-w-0 flex-1 cursor-grab active:cursor-grabbing"
          >
            <span className="h-2.5 w-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: getColumnColor(boardCol) }} />
            {editingColumnId === column.id ? (
              <input
                autoFocus
                value={editingColumnName}
                onChange={(e) => setEditingColumnName(e.target.value)}
                onBlur={() => handleColumnRenameSave(column.id)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') { e.preventDefault(); handleColumnRenameSave(column.id) }
                  if (e.key === 'Escape') setEditingColumnId(null)
                }}
                onPointerDown={(e) => e.stopPropagation()}
                onClick={(e) => e.stopPropagation()}
                className="flex-1 text-sm font-semibold bg-background border border-input rounded px-1.5 py-0.5 focus:outline-none focus:ring-1 focus:ring-ring min-w-0 cursor-text"
              />
            ) : (
              <Tooltip>
                <TooltipTrigger asChild>
                  <h3
                    className="text-sm font-semibold leading-5 truncate hover:bg-muted/40 rounded px-1 -mx-1 transition-colors cursor-text"
                    onClick={(e) => { e.stopPropagation(); handleColumnRenameStart(boardCol) }}
                    onPointerDown={(e) => e.stopPropagation()}
                  >
                    {boardCol.name}
                  </h3>
                </TooltipTrigger>
                <TooltipContent>{t('pm.clickToRename', { defaultValue: 'Click to rename' })}</TooltipContent>
              </Tooltip>
            )}
            <span className={`inline-flex items-center justify-center min-w-[1.25rem] h-5 rounded px-1.5 text-xs font-medium tabular-nums flex-shrink-0 ${
              wipExceeded ? 'bg-red-100 text-red-600 dark:bg-red-950 dark:text-red-400 font-bold' : 'bg-muted text-muted-foreground'
            }`}>
              {originalTaskCount}{boardCol.wipLimit != null && `/${boardCol.wipLimit}`}
            </span>
            {wipExceeded && (
              <span className="inline-flex items-center text-[10px] bg-red-100 text-red-600 dark:bg-red-950 dark:text-red-300 rounded-full px-1.5 py-0.5 font-medium leading-[1.1] border border-red-200 dark:border-red-800 flex-shrink-0">
                {t('pm.wipExceeded', { defaultValue: 'WIP!' })}
              </span>
            )}
          </div>
          <div className="flex items-center gap-0.5 flex-shrink-0">
            {onCreateTask && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-6 w-6 cursor-pointer"
                    onClick={(e) => { e.stopPropagation(); onCreateTask(column.id) }}
                    onPointerDown={(e) => e.stopPropagation()}
                    aria-label={`${t('pm.createTask')} - ${boardCol.name}`}
                  >
                    <Plus className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>{t('pm.addCard', { defaultValue: 'Add a card' })}</TooltipContent>
              </Tooltip>
            )}
            <ColumnActionsMenu
              column={boardCol}
              collapsedColumns={collapsedColumns}
              columnSortOverrides={columnSortOverrides}
              onRenameStart={handleColumnRenameStart}
              onSettingsOpen={() => setColumnSettingsId(column.id)}
              onSortChange={(key) => setColumnSortOverrides(prev => ({ ...prev, [column.id]: key }))}
              onToggleCollapse={() => toggleColumnCollapse(column.id)}
              onMoveAll={() => { setMoveAllColumnId(column.id); setMoveAllTargetColumnId(board?.columns.find(c => c.id !== column.id)?.id ?? '') }}
              onArchiveAll={() => setArchiveAllColumnId(column.id)}
              onDuplicate={() => duplicateColumnMutation.mutate({ projectId, columnId: column.id })}
              onDelete={() => { setDeleteColumnId(column.id); setDeleteColumnMoveToId(board?.columns.find(c => c.id !== column.id)?.id ?? '') }}
              t={t}
            />
          </div>
        </div>
      </>
    )
  }, [board?.columns, editingColumnId, editingColumnName, collapsedColumns, columnSortOverrides, getColumn, onCreateTask, handleColumnRenameStart, handleColumnRenameSave, toggleColumnCollapse, duplicateColumnMutation, projectId, t])

  // ── Render collapsed column ───────────────────────────────────────────────
  const renderCollapsedColumn = useCallback((column: KanbanColumnDef<TaskCardDto>, ctx: { cardCount: number; dragHandleProps: Record<string, unknown>; onExpand: () => void }) => {
    const boardCol = getColumn(column.id)
    if (!boardCol) return null
    return (
      <div
        className="w-10 flex-shrink-0 flex flex-col items-center rounded-lg border bg-muted/40 dark:bg-muted/50 cursor-pointer transition-all duration-150 overflow-hidden border-border dark:border-border/80 hover:border-primary/40"
        {...ctx.dragHandleProps}
        onClick={ctx.onExpand}
        title={t('pm.expandColumn', { defaultValue: 'Expand column' })}
      >
        <div className="h-1 w-full rounded-t-lg flex-shrink-0" style={{ backgroundColor: getColumnColor(boardCol) }} />
        <div className="flex-1 flex flex-col items-center justify-center gap-2 py-3 px-0 w-full overflow-hidden">
          <span
            className="text-[11px] font-semibold text-muted-foreground leading-none select-none max-h-32 overflow-hidden"
            style={{ writingMode: 'vertical-lr', textOrientation: 'mixed', transform: 'rotate(180deg)' }}
          >
            {boardCol.name}
          </span>
          <span className="inline-flex items-center justify-center h-5 min-w-5 rounded-full px-1 text-[10px] font-medium bg-muted text-muted-foreground tabular-nums">
            {ctx.cardCount}
          </span>
        </div>
      </div>
    )
  }, [getColumn, t])

  // ── Render column empty state ─────────────────────────────────────────────
  const renderColumnEmpty = useCallback((_column: KanbanColumnDef<TaskCardDto>, isCardOver: boolean) => (
    <div className={`flex flex-col items-center justify-center py-6 text-center border-2 border-dashed rounded-lg transition-all duration-150 ${
      isCardOver ? 'border-primary/60 bg-primary/5' : 'border-border/50 dark:border-border/60'
    }`}>
      <p className="text-xs text-muted-foreground">
        {t('pm.dropHere', { defaultValue: 'Drop tasks here' })}
      </p>
    </div>
  ), [t])

  // ── Render column footer (quick-add) ──────────────────────────────────────
  const renderColumnFooter = useCallback((column: KanbanColumnDef<TaskCardDto>) => {
    if (quickAddColumnId === column.id) {
      return (
        <div onClick={(e) => e.stopPropagation()}>
          <textarea
            autoFocus
            rows={2}
            value={quickAddTitle}
            onChange={(e) => setQuickAddTitle(e.target.value)}
            onPaste={(e) => {
              const text = e.clipboardData.getData('text')
              const lines = text.split('\n').map(l => l.trim()).filter(Boolean)
              if (lines.length > 1) {
                e.preventDefault()
                setMultiPasteLines(lines)
                setMultiPasteColumnId(column.id)
                setQuickAddColumnId(null)
                setQuickAddTitle('')
              }
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                if (quickAddTitle.trim()) handleQuickAddTask(column.id, quickAddTitle.trim())
              }
              if (e.key === 'Escape') { setQuickAddColumnId(null); setQuickAddTitle('') }
            }}
            placeholder={t('pm.taskTitlePlaceholder', { defaultValue: 'Task title...' })}
            className="w-full resize-none text-sm bg-background border border-input rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <div className="flex items-center gap-2 mt-2">
            <Button
              size="sm"
              className="cursor-pointer h-7 text-xs bg-green-600 hover:bg-green-700 text-white border-0"
              disabled={createTaskMutation.isPending}
              onClick={() => { if (quickAddTitle.trim()) handleQuickAddTask(column.id, quickAddTitle.trim()) }}
            >
              {createTaskMutation.isPending && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
              {t('pm.addCard', { defaultValue: 'Add card' })}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="cursor-pointer h-7 text-xs"
              onClick={() => { setQuickAddColumnId(null); setQuickAddTitle('') }}
            >
              {t('buttons.cancel')}
            </Button>
          </div>
        </div>
      )
    }
    return (
      <button
        className="w-full flex items-center gap-1.5 px-1 py-1.5 text-xs text-muted-foreground hover:text-foreground hover:bg-muted/40 rounded-md transition-colors cursor-pointer"
        onClick={() => setQuickAddColumnId(column.id)}
      >
        <Plus className="h-3.5 w-3.5" />
        {t('pm.addCard', { defaultValue: 'Add card' })}
      </button>
    )
  }, [quickAddColumnId, quickAddTitle, createTaskMutation.isPending, handleQuickAddTask, t])

  // ── Render drag overlay ───────────────────────────────────────────────────
  const renderDragOverlay = useCallback((activeItem: { type: 'card'; card: TaskCardDto } | { type: 'column'; column: KanbanColumnDef<TaskCardDto> }) => {
    if (activeItem.type === 'card') {
      return (
        <div className="w-[280px] rotate-2 opacity-95 rounded-lg shadow-2xl ring-1 ring-black/5">
          <TaskCard task={activeItem.card} onClick={() => {}} isDraggable={false} />
        </div>
      )
    }
    if (activeItem.type === 'column') {
      const boardCol = getColumn(activeItem.column.id)
      if (!boardCol) return null
      return (
        <div className="min-w-[280px] max-w-[320px] bg-muted/50 dark:bg-muted/60 rounded-lg border border-primary/40 shadow-2xl rotate-1 overflow-hidden">
          <div className="flex items-center gap-2 px-3 py-2.5 border-b border-border/50 bg-muted/60">
            {boardCol.color && <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: boardCol.color }} />}
            <h3 className="text-sm font-semibold">{boardCol.name}</h3>
            <span className="text-xs text-muted-foreground">{boardCol.tasks.length}</span>
          </div>
          <div className="p-2 space-y-1.5 max-h-48 overflow-hidden">
            {boardCol.tasks.slice(0, 3).map(task => (
              <div key={task.id} className="h-10 bg-background/60 rounded-md border border-border/40" />
            ))}
            {boardCol.tasks.length > 3 && (
              <p className="text-[10px] text-center text-muted-foreground py-1">+{boardCol.tasks.length - 3} more</p>
            )}
            {boardCol.tasks.length === 0 && (
              <div className="h-12 border-2 border-dashed border-border/30 rounded-md" />
            )}
          </div>
        </div>
      )
    }
    return null
  }, [getColumn])

  // ── Render board footer (add column) ──────────────────────────────────────
  const renderBoardFooter = useCallback(() => {
    if (isAddingColumn) {
      return (
        <div className="min-w-[240px] max-w-[280px] flex-shrink-0 bg-muted/40 dark:bg-muted/50 rounded-lg border border-border dark:border-border/80 p-3 space-y-2">
          <input
            autoFocus
            value={newColumnName}
            onChange={(e) => setNewColumnName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleAddColumn()
              if (e.key === 'Escape') { setIsAddingColumn(false); setNewColumnName('') }
            }}
            placeholder={t('pm.newColumnName', { defaultValue: 'Column name...' })}
            className="w-full text-sm bg-background border border-input rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <div className="flex gap-2">
            <Button size="sm" className="cursor-pointer" disabled={createColumnMutation.isPending} onClick={handleAddColumn}>
              {createColumnMutation.isPending && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
              {t('buttons.add', { defaultValue: 'Add' })}
            </Button>
            <Button variant="ghost" size="sm" className="cursor-pointer" onClick={() => { setIsAddingColumn(false); setNewColumnName('') }}>
              {t('buttons.cancel')}
            </Button>
          </div>
        </div>
      )
    }
    return (
      <button
        className="min-w-[200px] flex-shrink-0 h-12 flex items-center justify-center gap-2 rounded-lg border-2 border-dashed border-border/50 dark:border-border/60 text-muted-foreground hover:border-primary/50 hover:text-primary hover:bg-primary/5 transition-all cursor-pointer text-sm"
        onClick={() => setIsAddingColumn(true)}
      >
        <Plus className="h-4 w-4" />
        {t('pm.addColumn')}
      </button>
    )
  }, [isAddingColumn, newColumnName, createColumnMutation.isPending, handleAddColumn, t])

  // ── Loading / Empty ────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <KanbanBoard<TaskCardDto>
        columns={[]}
        getCardId={(t) => t.id}
        renderCard={() => null}
        renderColumnHeader={() => null}
        onMoveCard={() => {}}
        isLoading
      />
    )
  }

  if (!board || kanbanColumns.length === 0) {
    return (
      <EmptyState
        icon={Kanban}
        title={t('pm.noTasksFound')}
        description={t('pm.createTask')}
      />
    )
  }

  return (
    <div className="space-y-3">
      {/* ── Filter bar ── */}
      <div className="flex flex-wrap items-center gap-2">
        <TaskSearchInput
          value={boardSearch}
          onChange={(v) => setFilter('board-search', v)}
        />

        {/* Task type dropdown */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className={`inline-flex items-center gap-1.5 rounded-full px-3 h-9 text-sm font-medium border cursor-pointer transition-all ${
              boardTaskType !== 'all' ? 'bg-primary/5 text-primary border-primary/30 hover:bg-primary/10' : 'bg-background border-border hover:bg-muted'
            }`}>
              <Layers className="h-3.5 w-3.5" />
              {boardTaskType === 'tasks'
                ? t('pm.filterTasks', { defaultValue: 'Tasks' })
                : boardTaskType === 'subtasks'
                  ? t('pm.filterSubtasks', { defaultValue: 'Subtasks' })
                  : t('pm.filterAll', { defaultValue: 'All' })}
              <ChevronDown className="h-3.5 w-3.5 opacity-60" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-40">
            {([
              { key: '', label: t('pm.filterAll', { defaultValue: 'All' }) },
              { key: 'tasks', label: t('pm.filterTasks', { defaultValue: 'Tasks only' }) },
              { key: 'subtasks', label: t('pm.filterSubtasks', { defaultValue: 'Subtasks only' }) },
            ] as const).map(({ key, label }) => (
              <DropdownMenuItem
                key={key}
                onClick={() => setFilter('board-task-type', key)}
                className={`cursor-pointer ${boardTaskType === key ? 'bg-primary/5 text-primary font-medium' : ''}`}
              >
                {label}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>

        {/* Priority multi-select */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className={`inline-flex items-center gap-1.5 rounded-full px-3 h-9 text-sm font-medium border cursor-pointer transition-all ${
              boardPriorities.length > 0 ? 'bg-primary/5 text-primary border-primary/30 hover:bg-primary/10' : 'bg-background border-border hover:bg-muted'
            }`}>
              <AlertTriangle className="h-3.5 w-3.5" />
              {t('pm.priority', { defaultValue: 'Priority' })}
              {boardPriorities.length > 0 && (
                <span className="inline-flex items-center justify-center h-4 min-w-4 px-1 rounded-full text-[10px] font-bold leading-none bg-primary text-primary-foreground">
                  {boardPriorities.length}
                </span>
              )}
              <ChevronDown className="h-3.5 w-3.5 opacity-60" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-44">
            {(['Low', 'Medium', 'High', 'Urgent'] as const).map(p => {
              const icons = { Low: ArrowDown, Medium: Minus, High: ArrowUp, Urgent: AlertTriangle }
              const colors = { Low: 'text-slate-500', Medium: 'text-blue-500', High: 'text-orange-500', Urgent: 'text-red-500' }
              const Icon = icons[p]
              return (
                <DropdownMenuCheckboxItem
                  key={p}
                  checked={boardPriorities.includes(p)}
                  onCheckedChange={(checked) => {
                    const next = checked ? [...boardPriorities, p] : boardPriorities.filter(x => x !== p)
                    setFilter('board-priorities', next.join(','))
                  }}
                  onSelect={(e) => e.preventDefault()}
                  className="cursor-pointer"
                >
                  <Icon className={`h-3.5 w-3.5 mr-1.5 ${colors[p]}`} />
                  {p}
                </DropdownMenuCheckboxItem>
              )
            })}
            {boardPriorities.length > 0 && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setFilter('board-priorities', '')} className="cursor-pointer text-muted-foreground text-xs">
                  <X className="h-3 w-3 mr-1.5" />
                  {t('buttons.clearFilters', { defaultValue: 'Clear' })}
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>

        {/* Member pills */}
        {members && members.length > 0 && (
          <div className="flex flex-wrap gap-1.5 items-center">
            <button
              onClick={() => {
                const next = boardAssignees.includes('__unassigned__')
                  ? boardAssignees.filter(a => a !== '__unassigned__')
                  : [...boardAssignees, '__unassigned__']
                setFilter('board-assignees', next.join(','))
              }}
              className={`inline-flex items-center gap-1 rounded-full px-3 py-1.5 text-sm font-medium border cursor-pointer transition-all ${
                boardAssignees.includes('__unassigned__') ? 'bg-slate-600 text-white border-slate-600' : 'bg-background border-border hover:bg-muted'
              }`}
              title={t('pm.filterNoAssignee', { defaultValue: 'No assignee' })}
            >
              <UserX className="h-3.5 w-3.5" />
            </button>
            {members.map(member => {
              const firstName = member.employeeName.split(' ')[0]
              const active = boardAssignees.some(a => a !== '__unassigned__' && member.employeeName.toLowerCase().includes(a.toLowerCase()))
              return (
                <DraggableMemberPill
                  key={member.id}
                  member={member}
                  active={active}
                  onFilter={() => {
                    const next = active
                      ? boardAssignees.filter(a => !member.employeeName.toLowerCase().includes(a.toLowerCase()))
                      : [...boardAssignees, firstName]
                    setFilter('board-assignees', next.join(','))
                  }}
                />
              )
            })}
          </div>
        )}

        {/* Advanced filters */}
        <TaskFilterPopover
          showCompletion
          showAssignees
          showReporters
          showLabels
          showDueDate
          members={members}
          availableLabels={availableLabels}
          selectedAssignees={boardAssignees}
          onAssigneesChange={(v) => setFilter('board-assignees', v.join(','))}
          selectedReporters={boardReporters}
          onReportersChange={(v) => setFilter('board-reporters', v.join(','))}
          selectedLabels={boardLabels}
          onLabelsChange={(v) => setFilter('board-labels', v.join(','))}
          selectedDueDate={boardDue}
          onDueDateChange={(v) => setFilter('board-due', v)}
          dueDateSpecificStart={boardDueStart}
          onDueDateSpecificStartChange={(v) => setFilter('board-due-start', v)}
          dueDateSpecificEnd={boardDueEnd}
          onDueDateSpecificEndChange={(v) => setFilter('board-due-end', v)}
          completionFilter={boardCompletion}
          onCompletionChange={(v) => setFilter('board-completion', v)}
          onClearAll={clearAllFilters}
          activeCount={advancedFilterCount}
        />

        {hasActiveFilters && (
          <button
            onClick={clearAllFilters}
            className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground cursor-pointer transition-colors"
          >
            <X className="h-3 w-3" />
            {t('buttons.clearFilters', { defaultValue: 'Clear filters' })}
          </button>
        )}

        {/* Pan / Select mode toggle */}
        <div className="ml-auto flex items-center rounded-full border border-border bg-muted/40 p-0.5 gap-0.5">
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1.5 text-xs font-medium cursor-pointer transition-all ${
                  boardMode === 'drag' ? 'bg-background shadow-sm text-foreground' : 'text-muted-foreground hover:text-foreground'
                }`}
                onClick={() => switchMode('drag')}
                aria-label={t('pm.panMode', { defaultValue: 'Pan mode' })}
              >
                <Hand className="h-3.5 w-3.5" />
              </button>
            </TooltipTrigger>
            <TooltipContent>{t('pm.panMode', { defaultValue: 'Pan mode' })}</TooltipContent>
          </Tooltip>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1.5 text-xs font-medium cursor-pointer transition-all ${
                  boardMode === 'select' ? 'bg-background shadow-sm text-foreground' : 'text-muted-foreground hover:text-foreground'
                }`}
                onClick={() => switchMode('select')}
                aria-label={t('pm.selectMode', { defaultValue: 'Select mode' })}
              >
                <MousePointer2 className="h-3.5 w-3.5" />
              </button>
            </TooltipTrigger>
            <TooltipContent>{t('pm.selectMode', { defaultValue: 'Select mode' })}</TooltipContent>
          </Tooltip>
        </div>
      </div>

      {/* ── Bulk action bar ── */}
      {boardMode === 'select' && selectedTaskIds.size > 0 && (
        <div className="flex items-center gap-2 px-3 py-2 rounded-lg border border-primary/30 bg-primary/5 text-sm">
          <span className="font-medium text-primary">
            {t('pm.selectedCount', { count: selectedTaskIds.size, defaultValue: '{{count}} selected' })}
          </span>
          <div className="flex items-center gap-1 ml-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button className="inline-flex items-center justify-center gap-1.5 rounded-md px-2.5 h-7 text-xs font-medium border border-border bg-background hover:bg-muted cursor-pointer transition-all">
                  <Check className="h-3 w-3 shrink-0" />
                  {t('pm.bulkMoveToColumn', { defaultValue: 'Move to' })}
                  <ChevronDown className="h-3 w-3 shrink-0 opacity-60" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-44">
                {filteredBoardColumns.map(column => {
                  const status = COLUMN_STATUS_MAP[column.name.toLowerCase()]
                  return (
                    <DropdownMenuItem
                      key={column.id}
                      className="cursor-pointer gap-2"
                      onSelect={() => {
                        const ids = Array.from(selectedTaskIds)
                        if (status) {
                          bulkChangeStatusMutation.mutate({ taskIds: ids, status }, {
                            onSuccess: () => clearSelection(),
                            onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
                          })
                        } else {
                          const targetCol = board?.columns.find(c => c.id === column.id)
                          const baseSort = targetCol?.tasks.length ?? 0
                          Promise.all(ids.map((id, i) =>
                            moveTaskMutation.mutateAsync({ id, request: { columnId: column.id, sortOrder: baseSort + i + 1 } })
                          )).then(() => clearSelection())
                            .catch((err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')))
                        }
                      }}
                    >
                      <span className="h-2.5 w-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: getColumnColor(column) }} />
                      {column.name}
                    </DropdownMenuItem>
                  )
                })}
              </DropdownMenuContent>
            </DropdownMenu>

            <button
              className="inline-flex items-center justify-center gap-1.5 rounded-md px-2.5 h-7 text-xs font-medium border border-border bg-background hover:bg-amber-50 hover:text-amber-600 hover:border-amber-200 cursor-pointer transition-all"
              onClick={() => {
                bulkArchiveTasksMutation.mutate(Array.from(selectedTaskIds), {
                  onSuccess: () => clearSelection(),
                  onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
                })
              }}
            >
              <Archive className="h-3 w-3 shrink-0" />
              {t('pm.bulkArchive', { defaultValue: 'Archive' })}
            </button>

            {(() => {
              const allIds = filteredBoardColumns.flatMap(c => c.tasks.map(t => t.id))
              const isAllSelected = allIds.length > 0 && allIds.every(id => selectedTaskIds.has(id))
              return (
                <button
                  className="inline-flex items-center justify-center gap-1.5 rounded-md px-2.5 h-7 text-xs font-medium border border-border bg-background hover:bg-muted cursor-pointer transition-all"
                  onClick={() => { isAllSelected ? clearSelection() : setSelectedTaskIds(new Set(allIds)) }}
                >
                  {isAllSelected ? <CheckSquare className="h-3 w-3 shrink-0" /> : <Square className="h-3 w-3 shrink-0" />}
                  {isAllSelected ? t('pm.deselectAll', { defaultValue: 'Deselect all' }) : t('pm.selectAll', { defaultValue: 'Select all' })}
                </button>
              )
            })()}
          </div>
          <button className="ml-auto inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground cursor-pointer transition-colors" onClick={clearSelection}>
            <X className="h-3 w-3" />
            {t('pm.clearSelection', { defaultValue: 'Clear' })}
          </button>
        </div>
      )}

      {/* ── Kanban board (UIKit) ── */}
      <KanbanBoard<TaskCardDto>
        columns={kanbanColumns}
        getCardId={(task) => task.id}
        renderCard={renderCard}
        renderColumnHeader={renderColumnHeader}
        onMoveCard={handleMoveCard}
        onReorderColumns={handleReorderColumns}
        renderColumnFooter={renderColumnFooter}
        renderColumnEmpty={renderColumnEmpty}
        renderCollapsedColumn={renderCollapsedColumn}
        renderBoardFooter={renderBoardFooter}
        renderDragOverlay={renderDragOverlay}
        collapsedColumnIds={collapsedColumns}
        onToggleCollapse={toggleColumnCollapse}
        customDragTypes={customDragTypes}
        onCustomDrop={handleCustomDrop}
        filterCards={hasActiveFilters ? filterCards : undefined}
        sortCards={Object.keys(columnSortOverrides).length > 0 ? sortCards : undefined}
        boardMode={boardMode}
        selectedCardIds={selectedTaskIds}
        onLassoSelect={handleLassoSelect}
        isCardDraggable={() => boardMode === 'drag'}
        columnMinWidth={280}
        columnMaxWidth={320}
        columnClassName="bg-muted/40 dark:bg-muted/50 border-border/50 dark:border-border/80"
        fullHeight
      />

      {/* ── Dialogs ── */}
      <ColumnSettingsDialog
        open={columnSettingsId !== null}
        onOpenChange={(open) => { if (!open) setColumnSettingsId(null) }}
        projectId={projectId}
        column={board.columns.find(c => c.id === columnSettingsId) ?? null}
      />

      {/* Delete column confirmation */}
      <Credenza open={deleteColumnId !== null} onOpenChange={(open) => { if (!open) setDeleteColumnId(null) }}>
        <CredenzaContent className="border-destructive/30">
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.deleteColumn', { defaultValue: 'Delete column' })}</CredenzaTitle>
            <CredenzaDescription>{t('pm.deleteColumnDesc', { defaultValue: 'Tasks in this column will be moved to another column.' })}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody className="space-y-3">
            <div>
              <label className="text-sm font-medium">{t('pm.moveTasksTo', { defaultValue: 'Move tasks to' })}</label>
              <Select value={deleteColumnMoveToId} onValueChange={setDeleteColumnMoveToId}>
                <SelectTrigger className="mt-1 cursor-pointer">
                  <SelectValue placeholder={t('pm.selectColumn', { defaultValue: 'Select column' })} />
                </SelectTrigger>
                <SelectContent>
                  {(board?.columns ?? []).filter(c => c.id !== deleteColumnId).map(c => (
                    <SelectItem key={c.id} value={c.id} className="cursor-pointer">{c.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" className="cursor-pointer" onClick={() => setDeleteColumnId(null)}>
              {t('buttons.cancel')}
            </Button>
            <Button
              variant="destructive"
              className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
              disabled={!deleteColumnMoveToId || deleteColumnMutation.isPending}
              onClick={() => {
                if (!deleteColumnId || !deleteColumnMoveToId) return
                deleteColumnMutation.mutate(
                  { projectId, columnId: deleteColumnId, moveToColumnId: deleteColumnMoveToId },
                  {
                    onSuccess: () => setDeleteColumnId(null),
                    onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
                  }
                )
              }}
            >
              {deleteColumnMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              <Trash2 className="h-4 w-4 mr-1.5" />
              {t('pm.deleteColumn', { defaultValue: 'Delete column' })}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>

      {/* Move all cards dialog */}
      <Credenza open={moveAllColumnId !== null} onOpenChange={(open) => { if (!open) setMoveAllColumnId(null) }}>
        <CredenzaContent>
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.moveAllCards', { defaultValue: 'Move all cards to...' })}</CredenzaTitle>
            <CredenzaDescription>{t('pm.moveAllCardsDesc', { defaultValue: 'All cards in this column will be moved to the selected column.' })}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody className="space-y-3">
            <div>
              <label className="text-sm font-medium">{t('pm.targetColumn', { defaultValue: 'Target column' })}</label>
              <Select value={moveAllTargetColumnId} onValueChange={setMoveAllTargetColumnId}>
                <SelectTrigger className="mt-1 cursor-pointer">
                  <SelectValue placeholder={t('pm.selectColumn', { defaultValue: 'Select column' })} />
                </SelectTrigger>
                <SelectContent>
                  {(board?.columns ?? []).filter(c => c.id !== moveAllColumnId).map(c => (
                    <SelectItem key={c.id} value={c.id} className="cursor-pointer">{c.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" className="cursor-pointer" onClick={() => setMoveAllColumnId(null)}>
              {t('buttons.cancel')}
            </Button>
            <Button
              className="cursor-pointer"
              disabled={!moveAllTargetColumnId || moveAllColumnTasksMutation.isPending}
              onClick={() => {
                if (!moveAllColumnId || !moveAllTargetColumnId) return
                moveAllColumnTasksMutation.mutate(
                  { projectId, sourceColumnId: moveAllColumnId, targetColumnId: moveAllTargetColumnId },
                  {
                    onSuccess: () => setMoveAllColumnId(null),
                    onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
                  }
                )
              }}
            >
              {moveAllColumnTasksMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t('pm.moveCards', { defaultValue: 'Move cards' })}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>

      {/* Archive all cards confirmation */}
      <Credenza open={archiveAllColumnId !== null} onOpenChange={(open) => { if (!open) setArchiveAllColumnId(null) }}>
        <CredenzaContent className="border-destructive/30">
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.archiveAllCards', { defaultValue: 'Archive all cards' })}</CredenzaTitle>
            <CredenzaDescription>{t('pm.archiveAllCardsDesc', { defaultValue: 'All cards in this column will be moved to the archive. You can restore them later.' })}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaFooter>
            <Button variant="outline" className="cursor-pointer" onClick={() => setArchiveAllColumnId(null)}>
              {t('buttons.cancel')}
            </Button>
            <Button
              className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
              disabled={bulkArchiveTasksMutation.isPending}
              onClick={() => {
                if (!archiveAllColumnId) return
                const column = board?.columns.find(c => c.id === archiveAllColumnId)
                const taskIds = column?.tasks.map(t => t.id) ?? []
                if (taskIds.length === 0) { setArchiveAllColumnId(null); return }
                bulkArchiveTasksMutation.mutate(taskIds, {
                  onSuccess: () => setArchiveAllColumnId(null),
                  onError: (err) => toast.error(err instanceof Error ? err.message : t('errors.unknown')),
                })
              }}
            >
              {bulkArchiveTasksMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              <Archive className="h-4 w-4 mr-1.5" />
              {t('pm.archiveAll', { defaultValue: 'Archive all' })}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>

      {/* Task detail modal */}
      <TaskDetailModal
        taskId={selectedTaskId}
        open={taskModalOpen}
        onOpenChange={(open) => { if (!open) setSelectedTaskId(null) }}
        projectMembers={members}
        onNavigateToTask={(taskId) => {
          const task = boardColumnsRef.current.flatMap(c => c.tasks).find(t => t.id === taskId)
          if (task) setSelectedTaskId(task.taskNumber)
        }}
        boardColumns={filteredBoardColumns}
        onMoveToColumn={handleModalMoveToColumn}
      />

      {/* Multi-line paste confirmation */}
      <Credenza open={multiPasteLines.length > 0 && multiPasteColumnId !== null} onOpenChange={(open) => { if (!open) { setMultiPasteLines([]); setMultiPasteColumnId(null) } }}>
        <CredenzaContent>
          <CredenzaHeader>
            <CredenzaTitle>{t('pm.createMultipleTasks', { defaultValue: 'Create multiple tasks?' })}</CredenzaTitle>
            <CredenzaDescription>
              {t('pm.createMultipleTasksDesc', { count: multiPasteLines.length, defaultValue: `Create ${multiPasteLines.length} tasks from pasted text?` })}
            </CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody>
            <ul className="space-y-1 max-h-48 overflow-y-auto">
              {multiPasteLines.map((line, i) => (
                <li key={i} className="text-xs bg-muted rounded px-2 py-1 truncate">{line}</li>
              ))}
            </ul>
          </CredenzaBody>
          <CredenzaFooter>
            <Button variant="outline" className="cursor-pointer" onClick={() => { setMultiPasteLines([]); setMultiPasteColumnId(null) }}>
              {t('buttons.cancel')}
            </Button>
            <Button
              className="cursor-pointer bg-green-600 hover:bg-green-700 text-white border-0"
              disabled={createTaskMutation.isPending}
              onClick={async () => {
                if (!multiPasteColumnId) return
                for (const line of multiPasteLines) {
                  await createTaskMutation.mutateAsync({ projectId, title: line, columnId: multiPasteColumnId })
                }
                setMultiPasteLines([])
                setMultiPasteColumnId(null)
              }}
            >
              {createTaskMutation.isPending && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
              {t('pm.createAll', { defaultValue: 'Create all' })}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>
    </div>
  )
}

// ─── ColumnActionsMenu (extracted for readability) ──────────────────────────

const ColumnActionsMenu = ({
  column, collapsedColumns, columnSortOverrides,
  onRenameStart, onSettingsOpen, onSortChange, onToggleCollapse,
  onMoveAll, onArchiveAll, onDuplicate, onDelete, t,
}: {
  column: KanbanColumnDto
  collapsedColumns: Set<string>
  columnSortOverrides: Record<string, string>
  onRenameStart: (col: KanbanColumnDto) => void
  onSettingsOpen: () => void
  onSortChange: (key: 'default' | 'priority' | 'dueDate' | 'alpha') => void
  onToggleCollapse: () => void
  onMoveAll: () => void
  onArchiveAll: () => void
  onDuplicate: () => void
  onDelete: () => void
  t: (key: string, opts?: Record<string, unknown>) => string
}) => (
  <DropdownMenu>
    <Tooltip>
      <TooltipTrigger asChild>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            className="h-6 w-6 cursor-pointer"
            onPointerDown={(e) => e.stopPropagation()}
            onClick={(e) => e.stopPropagation()}
            aria-label={t('pm.columnOptions', { defaultValue: 'Column options' })}
          >
            <EllipsisVertical className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
      </TooltipTrigger>
      <TooltipContent>{t('pm.columnOptions', { defaultValue: 'Column options' })}</TooltipContent>
    </Tooltip>
    <DropdownMenuContent align="end" onClick={(e) => e.stopPropagation()}>
      <DropdownMenuItem className="cursor-pointer gap-2" onClick={() => onRenameStart(column)}>
        <Pencil className="h-3.5 w-3.5" />
        {t('pm.renameColumn', { defaultValue: 'Rename' })}
      </DropdownMenuItem>
      <DropdownMenuItem className="cursor-pointer gap-2" onClick={onSettingsOpen}>
        <EllipsisVertical className="h-3.5 w-3.5" />
        {t('pm.editColumn', { defaultValue: 'Edit column' })}
      </DropdownMenuItem>
      <DropdownMenuSeparator />
      <DropdownMenuSub>
        <DropdownMenuSubTrigger className="cursor-pointer gap-2">
          <ArrowUpDown className="h-3.5 w-3.5" />
          {t('pm.sortCards', { defaultValue: 'Sort by' })}
        </DropdownMenuSubTrigger>
        <DropdownMenuSubContent>
          {([
            { key: 'default', label: t('pm.sortDefault', { defaultValue: 'Default order' }) },
            { key: 'priority', label: t('pm.sortByPriority', { defaultValue: 'Priority' }) },
            { key: 'dueDate', label: t('pm.sortByDueDate', { defaultValue: 'Due date' }) },
            { key: 'alpha', label: t('pm.sortByAlpha', { defaultValue: 'Alphabetical' }) },
          ] as const).map(({ key, label }) => (
            <DropdownMenuItem key={key} className="cursor-pointer gap-2" onClick={() => onSortChange(key)}>
              <Check className={`h-3.5 w-3.5 ${(columnSortOverrides[column.id] ?? 'default') === key ? 'opacity-100' : 'opacity-0'}`} />
              {label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuSubContent>
      </DropdownMenuSub>
      <DropdownMenuItem className="cursor-pointer gap-2" onClick={onToggleCollapse}>
        <ChevronsUpDown className="h-3.5 w-3.5" />
        {collapsedColumns.has(column.id)
          ? t('pm.expandColumn', { defaultValue: 'Expand column' })
          : t('pm.collapseColumn', { defaultValue: 'Collapse column' })
        }
      </DropdownMenuItem>
      <DropdownMenuSeparator />
      <DropdownMenuItem className="cursor-pointer gap-2" onClick={onMoveAll}>
        <ArrowUp className="h-3.5 w-3.5" />
        {t('pm.moveAllCards', { defaultValue: 'Move all cards to...' })}
      </DropdownMenuItem>
      <DropdownMenuItem className="cursor-pointer gap-2" onClick={onArchiveAll}>
        <Archive className="h-3.5 w-3.5" />
        {t('pm.archiveAllCards', { defaultValue: 'Archive all cards' })}
      </DropdownMenuItem>
      <DropdownMenuItem className="cursor-pointer gap-2" onClick={onDuplicate}>
        <Copy className="h-3.5 w-3.5" />
        {t('pm.duplicateColumn', { defaultValue: 'Duplicate column' })}
      </DropdownMenuItem>
      <DropdownMenuSeparator />
      <DropdownMenuItem className="cursor-pointer gap-2 text-destructive focus:text-destructive" onClick={onDelete}>
        <Trash2 className="h-3.5 w-3.5" />
        {t('pm.deleteColumn', { defaultValue: 'Delete column' })}
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
)
