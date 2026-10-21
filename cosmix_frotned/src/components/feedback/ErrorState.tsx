import { RotateCw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
export function ErrorState({
  message = 'Something went wrong.',
  onRetry,
}: {
  message?: string;
  onRetry?: () => void;
}) {
  return (
    <div className="error-state" role="alert">
      <p>{message}</p>
      {onRetry && (
        <Button variant="secondary" onClick={onRetry}>
          <RotateCw size={15} />
          Retry
        </Button>
      )}
    </div>
  );
}
