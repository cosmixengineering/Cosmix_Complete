import { useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Command, Denied, QueryState } from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { canEmployee, canSelf, type Scope } from './access';
import { carryMonth } from './contracts';
import { EmployeeWorkspace, NumberedControls, SourceFacts } from './ReviewUi';
import { monthRange, today } from './helpers';

export function AttendanceCarryForwardMonthPage() {
  return <CarryMonthPage />;
}
export function MyAttendanceCarryForwardMonthPage() {
  return <CarryMonthPage self />;
}
function CarryMonthPage({ self = false }: { self?: boolean }) {
  return (
    <EmployeeWorkspace
      title={`${self ? 'My ' : ''}posted factual carry-forward`}
      permission="attendance.corrections.review"
      self={self}
    >
      {(employeeId, scope) => <MonthSelection self={self} employeeId={employeeId} scope={scope} />}
    </EmployeeWorkspace>
  );
}
function MonthSelection({
  self,
  employeeId,
  scope,
}: {
  self: boolean;
  employeeId: string;
  scope?: Scope;
}) {
  const [month, setMonth] = useState(today().slice(0, 7));
  return (
    <>
      <TextInput
        label="Target posting month"
        type="month"
        value={month}
        onChange={(e) => setMonth(e.target.value)}
      />
      {monthRange(month) ? (
        <MonthLedger key={month} month={month} self={self} employeeId={employeeId} scope={scope} />
      ) : (
        <p role="alert">Select a valid month.</p>
      )}
    </>
  );
}
function MonthLedger({
  month,
  self,
  employeeId,
  scope,
}: {
  month: string;
  self: boolean;
  employeeId: string;
  scope?: Scope;
}) {
  const access = useAttendanceAccess();
  const [page, setPage] = useState(1);
  const allowed = self
    ? canSelf(access.user, 'attendance.corrections.self')
    : canEmployee(access.user, 'attendance.corrections.review', employeeId, scope);
  const base = self
    ? '/me/attendance/carry-forward'
    : `/hr/employees/${employeeId}/attendance-carry-forward`;
  const query = useAttendanceQuery(
    base + '/months' + queryString({ month: `${month}-01`, page, pageSize: 25 }),
    carryMonth,
    allowed,
  );
  if (!allowed) return <Denied>Matching carry-forward ledger access is required.</Denied>;
  const data = !query.isError ? query.data : undefined;
  return (
    <section className="attendance-section">
      <QueryState query={query} empty={data?.entries.length === 0} />
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={query.isFetching}
        onClick={() => void query.refetch()}
      >
        Reload posted entries
      </Command>
      {data && (
        <>
          <dl className="attendance-record">
            <dt>Server total delta worked minutes</dt>
            <dd>{data.totalDeltaWorkedMinutes}</dd>
            <dt>Entries</dt>
            <dd>{data.totalCount}</dd>
            <dt>Financial state</dt>
            <dd>{data.financialState}</dd>
            <dt>Interpretation</dt>
            <dd>{data.interpretation}</dd>
          </dl>
          <p>
            Posted deltas are separate from target-month worked duty. Locked source attendance is
            not reopened; payable amounts are unavailable in this ledger.
          </p>
          {data.entries.map((row) => (
            <SourceFacts
              key={row.id}
              title={`${row.sourceBusinessDate} / revision ${row.revision} / ${row.deltaWorkedMinutes} factual minute delta`}
              value={row}
            />
          ))}
          <NumberedControls
            page={page}
            data={data}
            onChange={setPage}
            disabled={query.isFetching}
          />
        </>
      )}
    </section>
  );
}
