import React, { useEffect, useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { useServices } from '../../app/AppProvider';
import { Button, Field, Notice } from '../Controls';
import { ChoiceField, DateField, InfoRow } from '../ServiceControls';
import { SubmissionFrame } from './SubmissionFrame';
import { EvidencePicker } from '../../services/documents/EvidencePicker';
import type { DataRecord, SelectedEvidence } from '../../types/selfService';
import { layout } from '../../theme/theme';
import { AppError, describeError } from '../../utils/errors';
import {
  businessToday,
  companyInstant,
  dateRange,
  fraction,
  reason,
  record,
  records,
  text,
} from '../../utils/serviceValues';
import { isGuid, isTimestamp } from '../../utils/validation';
import { localTime } from '../../utils/presentation';

const evidenceKinds = [
  { value: '1', label: 'Medical' },
  { value: '2', label: 'Admit card' },
  { value: '3', label: 'Date sheet' },
  { value: '4', label: 'Emergency' },
  { value: '5', label: 'Injury medical' },
  { value: '6', label: 'Other' },
];

export function LeaveRequestForm({
  previousRequestId,
}: {
  previousRequestId?: string;
}): React.JSX.Element {
  const { session } = useServices();
  const [types, setTypes] = useState<DataRecord[]>([]);
  const [typeId, setTypeId] = useState('');
  const [from, setFrom] = useState(businessToday);
  const [to, setTo] = useState(businessToday);
  const [mode, setMode] = useState('full');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [endDate, setEndDate] = useState(businessToday);
  const [note, setNote] = useState('');
  const [contact, setContact] = useState('');
  const [handover, setHandover] = useState('');
  const [files, setFiles] = useState<SelectedEvidence[]>([]);
  const filesRef = useRef(files);
  filesRef.current = files;
  const [picker] = useState(() => new EvidencePicker());
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [policies, setPolicies] = useState<DataRecord[]>([]);
  const [policyId, setPolicyId] = useState('');
  const [balance, setBalance] = useState<DataRecord | null>(null);
  const [previousReady, setPreviousReady] = useState(!previousRequestId);
  const mounted = useRef(true);
  const context = useRef('');
  context.current = JSON.stringify([typeId, from, to, policyId]);
  useEffect(() => {
    let active = true;
    mounted.current = true;
    session
      .request('/api/v1/leaves/mine/types')
      .then(value => {
        if (active) {
          setTypes(records(value).filter(item => item.isActive === true));
        }
      })
      .catch(failure => {
        if (active) {
          setError(describeError(failure));
        }
      });
    if (previousRequestId) {
      session
        .request(`/api/v1/leaves/mine/${previousRequestId}`)
        .then(value => {
          const row = record(value);
          if (row.state !== 2 && row.state !== 'Returned') {
            throw new AppError('request_source_changed');
          }
          if (active) {
            setTypeId(text(row.leaveTypeId, ''));
            setFrom(text(row.startDate, businessToday()));
            setTo(text(row.endDate, businessToday()));
            setNote(text(row.reason, ''));
            setPreviousReady(true);
          }
        })
        .catch(failure => {
          if (active) {
            setError(describeError(failure));
          }
        });
    }
    return () => {
      active = false;
      mounted.current = false;
      picker.remove(filesRef.current).catch(() => undefined);
    };
  }, [session, picker, previousRequestId]);
  useEffect(() => {
    setPolicies([]);
    setPolicyId('');
    setBalance(null);
  }, [typeId, from, to]);
  const selectEvidence = async () => {
    setLoading(true);
    setError('');
    try {
      const selected = await picker.select();
      if (!mounted.current) {
        await picker.remove(selected);
        return;
      }
      await picker.remove(filesRef.current);
      filesRef.current = selected;
      setFiles(selected);
    } catch (failure) {
      if (mounted.current) {
        setError(describeError(failure));
      }
    } finally {
      if (mounted.current) {
        setLoading(false);
      }
    }
  };
  const checkOptions = async () => {
    const started = context.current;
    setLoading(true);
    setError('');
    setBalance(null);
    try {
      if (!isGuid(typeId)) {
        throw new AppError('invalid_leave_type');
      }
      dateRange(from, to);
      const data = record(
        await session.request(
          `/api/v1/leaves/mine/policies?leaveTypeId=${typeId}&from=${from}&to=${to}`,
        ),
      );
      if (mounted.current && started === context.current) {
        setPolicies(records(data.policies));
        if (data.status !== 'available') {
          throw new AppError('leave_policy_unavailable');
        }
      }
    } catch (failure) {
      if (mounted.current && started === context.current) {
        setError(describeError(failure));
      }
    } finally {
      if (mounted.current) {
        setLoading(false);
      }
    }
  };
  const checkBalance = async () => {
    const started = context.current;
    setLoading(true);
    setError('');
    setBalance(null);
    try {
      if (!isGuid(policyId) || !isGuid(typeId)) {
        throw new AppError('leave_policy_unavailable');
      }
      const value = record(
        await session.request(
          `/api/v1/leaves/mine/balance?leaveTypeId=${typeId}&policyVersionId=${policyId}&asOf=${from}`,
        ),
      );
      if (mounted.current && started === context.current) {
        setBalance(value);
      }
    } catch (failure) {
      if (mounted.current && started === context.current) {
        setError(describeError(failure));
      }
    } finally {
      if (mounted.current) {
        setLoading(false);
      }
    }
  };
  const type = types.find(item => item.id === typeId);
  const requiredEvidence =
    type?.kind === 1
      ? [1]
      : type?.kind === 3
      ? [2, 3]
      : type?.kind === 5
      ? [5]
      : [];
  const revision = JSON.stringify([
    typeId,
    from,
    to,
    mode,
    start,
    end,
    endDate,
    note,
    contact,
    handover,
    files.map(file => [file.id, file.kind]),
    previousReady,
  ]);
  return (
    <SubmissionFrame
      service="leaves"
      title={previousRequestId ? 'Resubmit returned leave' : 'Apply for leave'}
      route="/api/v1/leaves/mine"
      revision={revision}
      ready={previousReady && !!type && !loading}
      description="Choose your dates and leave type. Exact scheduled intervals are checked with your company; HR determines approval and leave treatment."
      onSubmitted={async () => {
        await picker.remove(filesRef.current);
        setFiles([]);
      }}
      build={async () => {
        if (!type || !isGuid(typeId) || !previousReady) {
          throw new AppError('invalid_leave_type');
        }
        const cleanReason = reason(note);
        const dates = dateRange(from, mode === 'full' ? to : from);
        const attachments = await picker.attachments(files);
        if (
          (type.requiresEvidence === true && attachments.length === 0) ||
          requiredEvidence.some(
            kind => !attachments.some(file => file.kind === kind),
          ) ||
          new Set(attachments.map(file => file.kind)).size !==
            attachments.length
        ) {
          throw new AppError('leave_evidence_required');
        }
        const days: { date: string; startsAtUtc: string; endsAtUtc: string }[] =
          [];
        const skipped: string[] = [];
        // Bounded batches avoid opening hundreds of parallel schedule requests.
        for (let offset = 0; offset < dates.length; offset += 4) {
          const batch = await Promise.all(
            dates.slice(offset, offset + 4).map(async date => ({
              date,
              shift: record(
                await session.request(`/api/v1/me/shift?businessDate=${date}`),
              ),
            })),
          );
          for (const { date, shift } of batch) {
            if (shift.timeZone !== 'Asia/Karachi') {
              throw new AppError('leave_schedule_unavailable');
            }
            if (mode === 'full' && shift.status === 'rest_day') {
              skipped.push(date);
              continue;
            }
            if (
              shift.status !== 'scheduled' ||
              !isTimestamp(shift.startsAt) ||
              !isTimestamp(shift.endsAt)
            ) {
              throw new AppError('leave_schedule_unavailable');
            }
            const startsAtUtc =
              mode === 'full' ? shift.startsAt : companyInstant(from, start);
            const endsAtUtc =
              mode === 'full' ? shift.endsAt : companyInstant(endDate, end);
            if (
              Date.parse(endsAtUtc) <= Date.parse(startsAtUtc) ||
              Date.parse(startsAtUtc) < Date.parse(shift.startsAt) ||
              Date.parse(endsAtUtc) > Date.parse(shift.endsAt)
            ) {
              throw new AppError('leave_interval_outside_shift');
            }
            days.push({ date, startsAtUtc, endsAtUtc });
          }
        }
        if (days.length === 0) {
          throw new AppError('leave_schedule_unavailable');
        }
        const body = {
          leaveTypeId: typeId,
          days,
          reason: cleanReason,
          contactDuringLeave: contact.trim() || null,
          handoverNotes: handover.trim() || null,
          attachments: attachments.length ? attachments : undefined,
          previousRequestId: previousRequestId || undefined,
        };
        return {
          body,
          summary: [
            text(type.name),
            ...days.map(
              day =>
                `${day.date}: ${localTime(
                  day.startsAtUtc,
                  'Asia/Karachi',
                )} – ${localTime(day.endsAtUtc, 'Asia/Karachi')}`,
            ),
            ...(skipped.length
              ? [`Rest days excluded: ${skipped.join(', ')}`]
              : []),
            cleanReason,
            `${attachments.length} evidence document(s)`,
          ],
        };
      }}
    >
      {disabled => (
        <>
          {error ? <Notice message={error} /> : null}
          <View style={layout.card}>
            <ChoiceField
              label="Leave type"
              value={typeId}
              onChange={setTypeId}
              disabled={disabled || loading}
              choices={types.map(item => ({
                value: text(item.id, ''),
                label: text(item.name),
              }))}
            />
            {type?.requiresEvidence === true || requiredEvidence.length > 0 ? (
              <Notice
                tone="info"
                message={
                  requiredEvidence.length
                    ? `Required documents: ${requiredEvidence
                        .map(
                          kind =>
                            evidenceKinds.find(
                              item => item.value === String(kind),
                            )?.label,
                        )
                        .join(', ')}.`
                    : 'Supporting evidence is required for this leave type.'
                }
              />
            ) : null}
            <ChoiceField
              label="Leave duration"
              value={mode}
              onChange={setMode}
              disabled={disabled}
              choices={[
                { value: 'full', label: 'Full scheduled days' },
                { value: 'hourly', label: 'Part of one shift' },
              ]}
            />
            <DateField
              label="First date"
              value={from}
              disabled={disabled}
              onChange={value => {
                setFrom(value);
                if (to < value) {
                  setTo(value);
                }
                setEndDate(value);
              }}
            />
            {mode === 'full' ? (
              <DateField
                label="Last date"
                value={to}
                onChange={setTo}
                disabled={disabled}
              />
            ) : (
              <>
                <Field
                  label="Start time · company time (HH:mm)"
                  value={start}
                  onChangeText={setStart}
                  maxLength={5}
                  editable={!disabled}
                />
                <DateField
                  label="End date"
                  value={endDate}
                  onChange={setEndDate}
                  disabled={disabled}
                />
                <Field
                  label="End time · company time (HH:mm)"
                  value={end}
                  onChangeText={setEnd}
                  maxLength={5}
                  editable={!disabled}
                />
              </>
            )}
            <Field
              label="Reason"
              value={note}
              onChangeText={setNote}
              multiline
              maxLength={1000}
              editable={!disabled}
            />
            <Field
              label="Contact during leave · optional"
              value={contact}
              onChangeText={setContact}
              maxLength={200}
              editable={!disabled}
            />
            <Field
              label="Handover notes · optional"
              value={handover}
              onChangeText={setHandover}
              multiline
              maxLength={2000}
              editable={!disabled}
            />
          </View>
          <View style={layout.card}>
            <Text style={layout.heading}>Evidence</Text>
            <Text style={layout.muted}>
              PNG, JPEG or PDF. Up to four documents, 10 MB each and 20 MB
              combined. Choose a distinct purpose for each document.
            </Text>
            {files.map(file => (
              <View key={file.id} style={layout.stack8}>
                <Text style={layout.label}>
                  {file.name} · {(file.size / 1048576).toFixed(1)}MB
                </Text>
                <ChoiceField
                  label="Document purpose"
                  value={String(file.kind)}
                  disabled={disabled}
                  choices={evidenceKinds}
                  onChange={value =>
                    setFiles(current =>
                      current.map(item =>
                        item.id === file.id
                          ? { ...item, kind: Number(value) }
                          : item,
                      ),
                    )
                  }
                />
              </View>
            ))}
            <Button
              title={files.length ? 'Replace evidence' : 'Choose evidence'}
              secondary
              busy={loading}
              disabled={disabled}
              onPress={() => selectEvidence()}
            />
            {files.length ? (
              <Button
                title="Remove evidence"
                secondary
                disabled={disabled || loading}
                onPress={() =>
                  picker.remove(filesRef.current).then(() => setFiles([]))
                }
              />
            ) : null}
          </View>
          <View style={layout.card}>
            <Text style={layout.heading}>Leave balance</Text>
            <Text style={layout.muted}>
              Balance depends on the published policy and fiscal year. Pending
              applications are separate from approved usage.
            </Text>
            <Button
              title="Find applicable leave policies"
              secondary
              disabled={!typeId || disabled}
              busy={loading}
              onPress={() => checkOptions()}
            />
            {policies.length ? (
              <>
                <ChoiceField
                  label="Policy to view"
                  value={policyId}
                  onChange={value => {
                    setPolicyId(value);
                    setBalance(null);
                  }}
                  disabled={disabled}
                  choices={policies.map(item => ({
                    value: text(item.versionId, ''),
                    label: `${text(item.name)} · ${text(item.effectiveFrom)}`,
                  }))}
                />
                <Button
                  title="View exact balance"
                  secondary
                  disabled={!policyId || disabled}
                  busy={loading}
                  onPress={() => checkBalance()}
                />
              </>
            ) : null}
            {balance ? (
              <>
                <InfoRow label="Balance status" value={text(balance.status)} />
                <InfoRow
                  label="Annual remaining"
                  value={
                    balance.annualBalance
                      ? fraction(record(balance.annualBalance).remainingDays)
                      : 'Unavailable'
                  }
                />
                <InfoRow
                  label="Type remaining"
                  value={
                    balance.typeBalance
                      ? fraction(record(balance.typeBalance).remainingDays)
                      : 'Not specified by this policy'
                  }
                />
              </>
            ) : null}
          </View>
        </>
      )}
    </SubmissionFrame>
  );
}
