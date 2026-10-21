import type { PropsWithChildren, ReactNode } from 'react';
import { useAuth } from './use-auth';

export function PermissionGate({
  permission,
  children,
  fallback = null,
}: PropsWithChildren<{
  permission: string;
  fallback?: ReactNode;
}>) {
  const { status, user } = useAuth();
  // Presentation only: the API still checks this grant's record/field scope.
  return status === 'authenticated' && user?.grants.some((grant) => grant.permission === permission)
    ? children
    : fallback;
}
