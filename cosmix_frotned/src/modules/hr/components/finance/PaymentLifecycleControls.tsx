import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { payment, paymentLifecycle, type PayrollPayment } from '../../schemas/payroll-accounting';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceAction } from '../../hooks/use-finance-queries';
import {
  paymentOutcomeChoices,
  preparePaymentOutcome,
  type PaymentOutcome,
} from '../../utils/payment-outcomes';
import { ActionMessage } from '../Feedback';
import { TextAreaField, FinanceNotice } from './FinanceChrome';
import { PayrollSelect } from './PayrollFields';
import { labelDate } from '../../utils/messages';
import { money } from './labels';

export function PaymentLifecycleControls({
  record,
  currency,
  onChanged,
}: {
  record: PayrollPayment;
  currency: string;
  onChanged(): void;
}) {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const authority = {
    actorId: access.user?.account?.accountId ?? '',
    canRequest: access.can('payroll.accounting.payment'),
    canConfirm: access.can('payroll.accounting.payment.confirm'),
  };
  const lifecycle = paymentLifecycle.safeParse(record);
  const choices = lifecycle.success ? paymentOutcomeChoices(lifecycle.data, authority) : [];
  const [outcome, setOutcome] = useState<PaymentOutcome | ''>('');
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState('');
  const [postingDate, setPostingDate] = useState('');
  const [prepared, setPrepared] = useState<ReturnType<typeof preparePaymentOutcome> | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [error, setError] = useState('');
  const selected = choices.some((choice) => choice.value === outcome)
    ? outcome
    : (choices[0]?.value ?? '');
  function review() {
    if (!selected || !lifecycle.success) return;
    try {
      setPrepared(
        preparePaymentOutcome(lifecycle.data, authority, selected, reason, evidence, postingDate),
      );
      setSubmitted(false);
      setError('');
    } catch {
      setError(
        'Choose an available action, enter a single-line reason (1–500 characters), receipt or statement evidence (1–8000 characters), and a valid posting date for approval.',
      );
    }
  }
  async function submit() {
    if (!prepared || action.pending) return;
    setSubmitted(true);
    const result = await action.run(
      prepared.path,
      prepared.body,
      'POST',
      payment,
      () => {
        setPrepared(null);
        setSubmitted(false);
      },
      'Payment action recorded. Review the refreshed payment state.',
    );
    if (result) {
      setPrepared(null);
      setSubmitted(false);
      setReason('');
      setEvidence('');
      setPostingDate('');
      onChanged();
    }
  }
  return (
    <article className="payroll-batch-card">
      <h4>
        {record.paymentReference} · {money(record.amount, currency)}
      </h4>
      <p>
        State: {record.state}
        {record.confirmedAt && ` · Confirmed ${labelDate(record.confirmedAt)}`}
        {record.reversedAt && ` · Reversed ${labelDate(record.reversedAt)}`}
      </p>
      {!lifecycle.success && (
        <FinanceNotice>
          This API response does not include the actor and outcome details required for payment
          decisions. Payment history remains available; confirmation and reversal controls require
          the updated backend contract.
        </FinanceNotice>
      )}
      {record.reversalRequestedAt && (
        <FinanceNotice>
          {record.reversalIsFailure === true ? 'Failed-payment' : 'Payment'} reversal requested{' '}
          {labelDate(record.reversalRequestedAt)}. The original state and ledger remain until
          independent approval.
        </FinanceNotice>
      )}
      {choices.length > 0 ? (
        <details>
          <summary>Review payment action</summary>
          <FinanceNotice>
            Confirmation and reversal approval require an independent actor. Evidence is retained
            privately by the server. A reversal posts a new balancing journal and preserves the
            original entry.
          </FinanceNotice>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              review();
            }}
          >
            <fieldset disabled={action.pending || Boolean(prepared)}>
              <PayrollSelect
                label="Payment action"
                value={selected}
                onChange={(value) => setOutcome(value as PaymentOutcome)}
                options={choices}
              />
              <TextAreaField
                label="Reason · maximum 500 characters"
                value={reason}
                onChange={setReason}
                rows={2}
                required
              />
              <TextAreaField
                label="Receipt or statement evidence · maximum 8000 characters"
                value={evidence}
                onChange={setEvidence}
                rows={5}
                required
              />
              {selected === 'approve' && (
                <TextInput
                  type="date"
                  label="Reversal posting date · within an open accounting period"
                  value={postingDate}
                  onChange={(event) => setPostingDate(event.target.value)}
                  required
                />
              )}
              <Button
                type="submit"
                disabled={
                  !reason.trim() || !evidence.trim() || (selected === 'approve' && !postingDate)
                }
              >
                Review action
              </Button>
            </fieldset>
          </form>
          {error && <p role="alert">{error}</p>}
          {prepared && (
            <section aria-label="Payment action review">
              <h4>{prepared.label}</h4>
              <p>
                {money(record.amount, currency)} · {record.paymentReference} · version{' '}
                {prepared.body.expectedVersion}
              </p>
              <p>{prepared.body.reason}</p>
              {'postingDate' in prepared.body && <p>Posting date: {prepared.body.postingDate}</p>}
              <p>The evidence shown above will be retained with this action.</p>
              <Button disabled={action.pending} onClick={() => void submit()}>
                {action.pending
                  ? 'Submitting…'
                  : submitted
                    ? 'Retry unchanged action'
                    : 'Submit reviewed action'}
              </Button>
              {!submitted && (
                <Button variant="secondary" onClick={() => setPrepared(null)}>
                  Edit review
                </Button>
              )}
              {submitted && !action.pending && (
                <FinanceNotice>
                  If the result is uncertain, retry this unchanged action. Keep this view open until
                  the server confirms the outcome.
                </FinanceNotice>
              )}
            </section>
          )}
        </details>
      ) : (
        <p>No payment action is available to your account in this state.</p>
      )}
      <ActionMessage action={action} />
    </article>
  );
}
