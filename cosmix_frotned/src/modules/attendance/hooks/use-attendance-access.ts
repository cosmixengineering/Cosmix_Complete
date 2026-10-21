import { useAuth } from '@/auth/use-auth';
import { attendanceSubject, hasAttendancePermission } from '../utils/access';

export function useAttendanceAccess() {
  const { user, status } = useAuth();
  const subject = attendanceSubject(user);
  return {
    user,
    subject,
    ready: status === 'authenticated',
    can: (code: string, siteId?: string) =>
      status === 'authenticated' && hasAttendancePermission(user, code, siteId),
  };
}
