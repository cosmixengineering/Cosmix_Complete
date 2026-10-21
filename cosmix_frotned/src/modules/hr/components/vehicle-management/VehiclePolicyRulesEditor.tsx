import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import {
  policyDraft,
  vehiclePolicyFields,
  type VehiclePolicyField,
} from '../../api/vehicle-policy-settings';

export function VehiclePolicyRulesEditor({
  value,
  onChange,
  field = vehiclePolicyFields,
  label = 'Policy rules',
}: {
  value: unknown;
  onChange: (value: unknown) => void;
  field?: VehiclePolicyField;
  label?: string;
}) {
  const name = label.replace(/([a-z])([A-Z])/g, '$1 $2');
  if (field.kind === 'constant')
    return (
      <p>
        {name}: {field.value}
      </p>
    );
  if (field.kind === 'nullable')
    return (
      <div className="wide">
        <SelectInput
          label={name}
          required
          value={value === undefined ? '' : value === null ? 'none' : 'configured'}
          options={[
            { value: '', label: 'Choose configuration' },
            { value: 'none', label: 'Explicitly unconfigured' },
            { value: 'configured', label: 'Configured' },
          ]}
          onChange={(event) =>
            onChange(
              event.target.value === ''
                ? undefined
                : event.target.value === 'none'
                  ? null
                  : policyDraft(field.field),
            )
          }
        />
        {value !== undefined && value !== null && (
          <VehiclePolicyRulesEditor
            field={field.field}
            label={label + ' values'}
            value={value}
            onChange={onChange}
          />
        )}
      </div>
    );
  if (field.kind === 'object') {
    const values = (value ?? {}) as Record<string, unknown>;
    return (
      <fieldset className="hr-form-grid wide" style={{ minWidth: 0 }}>
        <legend>{name}</legend>
        {Object.entries(field.fields).map(([key, child]) => (
          <VehiclePolicyRulesEditor
            key={key}
            label={key}
            field={child}
            value={values[key]}
            onChange={(next) => onChange({ ...values, [key]: next })}
          />
        ))}
      </fieldset>
    );
  }
  if (field.kind === 'list') {
    const rows: unknown[] = Array.isArray(value) ? value : [];
    return (
      <div className="wide">
        <h4>
          {name} ({rows.length})
        </h4>
        {rows.map((row, index) => (
          <div key={index} className="hr-repeat">
            <VehiclePolicyRulesEditor
              label={name + ' ' + (index + 1)}
              field={field.field}
              value={row}
              onChange={(next) => onChange(rows.map((item, i) => (i === index ? next : item)))}
            />
            <Button
              variant="secondary"
              type="button"
              title={'Remove ' + name + ' ' + (index + 1)}
              aria-label={'Remove ' + name + ' ' + (index + 1)}
              onClick={() => onChange(rows.filter((_, i) => i !== index))}
            >
              <Trash2 size={15} />
            </Button>
          </div>
        ))}
        <Button
          variant="secondary"
          type="button"
          disabled={rows.length >= field.max}
          onClick={() => onChange([...rows, policyDraft(field.field)])}
        >
          <Plus size={15} /> Add {name}
        </Button>
      </div>
    );
  }
  if (field.kind === 'set') {
    const selected: string[] = Array.isArray(value) ? value : [];
    return (
      <fieldset style={{ minWidth: 0 }}>
        <legend>{name}</legend>
        {field.options.map((option) => (
          <label key={option.value} className="hr-check">
            <input
              type="checkbox"
              checked={selected.includes(option.value)}
              onChange={(event) =>
                onChange(
                  event.target.checked
                    ? [...selected, option.value]
                    : selected.filter((item) => item !== option.value),
                )
              }
            />
            {option.label}
          </label>
        ))}
      </fieldset>
    );
  }
  if (field.kind === 'choice')
    return (
      <SelectInput
        label={name}
        required
        value={typeof value === 'string' ? value : ''}
        options={[{ value: '', label: 'Choose a value' }, ...field.options]}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  return (
    <TextInput
      label={name}
      required
      value={typeof value === 'string' ? value : ''}
      type={field.kind === 'number' ? 'number' : field.kind === 'date' ? 'date' : 'text'}
      step={field.kind === 'number' ? 10 ** -field.scale : undefined}
      min={field.kind === 'number' ? 0 : undefined}
      max={field.kind === 'number' ? field.max : undefined}
      maxLength={field.kind === 'text' ? field.max : undefined}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}
