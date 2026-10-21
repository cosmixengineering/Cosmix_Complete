import { useTranslation } from 'react-i18next'
import { ShoppingCart } from 'lucide-react'
import { Badge, Card, CardContent, CardHeader, CardTitle } from '@uikit'
import { getStatusBadgeClasses } from '@/utils/statusBadge'
import type { OrderCounts } from '@/services/dashboard'

interface OrderMetricsCardProps {
  orderCounts: OrderCounts
  totalOrders: number
}

const STATUS_COLORS: Record<string, Parameters<typeof getStatusBadgeClasses>[0]> = {
  pending: 'yellow',
  confirmed: 'blue',
  processing: 'blue',
  shipped: 'purple',
  delivered: 'green',
  completed: 'green',
  cancelled: 'red',
  refunded: 'gray',
  returned: 'gray',
}

export const OrderMetricsCard = ({ orderCounts, totalOrders }: OrderMetricsCardProps) => {
  const { t } = useTranslation('common')

  const statuses = Object.entries(orderCounts).filter(([, count]) => count > 0)

  return (
    <Card className="gap-0 shadow-sm hover:shadow-lg transition-all duration-300">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg flex items-center gap-2">
          <ShoppingCart className="h-5 w-5 text-primary" />
          {t('dashboard.orderMetrics')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-bold mb-3">{totalOrders.toLocaleString()}</p>
        <div className="flex flex-wrap gap-2">
          {statuses.map(([status, count]) => (
            <Badge
              key={status}
              variant="outline"
              className={getStatusBadgeClasses(STATUS_COLORS[status] ?? 'gray')}
            >
              {t(`orders.status.${status}`, status)} ({count})
            </Badge>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
