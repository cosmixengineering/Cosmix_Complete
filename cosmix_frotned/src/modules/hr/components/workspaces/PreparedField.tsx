import { EmployeeReference, EmployeePicker } from '@/components/employees/EmployeeReference';
import { useId } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { ReferenceField } from '../ReferenceField';
import { LongField } from '../hiring/FormFields';
import { isOwnEmployeeField } from '../../utils/prepared-draft';
import { BoundaryEditor } from './BoundaryEditor';
import type {
  PreparedField as FieldDefinition,
  PreparedValue,
} from '../../types/prepared-workspace';

export function PreparedField({
  field,
  value,
  error,
  onChange,
  fixedEmployee,
}: {
  field: FieldDefinition;
  value: PreparedValue;
  error?: string;
  onChange: (value: PreparedValue) => void;
  fixedEmployee?: { id: string; name: string };
}) {
  const id = useId();
  const text = typeof value === 'string' ? value : '';
  let control;
  if (fixedEmployee && isOwnEmployeeField(field)) {
    control = (
      <div className="field">
        <span>{field.label}</span>
        <EmployeeReference
          employeeId={fixedEmployee.id}
          employeeName={fixedEmployee.name}
          showName
        />
      </div>
    );
  } else if (field.kind === 'reference' && field.reference === 'employees') {
    control = (
      <EmployeePicker
        label={field.label}
        value={text}
        required={field.required}
        onChange={onChange}
      />
    );
  } else if (field.kind === 'reference' && fixedEmployee) {
    control = (
      <TextInput
        label={field.label}
        value={text}
        required={field.required}
        autoComplete="off"
        onChange={(event) => onChange(event.target.value)}
      />
    );
  } else if (field.kind === 'reference' && field.reference) {
    control = (
      <ReferenceField
        label={field.label}
        kind={field.reference}
        value={text || null}
        required={field.required}
        onChange={onChange}
      />
    );
  } else if (field.kind === 'select') {
    control = (
      <SelectInput
        label={field.label}
        required={field.required}
        value={text}
        options={[
          { value: '', label: 'Select ' + field.label.toLowerCase() },
          ...(field.options ?? []),
        ]}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  } else if (field.kind === 'checkbox') {
    control = (
      <label className="hr-check">
        <input
          type="checkbox"
          checked={value === true}
          required={field.required}
          onChange={(event) => onChange(event.target.checked)}
        />
        {field.label}
      </label>
    );
  } else if (field.kind === 'geojson') {
    control = (
      <BoundaryEditor
        label={field.label}
        value={text}
        required={field.required}
        onChange={onChange}
      />
    );
  } else if (field.kind === 'textarea') {
    control = (
      <LongField
        label={field.label}
        value={text || null}
        required={field.required}
        max={field.maxLength ?? 10000}
        onChange={onChange}
      />
    );
  } else if (field.kind === 'file') {
    control = (
      <div className="field">
        <label htmlFor={id}>{field.label}</label>
        <input
          id={id}
          type="file"
          required={field.required}
          accept="application/pdf,image/png,image/jpeg"
          onChange={(event) => onChange(event.target.files?.[0] ?? null)}
        />
        {typeof File !== 'undefined' && value instanceof File && <small>{value.name}</small>}
      </div>
    );
  } else {
    control = (
      <TextInput
        label={field.label}
        type={
          ['number', 'date', 'time', 'datetime-local', 'month'].includes(field.kind)
            ? field.kind
            : 'text'
        }
        value={text}
        required={field.required}
        min={field.min}
        max={field.max}
        step={field.step}
        maxLength={field.maxLength ?? 200}
        autoComplete="off"
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }
  return (
    <div className={field.kind === 'textarea' || field.kind === 'geojson' ? 'wide' : undefined}>
      {control}
      {error && (
        <p className="hr-field-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
