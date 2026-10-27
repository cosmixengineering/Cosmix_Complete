import { useId, useMemo, type ReactNode } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { ReferenceField } from '../ReferenceField';
import type { Address, Bank, Emergency, EmployeeDocument } from '../../types/hiring';

export function Field({
  label,
  value,
  onChange,
  max = 200,
  type = 'text',
  required = false,
  pattern,
  readOnly = false,
  error,
  placeholder,
  dataField,
}: {
  label: string;
  value: string | null;
  onChange: (value: string | null) => void;
  max?: number;
  type?: 'text' | 'date' | 'tel' | 'email';
  required?: boolean;
  pattern?: string;
  readOnly?: boolean;
  error?: string;
  placeholder?: string;
  dataField?: string;
}) {
  const effectivePlaceholder = placeholder ?? (type === 'date' ? undefined : 'Type here');
  return (
    <TextInput
      label={label}
      value={value ?? ''}
      maxLength={max}
      type={type}
      required={required}
      readOnly={readOnly}
      error={error}
      placeholder={effectivePlaceholder}
      data-field={dataField}
      pattern={pattern ?? (type === 'tel' ? '\\+[1-9][0-9]{1,14}' : undefined)}
      autoComplete="off"
      onChange={(e) => onChange(e.target.value || null)}
    />
  );
}

