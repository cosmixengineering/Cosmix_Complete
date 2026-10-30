import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { RefreshCw, AlertCircle, CheckCircle2, ArrowRight, Clock, Users } from 'lucide-react';
import { z } from 'zod';
import { queryString } from '../../api/attendance-api';
import { TableSkeleton } from '@/components/ui/Skeleton';
import { Denied } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { date, id, timestamp } from '../../schemas/common';
import { canScope, queueScopes, type Scope } from './access';
import { cursorPage } from './contracts';
import { CursorControls, type Range } from './ReviewUi';
import '@/styles/attendance.css';
import '@/styles/attendance-register.css';

const reviewPage = cursorPage(
  z.object({
    employeeId: id,
    businessDate: date,
    sessionRevisionId: id,
    sessionKey: id,
    checkInAtUtc: timestamp,
    scheduledEndAtUtc: timestamp.nullable(),
    state: z.enum(['missing_checkout_hr_review_required', 'schedule_review_required']),
    nextAction: z.literal('submit_or_review_correction'),
  }),
);

export function MissingCheckoutReview({ scope, range }: { scope: Scope; range: Range }) {
  const access = useAttendanceAccess();
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const cursor = cursors[cursors.length - 1];
  const allowed = canScope(access.user, 'attendance.exceptions.read', scope);
  const query = useAttendanceQuery(
    '/attendance/exceptions/missing-checkouts' +
      queryString({
        from: range.from,
        to: range.to,
        pageSize: range.pageSize,
        scope: queueScopes[scope.kind],
        targetId: scope.targetId ?? undefined,
        cursor,
      }),
    reviewPage,
    allowed,
  );
  if (!allowed) return <Denied>Matching attendance review access is unavailable.</Denied>;
  const data = query.isError ? undefined : query.data;

  return (
    <div className="att-exceptions-panel" aria-labelledby="missing-checkout-heading">
      <div className="att-exceptions-panel-header">
        <div className="att-panel-header-left">
          <div className="att-exceptions-icon-box amber" aria-hidden="true">
            <AlertCircle size={18} />
          </div>
          <div className="att-panel-titles">
            <h2 id="missing-checkout-heading" className="att-panel-title">
              Missing Check-outs (HR Review Queue)
            </h2>
            <p className="att-panel-subtitle">
              Open sessions needing a checkout correction or schedule review before payroll lock.
            </p>
          </div>
        </div>

        <div className="att-panel-header-actions">
          {data && (
            <span className={`att-count-pill ${data.items.length > 0 ? 'has-items' : ''}`}>
              {data.items.length} {data.items.length === 1 ? 'case' : 'cases'}
            </span>
          )}
          <button
            type="button"
            className="att-card-action-btn"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
            title="Refresh review queue"
          >
            <RefreshCw size={13} className={query.isFetching ? 'spin' : ''} />
            <span>{query.isFetching ? 'Refreshing...' : 'Refresh'}</span>
          </button>
        </div>
      </div>

      {query.isError && (
        <div style={{ padding: '16px 20px', color: '#b91c1c', background: '#fee2e2' }}>
          Could not load missing checkout records. Please try refreshing.
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
                  <th>EMPLOYEE</th>
                  <th>CHECK-IN TIME</th>
                  <th>SCHEDULED END</th>
                  <th>STATUS</th>
                  <th style={{ textAlign: 'right' }}>ACTION</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      <div className="att-empty-state">
                        <div className="att-empty-icon-box green">
                          <CheckCircle2 size={24} />
                        </div>
                        <div className="att-empty-title">No Missing Check-outs</div>
                        <div className="att-empty-desc">
                          All employee sessions in the selected range have verified checkouts or
                          schedules. No salary deduction is held.
                        </div>
                      </div>
                    </td>
                  </tr>
                ) : (
                  data.items.map((row) => (
                    <tr key={row.sessionKey}>
                      <td>
                        <span className="att-date-chip">{row.businessDate}</span>
                      </td>
                      <td>
                        <span className="att-emp-chip">
                          <Users size={12} style={{ marginRight: 4 }} />
                          <EmployeeReference employeeId={row.employeeId} />
                        </span>
                      </td>
                      <td className="att-time-cell">
                        {new Date(row.checkInAtUtc).toLocaleString(undefined, {
                          dateStyle: 'short',
                          timeStyle: 'short',
                        })}
                      </td>
                      <td className="att-time-cell">
                        {row.scheduledEndAtUtc ? (
                          new Date(row.scheduledEndAtUtc).toLocaleString(undefined, {
                            dateStyle: 'short',
                            timeStyle: 'short',
                          })
                        ) : (
                          <span className="att-warn-text">Schedule review required</span>
                        )}
                      </td>
                      <td>
                        <span
                          className={`att-status-pill ${
                            row.state === 'schedule_review_required' ? 'amber' : 'rose'
                          }`}
                        >
                          {row.state === 'schedule_review_required'
                            ? 'Schedule review'
                            : 'Checkout missing'}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <Link
                          className="att-action-link-btn"
                          to={`/hr/attendance/employees/${row.employeeId}/corrections`}
                        >
                          <span>Open correction</span>
                          <ArrowRight size={12} />
                        </Link>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="att-card-footer">
            <span className="att-card-footer-note">
              <Clock size={12} />
              Times are shown in your device timezone. Business dates follow assigned shifts.
            </span>
            <CursorControls
              cursor={cursor}
              next={data.nextCursor}
              back={cursors.length > 1}
              disabled={query.isFetching}
              onBack={() => setCursors((old) => old.slice(0, -1))}
              onNext={(value) => setCursors((old) => [...old, value])}
            />
          </div>
        </>
      )}
    </div>
  );
}
