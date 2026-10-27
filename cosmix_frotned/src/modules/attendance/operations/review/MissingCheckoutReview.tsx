import { useState } from 'react';
import { Link } from 'react-router-dom';
import { RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { queryString } from '../../api/attendance-api';
import { Command, Denied, QueryState } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { date, id, timestamp } from '../../schemas/common';
import { canScope, queueScopes, type Scope } from './access';
import { cursorPage } from './contracts';
import { CursorControls, type Range } from './ReviewUi';

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
    <section className="attendance-section" aria-labelledby="missing-checkout-heading">
      <h2 id="missing-checkout-heading">Missing checkout · HR review</h2>
      <p>
        These open sessions need a checkout correction or schedule review. No automatic salary
        deduction is made while the case is unresolved.
      </p>
      <QueryState query={query} empty={data?.items.length === 0} />
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={query.isFetching}
        onClick={() => void query.refetch()}
      >
        Refresh review queue
      </Command>
      {data && (
        <>
          <div style={{ overflowX: 'auto' }}>
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Business date</th>
                  <th>Employee</th>
                  <th>Check-in time</th>
                  <th>Scheduled end</th>
                  <th>Review</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((row) => (
                  <tr key={row.sessionKey}>
                    <td>{row.businessDate}</td>
                    <td>{row.employeeId}</td>
                    <td>{new Date(row.checkInAtUtc).toLocaleString()}</td>
                    <td>
                      {row.scheduledEndAtUtc
                        ? new Date(row.scheduledEndAtUtc).toLocaleString()
                        : 'Schedule requires review'}
                    </td>
                    <td>
                      <span>
                        {row.state === 'schedule_review_required'
                          ? 'Review schedule and open session'
                          : 'Checkout missing'}
                      </span>
                      <br />
                      <Link to={`/hr/attendance/employees/${row.employeeId}/corrections`}>
                        Open corrections
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p>Times are shown in your device timezone. Business dates follow the assigned shift.</p>
          <CursorControls
            cursor={cursor}
            next={data.nextCursor}
            back={cursors.length > 1}
            disabled={query.isFetching}
            onBack={() => setCursors((old) => old.slice(0, -1))}
            onNext={(value) => setCursors((old) => [...old, value])}
          />
        </>
      )}
    </section>
  );
}
