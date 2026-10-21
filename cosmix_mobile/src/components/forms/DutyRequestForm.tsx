import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Button, Field, Notice } from '../Controls';
import { ChoiceField, DateField, InfoRow } from '../ServiceControls';
import { SubmissionFrame } from './SubmissionFrame';
import { useSource } from '../../hooks/useSource';
import type { DataRecord } from '../../types/selfService';
import {
  businessToday,
  companyInstant,
  reason,
  record,
  records,
  status,
  text,
} from '../../utils/serviceValues';
import { AppError } from '../../utils/errors';
import { localTime } from '../../utils/presentation';
import { layout } from '../../theme/theme';

export function DutyRequestForm({
  kind,
}: {
  kind: 'overtime' | 'correction';
}): React.JSX.Element {
  const overtime = kind === 'overtime';
  const [date, setDate] = useState(businessToday);
  const [endDate, setEndDate] = useState(businessToday);
  const [startDate, setStartDate] = useState(businessToday);
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [selected, setSelected] = useState('');
  const [mode, setMode] = useState('4');
  const [activity, setActivity] = useState('1');
  const [note, setNote] = useState('');
  const [evidence, setEvidence] = useState('');
  const [job, setJob] = useState('');
  const base = overtime
    ? '/api/v1/me/overtime'
    : '/api/v1/me/attendance/corrections';
  const initial = useSource<DataRecord>(`${base}/sources?businessDate=${date}`);
  const sessions = initial.value
    ? records(initial.value[overtime ? 'sessions' : 'sessionRevisions'] ?? [])
    : [];
  const session = sessions.find(row => row.id === selected);
  const punch =
    mode === '1'
      ? session?.checkInPunchId
      : mode === '2'
      ? session?.checkOutPunchId
      : null;
  const punchSource = useSource<DataRecord>(
    !overtime && typeof punch === 'string'
      ? `${base}/sources?businessDate=${date}&rawPunchId=${encodeURIComponent(
          punch,
        )}`
      : null,
  );
  const source = !overtime && punch ? punchSource.value : initial.value;
  const eligible = source && overtime ? record(source.eligibility) : null;
  const duration = source && overtime ? record(source.durationPolicy) : null;
  const intervals = duration ? records(duration.eligibleIntervals) : [];
  const locked = source?.[overtime ? 'isLocked' : 'periodLocked'] !== false;
  const ready =
    !!source &&
    !locked &&
    (!overtime ||
      (eligible?.allowed === true &&
        session?.validDutyEvidence === true &&
        typeof session.endsAtUtc === 'string' &&
        intervals.length > 0));
  const title = overtime ? 'Overtime request' : 'Attendance review request';
  return (
    <SubmissionFrame
      service={overtime ? 'overtime' : 'corrections'}
      title={title}
      description={
        overtime
          ? 'Submit verified duty for independent overtime review.'
          : 'Describe missing or incorrect attendance for independent HR review.'
      }
      route={overtime ? `${base}/requests` : base}
      revision={JSON.stringify([
        kind,
        date,
        startDate,
        endDate,
        start,
        end,
        selected,
        mode,
        activity,
        note,
        evidence,
        job,
        source,
      ])}
      ready={!!ready && !initial.loading && !punchSource.loading}
      build={async () => {
        if (
          !ready ||
          !source ||
          typeof source.sourceFingerprint !== 'string' ||
          source.sourceFingerprint.length !== 64
        ) {
          throw new AppError('source_unavailable');
        }
        const clean = reason(note);
        const written = reason(evidence, 4000);
        const starts = companyInstant(startDate, start);
        if (overtime) {
          if (!session) {
            throw new AppError('source_unavailable');
          }
          const ends = companyInstant(endDate, end);
          if (
            Date.parse(ends) <= Date.parse(starts) ||
            !intervals.some(
              row =>
                Date.parse(starts) >= Date.parse(text(row.startsAtUtc)) &&
                Date.parse(ends) <= Date.parse(text(row.endsAtUtc)),
            ) ||
            Date.parse(starts) < Date.parse(text(session.startsAtUtc)) ||
            Date.parse(ends) > Date.parse(text(session.endsAtUtc))
          ) {
            throw new AppError('invalid_duty_interval');
          }
          return {
            body: {
              businessDate: date,
              workSessionId: session.id,
              startsAtUtc: starts,
              endsAtUtc: ends,
              activity: Number(activity),
              expectedSourceFingerprint: source.sourceFingerprint,
              reason: clean,
              writtenEvidence: written,
              ...(job.trim() ? { jobReference: reason(job, 200) } : {}),
            },
            summary: [
              title,
              `Business date ${date}`,
              `${startDate} ${start} → ${endDate} ${end} · Asia/Karachi`,
              activity === '1' ? 'Work' : 'Company travel',
              clean,
              written,
            ],
          };
        }
        const manual = mode === '4';
        const ends = manual ? companyInstant(endDate, end) : null;
        if (!manual && !session) {
          throw new AppError('source_unavailable');
        }
        if (mode === '1' && typeof punch !== 'string') {
          throw new AppError('source_unavailable');
        }
        if (ends && Date.parse(ends) <= Date.parse(starts)) {
          throw new AppError('invalid_duty_interval');
        }
        return {
          body: {
            businessDate: date,
            kind: Number(mode),
            rawPunchId: punch ?? null,
            expectedAssessmentId: punch
              ? source.latestAssessmentId ?? null
              : null,
            expectedSummaryId: source.latestSummaryId ?? null,
            requestedTimeUtc: starts,
            requestedEndUtc: ends,
            requestedStatus: null,
            reason: clean,
            writtenEvidence: written,
            expectedSourceFingerprint: source.sourceFingerprint,
            ...(!manual ? { targetWorkSessionId: session!.id } : {}),
          },
          summary: [
            title,
            mode === '1'
              ? 'Check In time'
              : mode === '2'
              ? 'Check Out time'
              : 'Documented missing duty',
            `Business date ${date}`,
            `${startDate} ${start}${
              manual ? ` → ${endDate} ${end}` : ''
            } · Asia/Karachi`,
            clean,
            written,
            'HR must verify the evidence. Submission does not create a successful face check or punch.',
          ],
        };
      }}
    >
      {disabled => (
        <View style={layout.card}>
          <DateField
            label="Business date"
            value={date}
            onChange={value => {
              setDate(value);
              setStartDate(value);
              setEndDate(value);
              setSelected('');
            }}
            disabled={disabled}
          />
          {!overtime ? (
            <ChoiceField
              label="What needs review?"
              value={mode}
              onChange={value => {
                setMode(value);
                setSelected('');
              }}
              disabled={disabled}
              choices={[
                { value: '4', label: 'Missing duty' },
                { value: '1', label: 'Check In time' },
                { value: '2', label: 'Check Out time' },
              ]}
            />
          ) : null}
          {initial.error || punchSource.error ? (
            <Notice message={initial.error || punchSource.error} />
          ) : null}
          <Button
            title="Refresh duty sources"
            secondary
            busy={initial.loading || punchSource.loading}
            disabled={disabled}
            onPress={() => {
              initial.reload();
              punchSource.reload();
            }}
          />
          {overtime || mode !== '4' ? (
            <ChoiceField
              label="Work session"
              value={selected}
              onChange={setSelected}
              disabled={disabled}
              choices={sessions
                .filter(row => !overtime || typeof row.endsAtUtc === 'string')
                .map(row => ({
                  value: text(row.id, ''),
                  label: `${localTime(
                    text(row.startsAtUtc),
                    'Asia/Karachi',
                  )} → ${
                    row.endsAtUtc
                      ? localTime(text(row.endsAtUtc), 'Asia/Karachi')
                      : 'Open'
                  }`,
                }))}
            />
          ) : null}
          {source ? (
            <InfoRow
              label="Availability"
              value={
                locked
                  ? 'This period is locked'
                  : overtime
                  ? status(eligible?.status)
                  : status(source.routingStatus)
              }
            />
          ) : null}
          {overtime ? (
            <>
              <ChoiceField
                label="Activity"
                value={activity}
                onChange={setActivity}
                disabled={disabled}
                choices={[
                  { value: '1', label: 'Work' },
                  { value: '2', label: 'Company travel' },
                ]}
              />
              {intervals.map((row, index) => (
                <Text key={index} style={layout.muted}>
                  Eligible interval:{' '}
                  {localTime(text(row.startsAtUtc), 'Asia/Karachi')} –{' '}
                  {localTime(text(row.endsAtUtc), 'Asia/Karachi')}
                </Text>
              ))}
              <Field
                label="Job reference (optional)"
                value={job}
                onChangeText={setJob}
                maxLength={200}
                editable={!disabled}
              />
            </>
          ) : null}
          <DateField
            label={overtime || mode === '4' ? 'Start date' : 'Requested date'}
            value={startDate}
            onChange={setStartDate}
            disabled={disabled}
          />
          <Field
            label={
              overtime || mode === '4'
                ? 'Start time · HH:mm, Asia/Karachi'
                : 'Requested time · HH:mm, Asia/Karachi'
            }
            value={start}
            onChangeText={setStart}
            maxLength={5}
            editable={!disabled}
            placeholder="HH:mm"
          />
          {overtime || mode === '4' ? (
            <>
              <DateField
                label="End date"
                value={endDate}
                onChange={setEndDate}
                disabled={disabled}
              />
              <Field
                label="End time · HH:mm, Asia/Karachi"
                value={end}
                onChangeText={setEnd}
                maxLength={5}
                editable={!disabled}
                placeholder="HH:mm"
              />
            </>
          ) : null}
          <Field
            label="Reason"
            value={note}
            onChangeText={setNote}
            editable={!disabled}
            multiline
            maxLength={1000}
          />
          <Field
            label="Written evidence / work details"
            value={evidence}
            onChangeText={setEvidence}
            editable={!disabled}
            multiline
            maxLength={4000}
          />
          <Notice
            tone="info"
            message={
              overtime
                ? 'Eligibility and approval are checked again by the server. Overtime approval is separate from payment.'
                : 'Attendance requests go to HR for independent review. Your existing submission records stay available.'
            }
          />
        </View>
      )}
    </SubmissionFrame>
  );
}
