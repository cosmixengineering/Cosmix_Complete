import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Denied, QueryState, ActionMessage } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import {
  FinanceNotice,
  FinanceStatus,
  FinanceTabs,
  HasMorePager,
  TextAreaField,
} from '../components/finance/FinanceChrome';
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

const permissions = {
  review: 'attendance.advancesloans.review',
  approve: 'attendance.advancesloans.approve',
  repay: 'attendance.advancesloans.repay',
};

export function AdvancesLoansPage() {
  const access = useFinanceAccess();
  const tabs = [
    { id: 'requests', label: 'Requests' },
    { id: 'agreements', label: 'Agreements' },
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');
  if (!access.can(permissions.review)) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');
  return (
    <section aria-label="Advances and loans">
      <div className="hr-page-heading">
        <p className="hr-kicker">HR &bull; Attendance money</p>
        <h1>Advances and loans</h1>
      </div>
      <FinanceTabs tabs={tabs} active={active} onChange={setTab} label="Advance and loan views" />
      {active === 'requests' && (
        <AdvanceRequests
          canApprove={access.can(permissions.approve)}
          canReview={access.can(permissions.review)}
        />
      )}
      {active === 'agreements' && <AdvanceAgreements canRepay={access.can(permissions.repay)} />}
    </section>
  );
}

function AdvanceRequests({ canApprove, canReview }: { canApprove: boolean; canReview: boolean }) {
  const [page, setPage] = useState(1);
  const [state, setState] = useState('0');
  const [employeeId, setEmployeeId] = useState('');
  const [selected, setSelected] = useState<AdvanceRequest | null>(null);
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
    setApproveForm({
      approvedPrincipal: String(request.requestedAmount),
      currency: request.currency,
      effectiveOn: new Date().toISOString().slice(0, 10),
      approvedTerms: '',
      numberOfInstallments: '1',
      firstDueDate: '',
    });
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
      void requests.refetch();
    }
  }

  async function reject(request: AdvanceRequest) {
    if (!window.confirm('Reject this request?')) return;
    const result = await action.run(
      `/hr/advances-loans/requests/${request.id}/reject`,
      { reason: rejectReason },
      'POST',
      advanceRequest,
    );
    if (result) {
      setSelected(null);
      setRejectReason('');
      void requests.refetch();
    }
  }

  return (
    <>
      <section className="hr-form-section">
        <h2>Request queue</h2>
        <div className="hr-toolbar">
          <SelectInput
            label="State"
            value={state}
            options={approvalStateOptions}
            onChange={(event) => {
              setState(event.target.value);
              setPage(1);
            }}
          />
          <TextInput
            label="Employee id"
            value={employeeId}
            placeholder="All employees"
            onChange={(event) => {
              setEmployeeId(event.target.value);
              setPage(1);
            }}
          />
        </div>
        <QueryState query={requests}>
          {!requests.data?.items.length && (
            <FinanceNotice>No requests match this filter.</FinanceNotice>
          )}
          {requests.data?.items.map((request) => (
            <article className="hr-repeat" key={request.id}>
              <div className="hr-repeat-heading">
                <h3>
                  {request.employeeCode} &middot;{' '}
                  {advanceKindOptions[request.kind - 1]?.label ?? request.kind} &middot;{' '}
                  <FinanceStatus value={request.state} />
                </h3>
                <div className="hr-actions">
                  {canApprove && request.state === 'Pending' && (
                    <Button variant="success" onClick={() => startApprove(request)}>
                      Approve
                    </Button>
                  )}
                  {canReview && request.state === 'Pending' && (
                    <Button variant="danger" onClick={() => setSelected(request)}>
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
                  reason: request.reason,
                  requestedOn: request.requestedOn,
                  createdAt: labelDate(request.createdAt),
                  agreement: request.agreementId,
                }}
              />
              {canApprove && selected?.id === request.id && (
                <form className="hr-form-grid" onSubmit={(event) => void approve(event)}>
                  <TextInput
                    label="Approved principal"
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
                    label="Effective on"
                    required
                    type="date"
                    value={approveForm.effectiveOn}
                    onChange={(event) =>
                      setApproveForm({ ...approveForm, effectiveOn: event.target.value })
                    }
                  />
                  <TextInput
                    label="Number of installments"
                    required
                    type="number"
                    min={1}
                    max={60}
                    value={approveForm.numberOfInstallments}
                    onChange={(event) =>
                      setApproveForm({ ...approveForm, numberOfInstallments: event.target.value })
                    }
                  />
                  <TextInput
                    label="First due date"
                    required
                    type="date"
                    value={approveForm.firstDueDate}
                    onChange={(event) =>
                      setApproveForm({ ...approveForm, firstDueDate: event.target.value })
                    }
                  />
                  <TextAreaField
                    label="Approved terms"
                    required
                    value={approveForm.approvedTerms}
                    onChange={(value) => setApproveForm({ ...approveForm, approvedTerms: value })}
                    disabled={action.pending}
                  />
                  <div className="hr-actions wide">
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
                      Approve and create agreement
                    </Button>
                  </div>
                </form>
              )}
              {canReview && selected?.id === request.id && (
                <form
                  className="hr-form-grid"
                  onSubmit={(event) => {
                    event.preventDefault();
                    void reject(request);
                  }}
                >
                  <TextAreaField
                    label="Rejection reason"
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
                      Reject request
                    </Button>
                  </div>
                </form>
              )}
            </article>
          ))}
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
      </section>
    </>
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
    <>
      <section className="hr-form-section">
        <h2>Agreements</h2>
        <div className="hr-toolbar">
          <TextInput
            label="Employee id"
            value={employeeId}
            placeholder="All employees"
            onChange={(event) => {
              setEmployeeId(event.target.value);
              setPage(1);
            }}
          />
          <SelectInput
            label="Active only"
            value={activeOnly}
            options={[
              { value: 'true', label: 'Active agreements' },
              { value: 'false', label: 'All agreements' },
            ]}
            onChange={(event) => {
              setActiveOnly(event.target.value);
              setPage(1);
            }}
          />
        </div>
        <QueryState query={agreements}>
          {!agreements.data?.items.length && (
            <FinanceNotice>No agreements match this filter.</FinanceNotice>
          )}
          {agreements.data?.items.map((agreement) => (
            <article className="hr-repeat" key={agreement.id}>
              <div className="hr-repeat-heading">
                <h3>
                  {agreement.employeeCode} &middot;{' '}
                  {advanceKindOptions[agreement.kind - 1]?.label ?? agreement.kind} &middot;{' '}
                  <FinanceStatus value={agreement.isFullyRepaid ? 'settled' : 'open'} />
                </h3>
                <div className="hr-actions">
                  <Button
                    variant="secondary"
                    onClick={() => setSelectedId(selectedId === agreement.id ? null : agreement.id)}
                  >
                    {selectedId === agreement.id ? 'Close agreement' : 'Open agreement'}
                  </Button>
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
              {selectedId === agreement.id && (
                <QueryState query={detail}>
                  {selected && (
                    <>
                      <h4>Repayment schedule</h4>
                      <div className="hr-table-scroll">
                        <table className="hr-prepared-table">
                          <thead>
                            <tr>
                              <th>#</th>
                              <th>Due</th>
                              <th>Principal</th>
                              <th>Other</th>
                              <th>Total due</th>
                              <th>Paid</th>
                              <th>Settled</th>
                            </tr>
                          </thead>
                          <tbody>
                            {selected.schedule.map((entry) => (
                              <tr key={entry.id}>
                                <td>{entry.sequence}</td>
                                <td>{entry.dueDate}</td>
                                <td>{money(entry.principalAmount, entry.currency)}</td>
                                <td>{money(entry.otherApprovedAmount, entry.currency)}</td>
                                <td>{money(entry.totalDue, entry.currency)}</td>
                                <td>{money(entry.paidAmount, entry.currency)}</td>
                                <td>
                                  <FinanceStatus value={entry.isSettled ? 'settled' : 'open'} />
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <h4>Payment ledger</h4>
                      {!selected.ledger.length && (
                        <p className="muted">No repayments recorded yet.</p>
                      )}
                      {selected.ledger.map((entry) => (
                        <RecordView
                          key={entry.id}
                          value={{
                            ledgerReference: entry.id,
                            kind:
                              entry.kind === 1
                                ? 'Payroll deduction'
                                : entry.kind === 2
                                  ? 'Received payment'
                                  : entry.kind === 3
                                    ? 'Reversal'
                                    : 'Adjustment',
                            amount: money(entry.amount, entry.currency),
                            valueDate: entry.valueDate,
                            sourceReference: entry.sourceReference,
                            reversesEntry: entry.reversesEntryId,
                            recordedAt: labelDate(entry.recordedAt),
                          }}
                        />
                      ))}
                      {canRepay && (
                        <>
                          <div className="hr-form-section">
                            <h3>Record a repayment</h3>
                            <form
                              className="hr-form-grid"
                              onSubmit={(event) => void recordRepayment(event)}
                            >
                              <SelectInput
                                label="Schedule entry"
                                value={repayment.repaymentScheduleEntryId}
                                options={[
                                  { value: '', label: 'Unallocated payment' },
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
                                label="Kind"
                                value={repayment.kind}
                                options={[
                                  { value: '1', label: 'Payroll deduction' },
                                  { value: '2', label: 'Received payment' },
                                ]}
                                onChange={(event) =>
                                  setRepayment({ ...repayment, kind: event.target.value })
                                }
                              />
                              <TextInput
                                label="Amount"
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
                                label="Value date"
                                required
                                type="date"
                                value={repayment.valueDate}
                                onChange={(event) =>
                                  setRepayment({ ...repayment, valueDate: event.target.value })
                                }
                              />
                              <TextInput
                                label="Source reference"
                                required
                                maxLength={150}
                                value={repayment.sourceReference}
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
                                  disabled={
                                    action.pending ||
                                    !repayment.amount ||
                                    !repayment.sourceReference.trim()
                                  }
                                >
                                  Record repayment
                                </Button>
                              </div>
                            </form>
                          </div>
                          <div className="hr-form-section">
                            <h3>Reverse a ledger entry</h3>
                            <form
                              className="hr-form-grid"
                              onSubmit={(event) => void recordReversal(event)}
                            >
                              <SelectInput
                                label="Entry to reverse"
                                required
                                value={reversal.reversesEntryId}
                                options={[
                                  { value: '', label: 'Choose an entry' },
                                  ...selected.ledger
                                    .filter((entry) => entry.kind !== 3)
                                    .map((entry) => ({
                                      value: entry.id,
                                      label: `${entry.sourceReference} (${money(entry.amount, entry.currency)})`,
                                    })),
                                ]}
                                onChange={(event) =>
                                  setReversal({ ...reversal, reversesEntryId: event.target.value })
                                }
                              />
                              <TextInput
                                label="Value date"
                                required
                                type="date"
                                value={reversal.valueDate}
                                onChange={(event) =>
                                  setReversal({ ...reversal, valueDate: event.target.value })
                                }
                              />
                              <TextInput
                                label="Source reference"
                                required
                                maxLength={150}
                                value={reversal.sourceReference}
                                onChange={(event) =>
                                  setReversal({ ...reversal, sourceReference: event.target.value })
                                }
                              />
                              <TextAreaField
                                label="Reason"
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
                                  Reverse entry
                                </Button>
                              </div>
                            </form>
                          </div>
                        </>
                      )}
                    </>
                  )}
                </QueryState>
              )}
            </article>
          ))}
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
      </section>
    </>
  );
}