export function PhoneField({
  label,
  value,
  onChange,
  required = false,
  error,
  placeholder = '3001234567',
  readOnly = false,
  dataField,
}: {
  label: string;
  value: string | null;
  onChange: (value: string | null) => void;
  required?: boolean;
  error?: string;
  placeholder?: string;
  readOnly?: boolean;
  dataField?: string;
}) {
  const id = useId();

  // Extract the local 10-digit number from value
  const extractLocalDigits = (val: string | null | undefined): string => {
    if (!val) return '';
    let digits = val.replace(/\D/g, '');
    if (digits.startsWith('92')) {
      digits = digits.slice(2);
    } else if (digits.startsWith('0')) {
      digits = digits.slice(1);
    }
    return digits.slice(0, 10);
  };

  const localDigits = extractLocalDigits(value);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let input = e.target.value.replace(/\D/g, '');
    // If user starts with 0 (e.g. 0300...), strip the leading 0
    if (input.startsWith('0')) {
      input = input.slice(1);
    }
    // If user pasted 92300... or +92300..., strip the leading 92
    if (input.startsWith('92') && input.length > 10) {
      input = input.slice(2);
    }
    const cleanDigits = input.slice(0, 10);

    if (cleanDigits.length === 0) {
      onChange(null);
    } else {
      onChange(`+92${cleanDigits}`);
    }
  };

  return (
    <div className="field" data-field={dataField}>
      <label htmlFor={id}>
        {label}
        {required && (
          <span className="hr-required-mark" aria-hidden="true">
            {' '}
            *
          </span>
        )}
      </label>
      <div className="phone-input-group" data-invalid={Boolean(error)}>
        <span className="phone-prefix-addon" aria-hidden="true">
          +92
        </span>
        <input
          id={id}
          type="tel"
          inputMode="numeric"
          maxLength={10}
          value={localDigits}
          placeholder={placeholder}
          required={required}
          readOnly={readOnly}
          data-field={dataField}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? id + '-error' : undefined}
          autoComplete="off"
          onChange={handleInputChange}
        />
      </div>
      {error && (
        <p className="field-error" id={id + '-error'}>
          {error}
        </p>
      )}
    </div>
  );
}
export function LongField({
  label,
  value,
  onChange,
  max = 1000,
  required = false,
  error,
}: {
  label: string;
  value: string | null;
  onChange: (value: string | null) => void;
  max?: number;
  required?: boolean;
  error?: string;
}) {
  const id = useId();
  return (
    <div className="field wide">
      <label htmlFor={id}>
        {label}
        {required && (
          <span className="hr-required-mark" aria-hidden="true">
            {' '}
            *
          </span>
        )}
      </label>
      <textarea
        id={id}
        value={value ?? ''}
        maxLength={max}
        required={required}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? id + '-error' : undefined}
        onChange={(e) => onChange(e.target.value || null)}
      />
      {error && (
        <p className="field-error" id={id + '-error'}>
          {error}
        </p>
      )}
    </div>
  );
}
export function MoneyField({
  label,
  value,
  onChange,
  required = false,
  error,
}: {
  label: string;
  value: number | null;
  onChange: (value: number | null) => void;
  required?: boolean;
  error?: string;
}) {
  return (
    <TextInput
      label={label}
      type="number"
      min="0"
      step="0.01"
      max="9999999999999.99"
      required={required}
      error={error}
      value={value ?? ''}
      onChange={(e) => onChange(e.target.value === '' ? null : e.target.valueAsNumber)}
    />
  );
}
export function EnumField({
  label,
  value,
  onChange,
  options,
  required = false,
  error,
  dataField,
}: {
  label: string;
  value: number | null;
  onChange: (value: number | null) => void;
  options: string[];
  required?: boolean;
  error?: string;
  dataField?: string;
}) {
  return (
    <SelectInput
      label={label}
      value={value ?? ''}
      required={required}
      error={error}
      data-field={dataField}
      options={[
        { value: '', label: 'Select ' + label.toLowerCase() },
        ...options.map((label, i) => ({ value: String(i + 1), label })),
      ]}
      onChange={(e) => onChange(e.target.value ? Number(e.target.value) : null)}
    />
  );
}
export function FormSection({
  title,
  subtitle,
  children,
  id,
  className,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  id?: string;
  className?: string;
}) {
  return (
    <section className={`hr-form-section ${className ?? ''}`} id={id}>
      <div className="hr-section-header">
        <h2 className="hr-section-title">{title}</h2>
        {subtitle && <p className="hr-section-subtitle">{subtitle}</p>}
      </div>
      <div className="hr-form-grid erp-form-grid-3">{children}</div>
    </section>
  );
}
export function AddressFields({
  label,
  value,
  onChange,
  required = false,
  errors,
  prefix = 'currentAddress',
}: {
  label: string;
  value: Address | null;
  onChange: (value: Address) => void;
  required?: boolean;
  errors?: Record<string, string>;
  prefix?: string;
}) {
  const a = value ?? {
    line1: null,
    line2: null,
    city: null,
    cityId: null,
    province: null,
    postalCode: null,
  };
  const set = (k: keyof Address, v: string | null) => onChange({ ...a, [k]: v });
  return (
    <FormSection title={label} id={label.toLowerCase().replaceAll(' ', '-')}>
      <Field
        label={label + ' line 1'}
        required={required}
        dataField={`${prefix}.line1`}
        error={errors?.[`${prefix}.line1`]}
        value={a.line1}
        max={250}
        onChange={(v) => set('line1', v)}
      />
      <Field
        label={label + ' line 2'}
        dataField={`${prefix}.line2`}
        value={a.line2}
        max={250}
        onChange={(v) => set('line2', v)}
      />
      <ReferenceField
        label={label + ' city reference'}
        kind="cities"
        dataField={`${prefix}.cityId`}
        value={a.cityId}
        error={errors?.[`${prefix}.cityId`]}
        onChange={(v) => set('cityId', v)}
      />
      <Field
        label={label + ' city'}
        required={required && !a.cityId}
        dataField={`${prefix}.city`}
        error={errors?.[`${prefix}.city`]}
        value={a.city}
        max={100}
        onChange={(v) => set('city', v)}
      />
      <Field
        label={label + ' province'}
        required={required}
        dataField={`${prefix}.province`}
        error={errors?.[`${prefix}.province`]}
        value={a.province}
        max={100}
        onChange={(v) => set('province', v)}
      />
      <Field
        label={label + ' postal code'}
        dataField={`${prefix}.postalCode`}
        value={a.postalCode}
        max={20}
        onChange={(v) => set('postalCode', v)}
      />
    </FormSection>
  );
}
export const RELATIONSHIP_OPTIONS = [
  { value: '', label: 'Select relationship' },
  { value: 'Father', label: 'Father' },
  { value: 'Mother', label: 'Mother' },
  { value: 'Spouse', label: 'Spouse' },
  { value: 'Brother', label: 'Brother' },
  { value: 'Sister', label: 'Sister' },
  { value: 'Son', label: 'Son' },
  { value: 'Daughter', label: 'Daughter' },
  { value: 'Guardian', label: 'Guardian' },
  { value: 'Uncle', label: 'Uncle' },
  { value: 'Aunt', label: 'Aunt' },
  { value: 'Cousin', label: 'Cousin' },
  { value: 'Friend', label: 'Friend' },
  { value: 'Colleague', label: 'Colleague' },
  { value: 'Other', label: 'Other' },
] as const;

