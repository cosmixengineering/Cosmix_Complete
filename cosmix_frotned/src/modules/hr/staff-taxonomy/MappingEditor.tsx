import { useId } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { policyUses } from './contracts';
import { mappingDraft, type MappingDraft } from './draft';

function BooleanChoice({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string;
  value: boolean | null;
  onChange: (value: boolean) => void;
  disabled: boolean;
}) {
  const name = useId();
  return (
    <fieldset className="taxonomy-choice" disabled={disabled}>
      <legend>{label}</legend>
      {[true, false].map((option) => (
        <label key={String(option)}>
          <input
            type="radio"
            name={name}
            checked={value === option}
            onChange={() => onChange(option)}
          />
          {option ? 'Yes' : 'No'}
        </label>
      ))}
    </fieldset>
  );
}
const sources = [
  { value: '0', label: 'Select source' },
  { value: '1', label: 'Office' },
  { value: '2', label: 'Site' },
];
const applicability = [
  { value: '0', label: 'Select applicability' },
  { value: '1', label: 'Not applicable' },
  { value: '2', label: 'Required' },
];
export function MappingEditor({
  rows,
  onChange,
  disabled,
}: {
  rows: MappingDraft[];
  onChange: (rows: MappingDraft[]) => void;
  disabled: boolean;
}) {
  function update(key: string, values: Partial<MappingDraft>) {
    onChange(rows.map((row) => (row.key === key ? { ...row, ...values } : row)));
  }
  return (
    <section aria-label="Category mappings">
      <div className="taxonomy-toolbar">
        <h3>Category mappings</h3>
        <Button
          variant="secondary"
          disabled={disabled || rows.length >= 100}
          onClick={() => onChange([...rows, mappingDraft()])}
        >
          <Plus size={16} />
          Add category
        </Button>
      </div>
      {rows.map((row, index) => {
        const baseline = row.code === 'OFFICE' || row.code === 'SITE';
        return (
          <fieldset className="taxonomy-category" key={row.key} disabled={disabled}>
            <legend>{row.code || `Category ${index + 1}`}</legend>
            <div className="taxonomy-toolbar">
              <span>{baseline ? 'Confirmed baseline' : 'Operator-selected mapping'}</span>
              {!baseline && (
                <Button
                  variant="ghost"
                  title="Remove category"
                  aria-label={`Remove category ${index + 1}`}
                  onClick={() => onChange(rows.filter((r) => r.key !== row.key))}
                >
                  <Trash2 size={16} />
                </Button>
              )}
            </div>
            <div className="taxonomy-grid">
              <TextInput
                label="Category code"
                maxLength={30}
                value={row.code}
                disabled={baseline}
                onChange={(e) => update(row.key, { code: e.target.value.toUpperCase() })}
              />
              <TextInput
                label="Category name"
                maxLength={150}
                value={row.name}
                onChange={(e) => update(row.key, { name: e.target.value })}
              />
              <SelectInput
                label="Work location"
                value={String(row.workLocation)}
                options={sources}
                disabled={baseline}
                onChange={(e) =>
                  update(row.key, {
                    workLocation: Number(e.target.value),
                    requireSiteBeforeActivation: null,
                  })
                }
              />
              <SelectInput
                label="Schedule basis"
                value={String(row.scheduleBasis)}
                options={sources}
                disabled={baseline}
                onChange={(e) => update(row.key, { scheduleBasis: Number(e.target.value) })}
              />
              <BooleanChoice
                label="Require site before activation"
                value={row.requireSiteBeforeActivation}
                disabled={disabled || baseline || row.workLocation === 1}
                onChange={(value) => update(row.key, { requireSiteBeforeActivation: value })}
              />
              <BooleanChoice
                label="Outside overtime inheritance"
                value={row.outsideOvertimeInheritance}
                disabled={disabled || baseline}
                onChange={(value) => update(row.key, { outsideOvertimeInheritance: value })}
              />
            </div>
            {row.workLocation === 1 && !baseline && (
              <label className="taxonomy-check">
                <input
                  type="checkbox"
                  checked={row.requireSiteBeforeActivation === false}
                  onChange={(e) =>
                    update(row.key, {
                      requireSiteBeforeActivation: e.target.checked ? false : null,
                    })
                  }
                />
                Confirm no site requirement for Office location
              </label>
            )}
            <div className="taxonomy-references">
              {policyUses.map(({ value, label }) => {
                const reference = row.policyReferences.find((r) => r.use === value)!;
                const replace = (values: Partial<typeof reference>) =>
                  update(row.key, {
                    policyReferences: row.policyReferences.map((r) =>
                      r.use === value ? { ...r, ...values } : r,
                    ),
                  });
                return (
                  <div className="taxonomy-reference" key={value}>
                    <SelectInput
                      label={`${label} applicability`}
                      options={applicability}
                      value={String(reference.applicability)}
                      onChange={(e) =>
                        replace({ applicability: Number(e.target.value), policyVersionId: '' })
                      }
                    />
                    {reference.applicability === 2 && (
                      <TextInput
                        label={`${label} immutable policy version ID`}
                        value={reference.policyVersionId}
                        onChange={(e) => replace({ policyVersionId: e.target.value })}
                      />
                    )}
                  </div>
                );
              })}
            </div>
          </fieldset>
        );
      })}
    </section>
  );
}
