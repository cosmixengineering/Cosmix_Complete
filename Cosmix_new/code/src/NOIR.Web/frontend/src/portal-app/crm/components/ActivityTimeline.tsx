import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Activity,
  Mail,
  MessageSquare,
  Phone,
  Plus,
  Users,
} from 'lucide-react'
import {
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  EmptyState,
  Skeleton,
} from '@uikit'
import { useActivitiesQuery } from '@/portal-app/crm/queries'
import { useRegionalSettings } from '@/contexts/RegionalSettingsContext'
import { usePermissions, Permissions } from '@/hooks/usePermissions'
import type { ActivityType } from '@/types/crm'
import { ActivityDialog } from './ActivityDialog'

const activityIcons: Record<ActivityType, React.ElementType> = {
  Call: Phone,
  Email: Mail,
  Meeting: Users,
  Note: MessageSquare,
}

const activityColors: Record<ActivityType, string> = {
  Call: 'bg-blue-100 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400',
  Email: 'bg-purple-100 text-purple-600 dark:bg-purple-900/30 dark:text-purple-400',
  Meeting: 'bg-amber-100 text-amber-600 dark:bg-amber-900/30 dark:text-amber-400',
  Note: 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400',
}

interface ActivityTimelineProps {
  contactId?: string
  leadId?: string
}

export const ActivityTimeline = ({ contactId, leadId }: ActivityTimelineProps) => {
  const { t } = useTranslation('common')
  const { formatDateTime } = useRegionalSettings()
  const { hasPermission } = usePermissions()
  const canCreate = hasPermission(Permissions.CrmActivitiesCreate)

  const [showDialog, setShowDialog] = useState(false)

  const { data: activitiesResponse, isLoading } = useActivitiesQuery({
    contactId,
    leadId,
    pageSize: 50,
  })
  const activities = activitiesResponse?.items ?? []

  if (isLoading) {
    return (
      <Card className="shadow-sm">
        <CardHeader>
          <Skeleton className="h-5 w-32" />
        </CardHeader>
        <CardContent className="space-y-4">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="flex gap-3">
              <Skeleton className="h-8 w-8 rounded-full" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-48" />
                <Skeleton className="h-3 w-32" />
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="shadow-sm">
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="text-base">{t('crm.activities.title')}</CardTitle>
        {canCreate && (
          <Button size="sm" variant="outline" onClick={() => setShowDialog(true)} className="cursor-pointer">
            <Plus className="h-4 w-4 mr-1" />
            {t('crm.activities.log')}
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {activities.length === 0 ? (
          <EmptyState
            icon={Activity}
            title={t('crm.activities.noActivities')}
            description={t('crm.activities.noActivitiesDescription')}
            action={canCreate ? {
              label: t('crm.activities.log'),
              onClick: () => setShowDialog(true),
            } : undefined}
            className="border-0 py-8"
          />
        ) : (
          <div className="relative">
            <div className="absolute left-4 top-0 bottom-0 w-px bg-border" />
            <div className="space-y-6">
              {activities.map((activity) => {
                const Icon = activityIcons[activity.type] || Activity
                const colorClass = activityColors[activity.type] || activityColors.Note

                return (
                  <div key={activity.id} className="relative flex gap-4 pl-0">
                    <div className={`relative z-10 flex h-8 w-8 items-center justify-center rounded-full ${colorClass}`}>
                      <Icon className="h-4 w-4" />
                    </div>
                    <div className="flex-1 min-w-0 pt-0.5">
                      <div className="flex items-center justify-between gap-2">
                        <p className="text-sm font-medium">{activity.subject}</p>
                        <span className="text-xs text-muted-foreground whitespace-nowrap">
                          {formatDateTime(activity.performedAt)}
                        </span>
                      </div>
                      {activity.description && (
                        <p className="text-sm text-muted-foreground mt-1">{activity.description}</p>
                      )}
                      <div className="flex items-center gap-2 mt-1">
                        <span className="text-xs text-muted-foreground">
                          {activity.performedByName}
                        </span>
                        {activity.durationMinutes != null && (
                          <span className="text-xs text-muted-foreground">
                            &middot; {activity.durationMinutes} min
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>
        )}
      </CardContent>

      <ActivityDialog
        open={showDialog}
        onOpenChange={setShowDialog}
        contactId={contactId}
        leadId={leadId}
      />
    </Card>
  )
}
