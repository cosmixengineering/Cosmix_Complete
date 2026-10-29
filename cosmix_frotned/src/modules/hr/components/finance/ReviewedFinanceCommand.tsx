import { useState, type ReactNode } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { useFinanceAction } from '../../hooks/use-finance-queries';
import { ActionMessage } from '../Feedback';
import { FinanceNotice } from './FinanceChrome';

type Intent = { path: string; body: unknown; summary: string };
export function ReviewedFinanceCommand<T>({
  label,
  schema,
  prepare,
  onSaved,
  children,
  disabled = false,
}: {
  label: string;
  schema: z.ZodType<T>;
  prepare(): Intent;
  onSaved(result: T): void;
  children: (locked: boolean) => ReactNode;
  disabled?: boolean;
}) {
  const action = useFinanceAction();
  const [intent, setIntent] = useState<Intent | null>(null);
  const [attempted, setAttempted] = useState(false);
  const [error, setError] = useState('');
  function review() {
    if (disabled) return;
    try {
      setIntent(prepare());
      setAttempted(false);
      setError('');
    } catch {
      setError('Check all required values, dates, bank mappings and evidence before reviewing.');
    }
  }
  async function submit() {
    if (!intent || action.pending || disabled) return;
    setAttempted(true);
    const result = await action.run(intent.path, intent.body, 'POST', schema, () => {
      setIntent(null);
      setAttempted(false);
    });
    if (result) {
      setIntent(null);
      setAttempted(false);
      onSaved(result);
    }
  }
  return (
    <>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          review();
        }}
      >
        <fieldset disabled={disabled || action.pending || Boolean(intent)}>
          {children(disabled || action.pending || Boolean(intent))}
          <Button type="submit">Review {label.toLowerCase()}</Button>
        </fieldset>
      </form>
      {error && <p role="alert">{error}</p>}
      {intent && (
        <section aria-label={`${label} review`}>
          <h4>{label}</h4>
          <p>{intent.summary}</p>
          <Button disabled={disabled || action.pending} onClick={() => void submit()}>
            {action.pending
              ? 'Submitting…'
              : attempted
                ? 'Retry unchanged action'
                : 'Submit reviewed action'}
          </Button>
          {!attempted && (
            <Button variant="secondary" onClick={() => setIntent(null)}>
              Edit review
            </Button>
          )}
          {attempted && !action.pending && (
            <FinanceNotice>
              Keep this view open and retry the unchanged action if its outcome is uncertain.
            </FinanceNotice>
          )}
        </section>
      )}
      <ActionMessage action={action} />
    </>
  );
}
