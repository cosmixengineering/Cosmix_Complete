import type { ReactNode } from 'react';
import { ShieldAlert } from 'lucide-react';
import { errorMessage } from '../utils/messages';
import { Button } from '@/components/ui/Button';

export function Denied() {
  return (
    <section className="hr-notice" role="alert">
      <ShieldAlert size={24} />
      <h2>Access restricted</h2>
      <p>Your current permissions do not allow this view.</p>
    </section>
  );
}
export function QueryState({
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
        <p>{errorMessage(query.error)}</p>
        <Button variant="secondary" onClick={() => void query.refetch()}>
          Retry
        </Button>
      </div>
    );
  return children;
}
export function ActionMessage({
  action,
}: {
  action: { error: string | null; success: string | null };
}) {
  return (
    <>
      {action.error && (
        <p className="hr-message hr-message--error" role="alert">
          {action.error}
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
