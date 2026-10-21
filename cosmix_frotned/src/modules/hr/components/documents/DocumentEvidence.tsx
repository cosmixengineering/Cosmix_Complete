import { useState } from 'react';
import { History, RefreshCw, ArrowLeft, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { useHrAccess } from '../../hooks/use-hr-access';
import { documentCompletenessFor, documentHistoryPage } from '../../schemas/document-evidence';
import { documentTypes } from '../../types/hiring';
import { Denied, QueryState } from '../Feedback';

const requiredPermissions = ['hr.employees.read', 'hr.employees.documents.read'];
export function DocumentCompleteness({ employeeId }: { employeeId: string }) {
  const access = useHrAccess();
  const [window, setWindow] = useState('30');
  const [days, setDays] = useState(30);
  const query = useHrQuery(
    `employees/${employeeId}/documents/completeness?expiringWithinDays=${days}`,
    documentCompletenessFor(employeeId, days),
    requiredPermissions,
  );
  const validWindow = /^\d+$/.test(window) && Number(window) >= 1 && Number(window) <= 365;
  if (!access.can(...requiredPermissions)) return <Denied />;
  return (
    <section className="hr-form-section">
      <h3>Missing And Expiring Evidence</h3>
      <form
        className="hr-actions"
        onSubmit={(event) => {
          event.preventDefault();
          if (validWindow) {
            if (Number(window) === days) void query.refetch();
            else setDays(Number(window));
          }
        }}
      >
        <TextInput
          label="Expiry window (days)"
          type="number"
          min={1}
          max={365}
          required
          value={window}
          onChange={(event) => setWindow(event.target.value)}
        />
        <Button type="submit" variant="secondary" disabled={!validWindow || query.isFetching}>
          <RefreshCw size={16} />
          Read
        </Button>
      </form>
      <QueryState query={{ ...query, isPending: query.isPending || query.isFetching }}>
        {query.data && (
          <>
            <p className="muted">
              As of {query.data.asOf}; expiry window through {query.data.reminderThrough}.
            </p>
            <ul>
              {query.data.requiredDocuments.map((row) => (
                <li key={row.type}>
                  {documentTypes[row.type - 1]}:{' '}
                  {row.isMissing ? 'Missing available evidence' : 'Available selected evidence'}
                </li>
              ))}
            </ul>
            {query.data.expiringDocuments.length > 0 && (
              <>
                <h4>Expiring Documents</h4>
                <ul>
                  {query.data.expiringDocuments.map((row) => (
                    <li key={row.id}>
                      {documentTypes[row.type - 1]}: {row.expiresOn} ({row.id})
                    </li>
                  ))}
                </ul>
              </>
            )}
            {query.data.certifications.length > 0 && (
              <>
                <h4>Certifications</h4>
                <ul>
                  {query.data.certifications.map((row, index) => (
                    <li key={`${row.reference}:${index}`}>
                      {row.type ?? row.reference}: {row.expiresOn ?? 'No expiry recorded'}
                      {row.isExpired ? ' · Expired' : row.isExpiring ? ' · Expiring' : ''}
                      {row.isMissingEvidence ? ' · Evidence unavailable' : ''}
                      {!row.isComplete ? ' · Incomplete' : ''}
                    </li>
                  ))}
                </ul>
              </>
            )}
            {query.data.activationBlockers.length > 0 ? (
              <>
                <h4>Activation Blockers</h4>
                <ul>
                  {query.data.activationBlockers.map((blocker) => (
                    <li key={blocker}>{blocker}</li>
                  ))}
                </ul>
              </>
            ) : (
              <p>No document activation blockers reported.</p>
            )}
          </>
        )}
      </QueryState>
    </section>
  );
}
export function DocumentHistory({
  employeeId,
  documentId,
  medical,
}: {
  employeeId: string;
  documentId: string;
  medical: boolean;
}) {
  const access = useHrAccess();
  const [open, setOpen] = useState(false);
  const [cursors, setCursors] = useState([0]);
  const cursor = cursors[cursors.length - 1]!;
  const path = `employees/${employeeId}/documents/${documentId}/history?medical=${medical}&afterRevision=${cursor}&take=50`;
  const query = useHrQuery(
    path,
    documentHistoryPage(cursor, 50),
    [...requiredPermissions, ...(medical ? ['hr.employees.medical.read'] : [])],
    open,
  );
  const actionNames = [
    '',
    'Uploaded',
    'Scan started',
    'Scan completed',
    'Verified',
    'Verification rejected',
    'Expired',
    'Revoked',
    'Archived',
    'Scan retried',
  ];
  if (!access.can(...requiredPermissions, ...(medical ? ['hr.employees.medical.read'] : [])))
    return <Denied />;
  return (
    <div>
      <Button variant="secondary" aria-expanded={open} onClick={() => setOpen((value) => !value)}>
        <History size={16} />
        {open ? 'Hide history' : 'Document history'}
      </Button>
      {open && (
        <section className="hr-form-section">
          <h4>Document History</h4>
          <QueryState query={{ ...query, isPending: query.isPending || query.isFetching }}>
            {query.data && (
              <>
                {!query.data.length && <p>No history in this page.</p>}
                <ol>
                  {query.data.map((row) => (
                    <li key={row.id}>
                      <strong>
                        Revision {row.revision}: {actionNames[row.action]}
                      </strong>
                      <p>
                        {row.recordedAt} · {row.actorUserId}
                      </p>
                      <p>{row.reason}</p>
                      <p>
                        Scan: {['Pending', 'Clean', 'Rejected', 'Failed'][row.after.scanStatus]} ·
                        Verification:{' '}
                        {['Pending', 'Verified', 'Rejected'][row.after.verificationStatus]} ·
                        Lifecycle:{' '}
                        {['Current', 'Expired', 'Revoked', 'Archived'][row.after.lifecycleStatus]}
                      </p>
                      {row.after.isScanInProgress && <p>Scan in progress</p>}
                      {row.after.hasScanConflict && <p>Conflicting scan evidence</p>}
                    </li>
                  ))}
                </ol>
                <div className="hr-actions">
                  <Button
                    variant="secondary"
                    disabled={query.isFetching || cursors.length === 1}
                    onClick={() => setCursors((values) => values.slice(0, -1))}
                  >
                    <ArrowLeft size={16} />
                    Previous
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={query.isFetching}
                    onClick={() => void query.refetch()}
                  >
                    <RefreshCw size={16} />
                    Reload
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={query.isFetching || query.data.length < 50}
                    onClick={() =>
                      setCursors((values) => [...values, query.data!.at(-1)!.revision])
                    }
                  >
                    Next
                    <ArrowRight size={16} />
                  </Button>
                </div>
              </>
            )}
          </QueryState>
        </section>
      )}
    </div>
  );
}
