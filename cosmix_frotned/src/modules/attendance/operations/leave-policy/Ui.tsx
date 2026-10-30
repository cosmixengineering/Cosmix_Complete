import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useId, type ReactNode } from 'react';
import { ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import { SelectInput } from '@/components/forms/SelectInput';
import { Command } from '../../components/SchedulingUi';
import { human } from './inputs';

export function Choice({
  label,
  value,
  options,
  onChange,
  required = true,
  disabled = false,
}: {
  label: string;
  value: string;
  options: readonly string[] | readonly { value: string; label: string }[];
  onChange: (value: string) => void;
  required?: boolean;
  disabled?: boolean;
}) {
  return (
    <SelectInput
      label={label}
      value={value}
      required={required}
      disabled={disabled}
      options={[
        { value: '', label: 'Select' },
        ...options.map((o) => (typeof o === 'string' ? { value: o, label: human(o) } : o)),
      ]}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}
export function Notes({
  label,
  value,
  onChange,
  maxLength = 2000,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  maxLength?: number;
}) {
  const fieldId = useId();
  return (
    <div className="field">
      <label htmlFor={fieldId}>{label}</label>
      <textarea
        id={fieldId}
        value={value}
        maxLength={maxLength}
        rows={3}
        onChange={(e) => onChange(e.target.value)}
        style={{ width: '100%', maxWidth: '100%', boxSizing: 'border-box' }}
      />
    </div>
  );
}
export function EvidenceRecord({ value, field }: { value: unknown; field?: string }) {
  if (field && /^(?:employeeId|.*EmployeeId)$/.test(field))
    return <EmployeeReference employeeId={typeof value === 'string' ? value : null} />;
  if (field === 'employeeNumber')
    return (
      <EmployeeReference
        employeeNumber={typeof value === 'number' || typeof value === 'string' ? value : null}
      />
    );
  if (value === null || value === undefined) return <span>Unavailable</span>;
  if (Array.isArray(value))
    return (
      <ul>
        {value.map((item, index) => (
          <li key={index}>
            <EvidenceRecord
              value={item}
              field={
                field && /^(?:employeeIds|.*EmployeeIds)$/.test(field) ? 'employeeId' : undefined
              }
            />
          </li>
        ))}
      </ul>
    );
  if (typeof value === 'object')
    return (
      <dl className="attendance-record">
        {Object.entries(value).map(([key, item]) => (
          <div key={key} style={{ display: 'contents' }}>
            <dt>{human(key)}</dt>
            <dd>
              <EvidenceRecord
                value={item}
                field={
                  key === 'targetId' && 'target' in value && value.target === 'employee'
                    ? 'employeeId'
                    : key
                }
              />
            </dd>
          </div>
        ))}
      </dl>
    );
  return (
    <span style={{ overflowWrap: 'anywhere' }}>
      {typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value)}
    </span>
  );
}
export function JsonEvidence({ json }: { json: string }) {
  let value: unknown;
  try {
    value = JSON.parse(json) as unknown;
  } catch {
    return <p role="alert">Stored configuration is unavailable.</p>;
  }
  return <EvidenceRecord value={value} />;
}
export function RuleErrors({
  errors,
}: {
  errors: readonly { path: string; code: string; message: string }[];
}) {
  return errors.length ? (
    <ul className="attendance-error" role="alert">
      {errors.map((e, i) => (
        <li key={i}>
          {e.path}: {e.message} ({e.code})
        </li>
      ))}
    </ul>
  ) : null;
}
export function MorePages({
  page,
  hasMore,
  disabled,
  onChange,
}: {
  page: number;
  hasMore: boolean;
  disabled: boolean;
  onChange: (page: number) => void;
}) {
  return (
    <nav className="attendance-commands" aria-label="Pagination">
      <Command
        icon={ChevronLeft}
        disabled={disabled || page === 1}
        onClick={() => onChange(page - 1)}
      >
        Previous
      </Command>
      <span>Page {page}</span>
      <Command
        icon={ChevronRight}
        disabled={disabled || !hasMore}
        onClick={() => onChange(page + 1)}
      >
        Next
      </Command>
    </nav>
  );
}
export function Reload({
  onClick,
  disabled = false,
  children = 'Reload',
}: {
  onClick: () => void;
  disabled?: boolean;
  children?: ReactNode;
}) {
  return (
    <Command icon={RefreshCw} variant="secondary" disabled={disabled} onClick={onClick}>
      {children}
    </Command>
  );
}
