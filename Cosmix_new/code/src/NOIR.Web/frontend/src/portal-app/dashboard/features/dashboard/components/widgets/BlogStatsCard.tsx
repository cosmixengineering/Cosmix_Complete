import { useTranslation } from 'react-i18next'
import { FileText, Send, Archive, MessageSquare } from 'lucide-react'
import { MetricCard } from './MetricCard'
import type { BlogDashboardDto } from '@/services/dashboard'

interface BlogStatsCardProps {
  data: BlogDashboardDto
}

export const BlogStatsCard = ({ data }: BlogStatsCardProps) => {
  const { t } = useTranslation('common')

  return (
    <>
      <MetricCard
        title={t('dashboard.publishedPosts')}
        value={data.publishedPosts.toLocaleString()}
        subtitle={`${data.totalPosts} ${t('dashboard.allTime').toLowerCase()}`}
        icon={Send}
        iconColor="text-emerald-600"
        iconBg="bg-emerald-100 dark:bg-emerald-900/30"
      />
      <MetricCard
        title={t('dashboard.draftPosts')}
        value={data.draftPosts.toLocaleString()}
        icon={FileText}
        iconColor="text-amber-600"
        iconBg="bg-amber-100 dark:bg-amber-900/30"
      />
      <MetricCard
        title={t('dashboard.archivedPosts')}
        value={data.archivedPosts.toLocaleString()}
        icon={Archive}
        iconColor="text-gray-600"
        iconBg="bg-gray-100 dark:bg-gray-900/30"
      />
      <MetricCard
        title={t('dashboard.pendingComments', 'Pending Comments')}
        value={data.pendingComments.toLocaleString()}
        icon={MessageSquare}
        iconColor="text-blue-600"
        iconBg="bg-blue-100 dark:bg-blue-900/30"
      />
    </>
  )
}
