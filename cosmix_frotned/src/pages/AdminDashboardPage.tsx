import { Link } from 'react-router-dom';
import { HrDashboardPage } from '@/modules/hr';
import { useAuth } from '@/auth/use-auth';

export function AdminDashboardPage() {
  const { user } = useAuth();
  const has = (code: string) =>
    user?.grants.some(
      (grant) => grant.permission === code && grant.scopeKind === 7 && grant.targetId === null,
    );
  return (
    <section aria-label="Administration dashboard">
      <nav className="attendance-commands" aria-label="Administration actions">
        {has('hr.employees.submissions.review') && (
          <Link className="button button--secondary" to="/admin/approvals?type=hiring">
            Hiring review
          </Link>
        )}
        {has('attendance.leaves.review') && (
          <Link className="button button--secondary" to="/hr/attendance/leaves">
            Leave review
          </Link>
        )}
        {has('access.roles.read') && (
          <Link className="button button--secondary" to="/admin/roles">
            Roles and permissions
          </Link>
        )}
        {has('hr.organization.read') && (
          <Link className="button button--secondary" to="/hr/organization?group=departments">
            Departments
          </Link>
        )}
      </nav>
      <HrDashboardPage />
    </section>
  );
}
