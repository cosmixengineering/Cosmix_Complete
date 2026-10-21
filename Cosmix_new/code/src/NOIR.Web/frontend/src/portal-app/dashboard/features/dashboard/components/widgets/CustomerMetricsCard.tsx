import { useTranslation } from 'react-i18next'
import { Users } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@uikit'

interface CustomerMetricsCardProps {
  totalCustomers: number
  newCustomers: number
}

export const CustomerMetricsCard = ({ totalCustomers, newCustomers }: CustomerMetricsCardProps) => {
  const { t } = useTranslation('common')

  return (
    <Card className="gap-0 shadow-sm hover:shadow-lg transition-all duration-300">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg flex items-center gap-2">
          <Users className="h-5 w-5 text-primary" />
          {t('dashboard.customerMetrics')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t('dashboard.totalCustomers')}</span>
            <span className="text-lg font-bold">{totalCustomers.toLocaleString()}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t('dashboard.newCustomers')}</span>
            <span className="text-lg font-bold text-emerald-600">{newCustomers.toLocaleString()}</span>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
