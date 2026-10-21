import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ClipboardCheck, ClipboardList, Gift, Receipt, Settings } from 'lucide-react';
import { z } from 'zod';
import { Pagination } from '@/components/data-display/Pagination';
import { TextInput } from '@/components/forms/TextInput';
import { Denied, PrivateWorkspace, QueryState } from '../components/SchedulingUi';
import { queryString } from '../api/attendance-api';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { LeaveAdminPage } from '../operations/leave-policy/LeavePages';
import { Reload } from '../operations/leave-policy/Ui';
import { supportedScopes } from '../operations/review/access';
import { EmployeeWorkspace } from '../operations/review/ReviewUi';
import { date, id, pageOf } from '../schemas/common';
import { businessToday } from '../utils/messages';

const calendarPermission = 'attendance.leaves.team-calendar.read';
const calendarStatus = z.enum([
  'pending',
  'approved',
  'returned',
  'rejected',
  'cancelled',
  'cancellation_pending',
  'superseded',
]);
const calendarPage = pageOf(z.object({ employeeId: id, date, status: calendarStatus }));
const statusLabels: Record<z.infer<typeof calendarStatus>, string> = {
  pending: 'Pending',
  approved: 'Approved',
  returned: 'Returned',
  rejected: 'Rejected',
  cancelled: 'Cancelled',
  cancellation_pending: 'Approved - cancellation pending',
  superseded: 'Superseded',
};

export function LeaveManagementPage() {
  return (
    <PrivateWorkspace resource="leave-management">
      <ManagementWorkspace />
    </PrivateWorkspace>
  );
}

function ManagementWorkspace() {
  const access = useAttendanceAccess();
  const canReadCalendar = supportedScopes(access.user, calendarPermission).length > 0;
  const canReview = access.can('attendance.leaves.review');
  const canConfigure = access.can('attendance.leaves.configure');
  const canReviewBenefits = access.can('attendance.leaves.benefits.review');

  return (
    <>
      <nav className="attendance-commands" aria-label="Leave management">
        {canReadCalendar && canReview && (
          <Link className="button button--secondary" to="/hr/attendance/leaves">
            <ClipboardList size={16} aria-hidden="true" />
            Review employee requests
          </Link>
        )}
        {canConfigure && (
          <Link className="button button--secondary" to="/hr/attendance/leave-settings">
            <Settings size={16} aria-hidden="true" />
            Leave settings
          </Link>
        )}
        {canReview && (
          <Link className="button button--secondary" to="/hr/leave-encashment">
            <Receipt size={16} aria-hidden="true" />
            Encashment review
          </Link>
        )}
        {canReviewBenefits && (
          <Link className="button button--secondary" to="/hr/attendance/leave-benefits">
            <Gift size={16} aria-hidden="true" />
            Leave benefits
          </Link>
        )}
        {canReview && (
          <Link className="button button--secondary" to="/hr/attendance/recovery-work">
            <ClipboardCheck size={16} aria-hidden="true" />
            Recovery work review
          </Link>
        )}
      </nav>
      {canReadCalendar ? (
        <EmployeeWorkspace permission={calendarPermission} title="Employee leave calendar">
          {(employeeId) => <EmployeeCalendar key={employeeId} employeeId={employeeId} />}
        </EmployeeWorkspace>
      ) : canReview ? (
        <LeaveAdminPage />
      ) : (
        <>
          <h1>Leave &amp; Absence</h1>
          <Denied>Employee leave access has not been assigned to this account.</Denied>
        </>
      )}
    </>
  );
}

function EmployeeCalendar({ employeeId }: { employeeId: string }) {
  const [from, setFrom] = useState(() => businessToday().slice(0, 8) + '01');
  const [to, setTo] = useState(businessToday);
  const [page, setPage] = useState(1);
  const rangeValid =
    date.safeParse(from).success &&
    date.safeParse(to).success &&
    from >= '1901-01-01' &&
    to <= '9997-12-31' &&
    to >= from &&
    Date.parse(to) - Date.parse(from) <= 30 * 86400000;
  const query = useAttendanceQuery(
    '/leaves/team-calendar' +
      queryString({ employeeIds: employeeId, from, to, page, pageSize: 25 }),
    calendarPage,
    rangeValid,
  );

  return (
    <section className="attendance-section" aria-label="Employee leave dates">
      <div className="attendance-grid">
        <TextInput
          label="From"
          type="date"
          value={from}
          onChange={(event) => {
            setFrom(event.target.value);
            setPage(1);
          }}
        />
        <TextInput
          label="To"
          type="date"
          value={to}
          onChange={(event) => {
            setTo(event.target.value);
            setPage(1);
          }}
        />
        <Reload onClick={() => void query.refetch()} disabled={!rangeValid || query.isFetching} />
      </div>
      {!rangeValid ? (
        <p role="alert">Select a date range of up to 31 days.</p>
      ) : (
        <>
          <QueryState
            query={query}
            empty={query.data?.items.length === 0}
            emptyText="No leave records for this employee in the selected dates."
          />
          {!query.isError && query.data && query.data.items.length > 0 && (
            <>
              <div className="table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {query.data.items.map((entry) => (
                      <tr key={`${entry.employeeId}:${entry.date}:${entry.status}`}>
                        <td>{entry.date}</td>
                        <td>{statusLabels[entry.status]}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination
                page={page}
                pageSize={25}
                total={query.data.totalCount}
                onChange={setPage}
                disabled={query.isFetching}
              />
            </>
          )}
        </>
      )}
    </section>
  );
}
