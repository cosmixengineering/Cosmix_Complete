import { useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Command, Denied, PrivateWorkspace, QueryState } from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { canScope, canSelf, queueScopes, type Scope } from './access';
import { cursorPage, exceptionDetail, exceptionItem, exceptionNotice, pageOf } from './contracts';
import { MissingCheckoutReview } from './MissingCheckoutReview';
import {
  CursorControls,
  NumberedControls,
  RangeFilter,
  ReadResult,
  RecordFacts,
  ScopeSelect,
  type Range,
} from './ReviewUi';

export function AttendanceExceptionsPage() {
  return <ExceptionsPage />;
}
export function MyAttendanceExceptionsPage() {
  return <ExceptionsPage self />;
}
function ExceptionsPage({ self = false }: { self?: boolean }) {
  return (
    <PrivateWorkspace resource={`exceptions:${self}`}>
      <ExceptionsSelection self={self} />
    </PrivateWorkspace>
  );
}
function ExceptionsSelection({ self }: { self: boolean }) {
  const [scope, setScope] = useState<Scope>();
  const [range, setRange] = useState<Range>();
  return (
    <>
      <h1>{self ? 'My exception notices' : 'Attendance exceptions'}</h1>
      {!self && (
        <ScopeSelect
          permission="attendance.exceptions.read"
          value={scope}
          onChange={(value) => {
            setScope(value);
            setRange(undefined);
          }}
        />
      )}
      <RangeFilter
        onChange={setRange}
        exceptionKinds={
          self
            ? undefined
            : [
                'Missing check-out',
                'Overlapping shifts',
                'Impossible travel',
                'Excessive duration',
                'Punch sequence error',
                'Outside site boundary',
                'Unreliable location',
                'Device risk',
                'Face verification',
                'Suspected false evidence',
              ].map((label, i) => ({ value: String(i + 1), label }))
        }
      />
      <p>Review attendance evidence and resolve changes through the correction approval process.</p>
      {!self && scope && range && (!range.kind || range.kind === 1) && (
        <MissingCheckoutReview
          key={`missing:${JSON.stringify(scope)}:${JSON.stringify(range)}`}
          scope={scope}
          range={range}
        />
      )}
      {range && (self || scope) && (
        <ExceptionsHistory
          key={`${JSON.stringify(scope)}:${JSON.stringify(range)}`}
          range={range}
          scope={scope}
          self={self}
        />
      )}
    </>
  );
}
function ExceptionsHistory({ self, scope, range }: { self: boolean; scope?: Scope; range: Range }) {
  const access = useAttendanceAccess();
  const [page, setPage] = useState(1);
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const [selected, setSelected] = useState<{ id: string; employeeId?: string }>();
  const cursor = cursors[cursors.length - 1];
  const allowed = self
    ? canSelf(access.user, 'attendance.exceptions.self')
    : Boolean(scope && canScope(access.user, 'attendance.exceptions.read', scope));
  const path = self ? '/me/attendance/exceptions' : '/attendance/exceptions';
  const schema = self ? pageOf(exceptionNotice) : cursorPage(exceptionItem);
  const query = useAttendanceQuery(
    path +
      queryString({
        from: range.from,
        to: range.to,
        pageSize: range.pageSize,
        ...(self
          ? { page }
          : {
              scope: scope ? queueScopes[scope.kind] : undefined,
              targetId: scope?.targetId ?? undefined,
              kind: range.kind,
              cursor,
            }),
      }),
    schema,
    allowed,
  );
  if (!allowed) return <Denied>Matching exception read access is unavailable.</Denied>;
  const data = !query.isError ? query.data : undefined;
  return (
    <section className="attendance-section">
      <QueryState query={query} empty={data?.items.length === 0} />
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={query.isFetching}
        onClick={() => {
          setSelected(undefined);
          void query.refetch();
        }}
      >
        Reload detection history
      </Command>
      {data && (
        <>
          <div style={{ overflowX: 'auto' }}>
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Business date</th>
                  <th>Employee / notice</th>
                  <th>Detected at (UTC offset)</th>
                  <th>Evidence</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((row) => (
                  <tr key={row.id}>
                    <td>{row.businessDate}</td>
                    <td>
                      {'employeeId' in row ? row.employeeId : 'My notice'}
                      <br />
                      {'kind' in row ? row.kind : row.noticeCode}
                    </td>
                    <td>{row.detectedAtUtc}</td>
                    <td>
                      <button
                        type="button"
                        disabled={query.isFetching}
                        onClick={() =>
                          setSelected({
                            id: row.id,
                            employeeId: 'employeeId' in row ? row.employeeId : undefined,
                          })
                        }
                      >
                        Review {row.id}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {'totalCount' in data ? (
            <NumberedControls
              page={page}
              data={data}
              onChange={(p) => {
                setSelected(undefined);
                setPage(p);
              }}
              disabled={query.isFetching}
            />
          ) : (
            <CursorControls
              cursor={cursor}
              next={data.nextCursor}
              back={cursors.length > 1}
              disabled={query.isFetching}
              onBack={() => {
                setSelected(undefined);
                setCursors((old) => old.slice(0, -1));
              }}
              onNext={(value) => {
                setSelected(undefined);
                setCursors((old) => [...old, value]);
              }}
            />
          )}
        </>
      )}
      {selected && (
        <ReadResult
          key={selected.id}
          path={
            self
              ? `/me/attendance/exceptions/${selected.id}`
              : `/attendance/employees/${selected.employeeId}/exceptions/${selected.id}`
          }
          schema={self ? exceptionNotice : exceptionDetail}
          allowed={allowed}
        >
          {(value) => <RecordFacts value={value} />}
        </ReadResult>
      )}
    </section>
  );
}
