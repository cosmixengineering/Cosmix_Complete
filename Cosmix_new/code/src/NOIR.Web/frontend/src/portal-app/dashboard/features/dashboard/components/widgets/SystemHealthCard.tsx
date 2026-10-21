import { useTranslation } from 'react-i18next'
import { Activity, CheckCircle, XCircle } from 'lucide-react'
import { Badge, Card, CardContent, CardHeader, CardTitle } from '@uikit'
import { getStatusBadgeClasses } from '@/utils/statusBadge'
import type { SystemHealthDto } from '@/services/dashboard'

interface SystemHealthCardProps {
  health: SystemHealthDto
}

export const SystemHealthCard = ({ health }: SystemHealthCardProps) => {
  const { t } = useTranslation('common')

  return (
    <Card className="gap-0 shadow-sm hover:shadow-lg transition-all duration-300">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg flex items-center gap-2">
          <Activity className="h-5 w-5 text-primary" />
          {t('dashboard.systemHealth')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t('dashboard.apiStatus')}</span>
            <Badge variant="outline" className={getStatusBadgeClasses(health.apiHealthy ? 'green' : 'red')}>
              {health.apiHealthy ? (
                <><CheckCircle className="h-3 w-3 mr-1" />{t('dashboard.healthy')}</>
              ) : (
                <><XCircle className="h-3 w-3 mr-1" />{t('dashboard.unhealthy')}</>
              )}
            </Badge>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t('dashboard.jobsQueued')}</span>
            <span className="text-sm font-medium">{health.backgroundJobsQueued}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t('dashboard.jobsFailed')}</span>
            <span className="text-sm font-medium">{health.backgroundJobsFailed}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t('dashboard.activeTenants')}</span>
            <span className="text-sm font-medium">{health.activeTenants}</span>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
