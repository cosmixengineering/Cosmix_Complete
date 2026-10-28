import { LeaveAdminPage } from '../operations/leave-policy';

// Both HR navigation aliases use the existing permission-scoped, server-backed leave queue.
export function LeaveManagementPage() {
  return <LeaveAdminPage />;
}
