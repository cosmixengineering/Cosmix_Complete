import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { Filter, RefreshCw, ShieldAlert, SearchX, Eye, Users } from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Denied } from '../../components/SchedulingUi';
import { TableSkeleton } from '@/components/ui/Skeleton';
import { Dialog } from '@/components/ui/Dialog';
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
import '@/styles/attendance.css';
import '@/styles/attendance-register.css';

export function AttendanceExceptionsPage() {
  return <ExceptionsPage />;
}
export function MyAttendanceExceptionsPage() {
  return <ExceptionsPage self />;
}
function ExceptionsPage({ self = false }: { self?: boolean }) {
  return <ExceptionsSelection self={self} />;
}
function ExceptionsSelection({ self }: { self: boolean }) {
  const [scope, setScope] = useState<Scope>();
  const [range, setRange] = useState<Range>();
  return (
    <div className="att-exceptions-workspace">
      <div className="att-exceptions-filter-card">
        <div className="att-filter-card-header">
          <div className="att-filter-card-title">
            <Filter size={15} />
            <span>
              {self ? 'My Exception Notices & Scope' : 'Attendance Exceptions & Scope Parameters'}
            </span>
          </div>
          <span className="att-filter-card-badge">Date Window: max 366 days</span>
        </div>

        {!self && (
          <div className="att-filter-scope-row">
            <ScopeSelect
              permission="attendance.exceptions.read"
              value={scope}
              onChange={(value) => {
                setScope(value);
                setRange(undefined);
              }}
            />
          </div>
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

        <div className="att-filter-hint">
          Review attendance evidence, geofence breaches, and resolve pending checkouts through the
          correction approval process.
        </div>
      </div>

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
    </div>
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
    <div className="att-exceptions-panel">
      <div className="att-exceptions-panel-header">
        <div className="att-panel-header-left">
          <div className="att-exceptions-icon-box blue" aria-hidden="true">
            <ShieldAlert size={18} />
          </div>
          <div className="att-panel-titles">
            <h3 className="att-panel-title">Exception Detection History</h3>
            <p className="att-panel-subtitle">
              Audit trail of automated biometric, GPS, duration, and sequence exception logs.
            </p>
          </div>
        </div>

        <div className="att-panel-header-actions">
          {data && (
            <span className={`att-count-pill ${data.items.length > 0 ? 'has-items' : ''}`}>
              {data.items.length} {data.items.length === 1 ? 'event' : 'events'}
            </span>
          )}
          <button
            type="button"
            className="att-card-action-btn"
            disabled={query.isFetching}
            onClick={() => {
              setSelected(undefined);
              void query.refetch();
            }}
            title="Reload detection history"
          >
            <RefreshCw size={13} className={query.isFetching ? 'spin' : ''} />
            <span>{query.isFetching ? 'Reloading...' : 'Reload history'}</span>
          </button>
        </div>
      </div>

      {query.isError && (
        <div style={{ padding: '16px 20px', color: '#b91c1c', background: '#fee2e2' }}>
          Could not load exception history records. Please try reloading.
        </div>
      )}

      {query.isPending && (
        <div style={{ padding: '16px' }}>
          <TableSkeleton rows={4} />
        </div>
      )}

      {data && (
        <>
          <div className="att-card-table-wrapper">
            <table className="att-card-table">
              <thead>
                <tr>
                  <th>BUSINESS DATE</th>
                  <th>EMPLOYEE / NOTICE</th>
                  <th>DETECTED AT (UTC)</th>
                  <th style={{ textAlign: 'right' }}>EVIDENCE & AUDIT</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 ? (
                  <tr>
                    <td colSpan={4}>
                      <div className="att-empty-state">
                        <div className="att-empty-icon-box slate">
                          <SearchX size={24} />
                        </div>
                        <div className="att-empty-title">No Exceptions Detected</div>
                        <div className="att-empty-desc">
                          No anomalous events or geofence breaches found for the selected scope and
                          date range.
                        </div>
                      </div>
                    </td>
                  </tr>
                ) : (
                  data.items.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <span className="att-date-chip">{row.businessDate}</span>
                      </td>
                      <td>
                        <div className="att-emp-notice-cell">
                          <span className="att-emp-chip">
                            <Users size={12} style={{ marginRight: 4 }} />
                            {'employeeId' in row ? (
                              <EmployeeReference employeeId={row.employeeId} />
                            ) : (
                              'My notice'
                            )}
                          </span>
                          <span className="att-kind-badge">
                            {'kind' in row ? row.kind : row.noticeCode}
                          </span>
                        </div>
                      </td>
                      <td className="att-time-cell">{row.detectedAtUtc}</td>
                      <td style={{ textAlign: 'right' }}>
                        <button
                          type="button"
                          className="att-action-pill-btn"
                          disabled={query.isFetching}
                          onClick={() =>
                            setSelected({
                              id: row.id,
                              employeeId: 'employeeId' in row ? row.employeeId : undefined,
                            })
                          }
                          title={`Review evidence #${row.id}`}
                        >
                          <Eye size={12} />
                          <span>View evidence</span>
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="att-card-footer">
            <span className="att-card-footer-note">
              Audit data signed and captured by the Cosmix attendance engine.
            </span>
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
          </div>
        </>
      )}

      <Dialog
        open={Boolean(selected)}
        onOpenChange={(open) => !open && setSelected(undefined)}
        title="Exception Evidence & Audit Trail"
        description={selected ? `Audit detection event #${selected.id}` : ''}
        className="att-exception-dialog"
      >
        {selected && (
          <div className="att-evidence-modal-body">
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
              {(value) => (
                <div className="att-evidence-card">
                  <RecordFacts value={value} />
                </div>
              )}
            </ReadResult>
          </div>
        )}
      </Dialog>
    </div>
  );
}
