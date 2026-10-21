import { useState } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { createShiftSchema, shiftDetail, type Shift } from '../../schemas/shifts';
import { reason, rowVersion } from '../../schemas/common';
import { businessToday } from '../../utils/messages';
import { emptySchedule, nextDate, validateSchedule } from '../../utils/shift-schedule';
import { MutationForm } from '../SchedulingUi';
import { ShiftScheduleForm } from './ShiftScheduleForm';

export function ShiftMetadataForm({
  mode,
  shift,
  onSaved,
  onCancel,
}: {
  mode: 'create' | 'rename' | 'status' | 'schedule';
  shift?: Shift;
  onSaved: (result: Shift) => void;
  onCancel: () => void;
}) {
  const [source] = useState(shift);
  const [code, setCode] = useState('');
  const [name, setName] = useState(source?.name ?? '');
  const [inputReason, setReason] = useState('');
  const today = businessToday();
  const latest =
    source?.versions.reduce(
      (max, version) => (version.effectiveFrom > max ? version.effectiveFrom : max),
      '',
    ) ?? '';
  const minimum = mode === 'schedule' ? nextDate(latest > today ? latest : today) : today;
  const [schedule, setSchedule] = useState(() => emptySchedule(minimum));
  const title =
    mode === 'create'
      ? 'Create shift'
      : mode === 'rename'
        ? 'Rename shift'
        : mode === 'status'
          ? `${source?.isActive ? 'Deactivate' : 'Activate'} shift`
          : 'Publish future schedule';
  function build() {
    if (mode === 'create' || mode === 'schedule') {
      const error = validateSchedule(
        schedule,
        businessToday(),
        mode === 'schedule' ? latest : undefined,
      );
      if (error) throw new Error(error);
    }
    if (mode === 'create')
      return {
        path: '/hr/shifts',
        method: 'POST' as const,
        body: createShiftSchema.parse({
          code,
          name,
          initialSchedule: schedule,
          reason: inputReason,
        }),
        schema: shiftDetail,
      };
    if (!source) throw new Error('Reload the shift before editing.');
    const expectedVersion = rowVersion.parse(source.version);
    const base = { expectedVersion, reason: reason(500).parse(inputReason) };
    return {
      path:
        `/hr/shifts/${source.id}` +
        (mode === 'rename' ? '' : mode === 'status' ? '/status' : '/versions'),
      method: mode === 'rename' ? ('PUT' as const) : ('POST' as const),
      schema: shiftDetail,
      body:
        mode === 'rename'
          ? { ...base, name: reason(150).parse(name) }
          : mode === 'status'
            ? { ...base, isActive: !source.isActive }
            : { ...base, schedule },
    };
  }
  return (
    <MutationForm
      title={title}
      build={build}
      onSaved={(result) => onSaved(shiftDetail.parse(result))}
      onCancel={onCancel}
    >
      {source && (
        <p>
          {source.code} - {source.name}
        </p>
      )}
      {mode === 'create' && (
        <TextInput
          label="Code"
          required
          maxLength={30}
          pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,29}"
          value={code}
          onChange={(event) => setCode(event.target.value)}
        />
      )}
      {(mode === 'create' || mode === 'rename') && (
        <TextInput
          label="Name"
          required
          maxLength={150}
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
      )}
      {(mode === 'create' || mode === 'schedule') && (
        <ShiftScheduleForm value={schedule} onChange={setSchedule} minDate={minimum} />
      )}
      {mode === 'status' && (
        <p>
          Current or future usage may prevent deactivation. Existing schedule history is retained.
        </p>
      )}
      <TextInput
        label="Reason"
        required
        maxLength={500}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
