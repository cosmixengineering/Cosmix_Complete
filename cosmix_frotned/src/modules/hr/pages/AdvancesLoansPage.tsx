import { useState, type FormEvent } from 'react';
import {
  Coins,
  Clock,
  CheckCircle2,
  AlertCircle,
  FileText,
  ChevronRight,
  ChevronDown,
  HandCoins,
  RefreshCw,
  FileSignature,
  Inbox,
  Layers,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Denied, QueryState, ActionMessage } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import { FinanceStatus, HasMorePager, TextAreaField } from '../components/finance/FinanceChrome';
import { advanceKindOptions, approvalStateOptions, money } from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { queryString } from '../api/hr-finance-api';
import { labelDate } from '../utils/messages';
import {
  advanceAgreement,
  advanceAgreementPage,
  advanceRequest,
  advanceRequestPage,
  repaymentLedgerEntry,
  type AdvanceRequest,
} from '../schemas/finance';
import '../components/finance/advances-loans.css';

const permissions = {
  review: 'attendance.advancesloans.review',
  approve: 'attendance.advancesloans.approve',
  repay: 'attendance.advancesloans.repay',
};

export function AdvancesLoansPage() {
  const access = useFinanceAccess();
  const tabs = [
    { id: 'requests', label: 'Requests Queue', icon: FileText },
    { id: 'agreements', label: 'Agreements & Repayments', icon: HandCoins },
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');

  // Pre-fetch count metrics for top KPI cards
  const pendingRequestsQuery = useFinanceQuery(
    '/hr/advances-loans/requests?state=0&page=1&pageSize=1',
    advanceRequestPage,
    access.can(permissions.review),
  );

  const activeAgreementsQuery = useFinanceQuery(
    '/hr/advances-loans/agreements?activeOnly=true&page=1&pageSize=1',
    advanceAgreementPage,
    access.can(permissions.review),
  );

  if (!access.can(permissions.review)) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');

  return (
    <div className="advances-loans-page" aria-label="Advances and loans">
      {/* Header Container */}
      <div className="adv-header-container">
        <div>
          <div className="adv-breadcrumb">
            <span className="adv-breadcrumb-link">Dashboard</span>
            <span>/</span>
            <span className="adv-breadcrumb-link">Human Resources</span>
            <span>/</span>
            <span style={{ color: '#0f172a', fontWeight: 600 }}>Claims & Advances</span>
          </div>
          <div className="adv-title-row">
            <h1 className="adv-main-title">Advances and Loans</h1>
            <span className="adv-badge-live">
              <span className="adv-badge-live-dot" />
              Live Payroll Linked
            </span>
          </div>
          <p className="adv-subtitle">
            Manage employee salary advance requests, approve installment plans, and track loan
            agreements with monthly deduction ledgers.
          </p>
        </div>
      </div>

      {/* 4 Executive KPI Metric Cards */}
      <div className="adv-kpi-grid">
        <div className="adv-kpi-card">
          <div className="adv-kpi-icon-wrap adv-bg-amber">
            <Clock size={22} />
          </div>
          <div className="adv-kpi-content">
            <span className="adv-kpi-label">Pending Requests</span>
            <span className="adv-kpi-value">
              {pendingRequestsQuery.data?.totalCount ??
                (pendingRequestsQuery.isFetching ? '...' : 0)}
            </span>
            <span className="adv-kpi-sub">Awaiting HR Approval</span>
          </div>
        </div>

        <div className="adv-kpi-card">
          <div className="adv-kpi-icon-wrap adv-bg-blue">
            <HandCoins size={22} />
          </div>
          <div className="adv-kpi-content">
            <span className="adv-kpi-label">Active Agreements</span>
            <span className="adv-kpi-value">
              {activeAgreementsQuery.data?.totalCount ??
                (activeAgreementsQuery.isFetching ? '...' : 0)}
            </span>
            <span className="adv-kpi-sub">Ongoing Repayments</span>
          </div>
        </div>

        <div className="adv-kpi-card">
          <div className="adv-kpi-icon-wrap adv-bg-emerald">
            <CheckCircle2 size={22} />
          </div>
          <div className="adv-kpi-content">
            <span className="adv-kpi-label">Payroll Sync</span>
            <span className="adv-kpi-value">Automated</span>
            <span className="adv-kpi-sub">Monthly Salary Deductions</span>
          </div>
        </div>

        <div className="adv-kpi-card">
          <div className="adv-kpi-icon-wrap adv-bg-indigo">
            <FileSignature size={22} />
          </div>
          <div className="adv-kpi-content">
            <span className="adv-kpi-label">Policy Guard</span>
            <span className="adv-kpi-value">Standard Terms</span>
            <span className="adv-kpi-sub">Single Active Advance Limit</span>
          </div>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="adv-nav-tabs" role="tablist" aria-label="Advance and loan views">
        {tabs.map((item) => {
          const Icon = item.icon;
          const isActive = active === item.id;
          return (
            <button
              type="button"
              key={item.id}
              role="tab"
              aria-selected={isActive}
              className={`adv-tab-btn ${isActive ? 'active' : ''}`}
              onClick={() => setTab(item.id)}
            >
              <Icon size={13} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Panels */}
      {active === 'requests' && (
        <AdvanceRequests
          canApprove={access.can(permissions.approve)}
          canReview={access.can(permissions.review)}
        />
      )}
      {active === 'agreements' && <AdvanceAgreements canRepay={access.can(permissions.repay)} />}
    </div>
  );
}

function AdvanceRequests({ canApprove, canReview }: { canApprove: boolean; canReview: boolean }) {
  const [page, setPage] = useState(1);
  const [state, setState] = useState('0'); // 0: Pending by default
  const [employeeId, setEmployeeId] = useState('');
  const [selected, setSelected] = useState<AdvanceRequest | null>(null);
  const [actionType, setActionType] = useState<'approve' | 'reject' | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [approveForm, setApproveForm] = useState({
    approvedPrincipal: '',
    currency: 'PKR',
    effectiveOn: '',
    approvedTerms: '',
    numberOfInstallments: '1',
    firstDueDate: '',
  });
  const action = useFinanceAction();

  const requests = useFinanceQuery(
    '/hr/advances-loans/requests' +
      queryString({
        employeeId: employeeId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 25,
      }),
    advanceRequestPage,
    canReview,
  );

  function startApprove(request: AdvanceRequest) {
    setSelected(request);
    setActionType('approve');
    setApproveForm({
      approvedPrincipal: String(request.requestedAmount),
      currency: request.currency || 'PKR',
      effectiveOn: new Date().toISOString().slice(0, 10),
      approvedTerms: 'Standard monthly salary deduction plan approved by HR.',
      numberOfInstallments: '1',
      firstDueDate: new Date(new Date().setDate(new Date().getDate() + 30))
        .toISOString()
        .slice(0, 10),
    });
  }

  function startReject(request: AdvanceRequest) {
    setSelected(request);
    setActionType('reject');
    setRejectReason('');
  }

  async function approve(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    const result = await action.run(
      `/hr/advances-loans/requests/${selected.id}/approve`,
      {
        approvedPrincipal: Number(approveForm.approvedPrincipal),
        currency: approveForm.currency.trim().toUpperCase(),
        effectiveOn: approveForm.effectiveOn,
        approvedTerms: approveForm.approvedTerms,
        numberOfInstallments: Number(approveForm.numberOfInstallments),
        firstDueDate: approveForm.firstDueDate,
      },
      'POST',
      advanceAgreement,
    );
    if (result) {
      setSelected(null);
      setActionType(null);
      void requests.refetch();
    }
  }

  async function reject(request: AdvanceRequest) {
    if (!window.confirm('Are you sure you want to reject this advance request?')) return;
    const result = await action.run(
      `/hr/advances-loans/requests/${request.id}/reject`,
      { reason: rejectReason },
      'POST',
      advanceRequest,
    );
    if (result) {
      setSelected(null);
      setActionType(null);
      setRejectReason('');
      void requests.refetch();
    }
  }

  return (
    <div className="adv-content-card">
      <div className="adv-card-header">
        <div>
          <h2 className="adv-card-title">
            <FileText size={18} style={{ color: '#0284c7' }} />
            Advance & Loan Request Queue
          </h2>
          <p className="adv-card-desc">
            Staff applications for salary advances, emergency funds, and installment loans awaiting
            departmental authorization.
          </p>
        </div>
        <Button
          variant="secondary"
          onClick={() => void requests.refetch()}
          title="Refresh requests"
          disabled={requests.isFetching}
          style={{
            height: '32px',
            padding: '0 10px',
            fontSize: '12px',
            display: 'flex',
            alignItems: 'center',
            gap: '5px',
          }}
        >
          <RefreshCw size={14} className={requests.isFetching ? 'animate-spin' : ''} />
          Refresh
        </Button>
      </div>

      {/* Filter Bar with Quick Chips */}
      <div className="adv-filter-box">
        <div className="adv-chips-row">
          <span
            style={{
              fontSize: '11px',
              fontWeight: 600,
              color: '#64748b',
              textTransform: 'uppercase',
            }}
          >
            Quick Filter:
          </span>
          {[
            { label: 'Pending Only', value: '0' },
            { label: 'All Statuses', value: '' },
            { label: 'Approved', value: '1' },
            { label: 'Rejected', value: '2' },
          ].map((chip) => (
            <button
              type="button"
              key={chip.label}
              className={`adv-chip ${state === chip.value ? 'active' : ''}`}
              onClick={() => {
                setState(chip.value);
                setPage(1);
              }}
            >
              {chip.label}
            </button>
          ))}
        </div>

        <div className="adv-filter-grid">
          <SelectInput
            label="Filter by State"
            value={state}
            options={approvalStateOptions}
            onChange={(event) => {
              setState(event.target.value);
              setPage(1);
            }}
          />
          <TextInput
            label="Search by Employee ID or Code"
            value={employeeId}
            placeholder="e.g. EMP-001 or UUID"
            onChange={(event) => {
              setEmployeeId(event.target.value);
              setPage(1);
            }}
          />
          {employeeId && (
            <Button
              variant="secondary"
              onClick={() => {
                setEmployeeId('');
                setPage(1);
              }}
              style={{ height: '38px', fontSize: '12px' }}
            >
              Clear Search
            </Button>
          )}
        </div>
      </div>

      <QueryState query={requests}>
        {!requests.data?.items.length && (
          <div className="adv-empty-state">
            <div className="adv-empty-icon">
              <Inbox size={26} />
            </div>
            <h3 className="adv-empty-title">No Requests Found</h3>
            <p className="adv-empty-desc">
              There are currently no employee salary advance or loan applications matching the
              selected filter criteria. When employees apply through the Employee Self-Service (ESS)
              mobile app or portal, their requests will appear here for HR review.
            </p>
          </div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          {requests.data?.items.map((request) => {
            const isSelected = selected?.id === request.id;
            const kindLabel =
              advanceKindOptions[request.kind - 1]?.label ?? `Kind #${request.kind}`;

            return (
              <article className="adv-item-card" key={request.id}>
                <div className="adv-item-header">
                  <div className="adv-user-badge">
                    <div className="adv-avatar">
                      {request.employeeName ? request.employeeName.charAt(0).toUpperCase() : 'E'}
                    </div>
                    <div>
                      <div className="adv-emp-name">
                        {request.employeeName || request.employeeCode}
                      </div>
                      <div className="adv-emp-meta">
                        Code: <strong>{request.employeeCode}</strong> &bull; Kind:{' '}
                        <span style={{ color: '#0369a1', fontWeight: 600 }}>{kindLabel}</span>
                      </div>
                    </div>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div className="adv-amount-badge">
                      {money(request.requestedAmount, request.currency)}
                    </div>
                    <FinanceStatus value={request.state} />
                    {canApprove && request.state === 'Pending' && (
                      <Button
                        variant="success"
                        onClick={() => startApprove(request)}
                        style={{ height: '32px', fontSize: '12px', padding: '0 12px' }}
                      >
                        Approve Plan
                      </Button>
                    )}
                    {canReview && request.state === 'Pending' && (
                      <Button
                        variant="danger"
                        onClick={() => startReject(request)}
                        style={{ height: '32px', fontSize: '12px', padding: '0 12px' }}
                      >
                        Reject
                      </Button>
                    )}
                  </div>
                </div>

                <RecordView
                  value={{
                    requestReference: request.id,
                    employee: request.employeeName,
                    employeeReference: request.employeeId,
                    requestedAmount: money(request.requestedAmount, request.currency),
                    reason: request.reason || 'No detailed reason provided.',
                    requestedOn: request.requestedOn,
                    createdAt: labelDate(request.createdAt),
                    agreement: request.agreementId || 'Pending Approval',
                  }}
                />

                {/* Approve Form Drawer */}
                {canApprove && isSelected && actionType === 'approve' && (
                  <div className="adv-form-drawer">
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '12px',
                      }}
                    >
                      <h4
                        style={{
                          margin: 0,
                          fontSize: '13.5px',
                          fontWeight: 700,
                          color: '#047857',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '6px',
                        }}
                      >
                        <CheckCircle2 size={16} />
                        Approve Advance & Create Repayment Agreement
                      </h4>
                      <Button
                        variant="ghost"
                        onClick={() => setSelected(null)}
                        style={{ fontSize: '12px' }}
                      >
                        Cancel
                      </Button>
                    </div>

                    <form className="hr-form-grid" onSubmit={(event) => void approve(event)}>
                      <TextInput
                        label="Approved Principal"
                        required
                        type="number"
                        min={1}
                        max={10_000_000}
                        step="0.01"
                        value={approveForm.approvedPrincipal}
                        onChange={(event) =>
                          setApproveForm({ ...approveForm, approvedPrincipal: event.target.value })
                        }
                      />
                      <TextInput
                        label="Currency"
                        required
                        maxLength={3}
                        value={approveForm.currency}
                        onChange={(event) =>
                          setApproveForm({ ...approveForm, currency: event.target.value })
                        }
                      />
                      <TextInput
                        label="Effective Date"
                        required
                        type="date"
                        value={approveForm.effectiveOn}
                        onChange={(event) =>
                          setApproveForm({ ...approveForm, effectiveOn: event.target.value })
                        }
                      />
                      <TextInput
                        label="Number of Monthly Installments"
                        required
                        type="number"
                        min={1}
                        max={60}
                        value={approveForm.numberOfInstallments}
                        onChange={(event) =>
                          setApproveForm({
                            ...approveForm,
                            numberOfInstallments: event.target.value,
                          })
                        }
                      />
                      <TextInput
                        label="First Installment Due Date"
                        required
                        type="date"
                        value={approveForm.firstDueDate}
                        onChange={(event) =>
                          setApproveForm({ ...approveForm, firstDueDate: event.target.value })
                        }
                      />
                      <TextAreaField
                        label="Approved Terms & Conditions"
                        required
                        value={approveForm.approvedTerms}
                        onChange={(value) =>
                          setApproveForm({ ...approveForm, approvedTerms: value })
                        }
                        disabled={action.pending}
                      />
                      <div className="hr-actions wide" style={{ marginTop: '12px' }}>
                        <Button
                          type="submit"
                          variant="success"
                          disabled={
                            action.pending ||
                            !approveForm.approvedPrincipal ||
                            !approveForm.effectiveOn ||
                            !approveForm.firstDueDate ||
                            !approveForm.approvedTerms.trim()
                          }
                        >
                          Confirm & Authorize Agreement
                        </Button>
                      </div>
                    </form>
                  </div>
                )}

                {/* Reject Form Drawer */}
                {canReview && isSelected && actionType === 'reject' && (
                  <div
                    className="adv-form-drawer"
                    style={{ borderColor: '#fca5a5', background: '#fef2f2' }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '12px',
                      }}
                    >
                      <h4
                        style={{
                          margin: 0,
                          fontSize: '13.5px',
                          fontWeight: 700,
                          color: '#b91c1c',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '6px',
                        }}
                      >
                        <AlertCircle size={16} />
                        Reject Request Reason
                      </h4>
                      <Button
                        variant="ghost"
                        onClick={() => setSelected(null)}
                        style={{ fontSize: '12px' }}
                      >
                        Cancel
                      </Button>
                    </div>

                    <form
                      className="hr-form-grid"
                      onSubmit={(event) => {
                        event.preventDefault();
                        void reject(request);
                      }}
                    >
                      <TextAreaField
                        label="Formal Rejection Reason"
                        required
                        value={rejectReason}
                        onChange={setRejectReason}
                        disabled={action.pending}
                      />
                      <div className="hr-actions wide">
                        <Button
                          type="submit"
                          variant="danger"
                          disabled={action.pending || !rejectReason.trim()}
                        >
                          Confirm Rejection
                        </Button>
                      </div>
                    </form>
                  </div>
                )}
              </article>
            );
          })}
        </div>

        {requests.data && (
          <HasMorePager
            page={page}
            hasMore={requests.data.hasMore}
            busy={requests.isFetching}
            onPrevious={() => setPage((value) => Math.max(1, value - 1))}
            onNext={() => setPage((value) => value + 1)}
          />
        )}
      </QueryState>
      <ActionMessage action={action} />
    </div>
  );
}

function AdvanceAgreements({ canRepay }: { canRepay: boolean }) {
  const [page, setPage] = useState(1);
  const [employeeId, setEmployeeId] = useState('');
  const [activeOnly, setActiveOnly] = useState('true');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [repayment, setRepayment] = useState({
    repaymentScheduleEntryId: '',
    kind: '1',
    amount: '',
    currency: 'PKR',
    valueDate: new Date().toISOString().slice(0, 10),
    sourceReference: '',
  });
  const [reversal, setReversal] = useState({
    reversesEntryId: '',
    valueDate: new Date().toISOString().slice(0, 10),
    sourceReference: '',
    reason: '',
  });
  const action = useFinanceAction();

  const agreements = useFinanceQuery(
    '/hr/advances-loans/agreements' +
      queryString({
        employeeId: employeeId.trim() || undefined,
        activeOnly: activeOnly === '' ? undefined : activeOnly === 'true',
        page,
        pageSize: 25,
      }),
    advanceAgreementPage,
    true,
  );

  const detail = useFinanceQuery(
    selectedId ? `/hr/advances-loans/agreements/${selectedId}` : '/hr/advances-loans/agreements',
    advanceAgreement,
    Boolean(selectedId),
    Boolean(selectedId),
  );
  const selected = detail.data ?? null;

  async function recordRepayment(event: FormEvent) {
    event.preventDefault();
    if (!selectedId || !selected) return;
    const result = await action.run(
      `/hr/advances-loans/agreements/${selected.id}/repayments`,
      {
        repaymentScheduleEntryId: repayment.repaymentScheduleEntryId || null,
        kind: Number(repayment.kind),
        amount: Number(repayment.amount),
        currency: repayment.currency.trim().toUpperCase(),
        valueDate: repayment.valueDate,
        sourceReference: repayment.sourceReference,
      },
      'POST',
      repaymentLedgerEntry,
    );
    if (result) {
      setRepayment({
        repaymentScheduleEntryId: '',
        kind: '1',
        amount: '',
        currency: selected.currency,
        valueDate: repayment.valueDate,
        sourceReference: '',
      });
      void agreements.refetch();
      void detail.refetch();
    }
  }

  async function recordReversal(event: FormEvent) {
    event.preventDefault();
    if (!selectedId || !selected) return;
    const result = await action.run(
      `/hr/advances-loans/agreements/${selected.id}/reversals`,
      {
        reversesEntryId: reversal.reversesEntryId,
        valueDate: reversal.valueDate,
        sourceReference: reversal.sourceReference,
        reason: reversal.reason,
      },
      'POST',
      repaymentLedgerEntry,
    );
    if (result) {
      setReversal({
        reversesEntryId: '',
        valueDate: reversal.valueDate,
        sourceReference: '',
        reason: '',
      });
      void agreements.refetch();
    }
  }

  return (
    <div className="adv-content-card">
      <div className="adv-card-header">
        <div>
          <h2 className="adv-card-title">
            <HandCoins size={18} style={{ color: '#0284c7' }} />
            Active Loan Agreements & Recovery Ledgers
          </h2>
          <p className="adv-card-desc">
            Legally approved advance contracts with installment breakdown, remaining principal
            balance, and repayment transactions.
          </p>
        </div>
        <Button
          variant="secondary"
          onClick={() => void agreements.refetch()}
          title="Refresh agreements"
          disabled={agreements.isFetching}
          style={{
            height: '32px',
            padding: '0 10px',
            fontSize: '12px',
            display: 'flex',
            alignItems: 'center',
            gap: '5px',
          }}
        >
          <RefreshCw size={14} className={agreements.isFetching ? 'animate-spin' : ''} />
          Refresh
        </Button>
      </div>

      {/* Filter Box */}
      <div className="adv-filter-box">
        <div className="adv-chips-row">
          <span
            style={{
              fontSize: '11px',
              fontWeight: 600,
              color: '#64748b',
              textTransform: 'uppercase',
            }}
          >
            Filter Agreements:
          </span>
          {[
            { label: 'Active Agreements Only', value: 'true' },
            { label: 'All Agreements (Including Settled)', value: 'false' },
          ].map((chip) => (
            <button
              type="button"
              key={chip.label}
              className={`adv-chip ${activeOnly === chip.value ? 'active' : ''}`}
              onClick={() => {
                setActiveOnly(chip.value);
                setPage(1);
              }}
            >
              {chip.label}
            </button>
          ))}
        </div>

        <div className="adv-filter-grid">
          <TextInput
            label="Search by Employee ID"
            value={employeeId}
            placeholder="e.g. EMP-001"
            onChange={(event) => {
              setEmployeeId(event.target.value);
              setPage(1);
            }}
          />
          <SelectInput
            label="Agreement Status"
            value={activeOnly}
            options={[
              { value: 'true', label: 'Active (Outstanding Balance > 0)' },
              { value: 'false', label: 'All Agreements (Active + Settled)' },
            ]}
            onChange={(event) => {
              setActiveOnly(event.target.value);
              setPage(1);
            }}
          />
          {employeeId && (
            <Button
              variant="secondary"
              onClick={() => {
                setEmployeeId('');
                setPage(1);
              }}
              style={{ height: '38px', fontSize: '12px' }}
            >
              Clear Search
            </Button>
          )}
        </div>
      </div>

      <QueryState query={agreements}>
        {!agreements.data?.items.length && (
          <div className="adv-empty-state">
            <div className="adv-empty-icon">
              <Coins size={26} />
            </div>
            <h3 className="adv-empty-title">No Loan Agreements Found</h3>
            <p className="adv-empty-desc">
              There are currently no active or historical advance agreements matching this filter.
              Once an advance request is approved from the &quot;Requests Queue&quot;, its binding
              contract and schedule will appear here.
            </p>
          </div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          {agreements.data?.items.map((agreement) => {
            const isSelected = selectedId === agreement.id;
            const kindLabel =
              advanceKindOptions[agreement.kind - 1]?.label ?? `Kind #${agreement.kind}`;
            const percentRepaid =
              agreement.approvedPrincipal > 0
                ? Math.min(
                    100,
                    Math.round((agreement.totalRepaid / agreement.approvedPrincipal) * 100),
                  )
                : 0;

            return (
              <article className="adv-item-card" key={agreement.id}>
                <div className="adv-item-header">
                  <div className="adv-user-badge">
                    <div className="adv-avatar" style={{ background: '#ecfdf5', color: '#059669' }}>
                      <HandCoins size={18} />
                    </div>
                    <div>
                      <div className="adv-emp-name">
                        {agreement.employeeName || agreement.employeeCode}
                      </div>
                      <div className="adv-emp-meta">
                        Code: <strong>{agreement.employeeCode}</strong> &bull; Plan:{' '}
                        <strong>{kindLabel}</strong> &bull; Effective:{' '}
                        <strong>{agreement.effectiveOn}</strong>
                      </div>
                    </div>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>
                        OUTSTANDING
                      </div>
                      <div
                        style={{
                          fontSize: '15px',
                          fontWeight: 800,
                          color: agreement.outstandingBalance > 0 ? '#b91c1c' : '#059669',
                        }}
                      >
                        {money(agreement.outstandingBalance, agreement.currency)}
                      </div>
                    </div>
                    <FinanceStatus value={agreement.isFullyRepaid ? 'settled' : 'open'} />
                    <Button
                      variant={isSelected ? 'primary' : 'secondary'}
                      onClick={() => setSelectedId(isSelected ? null : agreement.id)}
                      style={{
                        height: '32px',
                        fontSize: '12px',
                        padding: '0 12px',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                      }}
                    >
                      {isSelected ? 'Hide Details' : 'View Schedule & Ledger'}
                      {isSelected ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                    </Button>
                  </div>
                </div>

                {/* Repayment Progress Bar */}
                <div>
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      fontSize: '11px',
                      fontWeight: 600,
                      color: '#64748b',
                      marginBottom: '3px',
                    }}
                  >
                    <span>Repayment Progress: {percentRepaid}%</span>
                    <span>
                      Repaid {money(agreement.totalRepaid, agreement.currency)} of{' '}
                      {money(agreement.approvedPrincipal, agreement.currency)}
                    </span>
                  </div>
                  <div className="adv-progress-wrap">
                    <div className="adv-progress-bar" style={{ width: `${percentRepaid}%` }} />
                  </div>
                </div>

                <RecordView
                  value={{
                    agreementReference: agreement.id,
                    employee: agreement.employeeName,
                    approvedPrincipal: money(agreement.approvedPrincipal, agreement.currency),
                    effectiveOn: agreement.effectiveOn,
                    approvedTerms: agreement.approvedTerms,
                    totalRepaid: money(agreement.totalRepaid, agreement.currency),
                    outstandingBalance: money(agreement.outstandingBalance, agreement.currency),
                    recordedAt: labelDate(agreement.recordedAt),
                  }}
                />

                {isSelected && (
                  <QueryState query={detail}>
                    {selected && (
                      <div
                        className="adv-form-drawer"
                        style={{ background: '#ffffff', borderColor: '#bae6fd' }}
                      >
                        <div
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '8px',
                            borderBottom: '1px solid #e2e8f0',
                            paddingBottom: '10px',
                            marginBottom: '14px',
                          }}
                        >
                          <Layers size={18} style={{ color: '#0284c7' }} />
                          <h4
                            style={{
                              margin: 0,
                              fontSize: '14px',
                              fontWeight: 700,
                              color: '#0f172a',
                            }}
                          >
                            Repayment Schedule & Payment Ledger Breakdown
                          </h4>
                        </div>

                        {/* Schedule Table */}
                        <div style={{ marginBottom: '18px' }}>
                          <h5
                            style={{
                              margin: '0 0 8px',
                              fontSize: '12.5px',
                              fontWeight: 700,
                              color: '#334155',
                            }}
                          >
                            Monthly Deduction Installments ({selected.schedule.length} installments)
                          </h5>
                          <div className="hr-table-scroll">
                            <table className="hr-prepared-table" style={{ width: '100%' }}>
                              <thead>
                                <tr>
                                  <th>#</th>
                                  <th>Due Date</th>
                                  <th>Principal Due</th>
                                  <th>Other</th>
                                  <th>Total Due</th>
                                  <th>Amount Paid</th>
                                  <th>Status</th>
                                </tr>
                              </thead>
                              <tbody>
                                {selected.schedule.map((entry) => (
                                  <tr key={entry.id}>
                                    <td>
                                      <strong>#{entry.sequence}</strong>
                                    </td>
                                    <td>{entry.dueDate}</td>
                                    <td>{money(entry.principalAmount, entry.currency)}</td>
                                    <td>{money(entry.otherApprovedAmount, entry.currency)}</td>
                                    <td style={{ fontWeight: 700 }}>
                                      {money(entry.totalDue, entry.currency)}
                                    </td>
                                    <td
                                      style={{
                                        color: entry.paidAmount > 0 ? '#059669' : '#64748b',
                                        fontWeight: 600,
                                      }}
                                    >
                                      {money(entry.paidAmount, entry.currency)}
                                    </td>
                                    <td>
                                      <FinanceStatus value={entry.isSettled ? 'settled' : 'open'} />
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </div>

                        {/* Payment Ledger */}
                        <div style={{ marginBottom: '18px' }}>
                          <h5
                            style={{
                              margin: '0 0 8px',
                              fontSize: '12.5px',
                              fontWeight: 700,
                              color: '#334155',
                            }}
                          >
                            Payment & Deduction Ledger History
                          </h5>
                          {!selected.ledger.length && (
                            <p
                              style={{
                                fontSize: '12px',
                                color: '#64748b',
                                margin: '4px 0',
                                fontStyle: 'italic',
                              }}
                            >
                              No repayments recorded in ledger yet.
                            </p>
                          )}
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                            {selected.ledger.map((entry) => (
                              <RecordView
                                key={entry.id}
                                value={{
                                  ledgerReference: entry.id,
                                  kind:
                                    entry.kind === 1
                                      ? 'Payroll deduction (Salary)'
                                      : entry.kind === 2
                                        ? 'Manual received payment (Bank/Cash)'
                                        : entry.kind === 3
                                          ? 'Reversal entry'
                                          : 'Adjustment',
                                  amount: money(entry.amount, entry.currency),
                                  valueDate: entry.valueDate,
                                  sourceReference: entry.sourceReference,
                                  reversesEntry: entry.reversesEntryId || 'N/A',
                                  recordedAt: labelDate(entry.recordedAt),
                                }}
                              />
                            ))}
                          </div>
                        </div>

                        {canRepay && (
                          <div
                            style={{
                              display: 'grid',
                              gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
                              gap: '16px',
                              borderTop: '1px solid #e2e8f0',
                              paddingTop: '16px',
                            }}
                          >
                            {/* Record Repayment Form */}
                            <div className="adv-filter-box" style={{ background: '#f8fafc' }}>
                              <h5
                                style={{
                                  margin: '0 0 8px',
                                  fontSize: '13px',
                                  fontWeight: 700,
                                  color: '#047857',
                                }}
                              >
                                Record a Manual Payment / Deduction
                              </h5>
                              <form
                                className="hr-form-grid"
                                onSubmit={(event) => void recordRepayment(event)}
                              >
                                <SelectInput
                                  label="Target Installment Entry"
                                  value={repayment.repaymentScheduleEntryId}
                                  options={[
                                    { value: '', label: 'Unallocated general payment' },
                                    ...selected.schedule
                                      .filter((entry) => !entry.isSettled)
                                      .map((entry) => ({
                                        value: entry.id,
                                        label: `#${entry.sequence} due ${entry.dueDate} (${money(entry.totalDue, entry.currency)})`,
                                      })),
                                  ]}
                                  onChange={(event) =>
                                    setRepayment({
                                      ...repayment,
                                      repaymentScheduleEntryId: event.target.value,
                                    })
                                  }
                                />
                                <SelectInput
                                  label="Payment Method"
                                  value={repayment.kind}
                                  options={[
                                    { value: '1', label: 'Payroll salary deduction' },
                                    { value: '2', label: 'Cash or Direct Bank Transfer' },
                                  ]}
                                  onChange={(event) =>
                                    setRepayment({ ...repayment, kind: event.target.value })
                                  }
                                />
                                <TextInput
                                  label="Payment Amount"
                                  required
                                  type="number"
                                  min={1}
                                  max={10_000_000}
                                  step="0.01"
                                  value={repayment.amount}
                                  onChange={(event) =>
                                    setRepayment({ ...repayment, amount: event.target.value })
                                  }
                                />
                                <TextInput
                                  label="Currency"
                                  required
                                  maxLength={3}
                                  value={repayment.currency}
                                  onChange={(event) =>
                                    setRepayment({ ...repayment, currency: event.target.value })
                                  }
                                />
                                <TextInput
                                  label="Payment Value Date"
                                  required
                                  type="date"
                                  value={repayment.valueDate}
                                  onChange={(event) =>
                                    setRepayment({ ...repayment, valueDate: event.target.value })
                                  }
                                />
                                <TextInput
                                  label="Bank / Voucher Reference"
                                  required
                                  maxLength={150}
                                  value={repayment.sourceReference}
                                  placeholder="e.g. TR-98231 or Cash Voucher #12"
                                  onChange={(event) =>
                                    setRepayment({
                                      ...repayment,
                                      sourceReference: event.target.value,
                                    })
                                  }
                                />
                                <div className="hr-actions wide">
                                  <Button
                                    type="submit"
                                    variant="success"
                                    disabled={
                                      action.pending ||
                                      !repayment.amount ||
                                      !repayment.sourceReference.trim()
                                    }
                                  >
                                    Submit Repayment Entry
                                  </Button>
                                </div>
                              </form>
                            </div>

                            {/* Reverse Entry Form */}
                            <div
                              className="adv-filter-box"
                              style={{ background: '#fef2f2', borderColor: '#fecaca' }}
                            >
                              <h5
                                style={{
                                  margin: '0 0 8px',
                                  fontSize: '13px',
                                  fontWeight: 700,
                                  color: '#b91c1c',
                                }}
                              >
                                Reverse an Incorrect Ledger Entry
                              </h5>
                              <form
                                className="hr-form-grid"
                                onSubmit={(event) => void recordReversal(event)}
                              >
                                <SelectInput
                                  label="Select Ledger Entry to Reverse"
                                  required
                                  value={reversal.reversesEntryId}
                                  options={[
                                    { value: '', label: 'Select transaction entry' },
                                    ...selected.ledger
                                      .filter((entry) => entry.kind !== 3)
                                      .map((entry) => ({
                                        value: entry.id,
                                        label: `${entry.sourceReference} (${money(entry.amount, entry.currency)})`,
                                      })),
                                  ]}
                                  onChange={(event) =>
                                    setReversal({
                                      ...reversal,
                                      reversesEntryId: event.target.value,
                                    })
                                  }
                                />
                                <TextInput
                                  label="Reversal Date"
                                  required
                                  type="date"
                                  value={reversal.valueDate}
                                  onChange={(event) =>
                                    setReversal({ ...reversal, valueDate: event.target.value })
                                  }
                                />
                                <TextInput
                                  label="Audit Reference"
                                  required
                                  maxLength={150}
                                  value={reversal.sourceReference}
                                  placeholder="e.g. REV-REF-01"
                                  onChange={(event) =>
                                    setReversal({
                                      ...reversal,
                                      sourceReference: event.target.value,
                                    })
                                  }
                                />
                                <TextAreaField
                                  label="Reversal Justification"
                                  required
                                  value={reversal.reason}
                                  onChange={(value) => setReversal({ ...reversal, reason: value })}
                                  disabled={action.pending}
                                />
                                <div className="hr-actions wide">
                                  <Button
                                    type="submit"
                                    variant="danger"
                                    disabled={
                                      action.pending ||
                                      !reversal.reversesEntryId ||
                                      !reversal.sourceReference.trim() ||
                                      !reversal.reason.trim()
                                    }
                                  >
                                    Post Reversal
                                  </Button>
                                </div>
                              </form>
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </QueryState>
                )}
              </article>
            );
          })}
        </div>

        {agreements.data && (
          <HasMorePager
            page={page}
            hasMore={agreements.data.hasMore}
            busy={agreements.isFetching}
            onPrevious={() => setPage((value) => Math.max(1, value - 1))}
            onNext={() => setPage((value) => value + 1)}
          />
        )}
      </QueryState>
      <ActionMessage action={action} />
    </div>
  );
}
