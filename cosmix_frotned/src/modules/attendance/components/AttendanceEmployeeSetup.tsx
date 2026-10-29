import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { id } from '../schemas/common';
import { Command, QueryState } from './SchedulingUi';

export function AttendanceSetupNotice({
  employeeId,
  hiring = false,
}: {
  employeeId: string;
  hiring?: boolean;
}) {
  const access = useAttendanceAccess();
  return (
    <div className="attendance-empty" role="status">
      <p>
        {hiring
          ? 'This employee has no approved hiring record. Complete the hiring details and the existing submission and approval workflow before selecting them for attendance.'
          : 'Attendance setup required: no effective employment assignment or schedule is available for the selected dates. Review the assignment dates and office/site roster. Recorded session evidence remains factual; attendance classification and payable time are unavailable.'}
      </p>
      {access.can('hr.employees.read') ? (
        <Link
          to={
            hiring
              ? `/hr/employees/${employeeId}/hiring`
              : `/hr/employees/${employeeId}?section=employment${
                  access.can('hr.employees.changes.request') ? '&edit=1' : ''
                }`
          }
        >
          {hiring ? 'Complete employee hiring' : 'Manage employee / assignment setup'}
        </Link>
      ) : (
        <p>Ask HR to review this employee’s hiring and effective assignment.</p>
      )}
    </div>
  );
}

export function AttendanceEmployeeQueryState({
  employeeId,
  query,
  ...props
}: Parameters<typeof QueryState>[0] & { employeeId: string }) {
  if (
    query.isError &&
    query.error instanceof ApiError &&
    query.error.status === 409 &&
    ['employee_not_approved', 'employee_assignment_unavailable'].includes(query.error.code ?? '')
  )
    return (
      <AttendanceSetupNotice
        employeeId={employeeId}
        hiring={query.error.code === 'employee_not_approved'}
      />
    );
  return <QueryState query={query} {...props} />;
}

export function ApprovedAttendanceEmployee({
  employeeId,
  children,
}: {
  employeeId: string;
  children: ReactNode;
}) {
  const access = useAttendanceAccess();
  const canReadEmployee = access.can('hr.employees.read');
  // Current/basic verifies HasApprovedEmploymentAsync, including the retained approval
  // and submission. It does not require identity access or current Active status.
  const query = useAttendanceQuery(
    `/hr/employees/${employeeId}/current/basic`,
    z.object({ employeeId: id }).refine((value) => value.employeeId === employeeId),
    canReadEmployee,
  );
  // Preserve attendance-only and historical resource grants. The attendance endpoint
  // remains authoritative when the separate HR directory permission is unavailable.
  if (!canReadEmployee) return children;
  if (query.isError || query.isPending || query.isFetching || !query.data)
    return (
      <>
        <AttendanceEmployeeQueryState employeeId={employeeId} query={query} />
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          Recheck employee eligibility
        </Command>
      </>
    );
  return children;
}
