import { useState, type PropsWithChildren } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { queryClient } from '@/services/query/query-client';
import { AuthProvider } from '@/auth/AuthProvider';
import { ErrorBoundary } from '@/components/feedback/ErrorBoundary';
import { ConfirmDialogProvider } from '@/components/feedback/ConfirmDialog';

export function AppProviders({ children }: PropsWithChildren) {
  const [router] = useState(() =>
    createBrowserRouter([
      {
        path: '*',
        element: <ConfirmDialogProvider>{children}</ConfirmDialogProvider>,
      },
    ]),
  );
  return (
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  );
}
