import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Calendar,
  Clock,
  ExternalLink,
  Info,
  Radio,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  Wifi,
  WifiOff,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { TextInput } from '@/components/forms/TextInput';
import { DataTable } from '@/components/data-display/DataTable';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { attendancePunchAuditPageSchema, type PunchAuditItem } from '../../schemas/tracking';
import { Denied, QueryState } from '../Feedback';
import { labelDate } from '../../utils/messages';

function formatDateInput(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function getDefaultFromDate(): string {
  const d = new Date();
  d.setDate(d.getDate() - 30);
  return formatDateInput(d);
}

function getDefaultToDate(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return formatDateInput(d);
}

function outcomeBadgeClass(outcome: string): string {
  switch (outcome.toLowerCase()) {
    case 'accepted':
      return 'badge--positive';
    case 'acceptedwithexception':
      return 'badge--warning';
    case 'rejected':
      return 'badge--danger';
    default:
      return 'badge--neutral';
  }
}

export function EmployeeMapTracking({ employeeId }: { employeeId: string }) {
  const access = useHrAccess();
  const [fromDate, setFromDate] = useState(getDefaultFromDate);
  const [toDate, setToDate] = useState(getDefaultToDate);
  const [inspectPunch, setInspectPunch] = useState<PunchAuditItem | null>(null);

  const canReadAudit = access.can('attendance.punches.read') || access.can('hr.employees.read');

  const queryPath = `employees/${employeeId}/tracking?From=${fromDate}&To=${toDate}&PageSize=50`;
  const query = useHrQuery(
    queryPath,
    attendancePunchAuditPageSchema,
    ['attendance.punches.read'],
    canReadAudit && Boolean(fromDate && toDate && toDate > fromDate),
  );

  if (!canReadAudit) return <Denied />;

  const items = query.data?.items ?? [];
  const latestItem = items[0] ?? null;

  const acceptedCount = items.filter((i) => i.punch.outcome.toLowerCase() === 'accepted').length;
  const exceptionCount = items.filter(
    (i) =>
      i.punch.outcome.toLowerCase() === 'rejected' ||
      i.punch.outcome.toLowerCase() === 'acceptedwithexception',
  ).length;
  const offlineCount = items.filter((i) => i.punch.isOffline).length;

  const handleApplyPreset = (days: number) => {
    const end = new Date();
    end.setDate(end.getDate() + 1);
    const start = new Date();
    start.setDate(start.getDate() - days);
    setFromDate(formatDateInput(start));
    setToDate(formatDateInput(end));
  };

  return (
    <section className="hr-form-section" style={{ maxWidth: '100%' }}>
      {/* Header bar */}
      <div className="hr-repeat-heading" style={{ flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h2>Attendance Evidence & Punch Audit</h2>
          <p className="muted" style={{ margin: '4px 0 0' }}>
            Immutable server intake receipts, offline synchronization logs, and geofence evaluation
            assessments.
          </p>
        </div>
        <div className="hr-inline" style={{ gap: '8px' }}>
          <Button
            variant="ghost"
            title="Refresh audit data"
            aria-label="Refresh audit data"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            <RefreshCw size={16} />
            Refresh
          </Button>
          <Link
            to={`/attendance/processing?employeeId=${employeeId}`}
            className="button button--secondary"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              textDecoration: 'none',
            }}
          >
            <ExternalLink size={14} />
            Attendance Processing
          </Link>
        </div>
      </div>

      {/* Compliance & Hardware Boundary Note */}
      <div
        style={{
          background: '#f8fafc',
          border: '1px solid #e2e8f0',
          borderRadius: '8px',
          padding: '14px 18px',
          marginTop: '16px',
          display: 'flex',
          gap: '12px',
          alignItems: 'flex-start',
        }}
      >
        <Info size={18} color="#3b82f6" style={{ flexShrink: 0, marginTop: '2px' }} />
        <div style={{ fontSize: '13px', color: '#475569', lineHeight: 1.5 }}>
          <strong>Compliance & Hardware Security:</strong> To guarantee legal admissibility and
          protect employee privacy, raw facial vectors and device GPS coordinates are securely
          isolated on client hardware boundary agents. Web manual injection is disabled; attendance
          is captured strictly via registered mobile terminals or authorized HR attendance
          corrections.
        </div>
      </div>

      {/* Date Range Filter Bar */}
      <div
        style={{
          background: 'white',
          border: '1px solid #e3e7ec',
          borderRadius: '8px',
          padding: '14px 18px',
          marginTop: '16px',
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'flex-end',
          gap: '12px',
          boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
        }}
      >
        <div style={{ minWidth: '150px' }}>
          <TextInput
            label="From Date"
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </div>
        <div style={{ minWidth: '150px' }}>
          <TextInput
            label="To Date (Exclusive)"
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>
        <div
          style={{
            display: 'flex',
            gap: '6px',
            alignItems: 'center',
            marginBottom: '4px',
          }}
        >
          <Button
            type="button"
            variant="ghost"
            style={{ fontSize: '12px', padding: '6px 10px' }}
            onClick={() => handleApplyPreset(7)}
          >
            Last 7 Days
          </Button>
          <Button
            type="button"
            variant="ghost"
            style={{ fontSize: '12px', padding: '6px 10px' }}
            onClick={() => handleApplyPreset(30)}
          >
            Last 30 Days
          </Button>
          <Button
            type="button"
            variant="ghost"
            style={{ fontSize: '12px', padding: '6px 10px' }}
            onClick={() => handleApplyPreset(90)}
          >
            Last 90 Days
          </Button>
        </div>
      </div>

      <QueryState query={query}>
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '20px',
            marginTop: '16px',
          }}
        >
          {/* Quick Metrics Cards */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
              gap: '16px',
            }}
          >
            {/* Latest Recorded Punch Card */}
            <div
              style={{
                background: 'white',
                padding: '16px 20px',
                borderRadius: '8px',
                border: '1px solid #e3e7ec',
                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <span style={{ fontSize: '12px', fontWeight: 600, color: '#657080' }}>
                  LATEST PUNCH
                </span>
                {latestItem ? (
                  <span
                    className={`badge ${latestItem.punch.action === 'CheckIn' ? 'badge--positive' : 'badge--warning'}`}
                  >
                    <Radio size={12} style={{ marginRight: '4px' }} />
                    {latestItem.punch.action}
                  </span>
                ) : (
                  <span className="badge badge--neutral">No punches</span>
                )}
              </div>
              <div style={{ marginTop: '10px' }}>
                {latestItem ? (
                  <>
                    <div
                      style={{
                        fontSize: '15px',
                        fontWeight: 700,
                        color: '#1a202c',
                      }}
                    >
                      {labelDate(latestItem.punch.receivedAtUtc)}
                    </div>
                    <div
                      style={{
                        fontSize: '12px',
                        color: '#718096',
                        marginTop: '4px',
                      }}
                    >
                      Business Date: {latestItem.punch.businessDate}
                    </div>
                  </>
                ) : (
                  <div style={{ fontSize: '14px', color: '#718096' }}>
                    No punch recorded in selected window.
                  </div>
                )}
              </div>
            </div>

            {/* Total Audited Punches */}
            <div
              style={{
                background: 'white',
                padding: '16px 20px',
                borderRadius: '8px',
                border: '1px solid #e3e7ec',
                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <span style={{ fontSize: '12px', fontWeight: 600, color: '#657080' }}>
                  AUDIT WINDOW EVENTS
                </span>
                <Clock size={16} color="#475569" />
              </div>
              <div style={{ marginTop: '10px' }}>
                <div
                  style={{
                    fontSize: '22px',
                    fontWeight: 700,
                    color: '#1e293b',
                  }}
                >
                  {items.length}
                </div>
                <div
                  style={{
                    fontSize: '12px',
                    color: '#718096',
                    marginTop: '4px',
                  }}
                >
                  Punches evaluated by server
                </div>
              </div>
            </div>

            {/* Accepted & Exceptions */}
            <div
              style={{
                background: 'white',
                padding: '16px 20px',
                borderRadius: '8px',
                border: '1px solid #e3e7ec',
                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <span style={{ fontSize: '12px', fontWeight: 600, color: '#657080' }}>
                  VERIFICATION OUTCOME
                </span>
                <ShieldCheck size={16} color="#16a34a" />
              </div>
              <div style={{ marginTop: '10px' }}>
                <div
                  style={{
                    fontSize: '15px',
                    fontWeight: 700,
                    color: '#16a34a',
                  }}
                >
                  {acceptedCount} Accepted
                </div>
                <div
                  style={{
                    fontSize: '12px',
                    color: exceptionCount > 0 ? '#dc2626' : '#718096',
                    marginTop: '4px',
                  }}
                >
                  {exceptionCount} Flagged / Exceptions
                </div>
              </div>
            </div>

            {/* Offline vs Online Sync */}
            <div
              style={{
                background: 'white',
                padding: '16px 20px',
                borderRadius: '8px',
                border: '1px solid #e3e7ec',
                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <span style={{ fontSize: '12px', fontWeight: 600, color: '#657080' }}>
                  INTAKE CHANNEL
                </span>
                {offlineCount > 0 ? (
                  <WifiOff size={16} color="#d97706" />
                ) : (
                  <Wifi size={16} color="#2563eb" />
                )}
              </div>
              <div style={{ marginTop: '10px' }}>
                <div
                  style={{
                    fontSize: '15px',
                    fontWeight: 700,
                    color: '#1e293b',
                  }}
                >
                  {offlineCount} Offline Synced
                </div>
                <div
                  style={{
                    fontSize: '12px',
                    color: '#718096',
                    marginTop: '4px',
                  }}
                >
                  {items.length - offlineCount} Real-time Gateway
                </div>
              </div>
            </div>
          </div>

          {/* Punch Audit Table */}
          <div
            style={{
              background: 'white',
              borderRadius: '8px',
              border: '1px solid #e3e7ec',
              padding: '20px',
            }}
          >
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '16px',
                flexWrap: 'wrap',
                gap: '8px',
              }}
            >
              <h3 style={{ margin: 0, fontSize: '16px' }}>
                Punch Evidence Records ({items.length})
              </h3>
              <span className="muted" style={{ fontSize: '13px' }}>
                Showing events from {fromDate} to {toDate}
              </span>
            </div>

            {items.length > 0 ? (
              <DataTable
                caption="Employee punch audit trail"
                rows={items}
                rowKey={(r) => r.punch.id}
                columns={[
                  {
                    id: 'action',
                    header: 'ACTION',
                    cell: (r) => (
                      <span
                        className={`badge ${r.punch.action === 'CheckIn' ? 'badge--positive' : 'badge--warning'}`}
                      >
                        {r.punch.action}
                      </span>
                    ),
                  },
                  {
                    id: 'businessDate',
                    header: 'BUSINESS DATE',
                    cell: (r) => <span style={{ fontWeight: 600 }}>{r.punch.businessDate}</span>,
                  },
                  {
                    id: 'receivedAt',
                    header: 'SERVER INTAKE (UTC/LOCAL)',
                    cell: (r) => labelDate(r.punch.receivedAtUtc),
                  },
                  {
                    id: 'deviceCaptured',
                    header: 'DEVICE CAPTURE TIME',
                    cell: (r) =>
                      r.claimedDeviceCapturedAtUtc
                        ? labelDate(r.claimedDeviceCapturedAtUtc)
                        : 'Identical to intake',
                  },
                  {
                    id: 'outcome',
                    header: 'ASSESSMENT OUTCOME',
                    cell: (r) => (
                      <span
                        className={`badge ${outcomeBadgeClass(r.punch.outcome)}`}
                        style={{ display: 'inline-flex', alignItems: 'center' }}
                      >
                        {r.punch.outcome.toLowerCase() === 'accepted' ? (
                          <ShieldCheck size={12} style={{ marginRight: '4px' }} />
                        ) : (
                          <ShieldAlert size={12} style={{ marginRight: '4px' }} />
                        )}
                        {r.punch.outcome}
                      </span>
                    ),
                  },
                  {
                    id: 'reason',
                    header: 'REASON / POLICY CODE',
                    cell: (r) => (
                      <code
                        style={{
                          fontSize: '11px',
                          background: '#f1f5f9',
                          padding: '2px 6px',
                          borderRadius: '4px',
                        }}
                      >
                        {r.punch.reasonCode ?? 'N/A'}
                      </code>
                    ),
                  },
                  {
                    id: 'channel',
                    header: 'CHANNEL',
                    cell: (r) =>
                      r.punch.isOffline ? (
                        <span
                          className="badge badge--warning"
                          title="Captured offline and synced when network restored"
                        >
                          Offline Sync
                        </span>
                      ) : (
                        <span
                          className="badge badge--neutral"
                          title="Live authenticated socket/HTTP transport"
                        >
                          Live Gateway
                        </span>
                      ),
                  },
                  {
                    id: 'inspect',
                    header: '',
                    cell: (r) => (
                      <Button
                        variant="ghost"
                        style={{ fontSize: '12px', padding: '4px 8px' }}
                        onClick={() => setInspectPunch(r)}
                      >
                        Inspect
                      </Button>
                    ),
                  },
                ]}
              />
            ) : (
              <div
                style={{
                  padding: '40px 20px',
                  textAlign: 'center',
                  color: '#64748b',
                }}
              >
                <Calendar size={40} color="#94a3b8" style={{ margin: '0 auto 12px' }} />
                <h4 style={{ margin: 0, color: '#334155' }}>
                  No punch records in this date window
                </h4>
                <p
                  style={{
                    maxWidth: '420px',
                    margin: '6px auto 14px',
                    fontSize: '13px',
                  }}
                >
                  There are no server intake receipts found for the employee between {fromDate} and{' '}
                  {toDate}. Try expanding the date range above.
                </p>
                <Button type="button" variant="secondary" onClick={() => handleApplyPreset(90)}>
                  Expand to Last 90 Days
                </Button>
              </div>
            )}
          </div>
        </div>
      </QueryState>

      {/* Inspect Punch Details Modal */}
      {inspectPunch && (
        <Dialog
          open
          title="Punch Audit Verification Detail"
          description={`Immutable audit record for punch ID ${inspectPunch.punch.id}`}
          onOpenChange={(open) => {
            if (!open) setInspectPunch(null);
          }}
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr 1fr',
                gap: '12px',
                fontSize: '13px',
              }}
            >
              <div>
                <span className="muted">Punch ID:</span>
                <div style={{ fontWeight: 600, wordBreak: 'break-all' }}>
                  {inspectPunch.punch.id}
                </div>
              </div>
              <div>
                <span className="muted">Action:</span>
                <div style={{ fontWeight: 600 }}>{inspectPunch.punch.action}</div>
              </div>
              <div>
                <span className="muted">Business Date:</span>
                <div style={{ fontWeight: 600 }}>{inspectPunch.punch.businessDate}</div>
              </div>
              <div>
                <span className="muted">Calculation Status:</span>
                <div style={{ fontWeight: 600 }}>
                  {inspectPunch.punch.calculationStatus ?? 'factual_only_pending'}
                </div>
              </div>
              <div>
                <span className="muted">Server Intake (UTC):</span>
                <div style={{ fontWeight: 600 }}>{labelDate(inspectPunch.punch.receivedAtUtc)}</div>
              </div>
              <div>
                <span className="muted">Claimed Device Timestamp:</span>
                <div style={{ fontWeight: 600 }}>
                  {inspectPunch.claimedDeviceCapturedAtUtc
                    ? labelDate(inspectPunch.claimedDeviceCapturedAtUtc)
                    : 'N/A'}
                </div>
              </div>
              <div>
                <span className="muted">Outcome:</span>
                <div>
                  <span className={`badge ${outcomeBadgeClass(inspectPunch.punch.outcome)}`}>
                    {inspectPunch.punch.outcome}
                  </span>
                </div>
              </div>
              <div>
                <span className="muted">Reason Code:</span>
                <div style={{ fontWeight: 600 }}>{inspectPunch.punch.reasonCode ?? 'N/A'}</div>
              </div>
              <div>
                <span className="muted">Assessment Revision:</span>
                <div style={{ fontWeight: 600 }}>
                  {inspectPunch.assessmentRevision ?? '1 (Original)'}
                </div>
              </div>
              <div>
                <span className="muted">Offline Flag:</span>
                <div style={{ fontWeight: 600 }}>
                  {inspectPunch.punch.isOffline ? 'True (Synced)' : 'False (Live)'}
                </div>
              </div>
              {inspectPunch.punch.siteId && (
                <div>
                  <span className="muted">Site Reference:</span>
                  <div style={{ fontWeight: 600, wordBreak: 'break-all' }}>
                    {inspectPunch.punch.siteId}
                  </div>
                </div>
              )}
              {inspectPunch.punch.shiftVersionId && (
                <div>
                  <span className="muted">Shift Version:</span>
                  <div style={{ fontWeight: 600, wordBreak: 'break-all' }}>
                    {inspectPunch.punch.shiftVersionId}
                  </div>
                </div>
              )}
            </div>

            <div
              style={{
                marginTop: '12px',
                display: 'flex',
                justifyContent: 'flex-end',
              }}
            >
              <Button type="button" variant="secondary" onClick={() => setInspectPunch(null)}>
                Close
              </Button>
            </div>
          </div>
        </Dialog>
      )}
    </section>
  );
}
