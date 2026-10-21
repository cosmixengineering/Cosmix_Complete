import { useTranslation } from 'react-i18next'
import { FileText } from 'lucide-react'
import { EmptyState } from '@uikit'
import { useBlogDashboard } from '@/hooks/useDashboard'
import { DashboardSkeleton } from './widgets/DashboardSkeleton'
import { BlogStatsCard } from './widgets/BlogStatsCard'
import { PublishingTrendChart } from './widgets/PublishingTrendChart'

export const BlogWidgetGroup = () => {
  const { t } = useTranslation('common')
  const { data, isLoading, isError } = useBlogDashboard()

  if (isLoading) return <DashboardSkeleton count={2} />
  if (isError || !data) {
    return (
      <EmptyState
        icon={FileText}
        title={t('dashboard.loadError')}
        description={t('dashboard.loadErrorDescription')}
        className="md:col-span-2 xl:col-span-3"
      />
    )
  }

  return (
    <>
      <BlogStatsCard data={data} />
      <PublishingTrendChart data={data.publishingTrend} isLoading={false} />
    </>
  )
}
