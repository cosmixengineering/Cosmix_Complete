import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { Denied, QueryState, ActionMessage } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import {
  FinanceNotice,
  FinanceStatus,
  FinanceTabs,
  HasMorePager,
  TextAreaField,
} from '../components/finance/FinanceChrome';
import { money } from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { queryString } from '../api/hr-finance-api';
import { labelDate } from '../utils/messages';
import {
  payrollBatchDetail,
  payrollPage,
  payrollPreview,
  type PayrollBatch,
} from '../schemas/finance';

const permissions = {
  read: 'attendance.payroll-handoff.read',
  prepare: 'attendance.payroll-handoff.prepare',
  lock: 'attendance.payroll-handoff.lock',
  process: 'attendance.payroll-handoff.process',
};

export function PayrollHandoffPage() {
  const access = useFinanceAccess();
  const tabs = [
    ...(access.can(permissions.read) ? [{ id: 'preview', label: 'Preview and prepare' }] : []),
    ...(access.can(permissions.read) ? [{ id: 'batches', label: 'Batches' }] : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');
  if (!tabs.length) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');
  return (
    <section aria-label="Payroll handoff">
      <div className="hr-page-heading">
        <p className="hr-kicker">HR &bull; Attendance money</p>
        <h1>Payroll handoff</h1>
      </div>
      <FinanceTabs tabs={tabs} active={active} onChange={setTab} label="Payroll handoff views" />
      {active === 'preview' && <PayrollPreviewPanel canPrepare={access.can(permissions.prepare)} />}
      {active === 'batches' && (
        <PayrollBatches
          canLock={access.can(permissions.lock)}
          canProcess={access.can(permissions.process)}
          accountId={access.user?.account?.accountId ?? ''}
        />
      )}
    </section>
  );
}

function PayrollPreviewPanel({ canPrepare }: { canPrepare: boolean }) {
  const now = new Date();
  const [year, setYear] = useState(String(now.getFullYear()));
  const [month, setMonth] = useState(String(now.getMonth() + 1));
  const [loaded, setLoaded] = useState<{ year: string; month: string } | null>(null);
  const [reason, setReason] = useState('');
  const action = useFinanceAction();

  const preview = useFinanceQuery(
    loaded
      ? `/payroll-handoff/preview${queryString({ year: Number(loaded.year), month: Number(loaded.month) })}`
      : '/payroll-handoff/preview',
    payrollPreview,
    Boolean(loaded),
    Boolean(loaded),
  );

  async function load() {
    setLoaded({ year, month });
  }

  async function prepare(event: FormEvent) {
    event.preventDefault();
    if (!preview.data) return;
    const result = await action.run(
      '/payroll-handoff/batches',
      {
        year: Number(loaded?.year),
        month: Number(loaded?.month),
        expectedPreviewFingerprint: preview.data.previewFingerprint,
        reason,
      },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setReason('');
      void preview.refetch();
    }
  }

  return (
    <>
      <section className="hr-form-section">
        <h2>Month preview</h2>
        <p className="muted">
          The month must already be locked in attendance. A preview is read-only; only the prepare
          step freezes a batch for one locked month.
        </p>
        <div className="hr-toolbar">
          <TextInput
            label="Year"
            type="number"
            min={1}
            max={9998}
            value={year}
            onChange={(event) => setYear(event.target.value)}
          />
          <TextInput
            label="Month (1-12)"
            type="number"
            min={1}
            max={12}
            value={month}
            onChange={(event) => setMonth(event.target.value)}
          />
          <Button variant="secondary" disabled={!year || !month} onClick={() => void load()}>
            Load preview
          </Button>
        </div>
        <QueryState query={preview}>
          {preview.data && (
            <>
              <RecordView
                value={{
                  periodStart: preview.data.periodStart,
                  periodEndExclusive: preview.data.periodEndExclusive,
                  lockedAt: labelDate(preview.data.lockedAtUtc),
                  employeeCount: preview.data.employeeCount,
                  currency: preview.data.currency,
                  overtimeTotal: money(preview.data.totals.overtimeTotal, preview.data.currency),
                  allowanceTotal: money(preview.data.totals.allowanceTotal, preview.data.currency),
                  attendanceDeductionTotal: money(
                    preview.data.totals.attendanceDeductionTotal,
                    preview.data.currency,
                  ),
                  netAmount: money(preview.data.totals.netAmount, preview.data.currency),
                  previewFingerprint: preview.data.previewFingerprint,
                }}
              />
              <h3>Projected lines ({preview.data.lines.length})</h3>
              {!preview.data.lines.length && (
                <FinanceNotice>No payroll lines are available for this period.</FinanceNotice>
              )}
              {preview.data.lines.length > 0 && (
                <div className="hr-table-scroll">
                  <table className="hr-prepared-table">
                    <thead>
                      <tr>
                        <th>Employee</th>
                        <th>Component</th>
                        <th>Source</th>
                        <th>Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.data.lines.map((line) => (
                        <tr key={`${line.sourceType}-${line.sourceId}`}>
                          <td>{line.employeeId}</td>
                          <td>{line.component}</td>
                          <td>
                            {line.sourceType} &middot; v{line.sourceVersion}
                          </td>
                          <td>{money(line.amount, line.currency)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {preview.data.skipped.length > 0 && (
                <>
                  <h3>Skipped sources ({preview.data.skipped.length})</h3>
                  <div className="hr-message hr-message--error" role="alert">
                    <ul>
                      {preview.data.skipped.map((item, index) => (
                        <li key={`${item.reason}-${index}`}>
                          {item.reason}: {item.sourceType} {item.sourceId ?? '—'} for{' '}
                          {item.employeeId}
                        </li>
                      ))}
                    </ul>
                  </div>
                </>
              )}
              {canPrepare && (
                <form
                  className="hr-form-section hr-form-grid"
                  onSubmit={(event) => void prepare(event)}
                >
                  <TextAreaField
                    label="Preparation reason"
                    required
                    value={reason}
                    onChange={setReason}
                    disabled={action.pending}
                  />
                  <div className="hr-actions wide">
                    <Button
                      type="submit"
                      disabled={action.pending || !preview.data.lines.length || !reason.trim()}
                    >
                      Prepare and freeze batch
                    </Button>
                  </div>
                </form>
              )}
            </>
          )}
        </QueryState>
        <ActionMessage action={action} />
      </section>
    </>
  );
}

function PayrollBatches({
  canLock,
  canProcess,
  accountId,
}: {
  canLock: boolean;
  canProcess: boolean;
  accountId: string;
}) {
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [lockReason, setLockReason] = useState('');
  const [processingReference, setProcessingReference] = useState('');
  const [processingReason, setProcessingReason] = useState('');
  const action = useFinanceAction();

  const batches = useFinanceQuery(
    `/payroll-handoff/batches${queryString({ page, pageSize: 25 })}`,
    payrollPage,
    true,
  );
  const detail = useFinanceQuery(
    selectedId ? `/payroll-handoff/batches/${selectedId}` : '/payroll-handoff/batches',
    payrollBatchDetail,
    Boolean(selectedId),
    Boolean(selectedId),
  );
  const selected = detail.data?.batch ?? null;

  function isPreparer(batch: PayrollBatch | null) {
    return Boolean(batch && batch.preparedByUserId === accountId);
  }

  async function lock(event: FormEvent) {
    event.preventDefault();
    if (!selectedId || !selected) return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedId}/lock`,
      { expectedVersion: selected.version, reason: lockReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setLockReason('');
      void batches.refetch();
      void detail.refetch();
    }
  }

  async function process(event: FormEvent) {
    event.preventDefault();
    if (!selectedId || !selected) return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedId}/processing`,
      { externalProcessingReference: processingReference, reason: processingReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setProcessingReference('');
      setProcessingReason('');
      void batches.refetch();
      void detail.refetch();
    }
  }

  return (
    <section className="hr-form-section">
      <h2>Handoff batches</h2>
      <QueryState query={batches}>
        {!batches.data?.items.length && (
          <FinanceNotice>No payroll handoff batches yet.</FinanceNotice>
        )}
        {batches.data?.items.map((batch) => (
          <article className="hr-repeat" key={batch.id}>
            <div className="hr-repeat-heading">
              <h3>
                {batch.batchReference} &middot; {batch.periodStart} &middot;{' '}
                <FinanceStatus value={batch.state} />
              </h3>
              <div className="hr-actions">
                <Button
                  variant="secondary"
                  onClick={() => setSelectedId(selectedId === batch.id ? null : batch.id)}
                >
                  {selectedId === batch.id ? 'Close batch' : 'Open batch'}
                </Button>
              </div>
            </div>
            <RecordView
              value={{
                batchReference: batch.batchReference,
                periodStart: batch.periodStart,
                periodEndExclusive: batch.periodEndExclusive,
                currency: batch.currency,
                state: batch.state,
                lineCount: batch.lineCount,
                linesRevision: batch.linesRevision,
                preparedAt: labelDate(batch.preparedAt),
                preparedBy: batch.preparedByUserId,
                lockedAt: labelDate(batch.lockedAt),
                lockedBy: batch.lockedByUserId,
                overtimeTotal: money(batch.totals.overtimeTotal, batch.currency),
                allowanceTotal: money(batch.totals.allowanceTotal, batch.currency),
                attendanceDeductionTotal: money(
                  batch.totals.attendanceDeductionTotal,
                  batch.currency,
                ),
                netAmount: money(batch.totals.netAmount, batch.currency),
              }}
            />
            {selectedId === batch.id && (
              <QueryState query={detail}>
                {detail.data && (
                  <>
                    <h4>Frozen lines</h4>
                    <div className="hr-table-scroll">
                      <table className="hr-prepared-table">
                        <thead>
                          <tr>
                            <th>Employee</th>
                            <th>Component</th>
                            <th>Source</th>
                            <th>Amount</th>
                            <th>Trace</th>
                          </tr>
                        </thead>
                        <tbody>
                          {detail.data.lines.map((line) => (
                            <tr key={line.id}>
                              <td>{line.employeeId}</td>
                              <td>{line.component}</td>
                              <td>
                                {line.sourceType} &middot; v{line.sourceVersion}
                              </td>
                              <td>{money(line.amount, line.currency)}</td>
                              <td>{line.traceId}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <h4>Accounts processing evidence</h4>
                    {!detail.data.processing.length && (
                      <p className="muted">No processing evidence has been recorded yet.</p>
                    )}
                    {detail.data.processing.map((record) => (
                      <RecordView
                        key={record.id}
                        value={{
                          processingReference: record.externalProcessingReference,
                          processedBy: record.processedByUserId,
                          processedAt: labelDate(record.processedAt),
                        }}
                      />
                    ))}
                    {canLock && batch.state === 'prepared' && (
                      <form
                        className="hr-form-section hr-form-grid"
                        onSubmit={(event) => void lock(event)}
                      >
                        <p className="muted wide">
                          {isPreparer(batch)
                            ? 'The preparer cannot lock their own batch. A separate approver must lock it.'
                            : 'Locking freezes this batch and schedules the allowance settlements in one transaction.'}
                        </p>
                        <TextAreaField
                          label="Lock reason"
                          required
                          value={lockReason}
                          onChange={setLockReason}
                          disabled={action.pending || isPreparer(batch)}
                        />
                        <div className="hr-actions wide">
                          <Button
                            type="submit"
                            variant="success"
                            disabled={action.pending || isPreparer(batch) || !lockReason.trim()}
                          >
                            Lock batch
                          </Button>
                        </div>
                      </form>
                    )}
                    {canProcess && batch.state === 'locked' && (
                      <form
                        className="hr-form-section hr-form-grid"
                        onSubmit={(event) => void process(event)}
                      >
                        <TextInput
                          label="External processing reference"
                          required
                          maxLength={150}
                          value={processingReference}
                          onChange={(event) => setProcessingReference(event.target.value)}
                        />
                        <TextAreaField
                          label="Processing reason"
                          required
                          value={processingReason}
                          onChange={setProcessingReason}
                          disabled={action.pending}
                        />
                        <div className="hr-actions wide">
                          <Button
                            type="submit"
                            disabled={
                              action.pending ||
                              !processingReference.trim() ||
                              !processingReason.trim()
                            }
                          >
                            Record processing evidence
                          </Button>
                        </div>
                      </form>
                    )}
                  </>
                )}
              </QueryState>
            )}
          </article>
        ))}
        {batches.data && (
          <HasMorePager
            page={page}
            hasMore={batches.data.hasMore}
            busy={batches.isFetching}
            onPrevious={() => setPage((value) => Math.max(1, value - 1))}
            onNext={() => setPage((value) => value + 1)}
          />
        )}
      </QueryState>
      <ActionMessage action={action} />
    </section>
  );
}
