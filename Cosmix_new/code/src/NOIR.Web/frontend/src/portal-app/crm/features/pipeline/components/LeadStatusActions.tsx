import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Loader2, RotateCcw, Trophy, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import {
  Button,
  Credenza,
  CredenzaBody,
  CredenzaContent,
  CredenzaDescription,
  CredenzaFooter,
  CredenzaHeader,
  CredenzaTitle,
  Textarea,
} from '@uikit'
import type { LeadStatus } from '@/types/crm'
import { useWinLead, useLoseLead, useReopenLead } from '@/portal-app/crm/queries'

interface LeadStatusActionsProps {
  leadId: string
  status: LeadStatus
}

export const LeadStatusActions = ({ leadId, status }: LeadStatusActionsProps) => {
  const { t } = useTranslation('common')
  const winMutation = useWinLead()
  const loseMutation = useLoseLead()
  const reopenMutation = useReopenLead()

  const [showWinConfirm, setShowWinConfirm] = useState(false)
  const [showLoseConfirm, setShowLoseConfirm] = useState(false)
  const [showReopenConfirm, setShowReopenConfirm] = useState(false)
  const [lostReason, setLostReason] = useState('')

  const handleWin = async () => {
    try {
      await winMutation.mutateAsync(leadId)
      toast.success(t('crm.statuses.Won'))
      setShowWinConfirm(false)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : t('errors.unknown'))
    }
  }

  const handleLose = async () => {
    try {
      await loseMutation.mutateAsync({ id: leadId, reason: lostReason || undefined })
      toast.success(t('crm.statuses.Lost'))
      setShowLoseConfirm(false)
      setLostReason('')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : t('errors.unknown'))
    }
  }

  const handleReopen = async () => {
    try {
      await reopenMutation.mutateAsync(leadId)
      toast.success(t('crm.statuses.Active'))
      setShowReopenConfirm(false)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : t('errors.unknown'))
    }
  }

  if (status === 'Active') {
    return (
      <>
        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() => setShowWinConfirm(true)}
            className="cursor-pointer text-green-600 border-green-200 hover:bg-green-50 dark:text-green-400 dark:border-green-800 dark:hover:bg-green-950"
          >
            <Trophy className="h-4 w-4 mr-2" />
            {t('crm.leads.win')}
          </Button>
          <Button
            variant="outline"
            onClick={() => setShowLoseConfirm(true)}
            className="cursor-pointer text-red-600 border-red-200 hover:bg-red-50 dark:text-red-400 dark:border-red-800 dark:hover:bg-red-950"
          >
            <XCircle className="h-4 w-4 mr-2" />
            {t('crm.leads.lose')}
          </Button>
        </div>

        {/* Win Confirmation */}
        <Credenza open={showWinConfirm} onOpenChange={setShowWinConfirm}>
          <CredenzaContent>
            <CredenzaHeader>
              <CredenzaTitle>{t('crm.leads.confirmWin')}</CredenzaTitle>
              <CredenzaDescription>{t('crm.leads.confirmWinDescription')}</CredenzaDescription>
            </CredenzaHeader>
            <CredenzaBody />
            <CredenzaFooter>
              <Button variant="outline" onClick={() => setShowWinConfirm(false)} disabled={winMutation.isPending} className="cursor-pointer">
                {t('labels.cancel')}
              </Button>
              <Button onClick={handleWin} disabled={winMutation.isPending} className="cursor-pointer bg-green-600 hover:bg-green-700 text-white">
                {winMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {t('crm.leads.win')}
              </Button>
            </CredenzaFooter>
          </CredenzaContent>
        </Credenza>

        {/* Lose Confirmation */}
        <Credenza open={showLoseConfirm} onOpenChange={setShowLoseConfirm}>
          <CredenzaContent className="border-destructive/30">
            <CredenzaHeader>
              <CredenzaTitle>{t('crm.leads.confirmLose')}</CredenzaTitle>
              <CredenzaDescription>{t('crm.leads.confirmLoseDescription')}</CredenzaDescription>
            </CredenzaHeader>
            <CredenzaBody>
              <Textarea
                value={lostReason}
                onChange={(e) => setLostReason(e.target.value)}
                placeholder={t('crm.leads.lostReasonPlaceholder')}
                rows={3}
              />
            </CredenzaBody>
            <CredenzaFooter>
              <Button variant="outline" onClick={() => setShowLoseConfirm(false)} disabled={loseMutation.isPending} className="cursor-pointer">
                {t('labels.cancel')}
              </Button>
              <Button
                variant="destructive"
                onClick={handleLose}
                disabled={loseMutation.isPending}
                className="cursor-pointer bg-destructive/10 text-destructive border border-destructive/30 hover:bg-destructive hover:text-destructive-foreground transition-colors"
              >
                {loseMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {t('crm.leads.lose')}
              </Button>
            </CredenzaFooter>
          </CredenzaContent>
        </Credenza>
      </>
    )
  }

  return (
    <>
      <Button
        variant="outline"
        onClick={() => setShowReopenConfirm(true)}
        className="cursor-pointer"
      >
        <RotateCcw className="h-4 w-4 mr-2" />
        {t('crm.leads.reopen')}
      </Button>

      <Credenza open={showReopenConfirm} onOpenChange={setShowReopenConfirm}>
        <CredenzaContent>
          <CredenzaHeader>
            <CredenzaTitle>{t('crm.leads.confirmReopen')}</CredenzaTitle>
            <CredenzaDescription>{t('crm.leads.confirmReopenDescription')}</CredenzaDescription>
          </CredenzaHeader>
          <CredenzaBody />
          <CredenzaFooter>
            <Button variant="outline" onClick={() => setShowReopenConfirm(false)} disabled={reopenMutation.isPending} className="cursor-pointer">
              {t('labels.cancel')}
            </Button>
            <Button onClick={handleReopen} disabled={reopenMutation.isPending} className="cursor-pointer">
              {reopenMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t('crm.leads.reopen')}
            </Button>
          </CredenzaFooter>
        </CredenzaContent>
      </Credenza>
    </>
  )
}
