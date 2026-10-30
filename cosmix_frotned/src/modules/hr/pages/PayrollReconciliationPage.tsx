import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { Button } from '@/components/ui/Button';
import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { businessDate } from '@/utils/format';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceQuery } from '../hooks/use-finance-queries';
import { useDirtyForm } from '../hooks/use-dirty-form';
import { useHrLifetime } from '../hooks/use-hr-lifetime';
import { Denied } from '../components/Feedback';
import { FinanceQueryState } from '../components/finance/FinanceQueryState';
import { FinanceNotice, HasMorePager, TextAreaField } from '../components/finance/FinanceChrome';
import { ReviewedFinanceCommand } from '../components/finance/ReviewedFinanceCommand';
import { PayrollSelect } from '../components/finance/PayrollFields';
import { payrollPage } from '../schemas/finance';
import {
  comparisonReport,
  parallelRun,
  parallelRunPage,
  parallelRunHistory,
  parallelRunFor,
  reconciliationRoot as root,
  reconciliationPermissions as permissions,
  reconciliationColumns,
  reconciliationUpload,
  reviewExplanations,
  type ComparisonReport,
  type ParallelRun,
} from '../schemas/payroll-reconciliation';

const states: Record<number, string> = {
  1: 'Prepared — HR review required',
  2: 'HR reviewed — Finance review required',
  3: 'Finance reconciled — Management decision required',
  4: 'Management approved',
  5: 'Rejected',
  6: 'Superseded',
};
const causes = [
  'Exact match',
  'Rounding difference',
  'Proration policy difference',
  'Attendance adjustment',
  'Overtime rate policy',
  'Statutory bracket update',
  'Manual unapproved adjustment',
  'Roster missing employee',
];
const amount = (value: number) =>
  value.toLocaleString('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function PayrollReconciliationPage() {
  const access = useFinanceAccess();
  if (!access.can(permissions.read)) return <Denied />;
  return <ReconciliationWorkspace key={JSON.stringify([access.subject, access.user?.grants])} />;
}

