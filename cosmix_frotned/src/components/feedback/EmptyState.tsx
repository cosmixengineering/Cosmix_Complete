import { FolderOpen } from 'lucide-react';
export function EmptyState({ title, message }: { title: string; message?: string }) {
  return (
    <div className="empty-state">
      <FolderOpen size={26} aria-hidden="true" />
      <h2>{title}</h2>
      {message && <p>{message}</p>}
    </div>
  );
}
