import { useEffect, useState, type PropsWithChildren } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PrivateWorkspace } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';

function FieldQueryScope({ children }: PropsWithChildren) {
  const [cache] = useState(() => new QueryClient());
  useEffect(() => () => cache.clear(), [cache]);
  return <QueryClientProvider client={cache}>{children}</QueryClientProvider>;
}

export function FieldWorkspace({
  children,
  resource = '',
}: PropsWithChildren<{ resource?: string }>) {
  const access = useAttendanceAccess();
  // The shared subject omits employee identity; isolate private field queries
  // inside its workspace reset boundary, including employee changes.
  return (
    <PrivateWorkspace resource={`${access.user?.account?.employeeId ?? 'none'}:${resource}`}>
      <FieldQueryScope>{children}</FieldQueryScope>
    </PrivateWorkspace>
  );
}
