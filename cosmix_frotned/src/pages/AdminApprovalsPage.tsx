import { Link, useSearchParams } from 'react-router-dom';
import { CalendarDays, ClipboardCheck, Users } from 'lucide-react';
import { HiringApprovalsPage, hasHrPermission } from '@/modules/hr';
import { useAuth } from '@/auth/use-auth';
import { LeaveAdminPage } from '@/modules/attendance/operations/leave-policy/LeavePages';

export function AdminApprovalsPage() {
  const [params] = useSearchParams();
  const { user } = useAuth();
  const type = params.get('type') ?? 'hiring';

  return (
    <section aria-label="Approvals center">
      <nav className="hr-tabs" aria-label="Approval workspaces">
        <Link
          className={type === 'hiring' ? 'active' : undefined}
          to="/admin/approvals?type=hiring"
        >
          <ClipboardCheck size={16} /> Hiring &amp; onboarding
        </Link>
        {hasHrPermission(user, 'hr.employees.read') && (
          <Link to="/hr/requests">
            <Users size={16} /> Employee changes
          </Link>
        )}
        <Link
          className={type === 'leave' ? 'active' : undefined}
          to="/admin/approvals?type=leave"
        >
          <CalendarDays size={16} /> Leave requests &amp; Approvals
        </Link>
      </nav>
      {type === 'hiring' ? (
        <HiringApprovalsPage />
      ) : type === 'leave' ? (
        <div style={{ marginTop: '16px' }}>
          <LeaveAdminPage />
        </div>
      ) : (
        <p className="hr-notice" role="status">
          This approval category is not connected to a live review service. No decision has been
          recorded.
        </p>
      )}
    </section>
  );
}
