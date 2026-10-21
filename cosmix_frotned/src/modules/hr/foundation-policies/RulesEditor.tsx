import { Plus, Trash2 } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { documentTypes, identityTypes, type FoundationPolicyKind } from './contracts';
import type { Choice, PolicyDraft, ValueRow } from './draft';

const choiceOptions = [
  { value: '', label: 'Select approved value' },
  { value: 'true', label: 'Yes' },
  { value: 'false', label: 'No' },
];
function Choices({
  label,
  values,
  selected,
  onChange,
}: {
  label: string;
  values: readonly string[];
  selected: string[];
  onChange: (selected: string[]) => void;
}) {
  return (
    <fieldset className="foundation-checks">
      <legend>{label}</legend>
      {values.map((value) => (
        <label key={value}>
          <input
            type="checkbox"
            checked={selected.includes(value)}
            onChange={(event) =>
              onChange(
                event.target.checked
                  ? [...selected, value]
                  : selected.filter((item) => item !== value),
              )
            }
          />
          {value}
        </label>
      ))}
    </fieldset>
  );
}
function Vocabulary({
  label,
  rows,
  maxLength,
  onChange,
}: {
  label: string;
  rows: ValueRow[];
  maxLength: number;
  onChange: (rows: ValueRow[]) => void;
}) {
  return (
    <section className="foundation-vocabulary">
      <div className="foundation-heading">
        <h4>{label}</h4>
        <Button
          variant="ghost"
          title={`Add ${label.toLowerCase()}`}
          aria-label={`Add ${label.toLowerCase()}`}
          disabled={rows.length >= 100}
          onClick={() => onChange([...rows, { id: crypto.randomUUID(), value: '' }])}
        >
          <Plus size={16} />
        </Button>
      </div>
      {!rows.length && <p className="muted">No configured values</p>}
      {rows.map((row, index) => (
        <div className="foundation-value-row" key={row.id}>
          <TextInput
            label={`${label} ${index + 1}`}
            required
            maxLength={maxLength}
            value={row.value}
            onChange={(event) =>
              onChange(
                rows.map((item) =>
                  item.id === row.id ? { ...item, value: event.target.value } : item,
                ),
              )
            }
          />
          <Button
            variant="ghost"
            title={`Remove ${label.toLowerCase()} ${index + 1}`}
            aria-label={`Remove ${label.toLowerCase()} ${index + 1}`}
            onClick={() => onChange(rows.filter((item) => item.id !== row.id))}
          >
            <Trash2 size={16} />
          </Button>
        </div>
      ))}
    </section>
  );
}
export function RulesEditor({
  kind,
  draft,
  onChange,
}: {
  kind: FoundationPolicyKind;
  draft: PolicyDraft;
  onChange: (draft: PolicyDraft) => void;
}) {
  function change<K extends keyof PolicyDraft>(key: K, value: PolicyDraft[K]) {
    onChange({ ...draft, [key]: value });
  }
  if (kind === 'Lifecycle')
    return (
      <div className="foundation-grid">
        <SelectInput
          label="Allow retirement"
          required
          options={choiceOptions}
          value={draft.allowRetirement}
          onChange={(event) => change('allowRetirement', event.target.value as Choice)}
        />
        <SelectInput
          label="Allow suspension release"
          required
          options={choiceOptions}
          value={draft.allowSuspensionRelease}
          onChange={(event) => change('allowSuspensionRelease', event.target.value as Choice)}
        />
      </div>
    );
  if (kind === 'Records')
    return (
      <SelectInput
        label="Approved operational record review mode"
        required
        options={[
          { value: '', label: 'Select approved mode' },
          { value: 'FixedAdminReview', label: 'Fixed Admin review' },
        ]}
        value={draft.approvedReviewMode}
        onChange={(event) =>
          change('approvedReviewMode', event.target.value as PolicyDraft['approvedReviewMode'])
        }
      />
    );
  return (
    <>
      <div className="foundation-grid">
        {(
          [
            ['genderValues', 'Gender values', 50],
            ['maritalStatusValues', 'Marital status values', 50],
            ['simStatusValues', 'SIM status values', 50],
            ['simCreditTierValues', 'SIM credit tier values', 40],
          ] as const
        ).map(([key, label, max]) => (
          <Vocabulary
            key={key}
            label={label}
            rows={draft[key]}
            maxLength={max}
            onChange={(rows) => change(key, rows)}
          />
        ))}
      </div>
      <div className="foundation-grid">
        {(
          [
            ['requireGender', 'Require gender'],
            ['requireMaritalStatus', 'Require marital status'],
            ['requireFatherOrSpouseName', 'Require father or spouse name'],
            ['requireProbationEnd', 'Require probation end'],
          ] as const
        ).map(([key, label]) => (
          <SelectInput
            key={key}
            label={label}
            required
            options={choiceOptions}
            value={draft[key]}
            onChange={(event) => change(key, event.target.value as Choice)}
          />
        ))}
        {(
          [
            ['minimumAgeAtJoining', 'Minimum age at joining'],
            ['maximumAgeAtJoining', 'Maximum age at joining'],
            ['maximumProbationDays', 'Maximum probation days'],
          ] as const
        ).map(([key, label]) => (
          <TextInput
            key={key}
            label={label}
            type="number"
            min={0}
            max={2147483647}
            step={1}
            value={draft[key]}
            onChange={(event) => change(key, event.target.value)}
          />
        ))}
      </div>
      <Choices
        label="Identity types requiring expiry"
        values={identityTypes}
        selected={draft.requireExpiryForIdentityTypes}
        onChange={(value) => change('requireExpiryForIdentityTypes', value)}
      />
      <section>
        <div className="foundation-heading">
          <h4>Required documents</h4>
          <Button
            variant="secondary"
            disabled={draft.requiredDocuments.length >= 100}
            onClick={() =>
              change('requiredDocuments', [
                ...draft.requiredDocuments,
                { id: crypto.randomUUID(), global: false, staffCategoryCode: '', types: [] },
              ])
            }
          >
            <Plus size={16} />
            Add document rule
          </Button>
        </div>
        {!draft.requiredDocuments.length && <p className="muted">No additional document rules</p>}
        {draft.requiredDocuments.map((row, index) => (
          <fieldset className="foundation-document-rule" key={row.id}>
            <legend>Document rule {index + 1}</legend>
            <div className="foundation-heading">
              <label>
                <input
                  type="checkbox"
                  checked={row.global}
                  onChange={(event) =>
                    change(
                      'requiredDocuments',
                      draft.requiredDocuments.map((item) =>
                        item.id === row.id
                          ? { ...item, global: event.target.checked, staffCategoryCode: '' }
                          : item,
                      ),
                    )
                  }
                />
                All staff categories
              </label>
              <Button
                variant="ghost"
                title={`Remove document rule ${index + 1}`}
                aria-label={`Remove document rule ${index + 1}`}
                onClick={() =>
                  change(
                    'requiredDocuments',
                    draft.requiredDocuments.filter((item) => item.id !== row.id),
                  )
                }
              >
                <Trash2 size={16} />
              </Button>
            </div>
            {!row.global && (
              <TextInput
                label="Staff category code"
                required
                pattern="[A-Z0-9][A-Z0-9_.-]{0,39}"
                maxLength={40}
                value={row.staffCategoryCode}
                onChange={(event) =>
                  change(
                    'requiredDocuments',
                    draft.requiredDocuments.map((item) =>
                      item.id === row.id
                        ? { ...item, staffCategoryCode: event.target.value }
                        : item,
                    ),
                  )
                }
              />
            )}
            <Choices
              label="Document types"
              values={documentTypes}
              selected={row.types}
              onChange={(types) =>
                change(
                  'requiredDocuments',
                  draft.requiredDocuments.map((item) =>
                    item.id === row.id ? { ...item, types } : item,
                  ),
                )
              }
            />
          </fieldset>
        ))}
      </section>
    </>
  );
}
