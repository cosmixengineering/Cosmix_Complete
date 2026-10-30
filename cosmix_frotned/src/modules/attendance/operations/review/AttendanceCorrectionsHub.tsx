import { EmployeeReference, EmployeePicker } from '@/components/employees/EmployeeReference';
import { formatEmployeeCode } from '@/utils/employee-code';
import { useState, useMemo, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { z } from 'zod';
import {
  CalendarCheck2,
  Clock,
  CheckCircle2,
  XCircle,
  RotateCcw,
  RefreshCw,
  Search,
  Plus,
  FileText,
  ShieldCheck,
  X,
  SlidersHorizontal,
} from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { useAttendanceAction } from '../../hooks/use-attendance-action';
import { queryString } from '../../api/attendance-api';
import { pageOf } from '../../schemas/common';
import {
  correction,
  correctionDetail,
  correctionDecision,
  correctionSource,
  cursorPage,
} from './contracts';
import { canSelf, queueScopes, supportedScopes, type Scope } from './access';
import { today, nextDate } from './helpers';
import { PrivateWorkspace, QueryState } from '../../components/SchedulingUi';
import '@/styles/attendance-corrections.css';

const employeeChoices = pageOf(
  correction.pick({ id: true }).extend({
    employeeNumber: z.number().int(),
    name: z.string().nullable(),
  }),
);

export function AttendanceCorrectionsHub({
  initialTab = 'queue',
  self = false,
}: {
  initialTab?: 'queue' | 'new';
  self?: boolean;
}) {
  const access = useAttendanceAccess();
  const params = useParams<{ employeeId?: string }>();
  const routeEmployeeId = params.employeeId ?? '';

  const [activeTab, setActiveTab] = useState<'queue' | 'new'>(routeEmployeeId ? 'new' : initialTab);
  const [selectedRequest, setSelectedRequest] = useState<{ id: string; employeeId: string } | null>(
    null,
  );
  const [stateFilter, setStateFilter] = useState<number | 'all'>('all');
  const [searchFilter, setSearchFilter] = useState('');
  const [fromDate, setFromDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() - 30);
    return d.toISOString().slice(0, 10);
  });
  const [toDate, setToDate] = useState(() => nextDate(today()));

  // Scopes & Permissions
  const canSubmit = self
    ? canSelf(access.user, 'attendance.corrections.self')
    : access.can('attendance.corrections.submit');
  const canReview = access.can('attendance.corrections.review');
  const availableScopes = supportedScopes(access.user, 'attendance.corrections.review');
  const effectiveScope: Scope | undefined =
    availableScopes.find((s) => s.kind === 7) ?? availableScopes[0];

  // Employee Directory mapping for Name & EMP-ID resolution
  const employeesQuery = useAttendanceQuery(
    '/hr/employees' + queryString({ page: 1, pageSize: 100 }),
    employeeChoices,
    access.can('hr.employees.read'),
  );

  const employeeMap = useMemo(() => {
    const map = new Map<string, { name: string; code: string; initials: string }>();
    for (const emp of employeesQuery.data?.items ?? []) {
      const name = emp.name || 'Employee';
      const initials = name
        .replace(/^Engr\.\s+/, '')
        .split(' ')
        .filter(Boolean)
        .map((w: string) => w[0]?.toUpperCase() ?? '')
        .slice(0, 2)
        .join('');
      const empNum = emp.employeeNumber;
      const code = formatEmployeeCode(empNum);
      map.set(emp.id.toLowerCase(), {
        name,
        code,
        initials: initials || 'EM',
      });
    }
    return map;
  }, [employeesQuery.data?.items]);

  // Query Correction Queue
  const queuePath = self
    ? '/me/attendance/corrections'
    : routeEmployeeId
      ? `/attendance/employees/${routeEmployeeId}/correction-requests`
      : '/attendance/corrections/review';

  const queryParams = {
    from: fromDate,
    to: toDate,
    pageSize: 50,
    ...(stateFilter !== 'all' ? { state: stateFilter } : {}),
    ...(!self && !routeEmployeeId && effectiveScope
      ? {
          scope: queueScopes[effectiveScope.kind],
          targetId: effectiveScope.targetId ?? undefined,
        }
      : {}),
  };

  const requestsQuery = useAttendanceQuery(
    queuePath + queryString(queryParams),
    self ? pageOf(correction) : cursorPage(correction),
    canReview || canSubmit,
  );

  const requestItems = useMemo(() => {
    if (!requestsQuery.data || requestsQuery.isError) return [];
    const items = requestsQuery.data.items ?? [];
    if (!searchFilter.trim()) return items;

    const q = searchFilter.toLowerCase();
    return items.filter((row) => {
      const emp = employeeMap.get(row.employeeId.toLowerCase());
      const nameMatch = emp?.name.toLowerCase().includes(q) ?? false;
      const codeMatch = emp?.code.toLowerCase().includes(q) ?? false;
      const reasonMatch = row.reason.toLowerCase().includes(q);
      const idMatch = row.id.toLowerCase().includes(q);
      return nameMatch || codeMatch || reasonMatch || idMatch;
    });
  }, [requestsQuery.data, searchFilter, employeeMap]);

  // KPI Calculations
  const kpis = useMemo(() => {
    const all = requestsQuery.data?.items ?? [];
    const pending = all.filter((r) => r.state === 'Pending').length;
    const approved = all.filter((r) => r.state === 'Approved').length;
    const returned = all.filter((r) => r.state === 'Returned').length;
    return {
      pending,
      approved,
      returned,
      total: all.length,
    };
  }, [requestsQuery.data?.items]);

  function reloadAll() {
    void requestsQuery.refetch();
  }

  return (
    <PrivateWorkspace resource={`corrections-hub:${self ? 'self' : routeEmployeeId || 'review'}`}>
      <div className="att-corrections-container">
        {/* 1. Header Banner */}
        <header className="att-corrections-banner">
          <div className="att-corrections-header-top">
            <div className="att-corrections-title-group">
              <h1>
                <CalendarCheck2 size={22} style={{ color: '#2563eb' }} />
                {self ? 'My Attendance Corrections' : 'Attendance Corrections & Adjustments'}
              </h1>
              <p>
                {self
                  ? 'Submit attendance punch adjustments and track approval progress.'
                  : 'Review employee punch corrections, resolve missing check-outs, and authorize attendance adjustments.'}
              </p>
            </div>
            <button
              type="button"
              className="att-action-btn"
              onClick={reloadAll}
              disabled={requestsQuery.isFetching}
            >
              <RefreshCw size={13} className={requestsQuery.isFetching ? 'animate-spin' : ''} />
              Reload
            </button>
          </div>

          {/* Navigation Tabs */}
          <div className="att-corrections-tabs">
            <button
              type="button"
              className={`att-corrections-tab ${activeTab === 'queue' ? 'active' : ''}`}
              onClick={() => setActiveTab('queue')}
            >
              <FileText size={14} />
              <span>Correction Requests &amp; Queue</span>
              <span className="att-tab-count">{kpis.total}</span>
            </button>

            {canSubmit && (
              <button
                type="button"
                className={`att-corrections-tab ${activeTab === 'new' ? 'active' : ''}`}
                onClick={() => setActiveTab('new')}
              >
                <Plus size={14} />
                <span>+ Request Attendance Correction</span>
              </button>
            )}
          </div>
        </header>

        {/* 2. 4 Executive KPI Metrics (56px) */}
        <div className="att-corrections-kpi-grid">
          <div className="att-corrections-kpi-card">
            <div className="att-kpi-icon pending">
              <Clock size={18} />
            </div>
            <div className="att-kpi-info">
              <span className="att-kpi-label">Pending Review</span>
              <span className="att-kpi-value" style={{ color: '#d97706' }}>
                {kpis.pending} Requests
              </span>
              <span className="att-kpi-sub">Awaiting HR authorization</span>
            </div>
          </div>

          <div className="att-corrections-kpi-card">
            <div className="att-kpi-icon approved">
              <CheckCircle2 size={18} />
            </div>
            <div className="att-kpi-info">
              <span className="att-kpi-label">Approved Adjustments</span>
              <span className="att-kpi-value" style={{ color: '#059669' }}>
                {kpis.approved} Completed
              </span>
              <span className="att-kpi-sub">Applied to duty ledger</span>
            </div>
          </div>

          <div className="att-corrections-kpi-card">
            <div className="att-kpi-icon returned">
              <RotateCcw size={18} />
            </div>
            <div className="att-kpi-info">
              <span className="att-kpi-label">Returned for Revision</span>
              <span className="att-kpi-value" style={{ color: '#2563eb' }}>
                {kpis.returned} Returned
              </span>
              <span className="att-kpi-sub">Needs additional evidence</span>
            </div>
          </div>

          <div className="att-corrections-kpi-card">
            <div className="att-kpi-icon guard">
              <ShieldCheck size={18} />
            </div>
            <div className="att-kpi-info">
              <span className="att-kpi-label">Governance Mode</span>
              <span className="att-kpi-value" style={{ color: '#7c3aed' }}>
                Dual-Authorized
              </span>
              <span className="att-kpi-sub">Immutable audit trail</span>
            </div>
          </div>
        </div>

        {/* 3. Tab: Correction Requests Queue */}
        {activeTab === 'queue' && (
          <section className="att-section-card">
            <div className="att-section-header">
              <div className="att-section-title">
                <h2>
                  <FileText size={16} style={{ color: '#2563eb' }} />
                  Correction Requests Queue
                </h2>
                <span className="att-section-subtitle">
                  Showing {requestItems.length} attendance adjustment submissions
                </span>
              </div>
            </div>

            {/* Quick Filter Bar */}
            <div className="att-filter-bar">
              <div className="att-quick-states">
                <button
                  type="button"
                  className={`att-state-btn ${stateFilter === 'all' ? 'active' : ''}`}
                  onClick={() => setStateFilter('all')}
                >
                  All States
                </button>
                <button
                  type="button"
                  className={`att-state-btn ${stateFilter === 0 ? 'active' : ''}`}
                  onClick={() => setStateFilter(0)}
                >
                  Pending ({kpis.pending})
                </button>
                <button
                  type="button"
                  className={`att-state-btn ${stateFilter === 1 ? 'active' : ''}`}
                  onClick={() => setStateFilter(1)}
                >
                  Approved
                </button>
                <button
                  type="button"
                  className={`att-state-btn ${stateFilter === 2 ? 'active' : ''}`}
                  onClick={() => setStateFilter(2)}
                >
                  Returned
                </button>
                <button
                  type="button"
                  className={`att-state-btn ${stateFilter === 3 ? 'active' : ''}`}
                  onClick={() => setStateFilter(3)}
                >
                  Rejected
                </button>
              </div>

              <div className="att-filter-input">
                <Search size={13} style={{ color: '#94a3b8' }} />
                <input
                  type="text"
                  placeholder="Filter by staff, code, reason..."
                  value={searchFilter}
                  onChange={(e) => setSearchFilter(e.target.value)}
                />
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                <span className="att-filter-label">From:</span>
                <div className="att-filter-input">
                  <input
                    type="date"
                    value={fromDate}
                    onChange={(e) => setFromDate(e.target.value)}
                  />
                </div>
                <span className="att-filter-label">To:</span>
                <div className="att-filter-input">
                  <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
                </div>
              </div>
            </div>

            <QueryState query={requestsQuery} empty={requestItems.length === 0} />

            {/* Dense High-Density Requests Table */}
            {!requestsQuery.isError && requestItems.length > 0 && (
              <div className="att-table-wrap">
                <table className="att-dense-table">
                  <thead>
                    <tr>
                      <th>Staff Member</th>
                      <th>Business Date</th>
                      <th>Correction Type</th>
                      <th>Requested Timings</th>
                      <th>Reason / Note</th>
                      <th>Status</th>
                      <th style={{ textAlign: 'right' }}>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {requestItems.map((row) => {
                      const emp = employeeMap.get(row.employeeId.toLowerCase());
                      const empName = emp?.name || (self ? 'My Account' : 'Staff Member');
                      const empCode = <EmployeeReference employeeId={row.employeeId} />;
                      const initials = emp?.initials || 'EM';

                      const kindLower = row.kind.toLowerCase();
                      const kindClass = kindLower.includes('in')
                        ? 'checkin'
                        : kindLower.includes('out')
                          ? 'checkout'
                          : kindLower.includes('duration')
                            ? 'duration'
                            : 'status';

                      const stateLower = row.state.toLowerCase();
                      const stateClass = stateLower.includes('pending')
                        ? 'pending'
                        : stateLower.includes('appr')
                          ? 'approved'
                          : stateLower.includes('return')
                            ? 'returned'
                            : stateLower.includes('reject')
                              ? 'rejected'
                              : 'cancelled';

                      return (
                        <tr key={row.id}>
                          <td>
                            <div className="att-emp-cell">
                              <div className="att-emp-avatar">{initials}</div>
                              <div>
                                <div className="att-emp-name">{empName}</div>
                                <div className="att-emp-code">{empCode}</div>
                              </div>
                            </div>
                          </td>
                          <td>
                            <span style={{ fontWeight: 650, color: '#0f172a' }}>
                              {row.businessDate}
                            </span>
                          </td>
                          <td>
                            <span className={`att-kind-chip ${kindClass}`}>{row.kind}</span>
                          </td>
                          <td>
                            <div className="att-time-cell">
                              <span>{formatLocalTime(row.requestedTimeUtc)}</span>
                              {row.requestedEndUtc && (
                                <>
                                  <span className="att-time-arrow">→</span>
                                  <span>{formatLocalTime(row.requestedEndUtc)}</span>
                                </>
                              )}
                            </div>
                          </td>
                          <td>
                            <span
                              style={{
                                color: '#475569',
                                maxWidth: '220px',
                                display: 'inline-block',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                                whiteSpace: 'nowrap',
                              }}
                              title={row.reason}
                            >
                              {row.reason}
                            </span>
                          </td>
                          <td>
                            <span className={`att-status-pill ${stateClass}`}>
                              {row.state === 'Pending' && (
                                <span
                                  style={{
                                    width: '6px',
                                    height: '6px',
                                    borderRadius: '50%',
                                    background: '#d97706',
                                  }}
                                />
                              )}
                              {row.state}
                            </span>
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <button
                              type="button"
                              className={`att-action-btn ${row.state === 'Pending' && canReview ? 'primary' : ''}`}
                              onClick={() =>
                                setSelectedRequest({ id: row.id, employeeId: row.employeeId })
                              }
                            >
                              {row.state === 'Pending' && canReview ? 'Review' : 'Details'}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        )}

        {/* 4. Tab: New Attendance Correction Form */}
        {activeTab === 'new' && (
          <SubmitCorrectionWizard
            self={self}
            routeEmployeeId={routeEmployeeId}
            employeeMap={employeeMap}
            employees={employeesQuery.data?.items ?? []}
            onSuccess={() => {
              setActiveTab('queue');
              reloadAll();
            }}
            onCancel={() => setActiveTab('queue')}
          />
        )}

        {/* 5. Detail & Review Modal */}
        {selectedRequest && (
          <CorrectionDetailModal
            requestId={selectedRequest.id}
            employeeId={selectedRequest.employeeId}
            self={self}
            canReview={canReview}
            employeeMap={employeeMap}
            onClose={() => setSelectedRequest(null)}
            onUpdated={() => {
              setSelectedRequest(null);
              reloadAll();
            }}
          />
        )}
      </div>
    </PrivateWorkspace>
  );
}

/* ==========================================================================
   SUBMIT CORRECTION WIZARD
   ========================================================================== */
function SubmitCorrectionWizard({
  self,
  routeEmployeeId,
  employees,
  onSuccess,
  onCancel,
}: {
  self: boolean;
  routeEmployeeId: string;
  employeeMap: Map<string, { name: string; code: string; initials: string }>;
  employees: z.infer<typeof employeeChoices>['items'];
  onSuccess: () => void;
  onCancel: () => void;
}) {
  const access = useAttendanceAccess();
  const action = useAttendanceAction();

  const [employeeId, setEmployeeId] = useState(
    routeEmployeeId || (self ? (access.user?.account?.employeeId ?? '') : (employees[0]?.id ?? '')),
  );
  const [businessDate, setBusinessDate] = useState(today());
  const [kind, setKind] = useState<number>(3); // Default: CheckOut (kind 3 - missing checkout)
  const [localStartTime, setLocalStartTime] = useState('09:00');
  const [localEndTime, setLocalEndTime] = useState('18:00');
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState('');

  // Auto-fetch source for selected employee and date
  const sourcePath = self
    ? '/me/attendance/corrections/sources'
    : `/attendance/employees/${employeeId}/correction-requests/sources`;

  const sourceQuery = useAttendanceQuery(
    sourcePath + queryString({ businessDate }),
    correctionSource,
    Boolean(employeeId && businessDate),
  );

  const sourceData = !sourceQuery.isError ? sourceQuery.data : undefined;
  const roster = sourceData?.historicalRoster;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!sourceData || sourceData.periodLocked || !reason.trim() || !evidence.trim()) return;

    const startUtc = localToUtc(businessDate, localStartTime);
    const endUtc = kind === 4 ? localToUtc(businessDate, localEndTime) : null;

    const submitPath = self
      ? '/me/attendance/corrections'
      : `/attendance/employees/${employeeId}/correction-requests`;

    const payload = {
      businessDate,
      kind,
      rawPunchId: sourceData.rawPunchId,
      expectedAssessmentId: sourceData.latestAssessmentId,
      expectedSummaryId: sourceData.latestSummaryId,
      requestedTimeUtc: startUtc,
      requestedEndUtc: endUtc,
      requestedStatus: null,
      reason: reason.trim(),
      writtenEvidence: evidence.trim(),
      expectedSourceFingerprint: sourceData.sourceFingerprint,
    };

    const result = await action.run(submitPath, payload, 'POST', correction);
    if (result) {
      onSuccess();
    }
  }

  const quickReasons = [
    'Biometric Device Offline',
    'Forgotten Checkout',
    'Emergency Duty Callout',
    'Overtime Shift Extension',
    'System Network Delay',
  ];

  return (
    <div className="att-wizard-card">
      <div className="att-section-header">
        <div className="att-section-title">
          <h2>
            <Plus size={16} style={{ color: '#2563eb' }} />
            New Attendance Correction Request
          </h2>
          <span className="att-section-subtitle">
            Submit a proposed punch time adjustment with duty verification.
          </span>
        </div>
      </div>

      <form onSubmit={(e) => void handleSubmit(e)}>
        {/* Step 1: Staff & Shift Date */}
        <div className="att-wizard-step">
          <div className="att-step-header">
            <span className="att-step-num">1</span>
            <span>Select Staff Member &amp; Shift Date</span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
            {!self ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
                  Staff Member
                </label>
                <EmployeePicker
                  label="Employee"
                  value={employeeId}
                  onChange={setEmployeeId}
                  required
                />
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
                  Requester
                </label>
                <input
                  type="text"
                  disabled
                  value="My Self Identity"
                  style={{
                    height: '32px',
                    border: '1px solid #cbd5e1',
                    borderRadius: '5px',
                    padding: '0 8px',
                    fontSize: '12px',
                    background: '#f8fafc',
                  }}
                />
              </div>
            )}

            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
                Shift Business Date
              </label>
              <input
                type="date"
                required
                value={businessDate}
                onChange={(e) => setBusinessDate(e.target.value)}
                style={{
                  height: '32px',
                  border: '1px solid #cbd5e1',
                  borderRadius: '5px',
                  padding: '0 8px',
                  fontSize: '12px',
                }}
              />
            </div>
          </div>

          {/* Captured Shift Context Box */}
          {sourceData && (
            <div
              style={{
                marginTop: '6px',
                padding: '8px 12px',
                background: '#f8fafc',
                border: '1px solid #e2e8f0',
                borderRadius: '6px',
                fontSize: '11.5px',
                display: 'flex',
                gap: '16px',
                flexWrap: 'wrap',
              }}
            >
              <div>
                <strong style={{ color: '#64748b' }}>Scheduled Shift: </strong>
                <span>
                  {roster?.startsAt && roster?.endsAt
                    ? `${formatLocalTime(roster.startsAt)} - ${formatLocalTime(roster.endsAt)}`
                    : 'No recorded shift'}
                </span>
              </div>
              <div>
                <strong style={{ color: '#64748b' }}>Original Check-In: </strong>
                <span>
                  {sourceData.originalTimeUtc
                    ? formatLocalTime(sourceData.originalTimeUtc)
                    : 'Missing'}
                </span>
              </div>
              <div>
                <strong style={{ color: '#64748b' }}>Assessment Status: </strong>
                <span style={{ color: sourceData.periodLocked ? '#dc2626' : '#059669' }}>
                  {sourceData.periodLocked ? 'Period Locked' : 'Open for Adjustment'}
                </span>
              </div>
            </div>
          )}
        </div>

        {/* Step 2: Proposed Timings & Adjustment */}
        <div className="att-wizard-step">
          <div className="att-step-header">
            <span className="att-step-num">2</span>
            <span>Choose Correction Type &amp; Proposed Timings</span>
          </div>

          <div className="att-kind-options">
            <button
              type="button"
              className={`att-kind-btn ${kind === 3 ? 'active' : ''}`}
              onClick={() => setKind(3)}
            >
              <Clock size={14} />
              <span>Fix Check-Out (Missing Punch)</span>
            </button>
            <button
              type="button"
              className={`att-kind-btn ${kind === 2 ? 'active' : ''}`}
              onClick={() => setKind(2)}
            >
              <CheckCircle2 size={14} />
              <span>Fix Check-In Time</span>
            </button>
            <button
              type="button"
              className={`att-kind-btn ${kind === 4 ? 'active' : ''}`}
              onClick={() => setKind(4)}
            >
              <SlidersHorizontal size={14} />
              <span>Adjust Full Shift Duration</span>
            </button>
          </div>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: kind === 4 ? '1fr 1fr' : '1fr',
              gap: '10px',
              marginTop: '4px',
            }}
          >
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
                {kind === 3 ? 'Proposed Check-Out Time' : 'Proposed Check-In Time'} (Pakistan Local
                Time)
              </label>
              <input
                type="time"
                required
                value={localStartTime}
                onChange={(e) => setLocalStartTime(e.target.value)}
                style={{
                  height: '32px',
                  border: '1px solid #cbd5e1',
                  borderRadius: '5px',
                  padding: '0 8px',
                  fontSize: '12px',
                }}
              />
            </div>

            {kind === 4 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
                  Proposed Shift End Time (Pakistan Local Time)
                </label>
                <input
                  type="time"
                  required
                  value={localEndTime}
                  onChange={(e) => setLocalEndTime(e.target.value)}
                  style={{
                    height: '32px',
                    border: '1px solid #cbd5e1',
                    borderRadius: '5px',
                    padding: '0 8px',
                    fontSize: '12px',
                  }}
                />
              </div>
            )}
          </div>
        </div>

        {/* Step 3: Reason & Evidence */}
        <div className="att-wizard-step" style={{ borderBottom: 'none' }}>
          <div className="att-step-header">
            <span className="att-step-num">3</span>
            <span>Reason &amp; Justification</span>
          </div>

          <div className="att-reason-chips">
            <span style={{ fontSize: '11px', color: '#64748b' }}>Quick suggestions:</span>
            {quickReasons.map((r) => (
              <button
                key={r}
                type="button"
                className="att-reason-chip"
                onClick={() => setReason(r)}
              >
                {r}
              </button>
            ))}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', marginTop: '6px' }}>
            <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
              Reason / Explanation *
            </label>
            <input
              type="text"
              required
              placeholder="e.g. Employee completed scheduled site duty but biometric terminal was unresponsive at checkout"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              style={{
                height: '34px',
                border: '1px solid #cbd5e1',
                borderRadius: '5px',
                padding: '0 10px',
                fontSize: '12px',
              }}
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', marginTop: '6px' }}>
            <label style={{ fontSize: '11px', fontWeight: 600, color: '#475569' }}>
              Supporting Evidence / Reference
            </label>
            <input
              type="text"
              placeholder="e.g. Site Supervisor log verified; manual attendance register entry #42"
              value={evidence}
              onChange={(e) => setEvidence(e.target.value)}
              style={{
                height: '34px',
                border: '1px solid #cbd5e1',
                borderRadius: '5px',
                padding: '0 10px',
                fontSize: '12px',
              }}
            />
          </div>

          {action.error && (
            <div
              style={{
                padding: '8px 12px',
                background: '#fef2f2',
                border: '1px solid #fecaca',
                color: '#b91c1c',
                borderRadius: '5px',
                fontSize: '11.5px',
                marginTop: '8px',
              }}
            >
              {action.error}
            </div>
          )}
        </div>

        {/* Wizard Form Actions */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'flex-end',
            gap: '8px',
            paddingTop: '10px',
            borderTop: '1px solid #e2e8f0',
          }}
        >
          <button type="button" className="att-action-btn" onClick={onCancel}>
            Cancel
          </button>
          <button
            type="submit"
            className="att-action-btn primary"
            disabled={
              action.pending ||
              !sourceData ||
              sourceData.periodLocked ||
              !reason.trim() ||
              !evidence.trim()
            }
          >
            {action.pending ? 'Submitting Proposal...' : 'Submit Correction Request'}
          </button>
        </div>
      </form>
    </div>
  );
}

