import { useMemo, useState, type FormEvent } from 'react';
import { z } from 'zod';
import {
  BadgeCheck,
  Briefcase,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Clock,
  Coins,
  FileCheck,
  FileText,
  Filter,
  Plus,
  Receipt,
  RefreshCw,
  Search,
  ShieldCheck,
  Users,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { OutstationWorkspace } from '../outstation/OutstationWorkspace';
import { canDeployment } from '../outstation/contracts';
import { Denied, ActionMessage } from '../components/Feedback';
import { FinanceQueryState as QueryState } from '../components/finance/FinanceQueryState';
import '../components/finance/allowances.css';
import {
  FinanceStatus,
} from '../components/finance/FinanceChrome';
import {
  allowanceKindOptions,
  approvalStateOptions,
  dayRange,
  decisionOptions,
  money,
  policyTargetOptions,
} from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { queryString } from '../api/hr-finance-api';
import { labelDate } from '../utils/messages';
import {
  allowanceClaim,
  allowanceDetail,
  allowancePage,
  policyAssignmentPage,
  policyAssignmentResult,
  policyPage,
  policyPublicationPreview,
  policyPublicationResult,
  policyView,
  policyVersionPage,
  type PolicyView,
} from '../schemas/finance';

const permissions = {
  review: 'attendance.allowances.review',
  decide: 'attendance.allowances.decide',
  settlement: 'attendance.allowances.settlement',
  policy: 'attendance.allowances.policy',
};

export function AllowancesPage() {
  const { user, status } = useAuth();
  const epoch = useMemo(() => crypto.randomUUID(), [user, status]);
  if (status !== 'authenticated') return <Denied />;
  return <AllowancesContent key={epoch} />;
}

function AllowancesContent() {
  const [outstationBusy, setOutstationBusy] = useState(false);
  const access = useFinanceAccess();

  const tabs = [
    ...(access.can(permissions.review) ? [{ id: 'review', label: 'Review Queue', icon: Receipt }] : []),
    ...(access.can(permissions.policy) ? [{ id: 'policies', label: 'Allowance Policies', icon: ShieldCheck }] : []),
    ...(canDeployment(access.user, 'read')
      ? [{ id: 'outstation', label: 'Outstation Management', icon: Briefcase }]
      : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');

  // Pre-fetch count stats for executive KPI strip
  const policiesQuery = useFinanceQuery(
    '/hr/allowance-policies?page=1&pageSize=1',
    policyPage,
    access.can(permissions.policy),
  );

  const initialRange = useMemo(() => dayRange(30), []);
  const pendingReviewQuery = useFinanceQuery(
    '/attendance/allowances/review' +
      queryString({
        from: initialRange.from,
        to: initialRange.to,
        state: 0, // Pending
        page: 1,
        pageSize: 1,
      }),
    allowancePage,
    access.can(permissions.review),
  );

  if (!tabs.length) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');

  return (
    <section className="allowances-page" aria-label="Allowances Management">
      {/* Top Header & Breadcrumb Ribbon */}
      <header className="allowance-header-container">
        <div>
          <div className="allowance-breadcrumb">
            <span className="allowance-breadcrumb-link">Human Resources</span>
            <span>/</span>
            <span style={{ color: '#0f172a', fontWeight: 600 }}>Overtime & Allowances</span>
          </div>
          <div className="allowance-title-row">
            <h1 className="allowance-main-title">Overtime & Allowances</h1>
          </div>
          <p className="allowance-subtitle">
            Audit allowance intake claims, configure compensation policies, and process settlement disbursements.
          </p>
        </div>

        <div className="allowance-top-actions">
          <button
            type="button"
            className="btn-alw-secondary"
            onClick={() => {
              void policiesQuery.refetch();
              void pendingReviewQuery.refetch();
            }}
            title="Refresh statistics and active views"
          >
            <RefreshCw size={13} className={policiesQuery.isFetching ? 'spin' : ''} />
            <span>Refresh</span>
          </button>
        </div>
      </header>

      {/* 4 Executive KPI Metrics Cards */}
      <div className="allowance-kpis-grid">
        <div className="allowance-kpi-card">
          <div className="alw-kpi-icon-wrap alw-bg-blue">
            <ShieldCheck size={20} />
          </div>
          <div className="alw-kpi-content">
            <span className="alw-kpi-label">Active Policies</span>
            <div className="alw-kpi-value">{policiesQuery.data?.items.length ?? 0} Defined</div>
            <span className="alw-kpi-sub">Governing duty compensation</span>
          </div>
        </div>

        <div className="allowance-kpi-card">
          <div className="alw-kpi-icon-wrap alw-bg-amber">
            <Clock size={20} />
          </div>
          <div className="alw-kpi-content">
            <span className="alw-kpi-label">Pending Review</span>
            <div className="alw-kpi-value" style={{ color: '#b45309' }}>
              {pendingReviewQuery.data?.items.length ?? 0} Claims
            </div>
            <span className="alw-kpi-sub">Awaiting HR authorization</span>
          </div>
        </div>

        <div className="allowance-kpi-card">
          <div className="alw-kpi-icon-wrap alw-bg-emerald">
            <Coins size={20} />
          </div>
          <div className="alw-kpi-content">
            <span className="alw-kpi-label">Settlement Pipeline</span>
            <div className="alw-kpi-value" style={{ color: '#047857' }}>
              PKR Ready
            </div>
            <span className="alw-kpi-sub">External reference reconciliation</span>
          </div>
        </div>

        <div className="allowance-kpi-card">
          <div className="alw-kpi-icon-wrap alw-bg-violet">
            <Briefcase size={20} />
          </div>
          <div className="alw-kpi-content">
            <span className="alw-kpi-label">Outstation Travel</span>
            <div className="alw-kpi-value" style={{ color: '#7c3aed' }}>
              Active
            </div>
            <span className="alw-kpi-sub">Deployment vouchers & claims</span>
          </div>
        </div>
      </div>

      {/* Modern Navigation Tabs Strip */}
      <fieldset disabled={outstationBusy} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
        <div className="allowance-nav-tabs" role="tablist" aria-label="Allowance Views">
          {tabs.map((t) => {
            const Icon = t.icon;
            const isActive = active === t.id;
            return (
              <button
                type="button"
                key={t.id}
                role="tab"
                aria-selected={isActive}
                className={`allowance-tab-btn ${isActive ? 'active' : ''}`}
                onClick={() => setTab(t.id)}
              >
                <Icon size={14} />
                <span>{t.label}</span>
                {t.id === 'review' && (pendingReviewQuery.data?.items.length ?? 0) > 0 && (
                  <span className="allowance-tab-badge">
                    {pendingReviewQuery.data?.items.length}
                  </span>
                )}
              </button>
            );
          })}
        </div>
      </fieldset>

      {/* Tab Panels */}
      {active === 'outstation' && <OutstationWorkspace onBusyChange={setOutstationBusy} />}
      {active === 'review' && (
        <AllowanceReview
          canDecide={access.can(permissions.decide)}
          canSettle={access.can(permissions.settlement)}
        />
      )}
      {active === 'policies' && <AllowancePolicies />}
    </section>
  );
}

function AllowanceReview({ canDecide, canSettle }: { canDecide: boolean; canSettle: boolean }) {
  const initial = useMemo(() => dayRange(30), []);
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [state, setState] = useState('0'); // Default: Pending (0)
  const [employeeId, setEmployeeId] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<{ employeeId: string; id: string } | null>(null);
  const [decision, setDecision] = useState('1');
  const [decisionReason, setDecisionReason] = useState('');
  const [paymentReference, setPaymentReference] = useState('');
  const [settlementReason, setSettlementReason] = useState('');
  const [activePreset, setActivePreset] = useState<'7' | '30' | '90' | 'all'>('30');
  const action = useFinanceAction();
  const employeeFilterValid = !employeeId.trim() || z.uuid().safeParse(employeeId.trim()).success;

  const queue = useFinanceQuery(
    '/attendance/allowances/review' +
      queryString({
        from,
        to,
        employeeId: employeeId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 25,
      }),
    allowancePage,
    true,
    dayRangeValid(from, to) && employeeFilterValid,
  );

  const detail = useFinanceQuery(
    selected
      ? `/attendance/allowances/employees/${selected.employeeId}/claims/${selected.id}`
      : '/attendance/allowances/review',
    allowanceDetail,
    Boolean(selected),
    Boolean(selected),
  );

  const handleApplyPreset = (days: number, key: '7' | '30' | '90' | 'all') => {
    setActivePreset(key);
    const range = dayRange(days);
    setFrom(range.from);
    setTo(range.to);
    setPage(1);
  };

  async function decide(event: FormEvent) {
    event.preventDefault();
    if (!selected || detail.data?.claim.kind === 5) return;
    const result = await action.run(
      `/attendance/allowances/employees/${selected.employeeId}/claims/${selected.id}/decide`,
      {
        expectedVersion: detail.data?.claim.version,
        decision: Number(decision),
        reason: decisionReason,
      },
      'POST',
      allowanceClaim,
    );
    if (result) {
      setDecisionReason('');
      void queue.refetch();
      void detail.refetch();
    }
  }

  async function settle(event: FormEvent) {
    event.preventDefault();
    if (!selected || detail.data?.claim.kind === 5) return;
    const result = await action.run(
      `/attendance/allowances/employees/${selected.employeeId}/claims/${selected.id}/settlement`,
      {
        expectedVersion: detail.data?.claim.version,
        externalPaymentReference: paymentReference,
        reason: settlementReason,
      },
      'POST',
      allowanceClaim,
    );
    if (result) {
      setPaymentReference('');
      setSettlementReason('');
      void queue.refetch();
      void detail.refetch();
    }
  }

  return (
    <div className="allowance-card">
      <div className="allowance-card-header">
        <div>
          <h2 className="allowance-card-title">
            <Filter size={16} color="#2563eb" />
            <span>Review Queue & Claims Filter</span>
          </h2>
          <p className="allowance-card-desc">
            Filter intake receipts by verification state, business dates, and scoped employee IDs.
          </p>
        </div>
      </div>

      {/* Preset Chips & Input Bar */}
      <div className="allowance-filter-wrap">
        <div className="allowance-presets-bar">
          <span style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>Quick Windows:</span>
          <button
            type="button"
            className={`preset-chip-btn ${activePreset === '7' ? 'active' : ''}`}
            onClick={() => handleApplyPreset(7, '7')}
          >
            Last 7 Days
          </button>
          <button
            type="button"
            className={`preset-chip-btn ${activePreset === '30' ? 'active' : ''}`}
            onClick={() => handleApplyPreset(30, '30')}
          >
            Last 30 Days
          </button>
          <button
            type="button"
            className={`preset-chip-btn ${activePreset === '90' ? 'active' : ''}`}
            onClick={() => handleApplyPreset(90, '90')}
          >
            Last 90 Days
          </button>
          <button
            type="button"
            className={`preset-chip-btn ${state === '0' ? 'active' : ''}`}
            onClick={() => {
              setState('0');
              setPage(1);
            }}
          >
            Pending Only
          </button>
          <button
            type="button"
            className={`preset-chip-btn ${state === '' ? 'active' : ''}`}
            onClick={() => {
              setState('');
              setPage(1);
            }}
          >
            All States
          </button>
        </div>

        <div className="allowance-filter-inputs-grid">
          <div className="allowance-form-group">
            <label className="allowance-form-label">From Date</label>
            <input
              type="date"
              className="allowance-input"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
                setActivePreset('all');
              }}
            />
          </div>

          <div className="allowance-form-group">
            <label className="allowance-form-label">To Date</label>
            <input
              type="date"
              className="allowance-input"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
                setActivePreset('all');
              }}
            />
          </div>

          <div className="allowance-form-group">
            <label className="allowance-form-label">Approval State</label>
            <select
              className="allowance-select"
              value={state}
              onChange={(e) => {
                setState(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All States</option>
              {approvalStateOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div className="allowance-form-group">
            <label className="allowance-form-label">Employee ID (UUID)</label>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                className="allowance-input"
                style={{ width: '100%', paddingRight: employeeId ? '28px' : '10px' }}
                placeholder="Search by Employee UUID..."
                value={employeeId}
                onChange={(e) => {
                  setEmployeeId(e.target.value);
                  setPage(1);
                }}
              />
              {employeeId && (
                <button
                  type="button"
                  onClick={() => {
                    setEmployeeId('');
                    setPage(1);
                  }}
                  style={{
                    position: 'absolute',
                    right: '6px',
                    top: '50%',
                    transform: 'translateY(-50%)',
                    background: 'none',
                    border: 'none',
                    color: '#94a3b8',
                    cursor: 'pointer',
                    padding: 0,
                  }}
                  title="Clear employee filter"
                >
                  <X size={14} />
                </button>
              )}
            </div>
            {!employeeFilterValid && (
              <span style={{ fontSize: '10.5px', color: '#dc2626', marginTop: '2px' }}>
                Enter a complete UUID or leave blank.
              </span>
            )}
          </div>
        </div>
      </div>

      {!dayRangeValid(from, to) && (
        <div style={{ padding: '8px 12px', background: '#fef2f2', border: '1px solid #fee2e2', borderRadius: '6px', color: '#b91c1c', fontSize: '12px' }}>
          Choose a valid date window (end date after start date, max 366 days).
        </div>
      )}

      {/* Claims List View */}
      {dayRangeValid(from, to) && employeeFilterValid && (
        <QueryState query={queue}>
          {!queue.data?.items.length ? (
            <div className="allowance-empty-state">
              <div className="allowance-empty-icon">
                <Receipt size={24} />
              </div>
              <h4 className="allowance-empty-title">No allowance claims match this filter</h4>
              <p className="allowance-empty-desc">
                There are no claims recorded between {from} and {to} for the selected state. Try expanding the date window or clearing the filters.
              </p>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  type="button"
                  className="btn-alw-secondary"
                  onClick={() => handleApplyPreset(90, '90')}
                >
                  Expand to Last 90 Days
                </button>
                <button
                  type="button"
                  className="btn-alw-primary"
                  onClick={() => {
                    setState('');
                    setEmployeeId('');
                    handleApplyPreset(30, '30');
                  }}
                >
                  Reset All Filters
                </button>
              </div>
            </div>
          ) : (
            <div className="claims-list-grid">
              {queue.data?.items.map((claim) => {
                const isSelected = selected?.id === claim.id;
                const kindLabel =
                  allowanceKindOptions[claim.kind - 1]?.label ?? `Allowance Kind #${claim.kind}`;

                return (
                  <article
                    className={`claim-item-card ${isSelected ? 'selected' : ''}`}
                    key={claim.id}
                  >
                    <div className="claim-card-top">
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                        <span className="claim-emp-badge" title={`Employee: ${claim.employeeId}`}>
                          <Users size={12} color="#475569" />
                          <span>{claim.employeeId.slice(0, 8)}...</span>
                        </span>
                        <span className="claim-kind-pill">{kindLabel}</span>
                        <span
                          className={`alw-badge ${
                            claim.state.toLowerCase().includes('approved')
                              ? 'alw-badge-approved'
                              : claim.state.toLowerCase().includes('rejected')
                                ? 'alw-badge-rejected'
                                : claim.state.toLowerCase().includes('settled')
                                  ? 'alw-badge-settled'
                                  : 'alw-badge-pending'
                          }`}
                        >
                          <FinanceStatus value={claim.state} />
                        </span>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                        <span className="claim-amount-highlight">
                          {money(claim.claimedAmount, claim.currency)}
                        </span>
                        <button
                          type="button"
                          className={isSelected ? 'btn-alw-primary' : 'btn-alw-secondary'}
                          style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                          onClick={() =>
                            setSelected(
                              isSelected ? null : { employeeId: claim.employeeId, id: claim.id },
                            )
                          }
                        >
                          {isSelected ? 'Close Claim' : 'Review Claim'}
                        </button>
                      </div>
                    </div>

                    {/* Metadata Grid */}
                    <div className="claim-grid-metrics">
                      <div className="claim-metric-item">
                        <dt>Payable Date</dt>
                        <dd>{claim.payableDate}</dd>
                      </div>
                      <div className="claim-metric-item">
                        <dt>Settlement State</dt>
                        <dd>{claim.settlementState}</dd>
                      </div>
                      <div className="claim-metric-item">
                        <dt>Created Date</dt>
                        <dd>{labelDate(claim.createdAt)}</dd>
                      </div>
                      <div className="claim-metric-item">
                        <dt>Claim Reference</dt>
                        <dd title={claim.id}>{claim.id.slice(0, 13)}...</dd>
                      </div>
                    </div>

                    {/* In-depth Review Drawer */}
                    {isSelected && (
                      <div className="claim-review-drawer">
                        <div className="claim-review-header">
                          <span className="claim-review-title">
                            <FileCheck size={15} />
                            <span>Audit & Evaluation Verification Detail</span>
                          </span>
                          <span style={{ fontSize: '11px', color: '#64748b' }}>
                            ID: {claim.id}
                          </span>
                        </div>

                        <QueryState query={detail}>
                          {detail.data && (
                            <>
                              <div className="eligibility-box">
                                <div className="eligibility-item">
                                  <dt>Eligibility State</dt>
                                  <dd style={{ color: '#2563eb' }}>
                                    {detail.data.eligibility.state}
                                  </dd>
                                </div>
                                <div className="eligibility-item">
                                  <dt>Required Continuous Hours</dt>
                                  <dd>{detail.data.eligibility.requiredHours ?? 'N/A'}</dd>
                                </div>
                                <div className="eligibility-item">
                                  <dt>Longest Recorded Cycle</dt>
                                  <dd>{detail.data.eligibility.continuousHours ?? 'N/A'} hrs</dd>
                                </div>
                                <div className="eligibility-item">
                                  <dt>Server Rationale</dt>
                                  <dd>{detail.data.reason || 'Verified via scheduled shift window.'}</dd>
                                </div>
                              </div>

                              {detail.data.claim.kind === 5 && (
                                <div style={{ padding: '8px 12px', background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '6px', fontSize: '11.5px', color: '#1e40af' }}>
                                  Outstation claims require retained deployment evidence. Independent deployment review must be completed.
                                </div>
                              )}

                              {detail.data.claim.kind !== 5 && (
                                <div className="review-actions-split">
                                  {/* Decision Form */}
                                  {canDecide && (
                                    <form className="review-action-card" onSubmit={(e) => void decide(e)}>
                                      <span className="review-action-title">
                                        <BadgeCheck size={14} color="#2563eb" />
                                        <span>HR Approval Decision</span>
                                      </span>
                                      <div className="allowance-form-group">
                                        <label className="allowance-form-label">Decision Outcome</label>
                                        <select
                                          className="allowance-select"
                                          value={decision}
                                          onChange={(e) => setDecision(e.target.value)}
                                        >
                                          {decisionOptions.map((opt) => (
                                            <option key={opt.value} value={opt.value}>
                                              {opt.label}
                                            </option>
                                          ))}
                                        </select>
                                      </div>
                                      <div className="allowance-form-group">
                                        <label className="allowance-form-label">Decision Note / Reason</label>
                                        <textarea
                                          className="allowance-textarea"
                                          rows={2}
                                          placeholder="Enter approval rationale or rejection explanation..."
                                          value={decisionReason}
                                          onChange={(e) => setDecisionReason(e.target.value)}
                                          required
                                        />
                                      </div>
                                      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '4px' }}>
                                        <button
                                          type="submit"
                                          className="btn-alw-primary"
                                          disabled={action.pending || !decisionReason.trim()}
                                        >
                                          {action.pending ? 'Saving...' : 'Submit Decision'}
                                        </button>
                                      </div>
                                    </form>
                                  )}

                                  {/* Settlement Form */}
                                  {canSettle && (
                                    <form className="review-action-card" onSubmit={(e) => void settle(e)}>
                                      <span className="review-action-title">
                                        <Coins size={14} color="#059669" />
                                        <span>Settlement & Payment Reference</span>
                                      </span>
                                      <div className="allowance-form-group">
                                        <label className="allowance-form-label">External Payment Voucher Ref</label>
                                        <input
                                          type="text"
                                          className="allowance-input"
                                          placeholder="e.g. TR-2026-9812 / Bank Ref"
                                          value={paymentReference}
                                          onChange={(e) => setPaymentReference(e.target.value)}
                                          required
                                        />
                                      </div>
                                      <div className="allowance-form-group">
                                        <label className="allowance-form-label">Settlement Reason</label>
                                        <textarea
                                          className="allowance-textarea"
                                          rows={2}
                                          placeholder="e.g. Verified and queued for monthly payroll batch"
                                          value={settlementReason}
                                          onChange={(e) => setSettlementReason(e.target.value)}
                                          required
                                        />
                                      </div>
                                      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '4px' }}>
                                        <button
                                          type="submit"
                                          className="btn-alw-primary"
                                          style={{ background: '#059669' }}
                                          disabled={
                                            action.pending ||
                                            !paymentReference.trim() ||
                                            !settlementReason.trim()
                                          }
                                        >
                                          {action.pending ? 'Recording...' : 'Record Payment'}
                                        </button>
                                      </div>
                                    </form>
                                  )}
                                </div>
                              )}
                            </>
                          )}
                        </QueryState>
                      </div>
                    )}
                  </article>
                );
              })}

              {/* Pagination Controls */}
              {queue.data && (
                <div className="allowance-pagination-bar">
                  <span>Page {page} of results</span>
                  <div style={{ display: 'flex', gap: '6px' }}>
                    <button
                      type="button"
                      className="btn-alw-secondary"
                      style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                      disabled={queue.isFetching || page <= 1}
                      onClick={() => setPage((v) => Math.max(1, v - 1))}
                    >
                      <ChevronLeft size={14} />
                      Previous
                    </button>
                    <button
                      type="button"
                      className="btn-alw-secondary"
                      style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                      disabled={queue.isFetching || !queue.data.hasMore}
                      onClick={() => setPage((v) => v + 1)}
                    >
                      Next
                      <ChevronRight size={14} />
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </QueryState>
      )}

      <ActionMessage action={action} />
    </div>
  );
}

function AllowancePolicies() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [selected, setSelected] = useState<PolicyView | null>(null);
  const [isCreatingOpen, setIsCreatingOpen] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [createReason, setCreateReason] = useState('');
  const create = useFinanceAction();
  const publish = useFinanceAction();
  const assign = useFinanceAction();
  const access = useFinanceAccess();

  const [rules, setRules] = useState({
    currency: 'PKR',
    officeDutyStartLocalTime: '17:00',
    continuityGapToleranceMinutes: '30',
    officeMealHours: '8',
    officeMealAmount: '',
    siteDinnerHours: '8',
    siteDinnerAmount: '',
    siteBreakfastHours: '2',
    siteBreakfastAmount: '',
    siteLunchHours: '1',
    siteLunchAmount: '',
    outstationDailyAmount: '',
    workTravelAmount: '',
    effectiveFrom: '',
    effectiveTo: '',
    reason: '',
  });
  const [preview, setPreview] = useState<z.infer<typeof policyPublicationPreview> | null>(null);
  const [assignForm, setAssignForm] = useState({
    policyVersionId: '',
    target: '1',
    targetId: '',
    effectiveFrom: '',
    effectiveTo: '',
    reason: '',
  });

  const canPolicy = access.can(permissions.policy);
  const list = useFinanceQuery(
    '/hr/allowance-policies' + queryString({ page, pageSize: 25, search: term || undefined }),
    policyPage,
    canPolicy,
  );
  const versions = useFinanceQuery(
    selected
      ? `/hr/allowance-policies/${selected.id}/versions` + queryString({ page: 1, pageSize: 50 })
      : '/hr/allowance-policies',
    policyVersionPage,
    canPolicy && Boolean(selected),
    canPolicy && Boolean(selected),
  );
  const assignments = useFinanceQuery(
    selected
      ? `/hr/allowance-policies/${selected.id}/assignments` + queryString({ page: 1, pageSize: 50 })
      : '/hr/allowance-policies',
    policyAssignmentPage,
    canPolicy && Boolean(selected),
    canPolicy && Boolean(selected),
  );

  function rulesBody() {
    const meal = (hours: string, amount: string) => ({
      requiredContinuousDutyHours: hours === '' ? null : Number(hours),
      amount: amount === '' ? null : Number(amount),
    });
    return {
      schemaVersion: 1,
      currency: rules.currency.trim().toUpperCase(),
      officeDutyStartLocalTime: rules.officeDutyStartLocalTime,
      continuityGapToleranceMinutes: Number(rules.continuityGapToleranceMinutes),
      officeMeal: meal(rules.officeMealHours, rules.officeMealAmount),
      siteDinner: meal(rules.siteDinnerHours, rules.siteDinnerAmount),
      siteBreakfast: meal(rules.siteBreakfastHours, rules.siteBreakfastAmount),
      siteLunch: meal(rules.siteLunchHours, rules.siteLunchAmount),
      ...(rules.outstationDailyAmount
        ? {
            outstationDaily: {
              requiredContinuousDutyHours: null,
              amount: Number(rules.outstationDailyAmount),
            },
          }
        : {}),
      ...(rules.workTravelAmount
        ? {
            workTravel: {
              requiredContinuousDutyHours: null,
              amount: Number(rules.workTravelAmount),
            },
          }
        : {}),
    };
  }

  function publishBody() {
    return {
      expectedVersion: selected?.version,
      expectedRevision: selected?.revision,
      effectiveFrom: rules.effectiveFrom,
      effectiveTo: rules.effectiveTo || null,
      rules: rulesBody(),
    };
  }

  async function createPolicy(event: FormEvent) {
    event.preventDefault();
    const result = await create.run(
      '/hr/allowance-policies',
      { code, name, reason: createReason },
      'POST',
      policyView,
    );
    if (result) {
      setCode('');
      setName('');
      setCreateReason('');
      setIsCreatingOpen(false);
      setSelected(result);
      void list.refetch();
    }
  }

  async function previewRules() {
    if (!selected) return;
    const result = await publish.run(
      `/hr/allowance-policies/${selected.id}/publication-preview`,
      publishBody(),
      'POST',
      policyPublicationPreview,
    );
    setPreview(result ?? null);
  }

  async function publishRules(event: FormEvent) {
    event.preventDefault();
    if (!selected || !preview?.canPublish) return;
    const result = await publish.run(
      `/hr/allowance-policies/${selected.id}/versions`,
      {
        ...publishBody(),
        expectedPreviewFingerprint: preview.previewFingerprint,
        reason: rules.reason,
      },
      'POST',
      policyPublicationResult,
    );
    if (result) {
      setPreview(null);
      void versions.refetch();
      void list.refetch();
    }
  }

  async function assignVersion(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    const result = await assign.run(
      `/hr/allowance-policies/${selected.id}/assignments`,
      {
        expectedVersion: selected.version,
        expectedRevision: selected.revision,
        policyVersionId: assignForm.policyVersionId,
        target: Number(assignForm.target),
        targetId: assignForm.targetId,
        effectiveFrom: assignForm.effectiveFrom,
        effectiveTo: assignForm.effectiveTo || null,
        reason: assignForm.reason,
      },
      'POST',
      policyAssignmentResult,
    );
    if (result) {
      setAssignForm({
        policyVersionId: '',
        target: '1',
        targetId: '',
        effectiveFrom: '',
        effectiveTo: '',
        reason: '',
      });
      void assignments.refetch();
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {/* Collapsible Create Policy Definition Card */}
      <div className="policy-create-collapsible">
        <div
          className="policy-create-toggle-header"
          onClick={() => setIsCreatingOpen((o) => !o)}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => e.key === 'Enter' && setIsCreatingOpen((o) => !o)}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Plus size={16} color="#2563eb" />
            <span style={{ fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
              Create a Policy Definition
            </span>
            <span style={{ fontSize: '11px', color: '#64748b' }}>
              (Definitions carry no money value until rules are published)
            </span>
          </div>
          <button
            type="button"
            className="btn-alw-secondary"
            style={{ height: '26px', padding: '0 8px', fontSize: '11px' }}
          >
            {isCreatingOpen ? (
              <>
                <ChevronUp size={13} />
                <span>Collapse Form</span>
              </>
            ) : (
              <>
                <Plus size={13} />
                <span>+ New Definition</span>
              </>
            )}
          </button>
        </div>

        {isCreatingOpen && (
          <div className="policy-create-body">
            <form onSubmit={(e) => void createPolicy(e)}>
              <div className="policy-create-grid">
                <div className="allowance-form-group">
                  <label className="allowance-form-label">Policy Code *</label>
                  <input
                    type="text"
                    className="allowance-input"
                    placeholder="e.g. POL-OFF-01"
                    maxLength={40}
                    required
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                  />
                </div>

                <div className="allowance-form-group">
                  <label className="allowance-form-label">Policy Name *</label>
                  <input
                    type="text"
                    className="allowance-input"
                    placeholder="e.g. Standard Head Office Meal Policy"
                    maxLength={120}
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                  />
                </div>

                <div className="allowance-form-group">
                  <label className="allowance-form-label">Documented Business Reason *</label>
                  <input
                    type="text"
                    className="allowance-input"
                    placeholder="Purpose and executive approval rationale..."
                    required
                    value={createReason}
                    onChange={(e) => setCreateReason(e.target.value)}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '12px' }}>
                <button
                  type="button"
                  className="btn-alw-secondary"
                  onClick={() => setIsCreatingOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-alw-primary"
                  disabled={create.pending || !code.trim() || !name.trim() || !createReason.trim()}
                >
                  {create.pending ? 'Creating...' : 'Create Policy Definition'}
                </button>
              </div>
            </form>
            <ActionMessage action={create} />
          </div>
        )}
      </div>

      {/* Policies List Card */}
      <div className="allowance-card">
        <div className="allowance-card-header">
          <div>
            <h2 className="allowance-card-title">
              <ShieldCheck size={16} color="#2563eb" />
              <span>Registered Allowance Policies</span>
            </h2>
            <p className="allowance-card-desc">
              Manage version rules, rate definitions, and employee/department population assignments.
            </p>
          </div>

          {/* Search Bar */}
          <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
            <div style={{ position: 'relative', width: '220px' }}>
              <input
                type="text"
                className="allowance-input"
                style={{ width: '100%', paddingLeft: '28px' }}
                placeholder="Search policies..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    setTerm(search.trim());
                    setPage(1);
                  }
                }}
              />
              <Search
                size={13}
                color="#94a3b8"
                style={{ position: 'absolute', left: '9px', top: '50%', transform: 'translateY(-50%)' }}
              />
            </div>
            <button
              type="button"
              className="btn-alw-secondary"
              onClick={() => {
                setTerm(search.trim());
                setPage(1);
              }}
            >
              Search
            </button>
          </div>
        </div>

        <QueryState query={list}>
          {!list.data?.items.length ? (
            <div className="allowance-empty-state">
              <div className="allowance-empty-icon">
                <FileText size={24} />
              </div>
              <h4 className="allowance-empty-title">No allowance policies found</h4>
              <p className="allowance-empty-desc">
                No policy definitions match your criteria. Click "+ New Definition" above to register your first policy.
              </p>
            </div>
          ) : (
            <div className="policies-table-container">
              <table className="policies-erp-table">
                <thead>
                  <tr>
                    <th>Policy Code</th>
                    <th>Policy Name</th>
                    <th>Version & Rev</th>
                    <th>Reference</th>
                    <th style={{ textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {list.data?.items.map((policy) => {
                    const isSelected = selected?.id === policy.id;
                    return (
                      <tr key={policy.id} style={{ background: isSelected ? '#f8fafc' : undefined }}>
                        <td>
                          <span className="policy-code-pill">{policy.code}</span>
                        </td>
                        <td style={{ fontWeight: 600 }}>{policy.name}</td>
                        <td>
                          <span style={{ fontSize: '11.5px', color: '#475569' }}>
                            Ver #{policy.version} &middot; Rev #{policy.revision}
                          </span>
                        </td>
                        <td style={{ fontFamily: 'ui-monospace, monospace', fontSize: '11px', color: '#64748b' }}>
                          {policy.id.slice(0, 12)}...
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <button
                            type="button"
                            className={isSelected ? 'btn-alw-primary' : 'btn-alw-secondary'}
                            style={{ height: '26px', padding: '0 10px', fontSize: '11.5px' }}
                            onClick={() => {
                              setPreview(null);
                              setSelected(isSelected ? null : policy);
                            }}
                          >
                            {isSelected ? 'Close Policy' : 'Configure Rules'}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {/* Selected Policy Management Panel */}
          {selected && (
            <div
              style={{
                marginTop: '10px',
                border: '1px solid #bfdbfe',
                borderRadius: '8px',
                background: '#ffffff',
                padding: '16px',
                display: 'flex',
                flexDirection: 'column',
                gap: '16px',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #e2e8f0', paddingBottom: '10px' }}>
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <span className="policy-code-pill">{selected.code}</span>
                    <h3 style={{ margin: 0, fontSize: '14px', fontWeight: 700, color: '#0f172a' }}>
                      {selected.name}
                    </h3>
                  </div>
                  <span style={{ fontSize: '11px', color: '#64748b' }}>
                    ID: {selected.id} &middot; Version #{selected.version} &middot; Revision #{selected.revision}
                  </span>
                </div>
                <button
                  type="button"
                  className="btn-alw-secondary"
                  style={{ height: '26px', padding: '0 8px', fontSize: '11.5px' }}
                  onClick={() => setSelected(null)}
                >
                  Close
                </button>
              </div>

              {/* Published Versions History */}
              <div>
                <h4 style={{ margin: '0 0 8px', fontSize: '12.5px', fontWeight: 700, color: '#334155' }}>
                  1. Published Versions History
                </h4>
                <QueryState query={versions}>
                  {!versions.data?.items.length ? (
                    <div style={{ padding: '10px 12px', background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', fontSize: '12px', color: '#64748b' }}>
                      No version has been published yet. Complete the rules configuration below to publish Version #1.
                    </div>
                  ) : (
                    <div className="policies-table-container">
                      <table className="policies-erp-table">
                        <thead>
                          <tr>
                            <th>Ver #</th>
                            <th>Effective Window</th>
                            <th>Published Date</th>
                            <th>Authority</th>
                            <th>Checksum (SHA256)</th>
                          </tr>
                        </thead>
                        <tbody>
                          {versions.data?.items.map((ver) => (
                            <tr key={ver.id}>
                              <td style={{ fontWeight: 700, color: '#2563eb' }}>v{ver.number}</td>
                              <td>
                                {ver.effectiveFrom} {ver.effectiveTo ? `to ${ver.effectiveTo}` : '(Ongoing)'}
                              </td>
                              <td>{labelDate(ver.publishedAt)}</td>
                              <td>{ver.authority || 'System'}</td>
                              <td style={{ fontFamily: 'ui-monospace, monospace', fontSize: '10.5px', color: '#64748b' }}>
                                {ver.configurationSha256.slice(0, 16)}...
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </QueryState>
              </div>

              {/* Population Assignments */}
              <div>
                <h4 style={{ margin: '0 0 8px', fontSize: '12.5px', fontWeight: 700, color: '#334155' }}>
                  2. Population Assignments
                </h4>
                <QueryState query={assignments}>
                  {!assignments.data?.items.length ? (
                    <div style={{ padding: '10px 12px', background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', fontSize: '12px', color: '#64748b' }}>
                      No population target assigned yet. Use the population assignment form below.
                    </div>
                  ) : (
                    <div className="policies-table-container">
                      <table className="policies-erp-table">
                        <thead>
                          <tr>
                            <th>Target Scope</th>
                            <th>Target ID</th>
                            <th>Effective Dates</th>
                            <th>Reason / Reference</th>
                            <th>Recorded At</th>
                          </tr>
                        </thead>
                        <tbody>
                          {assignments.data?.items.map((asg) => (
                            <tr key={asg.id}>
                              <td>
                                <span className="claim-kind-pill">
                                  {policyTargetOptions[asg.target - 1]?.label ?? `Target #${asg.target}`}
                                </span>
                              </td>
                              <td style={{ fontFamily: 'ui-monospace, monospace', fontSize: '11px' }}>
                                {asg.targetId}
                              </td>
                              <td>
                                {asg.effectiveFrom} {asg.effectiveTo ? `to ${asg.effectiveTo}` : '(Open-ended)'}
                              </td>
                              <td>{asg.reason}</td>
                              <td>{labelDate(asg.recordedAt)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </QueryState>
              </div>

              {/* Publish a Rules Version */}
              <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '14px' }}>
                <h4 style={{ margin: '0 0 10px', fontSize: '12.5px', fontWeight: 700, color: '#1e3a8a' }}>
                  3. Configure & Publish New Rate Version
                </h4>

                <div className="rules-config-grid">
                  {/* General Config */}
                  <div className="rules-config-card">
                    <span className="rules-card-title">General Policy Parameters</span>
                    <div className="rules-card-inputs">
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Currency</label>
                        <input
                          type="text"
                          className="allowance-input"
                          maxLength={3}
                          value={rules.currency}
                          onChange={(e) => setRules({ ...rules, currency: e.target.value })}
                        />
                      </div>
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Office Duty Start</label>
                        <input
                          type="text"
                          className="allowance-input"
                          placeholder="17:00"
                          value={rules.officeDutyStartLocalTime}
                          onChange={(e) => setRules({ ...rules, officeDutyStartLocalTime: e.target.value })}
                        />
                      </div>
                    </div>
                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Continuity Gap Tolerance (min)</label>
                      <input
                        type="number"
                        className="allowance-input"
                        min={0}
                        max={180}
                        value={rules.continuityGapToleranceMinutes}
                        onChange={(e) => setRules({ ...rules, continuityGapToleranceMinutes: e.target.value })}
                      />
                    </div>
                    <div className="rules-card-inputs">
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Effective From *</label>
                        <input
                          type="date"
                          className="allowance-input"
                          required
                          value={rules.effectiveFrom}
                          onChange={(e) => setRules({ ...rules, effectiveFrom: e.target.value })}
                        />
                      </div>
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Effective To</label>
                        <input
                          type="date"
                          className="allowance-input"
                          value={rules.effectiveTo}
                          onChange={(e) => setRules({ ...rules, effectiveTo: e.target.value })}
                        />
                      </div>
                    </div>
                  </div>

                  {/* Meal Rates */}
                  <div className="rules-config-card">
                    <span className="rules-card-title">Office & Site Meal Compensation</span>
                    <div className="rules-card-inputs">
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Office Meal Hours</label>
                        <input
                          type="number"
                          className="allowance-input"
                          value={rules.officeMealHours}
                          onChange={(e) => setRules({ ...rules, officeMealHours: e.target.value })}
                        />
                      </div>
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Office Meal PKR</label>
                        <input
                          type="number"
                          step="0.01"
                          className="allowance-input"
                          placeholder="Amount"
                          value={rules.officeMealAmount}
                          onChange={(e) => setRules({ ...rules, officeMealAmount: e.target.value })}
                        />
                      </div>
                    </div>
                    <div className="rules-card-inputs">
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Site Dinner Hours</label>
                        <input
                          type="number"
                          className="allowance-input"
                          value={rules.siteDinnerHours}
                          onChange={(e) => setRules({ ...rules, siteDinnerHours: e.target.value })}
                        />
                      </div>
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Site Dinner PKR</label>
                        <input
                          type="number"
                          step="0.01"
                          className="allowance-input"
                          placeholder="Amount"
                          value={rules.siteDinnerAmount}
                          onChange={(e) => setRules({ ...rules, siteDinnerAmount: e.target.value })}
                        />
                      </div>
                    </div>
                    <div className="rules-card-inputs">
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Breakfast Hours</label>
                        <input
                          type="number"
                          className="allowance-input"
                          value={rules.siteBreakfastHours}
                          onChange={(e) => setRules({ ...rules, siteBreakfastHours: e.target.value })}
                        />
                      </div>
                      <div className="allowance-form-group">
                        <label className="allowance-form-label">Breakfast PKR</label>
                        <input
                          type="number"
                          step="0.01"
                          className="allowance-input"
                          placeholder="Amount"
                          value={rules.siteBreakfastAmount}
                          onChange={(e) => setRules({ ...rules, siteBreakfastAmount: e.target.value })}
                        />
                      </div>
                    </div>
                  </div>

                  {/* Outstation & Travel */}
                  <div className="rules-config-card">
                    <span className="rules-card-title">Outstation & Travel Rates</span>
                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Outstation Daily Amount (PKR)</label>
                      <input
                        type="number"
                        step="0.01"
                        className="allowance-input"
                        placeholder="e.g. 2500.00"
                        value={rules.outstationDailyAmount}
                        onChange={(e) => setRules({ ...rules, outstationDailyAmount: e.target.value })}
                      />
                    </div>
                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Work Travel Daily Amount (PKR)</label>
                      <input
                        type="number"
                        step="0.01"
                        className="allowance-input"
                        placeholder="e.g. 1500.00"
                        value={rules.workTravelAmount}
                        onChange={(e) => setRules({ ...rules, workTravelAmount: e.target.value })}
                      />
                    </div>
                  </div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '12px' }}>
                  <button
                    type="button"
                    className="btn-alw-primary"
                    disabled={publish.pending}
                    onClick={() => void previewRules()}
                  >
                    Validate & Preview Rules
                  </button>
                </div>

                <ActionMessage action={publish} />

                {preview && !preview.canPublish && (
                  <div style={{ padding: '10px 12px', background: '#fef2f2', border: '1px solid #fee2e2', borderRadius: '6px', color: '#b91c1c', fontSize: '12px', marginTop: '10px' }}>
                    <p style={{ fontWeight: 600, margin: '0 0 4px' }}>Validation requirements remaining:</p>
                    <ul style={{ margin: 0, paddingLeft: '18px' }}>
                      {preview.errors.map((err) => (
                        <li key={err.path + err.code}>
                          {err.path}: {err.message}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {preview?.canPublish && (
                  <form
                    style={{ marginTop: '12px', background: '#ecfdf5', border: '1px solid #d1fae5', borderRadius: '6px', padding: '12px' }}
                    onSubmit={(e) => void publishRules(e)}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#047857', fontWeight: 600, fontSize: '12.5px', marginBottom: '8px' }}>
                      <CheckCircle2 size={16} />
                      <span>Validation passed! Ready to publish revision #{preview.revision}.</span>
                    </div>
                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Publication Reason *</label>
                      <input
                        type="text"
                        className="allowance-input"
                        placeholder="e.g. Annual cost of living revision approved by board"
                        required
                        value={rules.reason}
                        onChange={(e) => setRules({ ...rules, reason: e.target.value })}
                      />
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '10px' }}>
                      <button
                        type="submit"
                        className="btn-alw-primary"
                        style={{ background: '#059669' }}
                        disabled={publish.pending || !rules.reason.trim()}
                      >
                        {publish.pending ? 'Publishing...' : 'Confirm & Publish Version'}
                      </button>
                    </div>
                  </form>
                )}
              </div>

              {/* Assign to a Population */}
              <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '14px' }}>
                <h4 style={{ margin: '0 0 10px', fontSize: '12.5px', fontWeight: 700, color: '#1e3a8a' }}>
                  4. Assign Published Version to Population
                </h4>
                <form onSubmit={(e) => void assignVersion(e)}>
                  <div className="policy-create-grid">
                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Published Version *</label>
                      <select
                        className="allowance-select"
                        required
                        value={assignForm.policyVersionId}
                        onChange={(e) => setAssignForm({ ...assignForm, policyVersionId: e.target.value })}
                      >
                        <option value="">Choose published version</option>
                        {(versions.data?.items ?? []).map((ver) => (
                          <option key={ver.id} value={ver.id}>
                            Version {ver.number} ({ver.effectiveFrom})
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Target Population Scope</label>
                      <select
                        className="allowance-select"
                        value={assignForm.target}
                        onChange={(e) => setAssignForm({ ...assignForm, target: e.target.value })}
                      >
                        {policyTargetOptions.map((opt) => (
                          <option key={opt.value} value={opt.value}>
                            {opt.label}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Target ID (UUID) *</label>
                      <input
                        type="text"
                        className="allowance-input"
                        placeholder="Department or Designation UUID..."
                        required
                        value={assignForm.targetId}
                        onChange={(e) => setAssignForm({ ...assignForm, targetId: e.target.value })}
                      />
                    </div>
                  </div>

                  <div className="policy-create-grid" style={{ marginTop: '10px' }}>
                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Effective From *</label>
                      <input
                        type="date"
                        className="allowance-input"
                        required
                        value={assignForm.effectiveFrom}
                        onChange={(e) => setAssignForm({ ...assignForm, effectiveFrom: e.target.value })}
                      />
                    </div>

                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Effective To (Optional)</label>
                      <input
                        type="date"
                        className="allowance-input"
                        value={assignForm.effectiveTo}
                        onChange={(e) => setAssignForm({ ...assignForm, effectiveTo: e.target.value })}
                      />
                    </div>

                    <div className="allowance-form-group">
                      <label className="allowance-form-label">Assignment Reason *</label>
                      <input
                        type="text"
                        className="allowance-input"
                        placeholder="e.g. Standard compensation assignment"
                        required
                        value={assignForm.reason}
                        onChange={(e) => setAssignForm({ ...assignForm, reason: e.target.value })}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '12px' }}>
                    <button
                      type="submit"
                      className="btn-alw-primary"
                      disabled={
                        assign.pending ||
                        !assignForm.policyVersionId ||
                        !assignForm.targetId.trim() ||
                        !assignForm.effectiveFrom ||
                        !assignForm.reason.trim()
                      }
                    >
                      {assign.pending ? 'Assigning...' : 'Assign Policy to Population'}
                    </button>
                  </div>
                </form>
                <ActionMessage action={assign} />
              </div>
            </div>
          )}

          {/* Policies Pagination */}
          {list.data && (
            <div className="allowance-pagination-bar">
              <span>Page {page} of registered policies</span>
              <div style={{ display: 'flex', gap: '6px' }}>
                <button
                  type="button"
                  className="btn-alw-secondary"
                  style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                  disabled={list.isFetching || page <= 1}
                  onClick={() => setPage((v) => Math.max(1, v - 1))}
                >
                  <ChevronLeft size={14} />
                  Previous
                </button>
                <button
                  type="button"
                  className="btn-alw-secondary"
                  style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                  disabled={list.isFetching || !list.data.hasMore}
                  onClick={() => setPage((v) => v + 1)}
                >
                  Next
                  <ChevronRight size={14} />
                </button>
              </div>
            </div>
          )}
        </QueryState>
      </div>
    </div>
  );
}

function dayRangeValid(from: string, to: string) {
  if (!z.iso.date().safeParse(from).success || !z.iso.date().safeParse(to).success) return false;
  const start = Date.parse(from);
  const end = Date.parse(to);
  if (Number.isNaN(start) || Number.isNaN(end)) return false;
  return end > start && (end - start) / 86_400_000 <= 366;
}
