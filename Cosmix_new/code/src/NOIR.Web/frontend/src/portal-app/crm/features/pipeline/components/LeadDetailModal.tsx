import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  Building2,
  Calendar,
  Clock,
  Contact,
  DollarSign,
  ExternalLink,
  Kanban,
  StickyNote,
  User,
} from 'lucide-react'
import {
  Badge,
  Button,
  Credenza,
  CredenzaBody,
  CredenzaContent,
  CredenzaFooter,
  CredenzaHeader,
  CredenzaTitle,
  CredenzaDescription,
  Separator,
  Skeleton,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@uikit'
import { useLeadQuery } from '@/portal-app/crm/queries'
import { useRegionalSettings } from '@/contexts/RegionalSettingsContext'
import { getStatusBadgeClasses } from '@/utils/statusBadge'
import { LeadStatusActions } from './LeadStatusActions'
import { ActivityTimeline } from '../../../components/ActivityTimeline'

interface LeadDetailModalProps {
  leadId: string | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

const statusColorMap: Record<string, 'blue' | 'green' | 'red' | 'gray'> = {
  Active: 'blue',
  Won: 'green',
  Lost: 'red',
}

export const LeadDetailModal = ({ leadId, open, onOpenChange }: LeadDetailModalProps) => {
  const { t } = useTranslation('common')
  const navigate = useNavigate()
  const { formatDateTime, formatDate } = useRegionalSettings()
  const [activeTab, setActiveTab] = useState('overview')

  const { data: lead, isLoading } = useLeadQuery(open ? leadId ?? undefined : undefined)

  const formattedValue = lead
    ? new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency: lead.currency || 'USD',
        maximumFractionDigits: 0,
      }).format(lead.value)
    : null

  return (
    <Credenza open={open} onOpenChange={onOpenChange}>
      <CredenzaContent className="max-w-2xl">
        {isLoading ? (
          <>
            <CredenzaHeader>
              <Skeleton className="h-6 w-48" />
              <Skeleton className="h-4 w-32 mt-1" />
            </CredenzaHeader>
            <CredenzaBody>
              <div className="space-y-4">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-4 w-1/2" />
                <Skeleton className="h-32 w-full" />
              </div>
            </CredenzaBody>
          </>
        ) : !lead ? (
          <>
            <CredenzaHeader>
              <CredenzaTitle>{t('errors.notFound', 'Not Found')}</CredenzaTitle>
              <CredenzaDescription>{t('errors.resourceNotFound', 'The requested resource was not found.')}</CredenzaDescription>
            </CredenzaHeader>
            <CredenzaFooter>
              <Button variant="outline" className="cursor-pointer" onClick={() => onOpenChange(false)}>
                {t('buttons.close', 'Close')}
              </Button>
            </CredenzaFooter>
          </>
        ) : (
          <>
            <CredenzaHeader>
              <div className="flex items-center gap-2 flex-wrap">
                <CredenzaTitle className="text-lg">{lead.title}</CredenzaTitle>
                <Badge variant="outline" className={getStatusBadgeClasses(statusColorMap[lead.status] || 'gray')}>
                  {t(`crm.statuses.${lead.status}`, lead.status)}
                </Badge>
                <Badge variant="outline" className="gap-1.5">
                  <span
                    className="h-2.5 w-2.5 rounded-full shrink-0"
                    style={{ backgroundColor: lead.stageColor }}
                  />
                  {lead.stageName}
                </Badge>
              </div>
              <CredenzaDescription>
                {lead.pipelineName}
              </CredenzaDescription>
            </CredenzaHeader>

            <CredenzaBody>
              <Tabs value={activeTab} onValueChange={setActiveTab}>
                <TabsList>
                  <TabsTrigger value="overview" className="cursor-pointer">
                    {t('labels.overview', 'Overview')}
                  </TabsTrigger>
                  <TabsTrigger value="activities" className="cursor-pointer">
                    {t('crm.activities.title', 'Activities')}
                  </TabsTrigger>
                </TabsList>

                <TabsContent value="overview" className="mt-4 space-y-4">
                  {/* Info Grid */}
                  <div className="grid grid-cols-2 gap-x-6 gap-y-3">
                    {/* Value */}
                    <div className="flex items-center gap-2">
                      <DollarSign className="h-4 w-4 text-muted-foreground shrink-0" />
                      <span className="text-sm text-muted-foreground">{t('crm.leads.value', 'Value')}</span>
                    </div>
                    <span className="text-sm font-semibold">{formattedValue}</span>

                    {/* Contact */}
                    <div className="flex items-center gap-2">
                      <Contact className="h-4 w-4 text-muted-foreground shrink-0" />
                      <span className="text-sm text-muted-foreground">{t('crm.leads.contact', 'Contact')}</span>
                    </div>
                    <Button
                      variant="link"
                      className="p-0 h-auto text-sm justify-start cursor-pointer"
                      onClick={() => {
                        onOpenChange(false)
                        navigate(`/portal/crm/contacts/${lead.contactId}`)
                      }}
                    >
                      {lead.contactName}
                      <ExternalLink className="h-3 w-3 ml-1" />
                    </Button>

                    {/* Company */}
                    <div className="flex items-center gap-2">
                      <Building2 className="h-4 w-4 text-muted-foreground shrink-0" />
                      <span className="text-sm text-muted-foreground">{t('crm.leads.company', 'Company')}</span>
                    </div>
                    {lead.companyName && lead.companyId ? (
                      <Button
                        variant="link"
                        className="p-0 h-auto text-sm justify-start cursor-pointer"
                        onClick={() => {
                          onOpenChange(false)
                          navigate(`/portal/crm/companies/${lead.companyId}`)
                        }}
                      >
                        {lead.companyName}
                        <ExternalLink className="h-3 w-3 ml-1" />
                      </Button>
                    ) : (
                      <span className="text-sm text-muted-foreground">&mdash;</span>
                    )}

                    {/* Owner */}
                    <div className="flex items-center gap-2">
                      <User className="h-4 w-4 text-muted-foreground shrink-0" />
                      <span className="text-sm text-muted-foreground">{t('crm.leads.owner', 'Owner')}</span>
                    </div>
                    <span className="text-sm">{lead.ownerName || <span className="text-muted-foreground">&mdash;</span>}</span>

                    {/* Stage */}
                    <div className="flex items-center gap-2">
                      <Kanban className="h-4 w-4 text-muted-foreground shrink-0" />
                      <span className="text-sm text-muted-foreground">{t('crm.leads.stage', 'Stage')}</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <span
                        className="h-2.5 w-2.5 rounded-full shrink-0"
                        style={{ backgroundColor: lead.stageColor }}
                      />
                      <span className="text-sm">{lead.stageName}</span>
                    </div>

                    {/* Expected Close Date */}
                    {lead.expectedCloseDate && (
                      <>
                        <div className="flex items-center gap-2">
                          <Calendar className="h-4 w-4 text-muted-foreground shrink-0" />
                          <span className="text-sm text-muted-foreground">{t('crm.leads.expectedCloseDate', 'Expected Close')}</span>
                        </div>
                        <span className="text-sm">{formatDate(lead.expectedCloseDate)}</span>
                      </>
                    )}

                    {/* Won At */}
                    {lead.wonAt && (
                      <>
                        <div className="flex items-center gap-2">
                          <Clock className="h-4 w-4 text-green-600 dark:text-green-400 shrink-0" />
                          <span className="text-sm text-green-600 dark:text-green-400">{t('crm.leads.wonAt', 'Won At')}</span>
                        </div>
                        <span className="text-sm text-green-600 dark:text-green-400">{formatDateTime(lead.wonAt)}</span>
                      </>
                    )}

                    {/* Lost At */}
                    {lead.lostAt && (
                      <>
                        <div className="flex items-center gap-2">
                          <Clock className="h-4 w-4 text-red-600 dark:text-red-400 shrink-0" />
                          <span className="text-sm text-red-600 dark:text-red-400">{t('crm.leads.lostAt', 'Lost At')}</span>
                        </div>
                        <span className="text-sm text-red-600 dark:text-red-400">{formatDateTime(lead.lostAt)}</span>
                      </>
                    )}

                    {/* Lost Reason */}
                    {lead.lostReason && (
                      <>
                        <div className="flex items-center gap-2">
                          <StickyNote className="h-4 w-4 text-red-600 dark:text-red-400 shrink-0" />
                          <span className="text-sm text-red-600 dark:text-red-400">{t('crm.leads.lostReason', 'Lost Reason')}</span>
                        </div>
                        <span className="text-sm text-red-600 dark:text-red-400">{lead.lostReason}</span>
                      </>
                    )}
                  </div>

                  {/* Notes */}
                  {lead.notes && (
                    <>
                      <Separator />
                      <div className="space-y-1.5">
                        <h4 className="text-sm font-medium">{t('crm.leads.notes', 'Notes')}</h4>
                        <p className="text-sm text-muted-foreground whitespace-pre-wrap">{lead.notes}</p>
                      </div>
                    </>
                  )}

                  {/* Timestamps */}
                  <Separator />
                  <div className="flex gap-6 text-xs text-muted-foreground">
                    <span>{t('labels.created', 'Created')}: {formatDateTime(lead.createdAt)}</span>
                    {lead.modifiedAt && (
                      <span>{t('labels.modified', 'Modified')}: {formatDateTime(lead.modifiedAt)}</span>
                    )}
                  </div>
                </TabsContent>

                <TabsContent value="activities" className="mt-4">
                  <ActivityTimeline leadId={leadId ?? undefined} />
                </TabsContent>
              </Tabs>
            </CredenzaBody>

            <CredenzaFooter>
              <div className="flex w-full items-center justify-between">
                <LeadStatusActions leadId={lead.id} status={lead.status} />
                <div className="flex items-center gap-2">
                  <Button variant="outline" className="cursor-pointer" onClick={() => onOpenChange(false)}>
                    {t('buttons.close', 'Close')}
                  </Button>
                  <Button
                    variant="outline"
                    className="cursor-pointer"
                    onClick={() => {
                      onOpenChange(false)
                      navigate(`/portal/crm/pipeline/deals/${lead.id}`)
                    }}
                  >
                    {t('labels.viewDetail', 'View Detail')}
                    <ExternalLink className="h-4 w-4 ml-1.5" />
                  </Button>
                </div>
              </div>
            </CredenzaFooter>
          </>
        )}
      </CredenzaContent>
    </Credenza>
  )
}
