import { useState } from 'react';
import { Link } from 'react-router-dom';
import { RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { Command, QueryState } from '../components/SchedulingUi';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { date, id, pageOf } from '../schemas/common';
import { EmployeeWorkspace } from '../operations/review/ReviewUi';
import { canEmployee, type Scope } from '../operations/review/access';
import { canAll } from '../operations/leave-policy/inputs';
import { monthRange, today, validRange } from '../operations/review/helpers';

const permission = 'attendance.leaves.team-calendar.read';
const statusLabels = {
  pending: 'Pending Admin review',
  approved: 'Approved',
  returned: 'Returned',
  rejected: 'Rejected',
  cancelled: 'Cancelled',
  cancellation_pending: 'Cancellation pending',
  superseded: 'Superseded',
} as const;
const entry = z.object({
  employeeId: id,
  date,
  status: z.enum(
    Object.keys(statusLabels) as [keyof typeof statusLabels, ...Array<keyof typeof statusLabels>],
  ),
});

export function LeaveManagementPage() {
  const access = useAttendanceAccess();
  return (
    <div className="attendance">
      <header className="attendance-heading">
        <div>
          <h1>Leave management</h1>
          <p>Review recorded employee leave dates and follow up on pending requests.</p>
        </div>
        <nav className="attendance-commands" aria-label="Leave management actions">
          {canAll(access.user, 'attendance.leaves.review') && (
            <Link className="button button--secondary" to="/hr/attendance/leaves">
              Admin review queue
            </Link>
          )}
          {canAll(access.user, 'attendance.leaves.configure') && (
            <Link className="button button--secondary" to="/hr/attendance/leave-settings">
              Leave settings
            </Link>
          )}
        </nav>
      </header>
      <EmployeeWorkspace permission={permission} title="Employee leave calendar">
        {(employeeId, scope) => <LeaveCalendar employeeId={employeeId} scope={scope} />}
      </EmployeeWorkspace>
    </div>
  );
}

function LeaveCalendar({ employeeId, scope }: { employeeId: string; scope?: Scope }) {
  const access = useAttendanceAccess();
  const initial = monthRange(today().slice(0, 7))!;
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [page, setPage] = useState(1);
  const valid = validRange(from, to, true);
  const allowed = canEmployee(access.user, permission, employeeId, scope);
  const schema = pageOf(entry).refine(
    (result) =>
      result.page === page &&
      result.pageSize === 25 &&
      result.items.every(
        (row) => row.employeeId === employeeId && row.date >= from && row.date <= to,
      ),
    'The calendar response does not match the selected employee and dates.',
  );
  const query = useAttendanceQuery(
    '/leaves/team-calendar' +
      queryString({ employeeIds: employeeId, from, to, page, pageSize: 25 }),
    schema,
    allowed && valid,
  );
  const rows = valid && !query.isError ? query.data?.items : undefined;
  return (
    <section className="attendance-section" aria-label="Employee leave dates">
      <div className="attendance-grid">
        <TextInput
          label="From"
          type="date"
          value={from}
          onChange={(e) => {
            setFrom(e.target.value);
            setPage(1);
          }}
        />
        <TextInput
          label="To"
          type="date"
          value={to}
          onChange={(e) => {
            setTo(e.target.value);
            setPage(1);
          }}
        />
      </div>
      <p>Choose up to 31 calendar dates. An empty date does not mean the employee was absent.</p>
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={!valid || !allowed || query.isFetching}
        onClick={() => {
          if (valid && allowed) void query.refetch();
        }}
      >
        Refresh calendar
      </Command>
      {!valid ? (
        <p role="alert">Choose a valid inclusive range of at most 31 days.</p>
      ) : (
        <QueryState
          query={query}
          empty={rows?.length === 0}
          emptyText="No recorded leave dates for this employee in the selected period."
        />
      )}
      {rows && rows.length > 0 && (
        <>
          <div className="table-scroll">
            <table className="attendance-table">
              <caption>Recorded leave dates for the selected employee</caption>
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.date + ':' + row.status}>
                    <td>{row.date}</td>
                    <td>{statusLabels[row.status]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={page}
            pageSize={25}
            total={query.data!.totalCount}
            onChange={setPage}
            disabled={query.isFetching}
          />
        </>
      )}
    </section>
  );
}
