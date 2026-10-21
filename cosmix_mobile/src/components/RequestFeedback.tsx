import React from 'react';
import { Text, View } from 'react-native';
import { Button, Notice } from './Controls';
import { layout } from '../theme/theme';
import type { DataRecord, RequestDraft } from '../types/selfService';
import { records, status, text } from '../utils/serviceValues';

export function RequestFeedback({
  error,
  receipt,
  pending,
  busy,
  onRetry,
}: {
  error: string;
  receipt: DataRecord | null;
  pending: RequestDraft | null;
  busy: boolean;
  onRetry(): Promise<unknown>;
}): React.JSX.Element {
  return (
    <>
      {error ? <Notice message={error} /> : null}
      {receipt ? (
        <View style={layout.card}>
          <Text style={layout.heading}>Server response received</Text>
          {Array.isArray(receipt.cancellations) &&
          receipt.cancellations.length > 0 ? (
            <Text style={layout.body}>
              Original leave status: {status(receipt.state)}. Cancellation
              request statuses:{' '}
              {records(receipt.cancellations)
                .map(row => status(row.state))
                .join(', ')}
              . A cancellation needs a separate company decision.
            </Text>
          ) : (
            <Text style={layout.body}>
              Request status: {status(receipt.state ?? receipt.approvalState)}.
              Submission is separate from approval, payment or a change to your
              employment.
            </Text>
          )}
          <Text selectable style={layout.muted}>
            Reference: {text(receipt.id)}
          </Text>
        </View>
      ) : null}
      {pending ? (
        <View style={layout.card}>
          <Text style={layout.heading}>Confirm previous request</Text>
          <Text style={layout.body}>
            {pending.label} may already be recorded. Confirm the same submission
            before starting another request.
          </Text>
          <Text selectable style={layout.muted}>
            Reference: {pending.key}
          </Text>
          <Button
            title="Confirm previous request"
            busy={busy}
            onPress={() => onRetry().catch(() => undefined)}
          />
        </View>
      ) : null}
    </>
  );
}
