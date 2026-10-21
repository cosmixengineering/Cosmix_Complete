import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './use-auth';
import { LoadingState } from '@/components/feedback/LoadingState';

export function RequireAuth() {
  const { status } = useAuth();
  if (status === 'loading') return <LoadingState />;
  if (status !== 'authenticated') return <Navigate to="/sign-in" replace />;
  return <Outlet />;
}
