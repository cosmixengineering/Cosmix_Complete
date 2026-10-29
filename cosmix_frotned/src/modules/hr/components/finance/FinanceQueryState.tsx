import type { ReactNode } from 'react';
import { CircleAlert, RotateCcw } from 'lucide-react';
import { ApiError } from '@/services/api/api-error';
import { Button } from '@/components/ui/Button';
import { TableSkeleton } from '@/components/ui/Skeleton';
import { financeError } from '../../api/finance-messages';

export function FinanceQueryState({
  query,
  children,
  title = 'Records unavailable',
}: {
  query: {
    isPending: boolean;
    isFetching: boolean;
    isError: boolean;
    error: unknown;
    refetch: () => unknown;
  };
  children: ReactNode;
  title?: string;
}) {
  if (query.isPending) return <TableSkeleton rows={6} />;
  if (!query.isError) return children;
  const failure = query.error instanceof ApiError ? query.error : null;
  const retryable = !failure || failure.status >= 500 || failure.status === 429;
  return (
    <div className="allowance-state allowance-state--error" role="alert">
      <CircleAlert size={18} aria-hidden="true" />
      <div>
        <h3>{failure?.status === 403 ? 'Access restricted' : title}</h3>
        <p>{financeError(query.error)}</p>
        {failure?.correlationId && (
          <details>
            <summary>Support reference</summary>
            <code>
              {failure.code || failure.status} / {failure.correlationId}
            </code>
          </details>
        )}
      </div>
      {retryable && (
        <Button
          variant="secondary"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          <RotateCcw size={14} aria-hidden="true" /> Retry
        </Button>
      )}
    </div>
  );
}
