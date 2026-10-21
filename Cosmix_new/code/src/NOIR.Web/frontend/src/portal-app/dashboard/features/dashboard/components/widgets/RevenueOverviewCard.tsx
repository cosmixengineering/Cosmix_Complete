import { useTranslation } from 'react-i18next'
import { DollarSign, TrendingUp, CalendarDays, Coins } from 'lucide-react'
import { formatCurrency } from '@/lib/utils/currency'
import { MetricCard } from './MetricCard'
import type { RevenueMetrics } from '@/services/dashboard'

interface RevenueOverviewCardProps {
  revenue: RevenueMetrics
}

export const RevenueOverviewCard = ({ revenue }: RevenueOverviewCardProps) => {
  const { t } = useTranslation('common')

  const monthChange = revenue.revenueLastMonth > 0
    ? ((revenue.revenueThisMonth - revenue.revenueLastMonth) / revenue.revenueLastMonth) * 100
    : 0
  const monthTrend: 'up' | 'down' | 'neutral' = monthChange > 0 ? 'up' : monthChange < 0 ? 'down' : 'neutral'

  return (
    <>
      <MetricCard
        title={t('dashboard.totalRevenue')}
        value={formatCurrency(revenue.totalRevenue)}
        subtitle={t('dashboard.allTime')}
        icon={DollarSign}
        iconColor="text-emerald-600"
        iconBg="bg-emerald-100 dark:bg-emerald-900/30"
      />
      <MetricCard
        title={t('dashboard.thisMonth')}
        value={formatCurrency(revenue.revenueThisMonth)}
        icon={CalendarDays}
        trend={monthTrend}
        trendValue={monthChange !== 0 ? `${monthChange >= 0 ? '+' : ''}${monthChange.toFixed(1)}% ${t('dashboard.vsLastMonth')}` : undefined}
        iconColor="text-blue-600"
        iconBg="bg-blue-100 dark:bg-blue-900/30"
      />
      <MetricCard
        title={t('dashboard.today')}
        value={formatCurrency(revenue.revenueToday)}
        subtitle={`${revenue.ordersToday} ${t('dashboard.orders').toLowerCase()}`}
        icon={Coins}
        iconColor="text-violet-600"
        iconBg="bg-violet-100 dark:bg-violet-900/30"
      />
      <MetricCard
        title={t('dashboard.averageOrderValue')}
        value={formatCurrency(revenue.averageOrderValue)}
        icon={TrendingUp}
        iconColor="text-amber-600"
        iconBg="bg-amber-100 dark:bg-amber-900/30"
      />
    </>
  )
}