export function EmergencyFields({
  value,
  onChange,
  required = false,
  errors,
}: {
  value: Emergency | null;
  onChange: (value: Emergency) => void;
  required?: boolean;
  errors?: Record<string, string>;
}) {
  const a = value ?? { name: null, relationship: null, phone: null, alternatePhone: null };
  const set = (k: keyof Emergency, v: string | null) => onChange({ ...a, [k]: v });

  const relationshipOptions = useMemo(() => {
    if (
      !a.relationship ||
      RELATIONSHIP_OPTIONS.some((opt) => opt.value.toLowerCase() === a.relationship?.toLowerCase())
    ) {
      return RELATIONSHIP_OPTIONS;
    }
    return [...RELATIONSHIP_OPTIONS, { value: a.relationship, label: a.relationship }];
  }, [a.relationship]);

  return (
    <FormSection title="Emergency contact" id="emergency-contact">
      <Field
        label="Emergency contact name"
        required={required}
        dataField="emergencyContact.name"
        error={errors?.['emergencyContact.name']}
        value={a.name}
        onChange={(v) => set('name', v)}
      />
      <SelectInput
        label="Relationship"
        required={required}
        data-field="emergencyContact.relationship"
        error={errors?.['emergencyContact.relationship']}
        value={a.relationship ?? ''}
        options={relationshipOptions}
        onChange={(e) => set('relationship', e.target.value || null)}
      />
      <PhoneField
        label="Emergency phone"
        required={required}
        dataField="emergencyContact.phone"
        error={errors?.['emergencyContact.phone']}
        value={a.phone}
        onChange={(v) => set('phone', v)}
      />
      <PhoneField
        label="Emergency alternate phone"
        dataField="emergencyContact.alternatePhone"
        error={errors?.['emergencyContact.alternatePhone']}
        value={a.alternatePhone}
        onChange={(v) => set('alternatePhone', v)}
      />
    </FormSection>
  );
}
export function BankFields({
  value,
  onChange,
}: {
  value: Bank | null;
  onChange: (value: Bank) => void;
}) {
  const a = value ?? { bankName: null, accountTitle: null, accountNumber: null, iban: null };
  return (
    <FormSection title="Bank details">
      {(
        [
          ['bankName', 'Bank name', 200],
          ['accountTitle', 'Account title', 200],
          ['accountNumber', 'Account number', 100],
          ['iban', 'IBAN', 34],
        ] as const
      ).map(([key, label, max]) => (
        <Field
          key={key}
          label={label}
          max={max}
          value={a[key]}
          onChange={(v) => onChange({ ...a, [key]: v })}
        />
      ))}
    </FormSection>
  );
}
export function DocumentChoice({
  label,
  value,
  onChange,
  documents,
}: {
  label: string;
  value: string | null;
  onChange: (value: string | null) => void;
  documents: EmployeeDocument[];
}) {
  const options = [
    { value: '', label: 'No document selected' },
    ...documents.map((d) => ({
      value: d.id,
      label: d.fileName + (d.isAvailable ? '' : ' (unavailable)'),
    })),
  ];
  if (value && !documents.some((d) => d.id === value))
    options.push({ value, label: 'Existing protected reference' });
  return (
    <SelectInput
      label={label}
      value={value ?? ''}
      options={options}
      onChange={(e) => onChange(e.target.value || null)}
    />
  );
}
