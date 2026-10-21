import { useCoreDashboard } from '@/hooks/useDashboard'
import { useAuthContext } from '@/contexts/AuthContext'
import { isPlatformAdmin } from '@/lib/roles'
import { DashboardSkeleton } from './widgets/DashboardSkeleton'
import { WelcomeCard } from './widgets/WelcomeCard'
import { QuickActionsCard } from './widgets/QuickActionsCard'
import { ActivityFeed } from './widgets/ActivityFeed'
import { SystemHealthCard } from './widgets/SystemHealthCard'

export const CoreWidgetGroup = () => {
  const { data, isLoading } = useCoreDashboard()
  const { user } = useAuthContext()

  if (isLoading) return <DashboardSkeleton count={3} />
  if (!data) return null

  return (
    <>
      <WelcomeCard user={user} />
      <QuickActionsCard counts={data.quickActions} />
      <ActivityFeed items={data.recentActivity} />
      {data.systemHealth && isPlatformAdmin(user?.roles) && (
        <SystemHealthCard health={data.systemHealth} />
      )}
    </>
  )
}
