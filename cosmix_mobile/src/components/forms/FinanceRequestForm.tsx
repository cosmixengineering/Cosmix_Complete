import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Field } from '../Controls';
import { DateField, InfoRow } from '../ServiceControls';
import { SubmissionFrame } from './SubmissionFrame';
import { layout } from '../../theme/theme';
import {
  amount,
  businessToday,
  moneyInput,
  reason,
  requireDate,
} from '../../utils/serviceValues';

export function FinanceRequestForm({
  kind,
}: {
  kind: 'advance' | 'loan';
}): React.JSX.Element {
  const [value, setValue] = useState('');
  const [requestedOn, setRequestedOn] = useState(businessToday);
  const [note, setNote] = useState('');
  const title = kind === 'advance' ? 'Salary advance request' : 'Loan request';
  return (
    <SubmissionFrame
      service="loans"
      title={title}
      description="Send a request to your company. Approved terms and repayment schedules will appear in Advances & loans."
      route="/api/v1/me/advances-loans/requests"
      revision={JSON.stringify([kind, value, requestedOn, note])}
      build={async () => {
        const requestedAmount = moneyInput(value);
        const cleanReason = reason(note, 2000);
        requireDate(requestedOn);
        return {
          body: {
            kind: kind === 'advance' ? 1 : 2,
            requestedAmount,
            currency: 'PKR',
            reason: cleanReason,
            requestedOn,
          },
          summary: [
            title,
            amount(requestedAmount, 'PKR'),
            `Requested on ${requestedOn}`,
            cleanReason,
          ],
        };
      }}
    >
      {disabled => (
        <View style={layout.card}>
          <InfoRow label="Currency" value="PKR" />
          <Field
            label="Requested amount · PKR"
            value={value}
            onChangeText={setValue}
            keyboardType="decimal-pad"
            editable={!disabled}
            maxLength={12}
          />
          <Text style={layout.muted}>
            Enter 1 to 10,000,000 PKR. Approval and repayment terms are decided
            by your company.
          </Text>
          <DateField
            label="Requested date"
            value={requestedOn}
            onChange={setRequestedOn}
            disabled={disabled}
          />
          <Field
            label="Why do you need this request?"
            value={note}
            onChangeText={setNote}
            multiline
            maxLength={2000}
            editable={!disabled}
          />
        </View>
      )}
    </SubmissionFrame>
  );
}
