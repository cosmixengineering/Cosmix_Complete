import { z } from 'zod';
import { id, pageOf } from './common';

// EmployeeListRequest.Status is OnboardingStatus, not EmployeeStatus.
// Backend: HR/Onboarding/OnboardingStatus.cs (Approved = 2).
// Keep approved former/archived employees available for historical attendance.
export const approvedAttendanceFilter = { status: 2, includeArchived: true } as const;
export const approvedAttendanceEmployees = pageOf(
  z.object({
    id,
    employeeNumber: z.number().int(),
    name: z.string().nullable(),
    onboardingStatus: z.literal(approvedAttendanceFilter.status),
  }),
);

export function needsAttendanceSetup(
  day: { roster: { status: string } | null; policy: { status: string } | null } | undefined,
) {
  return (
    day?.roster?.status === 'employee_assignment_unavailable' ||
    day?.policy?.status === 'schedule_configuration_required'
  );
}
