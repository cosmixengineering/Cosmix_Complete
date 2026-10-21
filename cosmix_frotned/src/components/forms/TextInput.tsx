import { useId, type InputHTMLAttributes } from 'react';
export function TextInput({
  label,
  error,
  id,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string }) {
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
      <input
        {...props}
        id={inputId}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? inputId + '-error' : undefined}
      />
      {error && (
        <p className="field-error" id={inputId + '-error'}>
          {error}
        </p>
      )}
    </div>
  );
}
