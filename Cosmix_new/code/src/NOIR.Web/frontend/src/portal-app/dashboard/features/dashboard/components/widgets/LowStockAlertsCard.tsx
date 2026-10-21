import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import {
  Badge,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  EmptyState,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@uikit'
import { getStatusBadgeClasses } from '@/utils/statusBadge'
import { ViewTransitionLink } from '@/components/navigation/ViewTransitionLink'
import type { LowStockAlertDto } from '@/services/dashboard'

interface LowStockAlertsCardProps {
  alerts: LowStockAlertDto[]
}

export const LowStockAlertsCard = ({ alerts }: LowStockAlertsCardProps) => {
  const { t } = useTranslation('common')

  return (
    <Card className="gap-0 shadow-sm hover:shadow-lg transition-all duration-300 md:col-span-2">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg flex items-center gap-2">
          <AlertTriangle className="h-5 w-5 text-amber-500" />
          {t('dashboard.inventoryAlerts')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {alerts.length === 0 ? (
          <EmptyState
            icon={AlertTriangle}
            title={t('reports.noLowStock', 'All products are well stocked')}
            size="sm"
            className="border-0 rounded-none py-6"
          />
        ) : (
          <div className="rounded-xl border border-border/50 overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('labels.product', 'Product')}</TableHead>
                  <TableHead>{t('labels.sku', 'SKU')}</TableHead>
                  <TableHead className="text-right">{t('dashboard.currentStock')}</TableHead>
                  <TableHead className="text-right">{t('dashboard.threshold')}</TableHead>
                  <TableHead>{t('labels.status', 'Status')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {alerts.slice(0, 5).map((alert, i) => (
                  <TableRow key={`${alert.productId}-${alert.sku ?? i}`} className="transition-colors hover:bg-muted/50">
                    <TableCell>
                      <ViewTransitionLink
                        to={`/portal/ecommerce/products/${alert.productId}`}
                        className="font-medium text-sm hover:underline cursor-pointer"
                      >
                        {alert.productName}
                      </ViewTransitionLink>
                    </TableCell>
                    <TableCell>
                      <span className="font-mono text-xs text-muted-foreground">
                        {alert.sku ?? '-'}
                      </span>
                    </TableCell>
                    <TableCell className="text-right font-medium">{alert.currentStock}</TableCell>
                    <TableCell className="text-right text-muted-foreground">{alert.threshold}</TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={getStatusBadgeClasses(alert.currentStock === 0 ? 'red' : 'yellow')}
                      >
                        {alert.currentStock === 0
                          ? t('products.outOfStock', 'Out of Stock')
                          : t('products.lowStock', 'Low Stock')}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
