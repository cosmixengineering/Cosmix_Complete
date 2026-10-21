import { useState } from 'react';
import type { z } from 'zod';
import { Archive, ArchiveRestore, RefreshCw, X } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { pageOf, writeResult } from '../../schemas/contracts';
import {
  archiveHistorySchema,
  archiveInputSchema,
  workflowRequestSchema,
} from '../../schemas/requests';
import type { StatusState } from '../../types/requests';
import { employmentLabels } from '../../types/contracts';
import { labelDate } from '../../utils/messages';
import { LongField } from '../hiring/FormFields';
import { ActionMessage, QueryState } from '../Feedback';

export function EmployeeArchive({
  current,
  currentReady,
  onSaved,
}: {
  current: StatusState;
  currentReady: boolean;
  onSaved: () => void;
}) {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  const ended = [4, 5, 6].includes(current.currentStatus);
  const history = useHrQuery(
    `employees/${current.employeeId}/lifecycle/archive/history?page=${page}&pageSize=10`,
    pageOf(archiveHistorySchema),
    ['hr.employees.lifecycle.read'],
  );
  const pending = useHrQuery(
    `employees/${current.employeeId}/lifecycle/requests?page=1&pageSize=1&state=0`,
    pageOf(workflowRequestSchema),
    ['hr.employees.lifecycle.read'],
    ended,
  );
  const canArchive =
    access.can('hr.employees.lifecycle.read', 'hr.employees.lifecycle.archive') &&
    !!access.user?.account &&
    access.user.account.employeeId.toLowerCase() !== current.employeeId.toLowerCase() &&
    ended &&
    !current.scheduledChange;
  const ready =
    currentReady &&
    pending.isFetchedAfterMount &&
    !pending.isFetching &&
    !pending.isError &&
    pending.data?.totalCount === 0;
  const Icon = current.isArchived ? ArchiveRestore : Archive;
  const label = current.isArchived ? 'Unarchive employee' : 'Archive employee';
  if (!access.can('hr.employees.lifecycle.read')) return null;
  return (
    <section className="hr-form-section">
      <h2>Directory archive</h2>
      <p>
        Directory status: <strong>{current.isArchived ? 'Archived' : 'Current'}</strong>
        {current.archivedAt && <> &middot; {labelDate(current.archivedAt)} (Pakistan)</>}
      </p>
      <ActionMessage action={{ error: null, success }} />
      {canArchive && (
        <QueryState query={pending}>
          {pending.data && pending.data.totalCount > 0 && (
            <p className="hr-notice">A status request is awaiting a decision.</p>
          )}
          {editing ? (
            <ArchiveForm
              key={current.version}
              current={current}
              ready={ready}
              onClose={() => setEditing(false)}
              onReload={() => {
                setEditing(false);
                void pending.refetch();
                onSaved();
              }}
              onSaved={() => {
                setEditing(false);
                setSuccess(
                  current.isArchived
                    ? 'Employee unarchived. Employment status is unchanged.'
                    : 'Employee archived. Employment status is unchanged.',
                );
                void history.refetch();
                void pending.refetch();
                onSaved();
              }}
            />
          ) : (
            <Button
              variant="secondary"
              disabled={!ready}
              onClick={() => {
                setSuccess(null);
                setEditing(true);
              }}
            >
              <Icon size={16} />
              {label}
            </Button>
          )}
        </QueryState>
      )}
      <h3>Archive history</h3>
      <QueryState query={history}>
        {!history.data?.items.length && <p className="muted">No archive history.</p>}
        {history.data?.items.map((entry) => (
          <article className="hr-repeat" key={entry.id}>
            <h3>
              {entry.previousArchived ? 'Archived' : 'Current'} to{' '}
              {entry.isArchived ? 'Archived' : 'Current'}
            </h3>
            <p>{entry.reason}</p>
            <p className="muted">
              {employmentLabels[entry.employmentStatus]} &middot; {labelDate(entry.recordedAt)}{' '}
              (Pakistan) &middot; {entry.actorRoleCode} &middot; {entry.actorAccountId}
            </p>
          </article>
        ))}
        {history.data && (
          <Pagination
            page={page}
            pageSize={10}
            total={history.data.totalCount}
            onChange={setPage}
          />
        )}
      </QueryState>
    </section>
  );
}

function ArchiveForm({
  current,
  ready,
  onSaved,
  onClose,
  onReload,
}: {
  current: StatusState;
  ready: boolean;
  onSaved: () => void;
  onClose: () => void;
  onReload: () => void;
}) {
  const access = useHrAccess();
  const action = useHrAction();
  const [reason, setReason] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [payload, setPayload] = useState<z.infer<typeof archiveInputSchema> | null>(null);
  const [sent, setSent] = useState(false);
  const dirty = !sent && (!!reason || !!payload);
  useDirtyForm(dirty || action.pending);
  const Icon = current.isArchived ? ArchiveRestore : Archive;
  const label = current.isArchived ? 'Unarchive' : 'Archive';
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        if (
          !ready ||
          action.pending ||
          sent ||
          !access.can('hr.employees.lifecycle.read', 'hr.employees.lifecycle.archive') ||
          !access.user?.account ||
          access.user.account.employeeId.toLowerCase() === current.employeeId.toLowerCase() ||
          ![4, 5, 6].includes(current.currentStatus) ||
          current.scheduledChange
        )
          return;
        const parsed = archiveInputSchema.safeParse({
          archived: !current.isArchived,
          expectedVersion: current.version,
          reason,
        });
        if (!payload && !parsed.success) {
          setError('Enter a reason up to 1000 characters without control characters.');
          return;
        }
        const body = payload ?? (parsed.success ? parsed.data : null);
        if (
          !body ||
          !window.confirm(
            `${label} this employee in the directory? Employment status will stay unchanged.`,
          )
        )
          return;
        setError(null);
        setPayload(body);
        void action
          .run(`employees/${current.employeeId}/lifecycle/archive`, body, 'POST', writeResult, () =>
            setPayload(null),
          )
          .then((result) => {
            if (result) {
              setSent(true);
              onSaved();
            }
          });
      }}
    >
      <fieldset disabled={!ready || action.pending || sent}>
        <fieldset disabled={!!payload}>
          <LongField
            label="Archive change reason"
            required
            max={1000}
            value={reason}
            onChange={setReason}
          />
        </fieldset>
        <div className="hr-actions">
          <Button type="submit">
            <Icon size={16} />
            {payload && action.error ? `Retry ${label.toLowerCase()}` : `${label} employee`}
          </Button>
          <Button
            variant="secondary"
            type="button"
            onClick={() => {
              if (
                !dirty ||
                window.confirm(
                  'Discard archive details? Check the current record before another action if the result was uncertain.',
                )
              ) {
                if (payload) onReload();
                else onClose();
              }
            }}
          >
            <X size={16} />
            Cancel
          </Button>
        </div>
      </fieldset>
      <ActionMessage action={{ error: error ?? action.error, success: null }} />
      {action.errorStatus !== null && action.errorStatus >= 400 && action.errorStatus < 500 && (
        <Button
          variant="secondary"
          type="button"
          disabled={action.pending}
          onClick={() => {
            if (window.confirm('Reload current status and discard unsaved archive details?'))
              onReload();
          }}
        >
          <RefreshCw size={16} />
          Reload current status
        </Button>
      )}
    </form>
  );
}
