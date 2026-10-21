import { useAuth } from '@/auth/use-auth';
import { PreparedWorkspace } from '../components/workspaces/PreparedWorkspace';
import { selfServiceWorkspace } from '../workspaces/self-service';
import { Denied } from '../components/Feedback';

export function EmployeeSelfServicePage() {
  const { user, status } = useAuth();
  if (status !== 'authenticated' || !user?.account) return <Denied />;
  return (
    <>
      <p className="hr-notice">{user.account.displayName ?? 'My account'}</p>
      <PreparedWorkspace
        key={user.account.accountId}
        definition={selfServiceWorkspace}
        fixedEmployee={{
          id: user.account.employeeId,
          name: user.account.displayName ?? 'My employee record',
        }}
      />
    </>
  );
}