/* ==========================================================================
   CORRECTION DETAIL & REVIEW MODAL
   ========================================================================== */
function CorrectionDetailModal({
  requestId,
  employeeId,
  self,
  canReview,
  employeeMap,
  onClose,
  onUpdated,
}: {
  requestId: string;
  employeeId: string;
  self: boolean;
  canReview: boolean;
  employeeMap: Map<string, { name: string; code: string; initials: string }>;
  onClose: () => void;
  onUpdated: () => void;
}) {
  const action = useAttendanceAction();

  const [decision, setDecision] = useState<number>(1); // 1 = Approve, 2 = Return, 3 = Reject
  const [decisionReason, setDecisionReason] = useState('');

  const detailPath = canReview
    ? `/attendance/employees/${employeeId}/corrections/${requestId}`
    : `/attendance/employees/${employeeId}/correction-requests/${requestId}`;

  const detailQuery = useAttendanceQuery(detailPath, correctionDetail, true);
  const data = !detailQuery.isError ? detailQuery.data : undefined;
  const req = data?.request;
  const captured = data?.capturedSource;

  const emp = employeeMap.get(employeeId.toLowerCase());
  const empName = emp?.name || (self ? 'My Account' : 'Staff Member');
  const empCode = <EmployeeReference employeeId={employeeId} />;

  async function handleDecision(e: FormEvent) {
    e.preventDefault();
    if (!req || !decisionReason.trim()) return;

    const result = await action.run(
      `/attendance/employees/${employeeId}/corrections/${requestId}/decide`,
      {
        expectedVersion: req.version,
        decision,
        reason: decisionReason.trim(),
      },
      'POST',
      correctionDecision,
    );

    if (result) {
      onUpdated();
    }
  }

  async function handleCancel() {
    if (!req) return;
    const result = await action.run(
      `/attendance/employees/${employeeId}/correction-requests/${requestId}/cancel`,
      {
        expectedVersion: req.version,
        reason: 'Cancelled by requester',
      },
      'POST',
      correction,
    );
    if (result) {
      onUpdated();
    }
  }

  return (
    <div className="att-modal-backdrop" onClick={onClose}>
      <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="att-modal-header">
          <h3>
            <CalendarCheck2 size={16} style={{ color: '#2563eb' }} />
            Attendance Correction Review &middot; #{requestId.slice(0, 8)}
          </h3>
          <button type="button" className="att-action-btn" onClick={onClose}>
            <X size={14} />
          </button>
        </div>

        <div className="att-modal-body">
          <QueryState query={detailQuery} />

          {req && (
            <>
              {/* Staff Header Info */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  background: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  borderRadius: '6px',
                  padding: '8px 12px',
                }}
              >
                <div>
                  <strong style={{ fontSize: '13px', color: '#0f172a' }}>{empName}</strong>
                  <span style={{ fontSize: '11px', color: '#64748b', marginLeft: '6px' }}>
                    ({empCode})
                  </span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <span style={{ fontSize: '11px', color: '#64748b' }}>Business Date:</span>
                  <span style={{ fontWeight: 700, color: '#0f172a' }}>{req.businessDate}</span>
                </div>
              </div>

              {/* Before vs After Comparison */}
              <div className="att-compare-grid">
                <div className="att-compare-card">
                  <span className="att-compare-title">
                    <Clock size={12} /> Captured Original Record
                  </span>
                  <div className="att-compare-row">
                    <span className="att-compare-label">Recorded Check-In:</span>
                    <span className="att-compare-value">
                      {captured?.originalTimeUtc
                        ? formatLocalTime(captured.originalTimeUtc)
                        : 'Missing / None'}
                    </span>
                  </div>
                  <div className="att-compare-row">
                    <span className="att-compare-label">Scheduled Duty:</span>
                    <span className="att-compare-value">
                      {captured?.historicalRoster?.startsAt && captured?.historicalRoster?.endsAt
                        ? `${formatLocalTime(captured.historicalRoster.startsAt)} - ${formatLocalTime(captured.historicalRoster.endsAt)}`
                        : 'Standard Duty'}
                    </span>
                  </div>
                  <div className="att-compare-row">
                    <span className="att-compare-label">Original Assessment:</span>
                    <span className="att-compare-value">
                      {captured?.assessmentOutcome ?? 'Unassessed'}
                    </span>
                  </div>
                </div>

                <div className="att-compare-card target">
                  <span className="att-compare-title">
                    <CheckCircle2 size={12} /> Proposed Correction
                  </span>
                  <div className="att-compare-row">
                    <span className="att-compare-label">Correction Type:</span>
                    <span className="att-compare-value">{req.kind}</span>
                  </div>
                  <div className="att-compare-row">
                    <span className="att-compare-label">Requested Time:</span>
                    <span className="att-compare-value" style={{ color: '#1d4ed8' }}>
                      {formatLocalTime(req.requestedTimeUtc)}
                      {req.requestedEndUtc && ` → ${formatLocalTime(req.requestedEndUtc)}`}
                    </span>
                  </div>
                  <div className="att-compare-row">
                    <span className="att-compare-label">Current State:</span>
                    <span className="att-compare-value">{req.state}</span>
                  </div>
                </div>
              </div>

              {/* Reason & Written Evidence */}
              <div className="att-reason-box">
                <strong>Requester Reason:</strong>
                <span>{req.reason}</span>
              </div>

              {data?.writtenEvidence && (
                <div className="att-reason-box">
                  <strong>Supporting Written Evidence:</strong>
                  <span>{data.writtenEvidence}</span>
                </div>
              )}

              {/* Decision Section for Authorized Reviewer */}
              {req.state === 'Pending' && canReview && (
                <form
                  onSubmit={(e) => void handleDecision(e)}
                  style={{
                    borderTop: '1px solid #e2e8f0',
                    paddingTop: '12px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                  }}
                >
                  <strong style={{ fontSize: '12px', color: '#0f172a' }}>
                    HR Authorization Decision
                  </strong>

                  <div className="att-decision-pills">
                    <div
                      className={`att-decision-pill approve ${decision === 1 ? 'active' : ''}`}
                      onClick={() => setDecision(1)}
                    >
                      <CheckCircle2 size={16} />
                      <span>Approve</span>
                    </div>

                    <div
                      className={`att-decision-pill return ${decision === 2 ? 'active' : ''}`}
                      onClick={() => setDecision(2)}
                    >
                      <RotateCcw size={16} />
                      <span>Return</span>
                    </div>

                    <div
                      className={`att-decision-pill reject ${decision === 3 ? 'active' : ''}`}
                      onClick={() => setDecision(3)}
                    >
                      <XCircle size={16} />
                      <span>Reject</span>
                    </div>
                  </div>

                  <input
                    type="text"
                    placeholder="Enter decision comments or clarification note..."
                    value={decisionReason}
                    onChange={(e) => setDecisionReason(e.target.value)}
                    style={{
                      height: '32px',
                      border: '1px solid #cbd5e1',
                      borderRadius: '5px',
                      padding: '0 8px',
                      fontSize: '12px',
                      marginTop: '4px',
                    }}
                  />

                  {action.error && (
                    <div style={{ color: '#b91c1c', fontSize: '11px' }}>{action.error}</div>
                  )}

                  <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '6px' }}>
                    <button
                      type="submit"
                      className="att-action-btn primary"
                      disabled={action.pending || !decisionReason.trim()}
                    >
                      {action.pending ? 'Saving Decision...' : 'Authorize Decision'}
                    </button>
                  </div>
                </form>
              )}

              {/* Cancel Option for Requester */}
              {req.state === 'Pending' && !canReview && (
                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                  <button
                    type="button"
                    className="att-action-btn"
                    style={{ color: '#b91c1c', borderColor: '#fca5a5' }}
                    onClick={() => void handleCancel()}
                    disabled={action.pending}
                  >
                    Cancel This Request
                  </button>
                </div>
              )}
            </>
          )}
        </div>

        <div className="att-modal-footer">
          <button type="button" className="att-action-btn" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

/* ==========================================================================
   TIME FORMATTER HELPERS
   ========================================================================== */
function formatLocalTime(utcStr: string | null | undefined): string {
  if (!utcStr) return '—';
  try {
    const d = new Date(utcStr);
    return d.toLocaleTimeString('en-PK', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true,
      timeZone: 'Asia/Karachi',
    });
  } catch {
    return utcStr;
  }
}

function localToUtc(businessDate: string, timeStr: string): string {
  const d = new Date(`${businessDate}T${timeStr}:00+05:00`);
  return d.toISOString();
}
