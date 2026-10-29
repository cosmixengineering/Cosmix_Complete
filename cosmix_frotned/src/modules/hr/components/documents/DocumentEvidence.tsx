import { useState } from 'react';
import {
  History,
  RefreshCw,
  ArrowLeft,
  ArrowRight,
  ShieldAlert,
  CheckCircle2,
  AlertCircle,
  FileText,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
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
    <div
      className="emp-card"
      style={{
        padding: '20px 24px',
        marginTop: '20px',
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '10px',
      }}
    >
      <div className="emp-card-header simple" style={{ marginBottom: '14px' }}>
        <div
          className="emp-card-title"
          style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a' }}
        >
          <ShieldAlert size={17} className="emp-card-title-icon" style={{ color: '#0284c7' }} />
          <span>Compliance &amp; Evidence Verification</span>
        </div>
      </div>

      <form
        style={{
          display: 'flex',
          alignItems: 'flex-end',
          gap: '12px',
          marginBottom: '16px',
          flexWrap: 'wrap',
        }}
        onSubmit={(event) => {
          event.preventDefault();
          if (validWindow) {
            if (Number(window) === days) void query.refetch();
            else setDays(Number(window));
          }
        }}
      >
        <div style={{ width: '160px' }}>
          <label
            style={{
              display: 'block',
              fontSize: '12px',
              fontWeight: 600,
              color: '#475569',
              marginBottom: '4px',
            }}
          >
            Expiry window (days)
          </label>
          <input
            type="number"
            min={1}
            max={365}
            required
            value={window}
            onChange={(event) => setWindow(event.target.value)}
            style={{
              height: '36px',
              width: '100%',
              padding: '6px 12px',
              border: '1px solid #cbd5e1',
              borderRadius: '6px',
              fontSize: '13px',
              color: '#0f172a',
              background: '#ffffff',
            }}
          />
        </div>
        <Button
          type="submit"
          variant="secondary"
          disabled={!validWindow || query.isFetching}
          style={{
            height: '36px',
            fontSize: '12.5px',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            borderColor: '#e2e8f0',
          }}
        >
          <RefreshCw size={13} className={query.isFetching ? 'animate-spin' : ''} />
          Check Evidence
        </Button>
      </form>

      {query.error ? (
        <div
          style={{
            padding: '12px 16px',
            background: '#f8fafc',
            border: '1px solid #e2e8f0',
            borderRadius: '8px',
            color: '#64748b',
            fontSize: '12.5px',
          }}
        >
          No category-specific document enforcement policy is configured for this employee.
          Documents uploaded above remain active and tracked.
        </div>
      ) : (
        <QueryState query={{ ...query, isPending: query.isPending || query.isFetching }}>
          {query.data && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <p className="muted" style={{ margin: 0, fontSize: '12.5px', color: '#64748b' }}>
                As of {query.data.asOf} &middot; Monitoring through {query.data.reminderThrough}
              </p>

              <div>
                <div
                  style={{
                    fontSize: '12.5px',
                    fontWeight: 600,
                    color: '#334155',
                    marginBottom: '8px',
                  }}
                >
                  Required Document Checklist:
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                  {query.data.requiredDocuments.map((row) => (
                    <span
                      key={row.type}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '4px 10px',
                        borderRadius: '6px',
                        fontSize: '12px',
                        fontWeight: 500,
                        background: row.isMissing ? '#fee2e2' : '#dcfce7',
                        color: row.isMissing ? '#991b1b' : '#166534',
                        border: `1px solid ${row.isMissing ? '#fecaca' : '#bbf7d0'}`,
                      }}
                    >
                      {row.isMissing ? <AlertCircle size={13} /> : <CheckCircle2 size={13} />}
                      {documentTypes[row.type - 1]}: {row.isMissing ? 'Missing' : 'Available'}
                    </span>
                  ))}
                </div>
              </div>

              {query.data.expiringDocuments.length > 0 && (
                <div>
                  <div
                    style={{
                      fontSize: '12.5px',
                      fontWeight: 600,
                      color: '#854d0e',
                      marginBottom: '6px',
                    }}
                  >
                    Expiring Documents:
                  </div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                    {query.data.expiringDocuments.map((row) => (
                      <span
                        key={row.id}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          padding: '4px 10px',
                          borderRadius: '6px',
                          fontSize: '12px',
                          background: '#fef3c7',
                          color: '#92400e',
                          border: '1px solid #fde68a',
                        }}
                      >
                        <FileText size={13} />
                        {documentTypes[row.type - 1]} &middot; Expires {row.expiresOn}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {query.data.activationBlockers.length > 0 && (
                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: '6px',
                    background: '#fef2f2',
                    border: '1px solid #fecaca',
                  }}
                >
                  <div
                    style={{
                      fontSize: '12.5px',
                      fontWeight: 600,
                      color: '#991b1b',
                      marginBottom: '4px',
                    }}
                  >
                    Activation Blockers:
                  </div>
                  <ul
                    style={{ margin: 0, paddingLeft: '18px', fontSize: '12px', color: '#b91c1c' }}
                  >
                    {query.data.activationBlockers.map((b) => (
                      <li key={b}>{b}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </QueryState>
      )}
    </div>
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
