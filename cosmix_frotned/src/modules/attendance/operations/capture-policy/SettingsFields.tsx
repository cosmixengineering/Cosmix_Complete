import { useId, type ReactNode } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Command } from '../../components/SchedulingUi';
import { changeMode, type CaptureDraft, type GroupMode } from './inputs';

function RuleGroup({
  title,
  mode,
  onChange,
  children,
}: {
  title: string;
  mode: GroupMode;
  onChange: (mode: GroupMode) => void;
  children?: ReactNode;
}) {
  const name = useId();
  return (
    <fieldset className="capture-policy-group">
      <legend>{title}</legend>
      <div className="capture-policy-modes">
        {(['enabled', 'disabled'] as const).map((value) => (
          <label key={value}>
            <input
              type="radio"
              name={name}
              value={value}
              checked={mode === value}
              required
              onChange={() => onChange(value)}
            />
            {value === 'enabled' ? 'Enabled' : 'Disabled'}
          </label>
        ))}
      </div>
      {mode === 'enabled' && children}
    </fieldset>
  );
}

function NumericLimit({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <TextInput
      label={label}
      type="number"
      inputMode="decimal"
      step="any"
      required
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}

export function SettingsFields({
  draft,
  onChange,
}: {
  draft: CaptureDraft;
  onChange: (draft: CaptureDraft) => void;
}) {
  const mode = (group: keyof CaptureDraft) => (value: GroupMode) =>
    onChange(changeMode(draft, group, value));
  return (
    <>
      <RuleGroup title="Duplicate capture" mode={draft.duplicate.mode} onChange={mode('duplicate')}>
        <NumericLimit
          label="Duplicate tolerance (seconds)"
          value={draft.duplicate.toleranceSeconds}
          onChange={(toleranceSeconds) =>
            onChange({ ...draft, duplicate: { ...draft.duplicate, toleranceSeconds } })
          }
        />
      </RuleGroup>
      <RuleGroup
        title="Active duration"
        mode={draft.activeDuration.mode}
        onChange={mode('activeDuration')}
      >
        <NumericLimit
          label="Maximum active duration (minutes)"
          value={draft.activeDuration.maximumMinutes}
          onChange={(maximumMinutes) =>
            onChange({ ...draft, activeDuration: { ...draft.activeDuration, maximumMinutes } })
          }
        />
      </RuleGroup>
      <RuleGroup title="Travel validation" mode={draft.travel.mode} onChange={mode('travel')}>
        <div className="attendance-grid">
          <NumericLimit
            label="Travel lookback (seconds)"
            value={draft.travel.lookbackSeconds}
            onChange={(lookbackSeconds) =>
              onChange({ ...draft, travel: { ...draft.travel, lookbackSeconds } })
            }
          />
          <NumericLimit
            label="Maximum travel speed (meters/second)"
            value={draft.travel.maximumMetersPerSecond}
            onChange={(maximumMetersPerSecond) =>
              onChange({ ...draft, travel: { ...draft.travel, maximumMetersPerSecond } })
            }
          />
          <NumericLimit
            label="Maximum location accuracy radius (meters)"
            value={draft.travel.maximumAccuracyMeters}
            onChange={(maximumAccuracyMeters) =>
              onChange({ ...draft, travel: { ...draft.travel, maximumAccuracyMeters } })
            }
          />
        </div>
      </RuleGroup>
      <RuleGroup title="Device risk" mode={draft.device.mode} onChange={mode('device')}>
        <TextInput
          label="Approved device code-set reference"
          required
          maxLength={200}
          value={draft.device.approvedCodeSetReference}
          onChange={(event) =>
            onChange({
              ...draft,
              device: { ...draft.device, approvedCodeSetReference: event.target.value },
            })
          }
        />
        {draft.device.riskMappings.map((row, index) => (
          <div className="capture-policy-mapping" key={row.entryId}>
            <TextInput
              label={`Observation code ${index + 1}`}
              required
              maxLength={100}
              value={row.observationCode}
              onChange={(event) =>
                onChange({
                  ...draft,
                  device: {
                    ...draft.device,
                    riskMappings: draft.device.riskMappings.map((item) =>
                      item.entryId === row.entryId
                        ? { ...item, observationCode: event.target.value }
                        : item,
                    ),
                  },
                })
              }
            />
            <SelectInput
              label={`Risk kind ${index + 1}`}
              required
              value={row.riskKind}
              options={[
                { value: '', label: 'Select risk kind' },
                { value: 'device_integrity', label: 'Device integrity' },
                { value: 'mock_location', label: 'Mock location' },
              ]}
              onChange={(event) =>
                onChange({
                  ...draft,
                  device: {
                    ...draft.device,
                    riskMappings: draft.device.riskMappings.map((item) =>
                      item.entryId === row.entryId
                        ? { ...item, riskKind: event.target.value as typeof row.riskKind }
                        : item,
                    ),
                  },
                })
              }
            />
            <Command
              icon={Trash2}
              variant="ghost"
              aria-label={`Remove mapping ${index + 1}`}
              title={`Remove mapping ${index + 1}`}
              onClick={() =>
                onChange({
                  ...draft,
                  device: {
                    ...draft.device,
                    riskMappings: draft.device.riskMappings.filter(
                      (item) => item.entryId !== row.entryId,
                    ),
                  },
                })
              }
            />
          </div>
        ))}
        <Command
          icon={Plus}
          variant="secondary"
          disabled={draft.device.riskMappings.length >= 100}
          onClick={() =>
            onChange({
              ...draft,
              device: {
                ...draft.device,
                riskMappings: [
                  ...draft.device.riskMappings,
                  { entryId: crypto.randomUUID(), observationCode: '', riskKind: '' },
                ],
              },
            })
          }
        >
          Add mapping
        </Command>
      </RuleGroup>
    </>
  );
}
