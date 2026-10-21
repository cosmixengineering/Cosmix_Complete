import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Notice } from './Controls';
import { ChoiceField, DateField, InfoRow } from './ServiceControls';
import { useSource } from '../hooks/useSource';
import type { DataRecord } from '../types/selfService';
import {
  businessToday,
  fraction,
  numeric,
  record,
  records,
  status,
  text,
} from '../utils/serviceValues';
import { isGuid } from '../utils/validation';
import { layout } from '../theme/theme';

/** Read-only balance selection needs leave access, independently of applying or reading a roster. */
export function LeaveBalancePanel(): React.JSX.Element {
  const [type, setType] = useState('');
  const [policy, setPolicy] = useState('');
  const [date, setDate] = useState(businessToday);
  const types = useSource<DataRecord[]>('/api/v1/leaves/mine/types');
  const options = useSource<DataRecord>(
    isGuid(type)
      ? `/api/v1/leaves/mine/policies?leaveTypeId=${type}&from=${date}&to=${date}`
      : null,
  );
  const candidates = options.value ? records(options.value.policies) : [];
  const balance = useSource<DataRecord>(
    isGuid(type) && isGuid(policy)
      ? `/api/v1/leaves/mine/balance?leaveTypeId=${type}&policyVersionId=${policy}&asOf=${date}`
      : null,
  );
  const annual = balance.value?.annualBalance
    ? record(balance.value.annualBalance)
    : null;
  const quota = balance.value?.typeBalance
    ? record(balance.value.typeBalance)
    : null;
  return (
    <View style={layout.card}>
      <Text style={layout.heading}>Your exact leave balance</Text>
      <Text style={layout.muted}>
        Choose a leave type and applicable company policy. The date selects its
        fiscal year; approved usage includes recorded future approved leave.
      </Text>
      <ChoiceField
        label="Leave type"
        value={type}
        onChange={value => {
          setType(value);
          setPolicy('');
        }}
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
      <DateField
        label="Balance reference date"
        value={date}
        onChange={value => {
          setDate(value);
          setPolicy('');
        }}
      />
      {options.value ? (
        <InfoRow
          label="Policy availability"
          value={status(options.value.status)}
        />
      ) : null}
      <ChoiceField
        label="Applicable policy"
        value={policy}
        onChange={setPolicy}
        choices={candidates.map(row => ({
          value: text(row.versionId, ''),
          label: `${text(row.name)} · ${text(row.effectiveFrom)}`,
        }))}
      />
      {types.loading || options.loading || balance.loading ? (
        <Text style={layout.muted}>Loading current company records…</Text>
      ) : null}
      {types.error || options.error || balance.error ? (
        <Notice message={types.error || options.error || balance.error} />
      ) : null}
      {balance.value ? (
        <>
          <InfoRow
            label="Balance status"
            value={status(balance.value.status)}
          />
          <InfoRow
            label="Fiscal year"
            value={`${text(balance.value.fiscalYearStart)} → ${text(
              balance.value.fiscalYearEndExclusive,
            )} (end exclusive)`}
          />
          <InfoRow
            label="Annual remaining"
            value={annual ? fraction(annual.remainingDays) : 'Unavailable'}
          />
          <InfoRow
            label="Annual used"
            value={annual ? fraction(annual.usedDays) : 'Unavailable'}
          />
          {annual ? (
            <InfoRow
              label="Annual limit"
              value={
                numeric(annual.limitDays) !== null
                  ? `${annual.limitDays} days`
                  : 'Unavailable'
              }
            />
          ) : null}
          <InfoRow
            label="Type remaining"
            value={
              quota
                ? fraction(quota.remainingDays)
                : 'No confirmed type quota supplied'
            }
          />
          <InfoRow
            label="Eligible service"
            value={
              balance.value.serviceEligible === true
                ? 'Eligible'
                : balance.value.serviceEligible === false
                ? 'Not eligible under this policy'
                : 'Unavailable'
            }
          />
          <Notice
            tone="info"
            message="These are the backend's exact recorded balances. A balance view does not approve a new leave request or reserve leave."
          />
        </>
      ) : null}
    </View>
  );
}
