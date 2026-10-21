interface StageColumnHeaderProps {
  name: string
  color: string
  totalValue: number
  leadCount: number
  currency?: string
}

export const StageColumnHeader = ({ name, color, totalValue, leadCount, currency = 'USD' }: StageColumnHeaderProps) => {
  const formattedValue = new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  }).format(totalValue)

  return (
    <>
      <div className="h-1 rounded-t-lg" style={{ backgroundColor: color }} />
      <div className="flex items-center justify-between px-3 py-2.5 border-b border-border/50">
        <div className="flex items-center gap-1.5 min-w-0 flex-1">
          <span className="h-2.5 w-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: color }} />
          <h3 className="text-sm font-semibold leading-5 truncate">{name}</h3>
          <span className="inline-flex items-center justify-center h-5 min-w-5 px-1.5 text-xs font-medium rounded-full bg-muted text-muted-foreground tabular-nums flex-shrink-0">
            {leadCount}
          </span>
        </div>
        <span className="text-xs font-medium text-muted-foreground flex-shrink-0 ml-2">{formattedValue}</span>
      </div>
    </>
  )
}
