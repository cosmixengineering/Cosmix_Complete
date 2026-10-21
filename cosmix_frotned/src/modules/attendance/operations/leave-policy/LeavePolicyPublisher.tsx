import { useState } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { Denied, MutationForm } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import type { PublishedLeavePolicy } from '../leave-benefits/api';
import { canAll, kindNames, treatmentNames } from './inputs';
import { Choice, EvidenceRecord } from './Ui';
import {
  baseNumbers,
  capabilities,
  capabilityNumbers,
  inputFromDraft,
  policyDraft,
  publishV4Intent,
  type Capability,
  type NumberField,
} from './extended-policy';

const labels: Record<Capability, string> = {
  encashment: 'Encashment',
  recovery: 'Recovery',
  benefits: 'Benefits',
  recoveryFulfilment: 'Recovery fulfilment',
};
const yesNo = [
  { value: 'true', label: 'Yes' },
  { value: 'false', label: 'No' },
];
const kindOptions = kindNames.map((label, value) => ({ label, value }));
const eligibleKindOptions = kindOptions.filter(({ value }) => value !== 5);
const fulfilmentChoices = [
  ['qualifyingWork', 'Qualifying work', 'Outside scheduled duty'],
  ['creditBasis', 'Credit basis', 'Exact worked minutes'],
  ['reviewAuthority', 'Review authority', 'Fixed Admin'],
  ['parentChangeHandling', 'Parent change handling', 'Block claims; retain allocations'],
  ['sourceChangeHandling', 'Source change handling', 'Reject changed sources; retain allocations'],
] as const;

