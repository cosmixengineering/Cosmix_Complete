import { useId, type SelectHTMLAttributes } from 'react';
export function SelectInput({
  label,
  options,
  id,
  error,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & {
  label: string;
  options: readonly { value: string; label: string }[];
  error?: string;
}) {
  const generated = useId();
  const inputId = id ?? generated;
  return (
    <div className="field">
      <label htmlFor={inputId}>
        {label}
        {props.required && (
          <span className="hr-required-mark" aria-hidden="true">
            {' '}
            *
          </span>
        )}
      </label>
      <select
        {...props}
        id={inputId}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? inputId + '-error' : undefined}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && (
        <p className="field-error" id={inputId + '-error'}>
          {error}
        </p>
      )}
    </div>
  );
}
