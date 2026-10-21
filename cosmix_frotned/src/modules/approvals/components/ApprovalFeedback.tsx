import type { ReactNode } from 'react';
import { Button } from '@/components/ui/Button';
import {
  approvalError,
  approvalWriteError,
  type ApprovalWriteOperation,
} from '../api/approval-messages';

// Same loading/error/retry contract as the HR Feedback components, but the message
// goes through the approval code map so server codes stay readable.
export function ApprovalQueryState({
  query,
  children,
}: {
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
  children: ReactNode;
}) {
  if (query.isPending)
    return (
      <p className="hr-notice" role="status">
        Loading records...
      </p>
    );
  if (query.isError)
    return (
      <div className="hr-notice" role="alert">
        <p>{approvalError(query.error)}</p>
        <Button variant="secondary" onClick={() => void query.refetch()}>
          Retry
        </Button>
      </div>
    );
  return children;
}

export function ApprovalActionMessage({
  action,
  operation,
}: {
  action: { error: string | null; errorStatus: number | null; success: string | null };
  operation: ApprovalWriteOperation;
}) {
  const error = approvalWriteError(operation, action.errorStatus, action.error);
  return (
    <>
      {error && (
        <p className="hr-message hr-message--error" role="alert">
          {error}
        </p>
      )}
      {action.success && (
        <p className="hr-message" role="status">
          {action.success}
        </p>
      )}
    </>
  );
}
