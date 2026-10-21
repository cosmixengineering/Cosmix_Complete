import { useState } from 'react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { calendarPage, date, reason, timeline } from '../../schemas/common';
import { officeRosterWrite, type OfficeRosterHistory } from '../../schemas/office-rosters';
import { rosterInput } from '../../schemas/site-rosters';
import { businessToday } from '../../utils/messages';
import { nextDate } from '../../utils/shift-schedule';
import { validateInterval } from '../../utils/roster-intent';
import { CalendarReference, ShiftReference, type ShiftReferenceValue } from '../References';
import { MutationForm } from '../SchedulingUi';

export function OfficeRosterForm({
  history,
  mode,
  assignmentId,
  onSaved,
  onCancel,
}: {
  history: OfficeRosterHistory;
  mode: 'assign' | 'replace' | 'end';
  assignmentId?: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(history);
  const assignment = source.assignments.find((row) => row.id === assignmentId);
  const [shift, setShift] = useState<ShiftReferenceValue | null>(null);
  const [calendar, setCalendar] = useState<z.infer<typeof calendarPage>['items'][number] | null>(
    null,
  );
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [inputReason, setReason] = useState('');
  function build() {
    const root = `/hr/branches/${source.branchId}/shift-assignments`;
    if (mode !== 'assign' && !assignment) throw new Error('Reload the assignment before editing.');
    if (mode === 'end') {
      date.parse(to);
      if (
        !assignment ||
        to <= businessToday() ||
        to <= assignment.effectiveFrom ||
        (assignment.effectiveTo && to >= assignment.effectiveTo)
      )
        throw new Error('Choose a future exclusive end that shortens this assignment.');
      return {
        path: `${root}/${assignmentId}/end`,
        method: 'POST' as const,
        schema: officeRosterWrite,
        body: {
          effectiveTo: to,
          expectedTimeline: timeline.parse(source.timeline),
          reason: reason().parse(inputReason),
        },
      };
    }
    const invalid = validateInterval(from, to || null, businessToday());
    if (invalid) throw new Error(invalid);
    if (!shift?.isActive) throw new Error('Select an active authorized shift.');
    if (
      mode === 'replace' &&
      assignment &&
      (from <= assignment.effectiveFrom ||
        (assignment.effectiveTo && from >= assignment.effectiveTo))
    )
      throw new Error('Replacement must start within and shorten the original interval.');
    return {
      path: root + (mode === 'replace' ? `/${assignmentId}/replace` : ''),
      method: 'POST' as const,
      schema: officeRosterWrite,
      body: rosterInput.parse({
        shiftId: shift.id,
        effectiveFrom: from,
        effectiveTo: to || null,
        holidayCalendarId: calendar?.id ?? null,
        expectedTimeline: source.timeline,
        expectedShiftVersion: shift.version,
        reason: inputReason,
      }),
    };
  }
  return (
    <MutationForm
      title={
        mode === 'assign'
          ? 'Assign office shift'
          : mode === 'replace'
            ? 'Replace office interval'
            : 'End office interval'
      }
      build={build}
      onSaved={onSaved}
      onCancel={onCancel}
    >
      {mode !== 'end' && (
        <>
          <ShiftReference value={shift} onChange={setShift} />
          <div className="hr-form-grid">
            <TextInput
              label="Effective from"
              type="date"
              required
              min={nextDate(businessToday())}
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
            <TextInput
              label="Exclusive end (optional)"
              type="date"
              min={from || nextDate(businessToday())}
              value={to}
              onChange={(event) => setTo(event.target.value)}
            />
          </div>
          <CalendarReference value={calendar} onChange={setCalendar} />
        </>
      )}
      {mode === 'end' && (
        <TextInput
          label="Exclusive end"
          type="date"
          required
          min={nextDate(businessToday())}
          value={to}
          onChange={(event) => setTo(event.target.value)}
        />
      )}
      <TextInput
        label="Reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
