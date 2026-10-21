import { useTranslation } from 'react-i18next'
import { ShoppingCart } from 'lucide-react'
import { EmptyState } from '@uikit'
import { useEcommerceDashboard } from '@/hooks/useDashboard'
import { DashboardSkeleton } from './widgets/DashboardSkeleton'
import { RevenueOverviewCard } from './widgets/RevenueOverviewCard'
import { RevenueChart } from './widgets/RevenueChart'
import { OrderMetricsCard } from './widgets/OrderMetricsCard'
import { OrderStatusChart } from './widgets/OrderStatusChart'
import { CustomerMetricsCard } from './widgets/CustomerMetricsCard'
import { ProductPerformanceCard } from './widgets/ProductPerformanceCard'

export const EcommerceWidgetGroup = () => {
  const { t } = useTranslation('common')
  const { data, isLoading, isError } = useEcommerceDashboard()

  if (isLoading) return <DashboardSkeleton count={4} />
  if (isError || !data) {
    return (
      <EmptyState
        icon={ShoppingCart}
        title={t('dashboard.loadError')}
        description={t('dashboard.loadErrorDescription')}
        className="md:col-span-2 xl:col-span-3"
      />
    )
  }

  return (
    <>
      <RevenueOverviewCard revenue={data.revenue} />
      <RevenueChart data={data.salesOverTime} isLoading={false} />
      <OrderMetricsCard orderCounts={data.orderCounts} totalOrders={data.revenue.totalOrders} />
      <OrderStatusChart orderCounts={data.orderCounts} isLoading={false} />
      <CustomerMetricsCard
        totalCustomers={0}
        newCustomers={0}
      />
      <ProductPerformanceCard products={data.topSellingProducts} />
    </>
  )
}
