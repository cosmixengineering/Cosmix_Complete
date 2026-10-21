import { ChevronLeft, ChevronRight } from 'lucide-react';
import { IconButton } from '@/components/ui/IconButton';
export function Pagination({
  page,
  pageSize,
  total,
  onChange,
  disabled = false,
}: {
  page: number;
  pageSize: number;
  total: number;
  onChange: (page: number) => void;
  disabled?: boolean;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <nav className="pagination" aria-label="Pagination">
      <span>
        Page {page} of {pages}
      </span>
      <IconButton
        label="Previous page"
        disabled={disabled || page <= 1}
        onClick={() => onChange(page - 1)}
      >
        <ChevronLeft size={16} />
      </IconButton>
      <IconButton
        label="Next page"
        disabled={disabled || page >= pages}
        onClick={() => onChange(page + 1)}
      >
        <ChevronRight size={16} />
      </IconButton>
    </nav>
  );
}
