import { useState } from 'react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { calendarPage, date, reason, timeline } from '../../schemas/common';
import { rosterInput, rosterWrite, type SiteRosterHistory } from '../../schemas/site-rosters';
import { businessToday } from '../../utils/messages';
import { nextDate } from '../../utils/shift-schedule';
import { validateInterval } from '../../utils/roster-intent';
import { CalendarReference, ShiftReference, type ShiftReferenceValue } from '../References';
import { MutationForm } from '../SchedulingUi';

export function SiteRosterForm({
  history,
  mode,
  assignmentId,
  onSaved,
  onCancel,
}: {
  history: SiteRosterHistory;
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
  const min = nextDate(businessToday());
  function build() {
    const root = `/hr/sites/${source.siteId}/shift-assignments`;
    if (mode !== 'assign' && !assignment) throw new Error('Reload the assignment before editing.');
    if (mode === 'end') {
      date.parse(to);
      if (
        !assignment ||
        to <= businessToday() ||
        to <= assignment.effectiveFrom ||
        (assignment.effectiveTo && to >= assignment.effectiveTo)
      )
        throw new Error(
          'Choose a future exclusive end after the assignment starts and before its existing end.',
        );
      return {
        path: `${root}/${assignmentId}/end`,
        method: 'POST' as const,
        schema: rosterWrite,
        body: {
          effectiveTo: to,
          expectedTimeline: timeline.parse(source.timeline),
          reason: reason().parse(inputReason),
        },
      };
    }
    const error = validateInterval(from, to || null, businessToday());
    if (error) throw new Error(error);
    if (!shift?.isActive) throw new Error('Select an active shift from the authorized references.');
    return {
      path: root + (mode === 'replace' ? `/${assignmentId}/replace` : ''),
      method: 'POST' as const,
      schema: rosterWrite,
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
          ? 'Assign site shift'
          : mode === 'replace'
            ? 'Replace site assignment'
            : 'End site assignment'
      }
      build={build}
      onSaved={onSaved}
      onCancel={onCancel}
    >
      {assignment && (
        <p>
          Original assignment: {assignment.id}, {assignment.effectiveFrom} to{' '}
          {assignment.effectiveTo ?? 'open'} (exclusive). Shift: {assignment.shiftId}
        </p>
      )}
      {mode !== 'end' && (
        <div className="attendance-grid">
          <ShiftReference value={shift} onChange={setShift} />
          <CalendarReference value={calendar} onChange={setCalendar} />
        </div>
      )}
      <div className="attendance-grid">
        {mode !== 'end' && (
          <TextInput
            label="Effective from"
            type="date"
            min={min}
            value={from}
            required
            onChange={(event) => setFrom(event.target.value)}
          />
        )}
        <TextInput
          label={mode === 'end' ? 'Exclusive end date' : 'Exclusive end date (optional)'}
          type="date"
          min={min}
          value={to}
          required={mode === 'end'}
          onChange={(event) => setTo(event.target.value)}
        />
      </div>
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
