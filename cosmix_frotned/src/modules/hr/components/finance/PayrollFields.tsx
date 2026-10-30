import { EmployeePicker } from '@/components/employees/EmployeeReference';
import { useId } from 'react';
export function PayrollSelect({
  label,
  value,
  options,
  onChange,
  required = true,
}: {
  label: string;
  value: string;
  options: { value: string; label: string }[];
  onChange(value: string): void;
  required?: boolean;
}) {
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        required={required}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">Select…</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}
export function PayrollEmployeePicker({
  value,
  onChange,
}: {
  value: string;
  onChange(value: string): void;
}) {
  return <EmployeePicker value={value} onChange={onChange} required />;
}
