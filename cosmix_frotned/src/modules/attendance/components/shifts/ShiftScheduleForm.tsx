import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import type { ShiftSchedule } from '../../schemas/shifts';
import { weekdays } from '../../utils/shift-schedule';

export function ShiftScheduleForm({
  value,
  onChange,
  minDate,
}: {
  value: ShiftSchedule;
  onChange: (value: ShiftSchedule) => void;
  minDate: string;
}) {
  function number(field: keyof ShiftSchedule, label: string, min: number, max: number) {
    return (
      <TextInput
        label={label}
        type="number"
        min={min}
        max={max}
        step={1}
        required
        value={value[field] as number}
        onChange={(event) =>
          onChange({
            ...value,
            [field]: event.target.value === '' ? NaN : Number(event.target.value),
          })
        }
      />
    );
  }
  return (
    <>
      <div className="attendance-grid">
        <TextInput
          label="Effective from (Asia/Karachi)"
          type="date"
          min={minDate}
          required
          value={value.effectiveFrom}
          onChange={(event) => onChange({ ...value, effectiveFrom: event.target.value })}
        />
        <SelectInput
          label="End day"
          value={value.endDayOffset}
          options={[
            { value: '0', label: 'Same business day' },
            { value: '1', label: 'Next day (overnight)' },
          ]}
          onChange={(event) => onChange({ ...value, endDayOffset: Number(event.target.value) })}
        />
        <TextInput
          label="Start time"
          type="time"
          step={1}
          required
          value={value.startTime}
          onChange={(event) => onChange({ ...value, startTime: event.target.value })}
        />
        <TextInput
          label="End time"
          type="time"
          step={1}
          required
          value={value.endTime}
          onChange={(event) => onChange({ ...value, endTime: event.target.value })}
        />
        {number('scheduledMinutes', 'Scheduled minutes', 1, 1440)}
        {number('paidBreakMinutes', 'Paid break minutes', 0, 1440)}
        {number('unpaidBreakMinutes', 'Unpaid break minutes', 0, 1440)}
        <TextInput
          label="Missing checkout reminder delay (minutes, optional)"
          type="number"
          min={1}
          max={2147483647}
          step={1}
          value={value.missingCheckoutReminderAfterMinutes ?? ''}
          onChange={(event) => {
            const schedule = { ...value };
            delete schedule.missingCheckoutReminderAfterMinutes;
            onChange(
              event.target.value === ''
                ? schedule
                : { ...schedule, missingCheckoutReminderAfterMinutes: Number(event.target.value) },
            );
          }}
        />
      </div>
      <div className="attendance-days" role="group" aria-label="Applicable weekdays">
        {weekdays.map((day) => (
          <label className="attendance-check" key={day.value}>
            <input
              type="checkbox"
              checked={(value.applicableWeekdays & day.value) !== 0}
              onChange={(event) =>
                onChange({
                  ...value,
                  applicableWeekdays: event.target.checked
                    ? value.applicableWeekdays | day.value
                    : value.applicableWeekdays & ~day.value,
                })
              }
            />
            {day.label}
          </label>
        ))}
      </div>
    </>
  );
}