export function LeavePolicyPublisher({
  source,
  publicationBlock,
  onSaved,
  onCancel,
}: {
  source?: PublishedLeavePolicy;
  publicationBlock: string;
  onSaved: (result: unknown) => void;
  onCancel: () => void;
}) {
  const access = useAttendanceAccess();
  const allowed = access.ready && canAll(access.user, 'attendance.leaves.configure');
  const [draft, setDraft] = useState(() => policyDraft(source));
  const block = publicationBlock || draft.copyProblem;
  const update = (key: string, value: string) =>
    setDraft((current) => ({
      ...current,
      fields: { ...current.fields, [key]: value },
    }));
  const selection = (key: string, values: number[]) =>
    setDraft((current) => ({
      ...current,
      selections: { ...current.selections, [key]: values },
      paidReviewed: key === 'paidKinds' ? false : current.paidReviewed,
    }));
  const numbers = (fields: NumberField[], prefix = '') => (
    <div className="attendance-grid">
      {fields.map((field) => (
        <TextInput
          key={field.key}
          label={field.label}
          type="number"
          required
          min={field.min}
          max={field.max}
          step={field.step ?? 1}
          value={draft.fields[prefix + field.key] ?? ''}
          onChange={(event) => update(prefix + field.key, event.target.value)}
        />
      ))}
    </div>
  );
  if (!allowed)
    return <Denied>Leave policy publication requires All-scoped configure access.</Denied>;
  return (
    <MutationForm
      title="Publish v4 leave settings"
      submitLabel="Publish directly"
      disabled={Boolean(block)}
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => {
        if (!allowed) throw new Error('Leave configure access is required.');
        if (block) throw new Error(block);
        return publishV4Intent(inputFromDraft(draft));
      }}
    >
      {block && <p role="alert">{block}</p>}
      {source && (
        <details>
          <summary>
            Source publication: {source.code} / {source.versionId}
          </summary>
          <EvidenceRecord value={source} />
        </details>
      )}
      <fieldset disabled={Boolean(block)}>
        <section className="attendance-section">
          <h3>Publication</h3>
          <div className="attendance-grid">
            <TextInput
              label="New unique policy code"
              required
              maxLength={40}
              value={draft.fields.code ?? ''}
              onChange={(event) => update('code', event.target.value)}
            />
            <TextInput
              label="Policy name"
              required
              maxLength={200}
              value={draft.fields.name ?? ''}
              onChange={(event) => update('name', event.target.value)}
            />
            <TextInput
              label="Effective from"
              type="date"
              required
              min="1901-01-01"
              max="9997-12-31"
              value={draft.fields.effectiveFrom ?? ''}
              onChange={(event) => update('effectiveFrom', event.target.value)}
            />
            <TextInput
              label="Exclusive end"
              type="date"
              required
              max="9998-12-31"
              value={draft.fields.effectiveTo ?? ''}
              onChange={(event) => update('effectiveTo', event.target.value)}
            />
          </div>
        </section>
        <section className="attendance-section">
          <h3>Base settings</h3>
          {numbers(baseNumbers)}
          <TextInput
            label="Eligible staff category IDs (comma separated)"
            required
            value={draft.fields.eligibleStaffCategoryIds ?? ''}
            onChange={(event) => update('eligibleStaffCategoryIds', event.target.value)}
          />
          <TextInput
            label="Exact holiday calendar ID"
            required
            value={draft.fields.holidayCalendarId ?? ''}
            onChange={(event) => update('holidayCalendarId', event.target.value)}
          />
          <Choice
            label="Holiday treatment"
            value={draft.fields.holidayTreatment ?? ''}
            options={[
              { value: '1', label: 'Exclude' },
              { value: '2', label: 'Count' },
            ]}
            onChange={(value) => update('holidayTreatment', value)}
          />
          <MultiChoice
            label="Paid kinds"
            options={kindOptions}
            value={draft.selections.paidKinds ?? []}
            onChange={(values) => selection('paidKinds', values)}
          />
          <label className="attendance-check">
            <input
              type="checkbox"
              checked={draft.paidReviewed}
              onChange={(event) =>
                setDraft((current) => ({ ...current, paidReviewed: event.target.checked }))
              }
            />
            Confirm paid-kind selection, including an empty selection
          </label>
        </section>
        {capabilities.map((capability) => (
          <section className="attendance-section" key={capability}>
            <h3>{labels[capability]}</h3>
            <Choice
              label={`${labels[capability]} configuration`}
              value={draft.capabilities[capability]}
              options={[
                { value: 'enabled', label: 'Enabled' },
                { value: 'unconfigured', label: 'Unconfigured' },
              ]}
              onChange={(value) =>
                setDraft((current) => ({
                  ...current,
                  capabilities: {
                    ...current.capabilities,
                    [capability]: value === 'enabled' || value === 'unconfigured' ? value : '',
                  },
                }))
              }
            />
            {draft.capabilities[capability] === 'enabled' && (
              <>
                {numbers(capabilityNumbers[capability], `${capability}.`)}
                {(capability === 'encashment' || capability === 'recovery') && (
                  <MultiChoice
                    label={`${labels[capability]} eligible kinds`}
                    options={eligibleKindOptions}
                    value={draft.selections[`${capability}.eligibleKinds`] ?? []}
                    onChange={(values) => selection(`${capability}.eligibleKinds`, values)}
                  />
                )}
                {capability === 'encashment' && (
                  <>
                    <Choice
                      label="Amount rounding"
                      value={draft.fields['encashment.rounding'] ?? ''}
                      options={[
                        { value: '0', label: 'To even' },
                        { value: '1', label: 'Away from zero' },
                      ]}
                      onChange={(value) => update('encashment.rounding', value)}
                    />
                    <TextInput
                      label="Currency"
                      required
                      maxLength={3}
                      pattern="[A-Z]{3}"
                      value={draft.fields['encashment.currency'] ?? ''}
                      onChange={(event) => update('encashment.currency', event.target.value)}
                    />
                    <Choice
                      label="Valuation date"
                      value={draft.fields['encashment.valuationDate'] ?? ''}
                      options={[{ value: '1', label: 'Last day of leave year' }]}
                      onChange={(value) => update('encashment.valuationDate', value)}
                    />
                  </>
                )}
                {capability === 'recovery' && (
                  <MultiChoice
                    label="Recovery eligible treatments"
                    options={[1, 2].map((value) => ({ value, label: treatmentNames[value]! }))}
                    value={draft.selections['recovery.eligibleTreatments'] ?? []}
                    onChange={(values) => selection('recovery.eligibleTreatments', values)}
                  />
                )}
                {capability === 'benefits' && (
                  <>
                    <Choice
                      label="Benefit eligible kind"
                      value={draft.fields['benefits.eligibleKind'] ?? ''}
                      options={eligibleKindOptions.map((option) => ({
                        ...option,
                        value: String(option.value),
                      }))}
                      onChange={(value) => update('benefits.eligibleKind', value)}
                    />
                    <Choice
                      label="Benefit encashment eligible"
                      value={draft.fields['benefits.encashmentEligible'] ?? ''}
                      options={yesNo}
                      onChange={(value) => update('benefits.encashmentEligible', value)}
                    />
                  </>
                )}
                {capability === 'recoveryFulfilment' && (
                  <>
                    {fulfilmentChoices.map(([key, label, configured]) => (
                      <Choice
                        key={key}
                        label={label}
                        value={draft.fields[`recoveryFulfilment.${key}`] ?? ''}
                        options={[
                          { value: '0', label: 'Pending configuration' },
                          { value: '1', label: configured },
                        ]}
                        onChange={(value) => update(`recoveryFulfilment.${key}`, value)}
                      />
                    ))}
                    {fulfilmentChoices.some(
                      ([key]) => draft.fields[`recoveryFulfilment.${key}`] === '0',
                    ) && (
                      <p role="status">
                        Recovery fulfilment remains pending while any governing choice is Pending
                        configuration.
                      </p>
                    )}
                    <Choice
                      label="Allow partial fulfilment"
                      value={draft.fields['recoveryFulfilment.allowPartialFulfilment'] ?? ''}
                      options={yesNo}
                      onChange={(value) =>
                        update('recoveryFulfilment.allowPartialFulfilment', value)
                      }
                    />
                  </>
                )}
              </>
            )}
          </section>
        ))}
        <TextInput
          label="Publication reason"
          required
          maxLength={1000}
          value={draft.fields.reason ?? ''}
          onChange={(event) => update('reason', event.target.value)}
        />
      </fieldset>
    </MutationForm>
  );
}

function MultiChoice({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: { value: number; label: string }[];
  value: number[];
  onChange: (value: number[]) => void;
}) {
  return (
    <fieldset>
      <legend>{label}</legend>
      <div className="attendance-days">
        {options.map((option) => (
          <label className="attendance-check" key={option.value}>
            <input
              type="checkbox"
              checked={value.includes(option.value)}
              onChange={(event) =>
                onChange(
                  event.target.checked
                    ? [...value, option.value]
                    : value.filter((item) => item !== option.value),
                )
              }
            />
            {option.label}
          </label>
        ))}
      </div>
    </fieldset>
  );
}
