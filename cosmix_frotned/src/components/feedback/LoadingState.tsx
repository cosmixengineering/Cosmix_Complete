import { LoaderCircle } from 'lucide-react';
export function LoadingState() {
  return (
    <div className="loading-state" role="status">
      <LoaderCircle className="spin" size={20} aria-hidden="true" />
      Loading...
    </div>
  );
}