function ReconciliationWorkspace() {
  const access = useFinanceAccess();
  const [params] = useSearchParams();
  const [month, setMonth] = useState(businessDate().slice(0, 7));
  const [page, setPage] = useState(1);
  const periodValid =
    /^20\d{2}-(0[1-9]|1[0-2])$/.test(month) &&
    Number(month.slice(0, 4)) >= 2020 &&
    Number(month.slice(0, 4)) <= 2035;
  const year = Number(month.slice(0, 4));
  const monthNumber = Number(month.slice(5, 7));
  const runs = useFinanceQuery(
    `${root}/parallel?year=${year}&month=${monthNumber}&page=${page}&pageSize=20`,
    parallelRunPage,
    true,
    periodValid,
  );
  const selectedId = params.get('run');
  const view = params.get('view');
  return (
    <section className="hr-page">
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">PAYROLL EVIDENCE</p>
          <h1>Payroll comparison and parallel review</h1>
        </div>
      </div>
      <p>
        Compare an actual salary sheet with a frozen PKR payroll batch. Saved parallel runs retain
        the comparison, source fingerprints and each independent decision.
      </p>
      <nav className="hr-actions" aria-label="Payroll reconciliation views">
        <Link to="/hr/payroll-reconciliation">Saved parallel runs</Link>
        {access.can(permissions.initiate) && <Link to="?view=prepare">Prepare parallel run</Link>}
        {access.can(permissions.historical) && (
          <Link to="?view=historical">Compare historical sheet</Link>
        )}
      </nav>
      {view === 'prepare' || view === 'historical' ? (
        access.can(view === 'prepare' ? permissions.initiate : permissions.historical) ? (
          <ReconciliationUpload key={view} historical={view === 'historical'} />
        ) : (
          <Denied />
        )
      ) : selectedId ? (
        z.uuid().safeParse(selectedId).success ? (
          <SavedRun key={selectedId} id={selectedId} />
        ) : (
          <p role="alert">Choose a saved run with a valid reference.</p>
        )
      ) : (
        <>
          <TextInput
            label="Payroll month"
            type="month"
            min="2020-01"
            max="2035-12"
            value={month}
            onChange={(event) => {
              setMonth(event.target.value);
              setPage(1);
            }}
          />
          {!periodValid ? (
            <p role="alert">Choose a month between January 2020 and December 2035.</p>
          ) : (
            <>
              <Button
                variant="secondary"
                disabled={runs.isFetching}
                onClick={() => void runs.refetch()}
              >
                Refresh saved runs
              </Button>
              <FinanceQueryState query={runs}>
                <div className="payroll-table-wrapper">
                  <table className="payroll-table">
                    <thead>
                      <tr>
                        <th>Prepared</th>
                        <th>Source</th>
                        <th>Status</th>
                        <th>Version</th>
                        <th>Net variance (PKR)</th>
                        <th>Review</th>
                      </tr>
                    </thead>
                    <tbody>
                      {runs.data?.items.map((run) => (
                        <tr key={run.id}>
                          <td>
                            {new Date(run.preparedAt).toLocaleString('en-GB', {
                              timeZone: 'Asia/Karachi',
                            })}
                          </td>
                          <td>{run.report.sourceFileName}</td>
                          <td>{states[run.state]}</td>
                          <td>{run.version}</td>
                          <td>{amount(run.summary.netVariance)}</td>
                          <td>
                            <Link to={`?run=${run.id}`}>Open saved run</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {runs.data?.items.length === 0 && <p>No saved parallel runs for this month.</p>}
                <HasMorePager
                  page={page}
                  hasMore={runs.data?.hasMore ?? false}
                  busy={runs.isFetching}
                  onPrevious={() => setPage(page - 1)}
                  onNext={() => setPage(page + 1)}
                />
              </FinanceQueryState>
            </>
          )}
        </>
      )}
    </section>
  );
}

function ReconciliationUpload({ historical }: { historical: boolean }) {
  const access = useFinanceAccess();
  const lifetime = useHrLifetime();
  const [month, setMonth] = useState(businessDate().slice(0, 7));
  const [batchId, setBatchId] = useState('');
  const [batchPage, setBatchPage] = useState(1);
  const [file, setFile] = useState<{
    fileName: string;
    contentType: string;
    base64: string;
  } | null>(null);
  const [reading, setReading] = useState(false);
  const [error, setError] = useState('');
  const [reason, setReason] = useState('');
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [report, setReport] = useState<ComparisonReport | null>(null);
  const [saved, setSaved] = useState(false);
  const [preparedRun, setPreparedRun] = useState<ParallelRun | null>(null);
  const batches = useFinanceQuery(
    `/payroll-handoff/batches?page=${batchPage}&pageSize=25`,
    payrollPage,
    access.can('attendance.payroll-handoff.read'),
  );
  useDirtyForm(!saved && Boolean(file || reason || batchId));
  async function readFile(source: File | undefined) {
    setFile(null);
    setError('');
    setReport(null);
    setSaved(false);
    if (!source) return;
    if (
      !/\.(csv|xlsx)$/i.test(source.name) ||
      source.size === 0 ||
      source.size > 4 * 1024 * 1024 ||
      source.name.length > 150
    ) {
      setError(
        'Choose a nonempty CSV or XLSX file of at most 4 MiB with a filename of at most 150 characters.',
      );
      return;
    }
    const signal = lifetime();
    setReading(true);
    try {
      const bytes = new Uint8Array(await source.arrayBuffer());
      signal.throwIfAborted();
      let binary = '';
      for (let offset = 0; offset < bytes.length; offset += 8192)
        binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
      setFile({
        fileName: source.name,
        contentType: source.name.toLowerCase().endsWith('.xlsx')
          ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
          : 'text/csv',
        base64: btoa(binary),
      });
    } catch {
      if (!signal.aborted) setError('The selected file could not be read. Choose it again.');
    } finally {
      if (!signal.aborted) setReading(false);
    }
  }
  function prepare() {
    const parsed = reconciliationUpload.parse({
      ...file,
      year: Number(month.slice(0, 4)),
      month: Number(month.slice(5, 7)),
      erpBatchId: batchId,
      columnMapping: mapping,
      reason,
    });
    setSaved(false);
    return {
      path: `${root}/${historical ? 'historical' : 'parallel/initiate'}`,
      body: historical
        ? parsed
        : {
            year: parsed.year,
            month: parsed.month,
            erpBatchId: parsed.erpBatchId,
            legacyFileName: parsed.fileName,
            legacySheetBase64: parsed.base64,
            columnMapping: parsed.columnMapping,
            reason: parsed.reason,
          },
      summary: `${historical ? 'Compare' : 'Retain a parallel comparison for'} ${month} against batch ${batchId}, using ${parsed.fileName}. Reason: ${parsed.reason}. No salary or payment is changed.`,
    };
  }
  const fields = (locked: boolean) => (
    <>
      <TextInput
        label="Payroll month"
        type="month"
        required
        min="2020-01"
        max="2035-12"
        value={month}
        onChange={(event) => setMonth(event.target.value)}
      />
      {access.can('attendance.payroll-handoff.read') && (
        <FinanceQueryState query={batches}>
          <PayrollSelect
            label="Select a retained payroll batch"
            required={false}
            value={batchId}
            onChange={setBatchId}
            options={(batches.data?.items ?? []).map((batch) => ({
              value: batch.id,
              label: `${batch.batchReference} · ${batch.periodStart} · ${batch.currency} · ${batch.state}`,
            }))}
          />
          <HasMorePager
            page={batchPage}
            hasMore={batches.data?.hasMore ?? false}
            busy={batches.isFetching || locked}
            onPrevious={() => setBatchPage(batchPage - 1)}
            onNext={() => setBatchPage(batchPage + 1)}
          />
        </FinanceQueryState>
      )}
      <TextInput
        label="Frozen payroll batch reference"
        value={batchId}
        required
        onChange={(event) => setBatchId(event.target.value.trim())}
      />
      <TextInput
        label="Actual salary sheet (CSV or XLSX)"
        type="file"
        accept=".csv,.xlsx"
        onChange={(event) => void readFile(event.target.files?.[0])}
      />
      {reading && <p role="status">Reading the selected file…</p>}
      {file && <p>Selected: {file.fileName}</p>}
      <p>
        Enter the exact source header for every field. Amounts must be explicit PKR numbers with at
        most two decimal places; use 0 where applicable. Deductions excludes EOBI, provincial social
        security and income tax. Allowances includes all other approved positive earnings; Sessi
        includes SESSI/PESSI employee contributions.
      </p>
      <div className="hr-form-grid">
        {reconciliationColumns.map((column) => (
          <TextInput
            key={column}
            label={`${column} source column`}
            value={mapping[column] ?? ''}
            required
            onChange={(event) => setMapping({ ...mapping, [column]: event.target.value })}
          />
        ))}
      </div>
      <TextAreaField label="Reason for comparison" required value={reason} onChange={setReason} />
    </>
  );
  return (
    <section
      onChange={() => {
        setSaved(false);
        setReport(null);
        setPreparedRun(null);
      }}
    >
      <h2>{historical ? 'Historical sheet comparison' : 'Prepare a parallel payroll run'}</h2>
      <FinanceNotice>
        Use authorized source evidence. A comparison does not approve payroll, change employee
        salaries, authorize bank upload or certify company readiness.
      </FinanceNotice>
      {error && <p role="alert">{error}</p>}
      {historical ? (
        <ReviewedFinanceCommand
          label="Historical comparison"
          schema={comparisonReport}
          prepare={prepare}
          disabled={reading}
          onSaved={(value) => {
            setReport(value);
            setSaved(true);
          }}
        >
          {fields}
        </ReviewedFinanceCommand>
      ) : (
        <ReviewedFinanceCommand
          label="Parallel run preparation"
          schema={parallelRun}
          prepare={prepare}
          disabled={reading}
          onSaved={(value) => {
            setSaved(true);
            setPreparedRun(value);
          }}
        >
          {fields}
        </ReviewedFinanceCommand>
      )}
      {preparedRun && (
        <p role="status">
          Parallel run saved.{' '}
          <Link to={`?run=${preparedRun.id}`}>Open saved comparison and review history</Link>
        </p>
      )}
      {report && <ComparisonEvidence report={report} />}
    </section>
  );
}

function SavedRun({ id }: { id: string }) {
  const [dirty, setDirty] = useState(false);
  const detail = useFinanceQuery(`${root}/parallel/${id}`, parallelRunFor(id), true);
  const history = useFinanceQuery(
    `${root}/parallel/${id}/history`,
    parallelRunHistory.refine(
      (runs) =>
        runs.every((run) => run.id === id) &&
        new Set(runs.map((run) => run.version)).size === runs.length,
    ),
    true,
  );
  return (
    <>
      <Button
        variant="secondary"
        disabled={dirty || detail.isFetching || history.isFetching}
        onClick={() => {
          void detail.refetch();
          void history.refetch();
        }}
      >
        Refresh saved evidence
      </Button>
      <FinanceQueryState query={detail}>
        {detail.data && (
          <RunEvidence
            key={`${detail.data.id}:${detail.data.version}`}
            run={detail.data}
            busy={detail.isFetching}
            onDirty={setDirty}
            onSaved={() => {
              setDirty(false);
              void detail.refetch();
              void history.refetch();
            }}
          />
        )}
      </FinanceQueryState>
      <h2>Retained revision history</h2>
      <FinanceQueryState query={history}>
        <ol>
          {history.data?.map((run) => (
            <li key={run.version}>
              <strong>
                Version {run.version}: {states[run.state]}
              </strong>
              <p>
                Prepared{' '}
                {new Date(run.preparedAt).toLocaleString('en-GB', { timeZone: 'Asia/Karachi' })}.
                HR: {run.hrNotes ?? 'Not reviewed'}. Finance: {run.financeNotes ?? 'Not reviewed'}.
                Management: {run.managementNotes ?? 'Not decided'}.
              </p>
              <details>
                <summary>Revision comparison and explanations</summary>
                <ComparisonEvidence report={run.report} />
                <ul>
                  {run.variances.map((row) => (
                    <li key={`${row.employeeCode}:${row.component}`}>
                      {row.employeeCode} · {row.component}:{' '}
                      {causes[row.rootCause - 1] ?? 'Unclassified'} —{' '}
                      {row.justification || 'No explanation retained'}
                    </li>
                  ))}
                </ul>
              </details>
            </li>
          ))}
        </ol>
      </FinanceQueryState>
    </>
  );
}

function RunEvidence({
  run,
  busy,
  onSaved,
  onDirty,
}: {
  run: ParallelRun;
  busy: boolean;
  onSaved(): void;
  onDirty(value: boolean): void;
}) {
  const access = useFinanceAccess();
  const [notes, setNotes] = useState('');
  const [decision, setDecision] = useState('');
  const [accountsVerified, setAccountsVerified] = useState(false);
  const [accrualVerified, setAccrualVerified] = useState(false);
  const [edits, setEdits] = useState(
    run.variances.map((variance) => ({
      rootCause: variance.rootCause,
      justification: variance.justification,
    })),
  );
  const [dirty, setLocalDirty] = useState(false);
  function setDirty(value: boolean) {
    setLocalDirty(value);
    onDirty(value);
  }
  useDirtyForm(dirty);
  const stage =
    run.state === 1 ? 'hr' : run.state === 2 ? 'finance' : run.state === 3 ? 'management' : null;
  const currentEmployee = access.employeeId;
  const independent = Boolean(
    currentEmployee &&
    access.user?.account &&
    ![
      run.preparedByUserId,
      run.hrReviewerUserId,
      run.financeReviewerUserId,
      run.managementApproverUserId,
    ].includes(access.user.account.accountId) &&
    ![
      run.preparedByEmployeeId,
      run.hrReviewerEmployeeId,
      run.financeReviewerEmployeeId,
      run.managementApproverEmployeeId,
    ].includes(currentEmployee),
  );
  const canDecide = Boolean(stage && access.can(permissions[stage]) && independent);
  function prepareDecision() {
    const approved = z.enum(['approve', 'reject']).parse(decision) === 'approve';
    const reason = z.string().trim().min(1).max(1000).parse(notes);
    const common = { parallelRunId: run.id, approved, expectedVersion: run.version };
    if (!stage || !canDecide) throw new Error('Decision unavailable.');
    if (stage === 'finance' && approved && (!accountsVerified || !accrualVerified))
      throw new Error('Review accounting evidence.');
    return {
      path: `${root}/parallel/${stage === 'management' ? 'management-approval' : `${stage}-review`}`,
      body:
        stage === 'hr'
          ? {
              ...common,
              explanations: approved ? reviewExplanations(run, edits) : run.variances,
              justification: reason,
            }
          : stage === 'finance'
            ? {
                ...common,
                generalLedgerAccountsVerified: accountsVerified,
                accrualBalanceMatches: accrualVerified,
                ledgerReconciliationNotes: reason,
              }
            : { ...common, executiveDecisionNotes: reason },
      summary: `${approved ? 'Approve' : 'Reject'} ${stage} review for ${run.year}-${String(run.month).padStart(2, '0')}, retained version ${run.version}. ${reason}`,
    };
  }
  return (
    <section>
      <h2>
        {run.year}-{String(run.month).padStart(2, '0')} · {states[run.state]}
      </h2>
      <p>
        Prepared by <EmployeeReference employeeId={run.preparedByEmployeeId} />.
        {run.hrReviewerEmployeeId && (
          <>
            {' '}
            HR reviewer: <EmployeeReference employeeId={run.hrReviewerEmployeeId} />.
          </>
        )}
        {run.financeReviewerEmployeeId && (
          <>
            {' '}
            Finance reviewer: <EmployeeReference employeeId={run.financeReviewerEmployeeId} />.
          </>
        )}
        {run.managementApproverEmployeeId && (
          <>
            {' '}
            Management reviewer: <EmployeeReference employeeId={run.managementApproverEmployeeId} />
            .
          </>
        )}
      </p>
      <p>
        Version {run.version}. ERP gross PKR {amount(run.summary.erpTotalGross)}; sheet gross PKR{' '}
        {amount(run.summary.legacyTotalGross)}. Net variance PKR {amount(run.summary.netVariance)}.{' '}
        {run.summary.discrepancyCount} discrepancies.
      </p>
      <details>
        <summary>Retained source references</summary>
        <dl>
          <dt>Batch</dt>
          <dd>{run.erpBatchId}</dd>
          <dt>Payroll source fingerprint</dt>
          <dd>{run.sourceFingerprint}</dd>
          <dt>Sheet SHA-256</dt>
          <dd>{run.sourceSha256}</dd>
          <dt>Finance verification fingerprint</dt>
          <dd>{run.financeFingerprint ?? 'Not verified'}</dd>
          <dt>Approval fingerprint</dt>
          <dd>{run.approvalFingerprint ?? 'Not approved'}</dd>
        </dl>
      </details>
      <ComparisonEvidence report={run.report} />
      {stage && !independent && (
        <FinanceNotice>
          An independent reviewer must make the next decision. A preparer or previous reviewer
          cannot review this stage.
        </FinanceNotice>
      )}
      {canDecide && (
        <ReviewedFinanceCommand
          label={`${stage} decision`}
          schema={parallelRunFor(run.id)}
          prepare={prepareDecision}
          disabled={busy}
          onSaved={() => {
            setDirty(false);
            onSaved();
          }}
        >
          {() => (
            <>
              <h3>
                {stage === 'hr'
                  ? 'Explain each retained component variance'
                  : stage === 'finance'
                    ? 'Review salary accrual and account mappings'
                    : 'Management decision'}
              </h3>
              {stage === 'hr' &&
                run.variances.map((row, index) => (
                  <fieldset key={`${row.employeeCode}:${row.component}`}>
                    <legend>
                      {row.employeeCode} · {row.component}
                    </legend>
                    <p>
                      ERP PKR {amount(row.erpAmount)}; sheet PKR {amount(row.legacyAmount)};
                      variance PKR {amount(row.variance)}.
                    </p>
                    <PayrollSelect
                      label="Root cause"
                      value={edits[index]!.rootCause === 9 ? '' : String(edits[index]!.rootCause)}
                      required={decision !== 'reject'}
                      onChange={(value) => {
                        setDirty(true);
                        setEdits(
                          edits.map((edit, i) =>
                            i === index ? { ...edit, rootCause: Number(value) } : edit,
                          ),
                        );
                      }}
                      options={causes.map((label, i) => ({ value: String(i + 1), label }))}
                    />
                    <TextAreaField
                      label="Evidence and justification"
                      value={edits[index]!.justification}
                      onChange={(value) => {
                        setDirty(true);
                        setEdits(
                          edits.map((edit, i) =>
                            i === index ? { ...edit, justification: value } : edit,
                          ),
                        );
                      }}
                    />
                  </fieldset>
                ))}
              {stage === 'finance' && (
                <>
                  <p>
                    The server independently verifies retained salary accruals and account mappings.
                    These acknowledgements cannot substitute for missing accounting evidence.
                  </p>
                  <label>
                    <input
                      type="checkbox"
                      checked={accountsVerified}
                      onChange={(event) => {
                        setDirty(true);
                        setAccountsVerified(event.target.checked);
                      }}
                    />
                    I reviewed the general ledger account mappings.
                  </label>
                  <label>
                    <input
                      type="checkbox"
                      checked={accrualVerified}
                      onChange={(event) => {
                        setDirty(true);
                        setAccrualVerified(event.target.checked);
                      }}
                    />
                    I reviewed the payroll accrual balance.
                  </label>
                </>
              )}
              <PayrollSelect
                label="Decision"
                value={decision}
                onChange={(value) => {
                  setDirty(true);
                  setDecision(value);
                }}
                options={[
                  { value: 'approve', label: 'Approve this review stage' },
                  { value: 'reject', label: 'Reject this run' },
                ]}
              />
              <TextAreaField
                label="Decision notes and evidence"
                required
                value={notes}
                onChange={(value) => {
                  setDirty(true);
                  setNotes(value);
                }}
              />
            </>
          )}
        </ReviewedFinanceCommand>
      )}
      {!stage && (
        <FinanceNotice>
          This run is {states[run.state]?.toLowerCase()}. Retained evidence is read-only. No payment
          instruction is created by this review.
        </FinanceNotice>
      )}
    </section>
  );
}

function ComparisonEvidence({ report }: { report: ComparisonReport }) {
  const components = [
    ['Basic salary', 'sheetBasicSalary', 'erpBasicSalary', 'basicVariance'],
    ['Allowances', 'sheetAllowances', 'erpAllowances', 'allowanceVariance'],
    ['Overtime', 'sheetOvertime', 'erpOvertime', 'overtimeVariance'],
    ['Gross', 'sheetGrossSalary', 'erpGrossSalary', 'grossVariance'],
    ['Deductions', 'sheetDeductions', 'erpDeductions', 'deductionVariance'],
    ['Statutory', 'sheetStatutory', 'erpStatutory', 'statutoryVariance'],
    ['EOBI employee contribution', 'sheetEobi', 'erpEobi', null],
    ['SESSI/PESSI employee contribution', 'sheetSessi', 'erpSessi', null],
    ['Income tax', 'sheetIncomeTax', 'erpIncomeTax', null],
    ['Net salary', 'sheetNetSalary', 'erpNetSalary', 'netVariance'],
  ] as const;
  return (
    <section aria-label="Saved payroll comparison">
      <h3>Comparison: {report.sourceFileName}</h3>
      <p>
        {report.summary.exactMatchCount} exact matches; {report.summary.varianceCount} employees
        with variances; {report.summary.missingInErpCount} missing in ERP;{' '}
        {report.summary.missingInSheetCount} missing in sheet;{' '}
        {report.summary.duplicateInSheetCount} duplicates.
      </p>
      <p>Reason: {report.auditReason}</p>
      {report.variances.map((row, index) => (
        <details key={`${row.employeeCode}:${index}`}>
          <summary>
            {row.employeeCode} · {row.employeeName} · Net variance PKR {amount(row.netVariance)}
          </summary>
          <p>{row.details}</p>
          <div className="payroll-table-wrapper">
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Component</th>
                  <th>Sheet (PKR)</th>
                  <th>ERP (PKR)</th>
                  <th>Variance (PKR)</th>
                </tr>
              </thead>
              <tbody>
                {components.map(([label, sheet, erp, variance]) => (
                  <tr key={label}>
                    <th>{label}</th>
                    <td>{amount(row[sheet])}</td>
                    <td>{amount(row[erp])}</td>
                    <td>{variance ? amount(row[variance]) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      ))}
    </section>
  );
}
