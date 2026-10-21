import { useEffect, useState, type PropsWithChildren, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Check, RefreshCw, Save, X, type LucideIcon } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useDirtyForm } from '@/hooks/use-dirty-form';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceAction } from '../hooks/use-attendance-action';
import { attendanceError } from '../utils/messages';
import { freezeBody } from '../utils/roster-intent';
import '@/styles/attendance.css';

export function Command({
  icon: Icon,
  children,
  title,
  ...props
}: React.ComponentProps<typeof Button> & { icon: LucideIcon }) {
  return (
    <Button {...props} title={title ?? (typeof children === 'string' ? children : undefined)}>
      <Icon size={16} aria-hidden="true" />
      {children}
    </Button>
  );
}
export function PrivateWorkspace({
  children,
  resource = '',
}: PropsWithChildren<{ resource?: string }>) {
  const access = useAttendanceAccess();
  const cache = useQueryClient();
  const [epoch, setEpoch] = useState(0);
  const [session, setSession] = useState({ user: access.user, revision: 0 });
  if (session.user !== access.user)
    setSession({ user: access.user, revision: session.revision + 1 });
  useEffect(
    () =>
      cache.getQueryCache().subscribe((event) => {
        if (
          event.type === 'removed' &&
          event.query.queryKey[0] === 'attendance' &&
          event.query.getObserversCount() > 0
        )
          setEpoch((value) => value + 1);
      }),
    [cache],
  );
  if (!access.ready)
    return <p role="status">The authenticated scheduling session is unavailable.</p>;
  return (
    <div className="attendance" key={`${access.subject}:${session.revision}:${resource}:${epoch}`}>
      {children}
    </div>
  );
}
export function QueryState({
  query,
  empty = false,
  emptyText = 'No records found.',
}: {
  query: { isPending: boolean; isFetching: boolean; isError: boolean; error: unknown };
  empty?: boolean;
  emptyText?: string;
}) {
  if (query.isError)
    return (
      <p className="attendance-error" role="alert">
        {attendanceError(query.error)}
      </p>
    );
  if (query.isPending) return <p role="status">Loading records...</p>;
  if (empty) return <p className="attendance-empty">{emptyText}</p>;
  return query.isFetching ? <p role="status">Refreshing records...</p> : null;
}
export function Denied({
  children = 'You do not have access to this scheduling resource.',
}: {
  children?: ReactNode;
}) {
  return (
    <p className="attendance-empty" role="status">
      {children}
    </p>
  );
}
export type WriteIntent = {
  path: string;
  method: 'POST' | 'PUT';
  body: unknown;
  schema: z.ZodType;
};
export function MutationForm({
  title,
  children,
  build,
  onSaved,
  onCancel,
  submitLabel = 'Save',
  disabled = false,
}: PropsWithChildren<{
  title: string;
  build: () => WriteIntent;
  onSaved: (result: unknown) => void;
  onCancel: () => void;
  submitLabel?: string;
  disabled?: boolean;
}>) {
  const action = useAttendanceAction();
  const [snapshot, setSnapshot] = useState<WriteIntent | null>(null);
  const [validation, setValidation] = useState('');
  const [saved, setSaved] = useState(false);
  useDirtyForm(!saved);
  const failed = Boolean(action.error) && !action.uncertain;
  const locked = action.pending || action.uncertain || failed;
  async function submit() {
    if (action.pending || failed || (disabled && !action.uncertain)) return;
    try {
      const intent = action.uncertain && snapshot ? snapshot : build();
      const frozen = { ...intent, body: freezeBody(intent.body) };
      setSnapshot(frozen);
      setValidation('');
      const result = await action.run(frozen.path, frozen.body, frozen.method, frozen.schema);
      if (result !== undefined) {
        setSaved(true);
        onSaved(result);
      }
    } catch (error) {
      setValidation(
        error instanceof z.ZodError
          ? error.issues.map((issue) => `${issue.path.join('.')}: ${issue.message}`).join('; ')
          : error instanceof Error
            ? error.message
            : 'Check the form values.',
      );
    }
  }
  function close() {
    if (action.pending) return;
    const prompt = action.uncertain
      ? 'The result is uncertain. Discard this retry and reload to check the server record?'
      : 'Discard this unsaved form?';
    if (window.confirm(prompt)) onCancel();
  }
  return (
    <section className="attendance-editor" aria-label={title}>
      <h2>{title}</h2>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <fieldset disabled={locked}>{children}</fieldset>
        {validation && (
          <p role="alert" className="attendance-error">
            {validation}
          </p>
        )}
        {action.error && (
          <p role="alert" className="attendance-error">
            {action.error}
          </p>
        )}
        {action.uncertain && (
          <p role="status">
            The saved result is not confirmed. The original submission is retained for an unchanged
            retry.
          </p>
        )}
        {failed && (
          <p role="status">Reload and review current server state before opening a new form.</p>
        )}
        <div className="attendance-commands">
          <Command
            icon={action.uncertain ? RefreshCw : Save}
            type="submit"
            disabled={action.pending || failed || (disabled && !action.uncertain)}
          >
            {action.pending ? 'Saving...' : action.uncertain ? 'Retry unchanged' : submitLabel}
          </Command>
          <Command icon={X} variant="secondary" disabled={action.pending} onClick={close}>
            {failed || action.uncertain ? 'Discard and reload' : 'Cancel'}
          </Command>
        </div>
      </form>
    </section>
  );
}
export function SavedNotice({ message }: { message: string }) {
  return message ? (
    <p className="attendance-success" role="status">
      <Check size={16} aria-hidden="true" />
      {message}
    </p>
  ) : null;
}
