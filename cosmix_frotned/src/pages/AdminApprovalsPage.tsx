import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { CalendarDays, ClipboardCheck, Users } from 'lucide-react';
import { HiringApprovalsPage, hasHrPermission } from '@/modules/hr';
import { useAuth } from '@/auth/use-auth';

export function AdminApprovalsPage() {
  const [params] = useSearchParams();
  const { user } = useAuth();
  const type = params.get('type') ?? 'hiring';
  if (type === 'leave') return <Navigate to="/hr/leaves" replace />;

  return (
    <section aria-label="Approvals center">
      <nav className="hr-tabs" aria-label="Approval workspaces">
        <Link
          className={type === 'hiring' || type === 'all' ? 'active' : undefined}
          to="/admin/approvals?type=hiring"
        >
          <ClipboardCheck size={16} /> Hiring &amp; onboarding
        </Link>
        {hasHrPermission(user, 'hr.employees.read') && (
          <Link to="/hr/requests">
            <Users size={16} /> Employee changes
          </Link>
        )}
        <Link to="/hr/leaves">
          <CalendarDays size={16} /> Leave requests
        </Link>
      </nav>
      {type === 'hiring' || type === 'all' ? (
        <HiringApprovalsPage />
      ) : (
        <p className="hr-notice" role="status">
          This approval category is not connected to a live review service. No decision has been
          recorded.
        </p>
      )}
    </section>
  );
}
