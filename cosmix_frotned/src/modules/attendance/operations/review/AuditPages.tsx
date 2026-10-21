import { useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { SelectInput } from '@/components/forms/SelectInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Command, Denied, QueryState } from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { canEmployee, canSelf, type Scope } from './access';
import { cursorPage, pageOf, punch, punchAudit, session, sessionAudit } from './contracts';
import {
  CursorControls,
  EmployeeWorkspace,
  NumberedControls,
  RangeFilter,
  SourceFacts,
  type Range,
} from './ReviewUi';

export function AttendanceAuditPage() {
  return <AuditPage />;
}
export function MyAttendanceAuditPage() {
  return <AuditPage self />;
}
function AuditPage({ self = false }: { self?: boolean }) {
  return (
    <EmployeeWorkspace
      title={self ? 'My attendance evidence' : 'Punch and session audit'}
      permission="attendance.punches.read"
      self={self}
    >
      {(employeeId, scope) => <AuditSelection self={self} employeeId={employeeId} scope={scope} />}
    </EmployeeWorkspace>
  );
}
function AuditSelection({
  self,
  employeeId,
  scope,
}: {
  self: boolean;
  employeeId: string;
  scope?: Scope;
}) {
  const [range, setRange] = useState<Range>();
  const [kind, setKind] = useState('punches');
  return (
    <>
      <SelectInput
        label="Evidence stream"
        value={kind}
        options={[
          { value: 'punches', label: 'Raw punch receipts and assessments' },
          {
            value: 'sessions',
            label: self ? 'Latest factual sessions' : 'All immutable session revisions',
          },
        ]}
        onChange={(e) => setKind(e.target.value)}
      />
      <RangeFilter onChange={setRange} />
      <p>
        Server receipt times and recorded business dates are authoritative. Device capture times are
        untrusted. Session revisions are interpretations of the same duty; elapsed minutes are not
        payable minutes. Timestamps below retain their explicit UTC offset.
      </p>
      {range && (
        <AuditHistory
          key={`${kind}:${JSON.stringify(range)}`}
          range={range}
          kind={kind}
          self={self}
          employeeId={employeeId}
          scope={scope}
        />
      )}
    </>
  );
}
function AuditHistory({
  range,
  kind,
  self,
  employeeId,
  scope,
}: {
  range: Range;
  kind: string;
  self: boolean;
  employeeId: string;
  scope?: Scope;
}) {
  const access = useAttendanceAccess();
  const [page, setPage] = useState(1);
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const cursor = cursors[cursors.length - 1];
  const allowed = self
    ? canSelf(access.user, 'attendance.history.self')
    : canEmployee(access.user, 'attendance.punches.read', employeeId, scope);
  const base = self ? '/me/attendance' : `/attendance/employees/${employeeId}`;
  const schema = self
    ? pageOf(kind === 'punches' ? punch : session)
    : cursorPage(kind === 'punches' ? punchAudit : sessionAudit);
  const query = useAttendanceQuery(
    base +
      '/' +
      kind +
      queryString({
        from: range.from,
        to: range.to,
        pageSize: range.pageSize,
        ...(self ? { page } : { cursor }),
      }),
    schema,
    allowed,
  );
  if (!allowed) return <Denied>Matching audit/history scope is required.</Denied>;
  const data = !query.isError ? query.data : undefined;
  return (
    <section className="attendance-section">
      <QueryState query={query} empty={data?.items.length === 0} />
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={query.isFetching}
        onClick={() => {
          setPage(1);
          setCursors([undefined]);
          void query.refetch();
        }}
      >
        Reload evidence
      </Command>
      {data && (
        <>
          {data.items.map((row) => {
            const record = 'punch' in row ? row.punch : 'session' in row ? row.session : row;
            return (
              <SourceFacts
                key={record.id}
                title={`${record.businessDate} / ${'action' in record ? record.action + ': ' + record.outcome : 'session revision ' + record.revision}`}
                value={row}
              />
            );
          })}
          {'totalCount' in data ? (
            <NumberedControls
              page={page}
              data={data}
              onChange={setPage}
              disabled={query.isFetching}
            />
          ) : (
            <CursorControls
              cursor={cursor}
              next={data.nextCursor}
              back={cursors.length > 1}
              disabled={query.isFetching}
              onBack={() => setCursors((old) => old.slice(0, -1))}
              onNext={(value) => setCursors((old) => [...old, value])}
            />
          )}
        </>
      )}
    </section>
  );
}
