import { useState } from 'react';
import { z } from 'zod';
import { History, MapPin, RefreshCw, Save } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { TextInput } from '@/components/forms/TextInput';
import { DataTable } from '@/components/data-display/DataTable';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { id, writeResult } from '../../schemas/contracts';
import {
  employeeAssignmentPageSchema,
  assignEmployeeSiteSchema,
  siteSchema,
  type EmployeeAssignmentPage,
} from '../../schemas/sites';
import { QueryState, Denied } from '../Feedback';
import { RecordView } from '../RecordView';
import { ReferenceField } from '../ReferenceField';
import { useSiteAction } from '../sites/use-site-action';

export interface EmployeeSiteAssignmentsProps {
  employeeId: string;
  onSaved?: (result: z.infer<typeof writeResult>) => void;
}
const read = ['hr.employees.read'];
const write = ['hr.employees.submissions.review', 'hr.employees.drafts.manage'];

export function EmployeeSiteAssignments(props: EmployeeSiteAssignmentsProps) {
  const access = useHrAccess();
  if (!id.safeParse(props.employeeId).success)
    return <p role="alert">Invalid employee reference.</p>;
  return (
    <AssignmentsWorkspace
      key={JSON.stringify([props.employeeId, access.subject, access.ready, access.user])}
      {...props}
    />
  );
}
function AssignmentsWorkspace({ employeeId, onSaved }: EmployeeSiteAssignmentsProps) {
  const access = useHrAccess();
  const [history, setHistory] = useState(false);
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const query = useHrQuery(
    `employees/${employeeId}/assignments${history ? '/history' : ''}?page=${page}&pageSize=20`,
    employeeAssignmentPageSchema,
    read,
  );
  if (!access.can(...read)) return <Denied />;
  return (
    <section className="hr-form-section">
      <div className="hr-repeat-heading">
        <h2>Site assignments</h2>
        <div className="hr-inline">
          <Button
            variant="ghost"
            title="Reload assignments"
            aria-label="Reload assignments"
            disabled={editing}
            onClick={() => void query.refetch()}
          >
            <RefreshCw size={16} />
          </Button>
          {access.can(...write, 'hr.sites.read') && (
            <Button onClick={() => setEditing(true)}>
              <MapPin size={16} />
              Assign / reassign site
            </Button>
          )}
        </div>
      </div>
      <div className="hr-tabs" role="group" aria-label="Assignment views">
        <Button
          variant={history ? 'ghost' : 'secondary'}
          className={!history ? 'active' : undefined}
          aria-pressed={!history}
          disabled={editing}
          onClick={() => {
            setHistory(false);
            setPage(1);
          }}
        >
          Current and scheduled
        </Button>
        <Button
          variant={history ? 'secondary' : 'ghost'}
          className={history ? 'active' : undefined}
          aria-pressed={history}
          disabled={editing}
          onClick={() => {
            setHistory(true);
            setPage(1);
          }}
        >
          <History size={16} />
          History
        </Button>
      </div>
      {saved && (
        <p role="status" className="hr-message">
          Site assignment saved successfully.
        </p>
      )}
      <QueryState query={query}>
        {query.data && (
          <>
            <DataTable
              caption={history ? 'Assignment history' : 'Current and scheduled assignments'}
              rows={query.data.assignments.items}
              rowKey={(row) => row.id}
              columns={[
                {
                  id: 'site',
                  header: 'SITE',
                  cell: (row) =>
                    row.siteId ? <AssignmentSite siteId={row.siteId} /> : 'Unassigned',
                },
                { id: 'from', header: 'EFFECTIVE FROM', cell: (row) => row.effectiveFrom },
                {
                  id: 'to',
                  header: 'EFFECTIVE TO (EXCLUSIVE)',
                  cell: (row) => row.effectiveTo ?? 'Open',
                },
                {
                  id: 'details',
                  header: 'REFERENCES',
                  cell: (row) => (
                    <details>
                      <summary>Assignment details</summary>
                      <RecordView value={row} omit={['version']} />
                    </details>
                  ),
                },
              ]}
            />
            {!query.data.assignments.items.length && (
              <p className="hr-notice">No assignments found.</p>
            )}
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.assignments.totalCount}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
      {editing && access.can(...read, ...write, 'hr.sites.read') && (
        <AssignmentEditor
          employeeId={employeeId}
          onClose={() => setEditing(false)}
          onSaved={(result) => {
            setEditing(false);
            setSaved(true);
            void query.refetch();
            onSaved?.(result);
          }}
        />
      )}
    </section>
  );
}
function AssignmentSite({ siteId }: { siteId: string }) {
  const access = useHrAccess();
  const query = useHrQuery(`sites/${siteId}`, siteSchema, ['hr.sites.read']);
  return (
    <span title={siteId}>
      {access.can('hr.sites.read') && query.data
        ? `${query.data.name} (${query.data.code})`
        : siteId}
    </span>
  );
}
function AssignmentEditor({
  employeeId,
  onClose,
  onSaved,
}: EmployeeSiteAssignmentsProps & { onClose: () => void }) {
  const [revision, setRevision] = useState(0);
  const query = useHrQuery(
    `employees/${employeeId}/assignments?page=1&pageSize=100`,
    employeeAssignmentPageSchema,
    read,
  );
  if (query.isSuccess)
    return (
      <AssignmentForm
        key={revision}
        employeeId={employeeId}
        initial={query.data}
        onClose={onClose}
        onSaved={onSaved}
        onReload={async () => {
          const result = await query.refetch();
          if (result.isSuccess) setRevision((value) => value + 1);
        }}
      />
    );
  return (
    <Dialog
      open
      title="Load assignments"
      description="Employee site assignment"
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <QueryState query={query}>{null}</QueryState>
      <Button variant="secondary" onClick={onClose}>
        Cancel
      </Button>
    </Dialog>
  );
}
function AssignmentForm({
  employeeId,
  initial,
  onClose,
  onSaved,
  onReload,
}: EmployeeSiteAssignmentsProps & {
  initial: EmployeeAssignmentPage;
  onClose: () => void;
  onReload: () => Promise<void>;
}) {
  const [baseline] = useState(initial);
  const [siteId, setSiteId] = useState<string | null>(null);
  const [effectiveFrom, setEffectiveFrom] = useState('');
  const [reason, setReason] = useState('');
  const [dirty, setDirty] = useState(false);
  const [validation, setValidation] = useState<string | null>(null);
  const action = useSiteAction();
  useDirtyForm(dirty || action.pending || action.locked);
  const close = () => {
    if (
      !action.pending &&
      (!(dirty || action.locked) ||
        window.confirm(
          'Discard this form? Check the record before starting another action if the result was uncertain.',
        ))
    )
      onClose();
  };
  return (
    <Dialog
      open
      title="Assign / reassign site"
      description="Employee site assignment"
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      {baseline.assignments.items.map((row) => (
        <p key={row.id}>
          {row.siteId ? <AssignmentSite siteId={row.siteId} /> : 'Unassigned'}: {row.effectiveFrom}{' '}
          / {row.effectiveTo ?? 'Open'}
        </p>
      ))}
      <form
        onSubmit={async (event) => {
          event.preventDefault();
          const parsed = assignEmployeeSiteSchema.safeParse({
            expectedVersion: baseline.version,
            siteId,
            effectiveFrom,
            reason,
          });
          if (!parsed.success) {
            setValidation(
              parsed.error.issues
                .map((issue) => `${issue.path.join('.')}: ${issue.message}`)
                .join(' '),
            );
            return;
          }
          setValidation(null);
          const result = await action.run(
            `employees/${employeeId}/assignments/site`,
            parsed.data,
            'POST',
            writeResult,
          );
          if (result) {
            setDirty(false);
            onSaved?.(result);
          }
        }}
      >
        <fieldset disabled={action.pending || action.locked || action.reloadRequired}>
          <ReferenceField
            label="Site"
            kind="sites"
            value={siteId}
            required
            onChange={(value) => {
              setSiteId(value);
              setDirty(true);
            }}
          />
          <TextInput
            label="Effective date"
            type="date"
            value={effectiveFrom}
            required
            onChange={(e) => {
              setEffectiveFrom(e.target.value);
              setDirty(true);
            }}
          />
          <TextInput
            label="Reason"
            value={reason}
            required
            maxLength={1000}
            onChange={(e) => {
              setReason(e.target.value);
              setDirty(true);
            }}
          />
        </fieldset>
        {validation && (
          <p className="hr-message hr-message--error" role="alert">
            {validation}
          </p>
        )}
        {action.error && (
          <p className="hr-message hr-message--error" role="alert">
            {action.error}
          </p>
        )}
        <div className="hr-actions">
          <Button type="submit" disabled={action.pending || action.reloadRequired}>
            <Save size={16} />
            {action.pending
              ? 'Saving...'
              : action.locked
                ? 'Retry unchanged action'
                : 'Save assignment'}
          </Button>
          <Button
            variant="secondary"
            disabled={action.pending}
            onClick={() => {
              if (
                !(dirty || action.locked) ||
                window.confirm('Discard the form and reload current assignments?')
              )
                void onReload();
            }}
          >
            <RefreshCw size={16} />
            Reload assignments
          </Button>
          <Button variant="secondary" disabled={action.pending} onClick={close}>
            Cancel
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
