import { useState } from 'react';
import { z } from 'zod';
import { Eye, Filter, RefreshCw, Save } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { id, reason } from '../../schemas/common';
import { businessToday } from '../../utils/messages';
import { hasReportGrant } from './access';
import {
  attestationHistoryFor,
  payrollDetailFor,
  payrollHistoryFor,
  payrollPreview,
  payrollPreviewFor,
} from './payroll-schemas';

function SaveInputs({
  preview,
  path,
  onSaved,
  onCancel,
}: {
  preview: z.infer<typeof payrollPreview>;
  path: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(preview);
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title="Prepare immutable payroll inputs"
      submitLabel="Prepare revision"
      onSaved={onSaved}
      onCancel={onCancel}
      disabled={!source.canSave}
      build={() => {
        if (!source.canSave || source.isLocked || source.state !== 'ready')
          throw new Error('Current server sources are not saveable.');
        return {
          path: path + '/revisions',
          method: 'POST',
          schema: payrollDetailFor(source.employeeId, source.monthStart.slice(0, 7)),
          body: {
            expectedPreviewFingerprint: source.previewFingerprint,
            expectedRevision: source.expectedRevision,
            reason: reason().parse(inputReason),
          },
        };
      }}
    >
      <dl className="attendance-record">
        <dt>Employee</dt>
        <dd>{source.employeeId}</dd>
        <dt>Month</dt>
        <dd>{source.monthStart}</dd>
        <dt>Expected revision</dt>
        <dd>{source.expectedRevision}</dd>
        <dt>Candidate deduction total</dt>
        <dd>
          {source.deductionTotal ?? 'Unavailable'} {source.currency}
        </dd>
        <dt>Legal status</dt>
        <dd>{source.legalStatus}</dd>
      </dl>
      <TextInput
        label="Reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
function Inputs({ value }: { value: z.infer<typeof payrollPreview> }) {
  return (
    <>
      <dl className="attendance-record">
        <dt>State</dt>
        <dd>{value.state}</dd>
        <dt>Month</dt>
        <dd>
          {value.monthStart} to {value.monthEndExclusive} (exclusive)
        </dd>
        <dt>Candidate deduction total</dt>
        <dd>
          {value.deductionTotal ?? 'Unavailable'} {value.currency}
        </dd>
        <dt>Locked</dt>
        <dd>{value.isLocked ? 'Yes' : 'No'}</dd>
        <dt>Legal status</dt>
        <dd>{value.legalStatus}</dd>
        <dt>Payroll ready</dt>
        <dd>{value.payrollReady ? 'Yes' : 'No'}</dd>
        <dt>Payment instruction</dt>
        <dd>{value.paymentInstruction ? 'Yes' : 'No'}</dd>
      </dl>
      {value.issues.length > 0 && (
        <div className="hr-table-scroll">
          <table className="attendance-table">
            <thead>
              <tr>
                <th>Issue</th>
                <th>Date</th>
                <th>Source dates</th>
              </tr>
            </thead>
            <tbody>
              {value.issues.map((issue, index) => (
                <tr key={`${issue.code}:${issue.businessDate}:${index}`}>
                  <td>{issue.code}</td>
                  <td>{issue.businessDate ?? 'Period'}</td>
                  <td>{issue.sourceDates?.join(', ') ?? 'None retained'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="hr-table-scroll">
        <table className="attendance-table">
          <thead>
            <tr>
              <th>Line / type</th>
              <th>Source dates</th>
              <th>Candidate / applied amount</th>
              <th>Application</th>
              <th>Source evidence</th>
            </tr>
          </thead>
          <tbody>
            {value.lines.map((line) => (
              <tr key={line.key}>
                <td>
                  {line.key}
                  <br />
                  {line.kind}
                </td>
                <td>{line.sourceDates.join(', ')}</td>
                <td>
                  {line.candidateAmount} / {line.amount} {line.currency}
                </td>
                <td>{line.applicationCode}</td>
                <td>
                  <details>
                    <summary>Retained calculation</summary>
                    <dl className="attendance-record">
                      <dt>Salary basis</dt>
                      <dd>{line.salaryBasis}</dd>
                      <dt>Monthly source</dt>
                      <dd>{line.monthlySalary}</dd>
                      <dt>Day divisor</dt>
                      <dd>{line.salaryDivisorDays}</dd>
                      <dt>Deduction days</dt>
                      <dd>{line.deductionDays}</dd>
                      <dt>Exact fraction</dt>
                      <dd>
                        {line.exactAmountNumerator} / {line.exactAmountDenominator}
                      </dd>
                      <dt>Rounding</dt>
                      <dd>{line.rounding}</dd>
                      <dt>Compensation</dt>
                      <dd>{line.compensationRecordId}</dd>
                      <dt>Policies</dt>
                      <dd>{line.policyVersionIds.join(', ')}</dd>
                      <dt>Sources</dt>
                      <dd>{line.sourceIds.join(', ')}</dd>
                    </dl>
                  </details>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h3>Historical jurisdiction context</h3>
      <div className="hr-table-scroll">
        <table className="attendance-table">
          <thead>
            <tr>
              <th>Date</th>
              <th>Site</th>
              <th>Jurisdiction</th>
              <th>State</th>
            </tr>
          </thead>
          <tbody>
            {value.jurisdictions.map((row) => (
              <tr key={row.businessDate}>
                <td>{row.businessDate}</td>
                <td>{row.siteId ?? 'Unavailable'}</td>
                <td>{row.jurisdictionCode ?? 'Unconfigured'}</td>
                <td>{row.state}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
function Revision({
  path,
  revisionId,
  employee,
  period,
}: {
  path: string;
  revisionId: string;
  employee: string;
  period: string;
}) {
  const access = useAttendanceAccess();
  const query = useAttendanceQuery(
    path + '/revisions/' + revisionId,
    payrollDetailFor(employee, period, revisionId),
    hasReportGrant(access.user, 'attendance.payroll-inputs.read'),
  );
  const attestations = useAttendanceQuery(
    path + '/revisions/' + revisionId + '/attestations',
    attestationHistoryFor(employee, period, revisionId),
    hasReportGrant(access.user, 'attendance.payroll-inputs.read') &&
      hasReportGrant(access.user, 'attendance.payroll-attestations.read'),
  );
  return (
    <section className="attendance-section">
      <h3>Retained revision</h3>
      <QueryState query={query} />
      {query.data && !query.isError && (
        <>
          <dl className="attendance-record">
            <dt>Revision</dt>
            <dd>
              {query.data.revision.id} / {query.data.revision.revision}
            </dd>
            <dt>Created</dt>
            <dd>{query.data.revision.createdAtUtc}</dd>
            <dt>Actor</dt>
            <dd>{query.data.revision.actorUserId}</dd>
            <dt>Supersedes</dt>
            <dd>{query.data.revision.supersedesRevisionId ?? 'Initial revision'}</dd>
          </dl>
          <Inputs value={query.data.inputs} />
        </>
      )}
      {hasReportGrant(access.user, 'attendance.payroll-attestations.read') && (
        <>
          <h3>Accounts and Finance evidence</h3>
          <QueryState
            query={attestations}
            empty={attestations.data?.length === 0}
            emptyText="No attestation is retained for this source."
          />
          {attestations.data &&
            !attestations.isError &&
            attestations.data.map((row) => (
              <section className="attendance-section" key={row.id}>
                <dl className="attendance-record">
                  <dt>Status</dt>
                  <dd>{row.status}</dd>
                  <dt>Confirms posting</dt>
                  <dd>{row.confirmsPosting ? 'Manual authorized assertion' : 'No'}</dd>
                  <dt>Occurred</dt>
                  <dd>{row.occurredAtUtc}</dd>
                  <dt>Posted</dt>
                  <dd>{row.postedAtUtc ?? 'Not posted'}</dd>
                  <dt>External system/reference</dt>
                  <dd>
                    {row.externalSystem} / {row.externalReference}
                  </dd>
                  <dt>Evidence locator</dt>
                  <dd>{row.evidenceReference}</dd>
                  <dt>Statement</dt>
                  <dd>{row.statement}</dd>
                  <dt>Reason</dt>
                  <dd>{row.reason}</dd>
                </dl>
              </section>
            ))}
        </>
      )}
    </section>
  );
}
function PayrollRecord({
  employee,
  period,
  onEditing,
}: {
  employee: string;
  period: string;
  onEditing: (value: boolean) => void;
}) {
  const access = useAttendanceAccess();
  const path = `/attendance/employees/${employee}/payroll-inputs/${Number(period.slice(0, 4))}/${Number(period.slice(5))}`;
  const allowed = hasReportGrant(access.user, 'attendance.payroll-inputs.read');
  const query = useAttendanceQuery(path + '/preview', payrollPreviewFor(employee, period), allowed);
  const [page, setPage] = useState(1);
  const history = useAttendanceQuery(
    path + `/revisions?page=${page}&pageSize=20`,
    payrollHistoryFor(employee, period),
    allowed,
  );
  const [selected, setSelected] = useState('');
  const [editing, setEditing] = useState(false);
  const [notice, setNotice] = useState('');
  function close() {
    setEditing(false);
    onEditing(false);
    void query.refetch();
    void history.refetch();
  }
  return (
    <>
      <div className="attendance-heading">
        <h2>
          {employee} / {period}
        </h2>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={editing || query.isFetching || history.isFetching}
          onClick={() => {
            void query.refetch();
            void history.refetch();
          }}
        >
          Reload
        </Command>
      </div>
      <SavedNotice message={notice} />
      <QueryState query={query} />
      {query.data && !query.isError && (
        <>
          <Inputs value={query.data} />
          {!editing && hasReportGrant(access.user, 'attendance.payroll-inputs.calculate') && (
            <Command
              icon={Save}
              disabled={!query.data.canSave || query.isFetching}
              onClick={() => {
                setEditing(true);
                onEditing(true);
              }}
            >
              Prepare revision
            </Command>
          )}
          {editing && (
            <SaveInputs
              preview={query.data}
              path={path}
              onCancel={close}
              onSaved={() => {
                setNotice('Input revision retained for review.');
                close();
              }}
            />
          )}
        </>
      )}
      <section className="attendance-section">
        <h3>Revision history</h3>
        <QueryState
          query={history}
          empty={history.data?.items.length === 0}
          emptyText="No input revisions retained."
        />
        {history.data && !history.isError && (
          <>
            <div className="hr-table-scroll">
              <table className="attendance-table">
                <thead>
                  <tr>
                    <th>Revision</th>
                    <th>Created</th>
                    <th>State</th>
                    <th>Evidence</th>
                  </tr>
                </thead>
                <tbody>
                  {history.data.items.map((row) => (
                    <tr key={row.id}>
                      <td>{row.revision}</td>
                      <td>{row.createdAtUtc}</td>
                      <td>{row.state}</td>
                      <td>
                        <Command icon={Eye} disabled={editing} onClick={() => setSelected(row.id)}>
                          Open revision
                        </Command>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={page}
              pageSize={20}
              total={history.data.totalCount}
              onChange={setPage}
              disabled={history.isFetching || editing}
            />
          </>
        )}
      </section>
      {selected && (
        <Revision
          key={selected}
          path={path}
          revisionId={selected}
          employee={employee}
          period={period}
        />
      )}
    </>
  );
}
function PayrollContent() {
  const access = useAttendanceAccess();
  const [employee, setEmployee] = useState('');
  const [period, setPeriod] = useState(businessToday().slice(0, 7));
  const [selected, setSelected] = useState<{ employee: string; period: string } | null>(null);
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState('');
  if (!hasReportGrant(access.user, 'attendance.payroll-inputs.read')) return <Denied />;
  return (
    <>
      <h1>Attendance Payroll Inputs</h1>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (
            id.safeParse(employee).success &&
            employee !== '00000000-0000-0000-0000-000000000000' &&
            /^(19\d\d|[2-9]\d{3})-(0[1-9]|1[0-2])$/.test(period) &&
            Number(period.slice(0, 4)) <= 9998
          ) {
            setSelected({ employee, period });
            setError('');
          } else setError('Choose a valid employee and month.');
        }}
      >
        <fieldset disabled={editing}>
          <div className="attendance-grid">
            <TextInput
              label="Employee ID"
              value={employee}
              required
              onChange={(event) => setEmployee(event.target.value.trim())}
            />
            <TextInput
              label="Business month"
              type="month"
              value={period}
              required
              onChange={(event) => setPeriod(event.target.value)}
            />
          </div>
          <Command type="submit" icon={Filter}>
            Load inputs
          </Command>
        </fieldset>
      </form>
      {error && (
        <p className="attendance-error" role="alert">
          {error}
        </p>
      )}
      {selected && (
        <PayrollRecord
          key={selected.employee + selected.period}
          {...selected}
          onEditing={setEditing}
        />
      )}
    </>
  );
}
export function PayrollInputsPage() {
  return (
    <PrivateWorkspace resource="payroll-inputs">
      <PayrollContent />
    </PrivateWorkspace>
  );
}
