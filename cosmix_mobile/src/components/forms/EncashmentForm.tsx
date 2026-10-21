import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Field, Notice } from '../Controls';
import { ChoiceField, DateField, InfoRow } from '../ServiceControls';
import { SubmissionFrame } from './SubmissionFrame';
import { useSource } from '../../hooks/useSource';
import type { DataRecord } from '../../types/selfService';
import {
  businessToday,
  fraction,
  reason,
  record,
  records,
  requireDate,
  text,
} from '../../utils/serviceValues';
import { AppError } from '../../utils/errors';
import { layout } from '../../theme/theme';

export function EncashmentForm(): React.JSX.Element {
  const [date, setDate] = useState(businessToday);
  const [type, setType] = useState('');
  const [policy, setPolicy] = useState('');
  const [minutes, setMinutes] = useState('');
  const [note, setNote] = useState('');
  const types = useSource<DataRecord[]>('/api/v1/leaves/mine/types');
  const policies = useSource<DataRecord>(
    type
      ? `/api/v1/leaves/mine/policies?leaveTypeId=${type}&from=${date}&to=${date}`
      : null,
  );
  const balance = useSource<DataRecord>(
    type && policy
      ? `/api/v1/leaves/mine/balance?leaveTypeId=${type}&policyVersionId=${policy}&asOf=${date}`
      : null,
  );
  const choices = policies.value ? records(policies.value.policies) : [];
  return (
    <SubmissionFrame
      service="encashment"
      title="Leave encashment request"
      description="Choose a published policy and its fiscal year. Your company evaluates eligibility and value."
      route="/api/v1/leaves/mine/encashment"
      revision={JSON.stringify([
        date,
        type,
        policy,
        minutes,
        note,
        balance.value,
      ])}
      ready={!!balance.value && !balance.loading}
      build={async () => {
        if (!balance.value || !choices.some(row => row.versionId === policy)) {
          throw new AppError('leave_policy_unavailable');
        }
        if (
          !/^\d{2,5}$/.test(minutes) ||
          Number(minutes) < 60 ||
          Number(minutes) > 43200
        ) {
          throw new AppError('invalid_requested_minutes');
        }
        const start = requireDate(text(balance.value.fiscalYearStart, ''));
        const exclusive = requireDate(
          text(balance.value.fiscalYearEndExclusive, ''),
        );
        const last = new Date(Date.parse(exclusive + 'T00:00:00Z') - 86400000)
          .toISOString()
          .slice(0, 10);
        const clean = reason(note);
        return {
          body: {
            requestedMinutes: Number(minutes),
            leaveYearStart: start,
            leaveYearEnd: last,
            reason: clean,
            leaveTypeId: type,
            policyVersionId: policy,
          },
          summary: [
            `${minutes} leave minutes`,
            `Fiscal year ${start} – ${last}`,
            clean,
            'The server will assess the value. Submission is not a payment.',
          ],
        };
      }}
    >
      {disabled => (
        <View style={layout.card}>
          <DateField
            label="Policy reference date"
            value={date}
            onChange={value => {
              setDate(value);
              setPolicy('');
            }}
            disabled={disabled}
          />
          <ChoiceField
            label="Leave type"
            value={type}
            onChange={value => {
              setType(value);
              setPolicy('');
            }}
            disabled={disabled}
            choices={
              types.value
                ? records(types.value)
                    .filter(row => row.isActive === true)
                    .map(row => ({
                      value: text(row.id, ''),
                      label: text(row.name),
                    }))
                : []
            }
          />
          <ChoiceField
            label="Published policy"
            value={policy}
            onChange={setPolicy}
            disabled={disabled}
            choices={choices.map(row => ({
              value: text(row.versionId, ''),
              label: text(row.name),
            }))}
          />
          {types.error || policies.error || balance.error ? (
            <Notice message={types.error || policies.error || balance.error} />
          ) : null}
          {balance.value ? (
            <>
              <InfoRow
                label="Balance status"
                value={text(balance.value.status)}
              />
              <InfoRow
                label="Annual remaining"
                value={
                  balance.value.annualBalance
                    ? fraction(
                        record(balance.value.annualBalance).remainingDays,
                      )
                    : 'Unavailable'
                }
              />
              <InfoRow
                label="Fiscal year starts"
                value={text(balance.value.fiscalYearStart)}
              />
            </>
          ) : null}
          <Field
            label="Requested leave minutes"
            value={minutes}
            onChangeText={setMinutes}
            editable={!disabled}
            keyboardType="number-pad"
            maxLength={5}
          />
          <Text style={layout.muted}>
            60 to 43,200 minutes. Available leave and company policy are checked
            again at submission.
          </Text>
          <Field
            label="Reason"
            value={note}
            onChangeText={setNote}
            editable={!disabled}
            multiline
            maxLength={1000}
          />
        </View>
      )}
    </SubmissionFrame>
  );
}
