import type { ReactNode } from 'react';
import { useId } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { statusClass, statusLabel } from './labels';

export function FinanceTabs({
  tabs,
  active,
  onChange,
  label,
}: {
  tabs: { id: string; label: string }[];
  active: string;
  onChange: (id: string) => void;
  label: string;
}) {
  return (
    <div className="hr-tabs" role="tablist" aria-label={label}>
      {tabs.map((tab) => (
        <button
          type="button"
          key={tab.id}
          role="tab"
          aria-selected={active === tab.id}
          onClick={() => onChange(tab.id)}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}

export function FinanceStatus({ value }: { value: string }) {
  return <span className={statusClass(value)}>{statusLabel(value)}</span>;
}

export function HasMorePager({
  page,
  hasMore,
  busy,
  onPrevious,
  onNext,
}: {
  page: number;
  hasMore: boolean;
  busy: boolean;
  onPrevious: () => void;
  onNext: () => void;
}) {
  return (
    <nav className="hr-pagination" aria-label="Pagination">
      <span>Page {page}</span>
      <Button variant="secondary" disabled={busy || page <= 1} onClick={onPrevious}>
        <ChevronLeft size={16} />
        Previous
      </Button>
      <Button variant="secondary" disabled={busy || !hasMore} onClick={onNext}>
        Next
        <ChevronRight size={16} />
      </Button>
    </nav>
  );
}

export function FinanceNotice({ children }: { children: ReactNode }) {
  return (
    <p className="hr-notice" role="status">
      {children}
    </p>
  );
}

export function TextAreaField({
  label,
  value,
  onChange,
  error,
  required = false,
  rows = 3,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  required?: boolean;
  rows?: number;
  disabled?: boolean;
}) {
  const fieldId = useId();
  return (
    <div className="field wide">
      <label htmlFor={fieldId}>
        {label}
        {required && (
          <span className="hr-required-mark" aria-hidden="true">
            {' '}
            *
          </span>
        )}
      </label>
      <textarea
        id={fieldId}
        rows={rows}
        value={value}
        required={required}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${fieldId}-error` : undefined}
        onChange={(event) => onChange(event.target.value)}
      />
      {error && (
        <p className="field-error" id={`${fieldId}-error`}>
          {error}
        </p>
      )}
    </div>
  );
}
