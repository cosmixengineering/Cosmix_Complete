import React, { useCallback, useRef, useState } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { Text, View } from 'react-native';
import { useServices } from '../app/AppProvider';
import { Button, Notice } from './Controls';
import { InfoRow, RecordCard } from './ServiceControls';
import type { DataRecord } from '../types/selfService';
import { amount, records, text } from '../utils/serviceValues';
import { describeError } from '../utils/errors';
import { fullLocalTime } from '../utils/presentation';
import { payslipPeriod } from '../utils/payslipPresentation';
import { paymentStatus } from '../utils/paymentHistory';
import { layout } from '../theme/theme';

export function PaymentHistoryCard({
  initial,
}: {
  initial: DataRecord;
}): React.JSX.Element {
  const { selfService } = useServices();
  const [row, setRow] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const lifetime = useRef(0);
  const requestGuard = useRef(false);
  useFocusEffect(
    useCallback(() => {
      lifetime.current++;
      requestGuard.current = false;
      setRow(initial);
      setBusy(false);
      setError('');
      return () => {
        lifetime.current++;
      };
    }, [initial]),
  );

  async function changePage(page: number) {
    if (requestGuard.current) return;
    requestGuard.current = true;
    const current = lifetime.current;
    setBusy(true);
    setError('');
    try {
      const result = await selfService.loadPaymentPage(text(row.batchId), page);
      if (current === lifetime.current) setRow(result);
    } catch (failure) {
      if (current === lifetime.current) setError(describeError(failure));
    } finally {
      if (current === lifetime.current) {
        requestGuard.current = false;
        setBusy(false);
      }
    }
  }

  const payments = records(row.payments, 20);
  const page = Number(row.paymentsPage);
  return (
    <RecordCard
      row={{ state: paymentStatus(row.paymentState) }}
      title={text(row.batchReference)}
    >
      <InfoRow
        label="Pay period"
        value={payslipPeriod(row.periodStart, row.periodEndExclusive)}
      />
      <InfoRow
        label="Net salary entitlement"
        value={amount(row.netEntitlement, row.currency)}
      />
      <InfoRow
        label="Confirmed paid"
        value={amount(row.confirmedAmount, row.currency)}
      />
      <InfoRow
        label="Awaiting confirmed payment"
        value={amount(row.outstandingAmount, row.currency)}
      />
      <InfoRow
        label="Posted · awaiting confirmation"
        value={amount(row.postedAmount, row.currency)}
      />
      <InfoRow
        label="Pending payments"
        value={amount(row.pendingAmount, row.currency)}
      />
      <InfoRow
        label="Available for payment initiation"
        value={amount(row.remainingToInitiate, row.currency)}
      />
      <InfoRow
        label="Failed payments"
        value={amount(row.failedAmount, row.currency)}
      />
      <InfoRow
        label="Reversed payments"
        value={amount(row.reversedAmount, row.currency)}
      />
      <Text style={layout.muted}>
        Totals include every payment for this period. Failed and reversed
        amounts are historical outcomes and do not count as confirmed paid.
      </Text>
      <Text style={layout.heading}>Payment entries</Text>
      {error ? <Notice tone="error" message={error} /> : null}
      {busy ? (
        <Text accessibilityLiveRegion="polite" style={layout.muted}>
          Loading payment entries…
        </Text>
      ) : null}
      {!payments.length ? (
        <Text style={layout.muted}>No payment entries on this page.</Text>
      ) : null}
      {payments.map(payment => (
        <View key={text(payment.id)} style={layout.card}>
          <Text style={layout.label}>{paymentStatus(payment.state)}</Text>
          <InfoRow
            label="Amount"
            value={amount(payment.amount, row.currency)}
          />
          <InfoRow
            label="Posting date"
            value={text(payment.valueDate, 'Not recorded')}
          />
          <InfoRow
            label="Created · Asia/Karachi"
            value={fullLocalTime(
              text(payment.createdAtUtc, ''),
              'Asia/Karachi',
            )}
          />
          {payment.confirmedAtUtc ? (
            <InfoRow
              label="Confirmed · Asia/Karachi"
              value={fullLocalTime(
                text(payment.confirmedAtUtc, ''),
                'Asia/Karachi',
              )}
            />
          ) : null}
          {payment.reversedAtUtc ? (
            <InfoRow
              label="Reversed · Asia/Karachi"
              value={fullLocalTime(
                text(payment.reversedAtUtc, ''),
                'Asia/Karachi',
              )}
            />
          ) : null}
        </View>
      ))}
      <Text style={layout.muted}>Payment entries · page {page}</Text>
      <View style={layout.stack8}>
        <Button
          title="Previous entries"
          secondary
          disabled={page <= 1 || busy}
          onPress={() => changePage(page - 1)}
        />
        <Button
          title="Next entries"
          secondary
          disabled={row.hasMorePayments !== true || busy}
          onPress={() => changePage(page + 1)}
        />
      </View>
      {error ? (
        <Button
          title="Refresh payment entries"
          secondary
          disabled={busy}
          onPress={() => changePage(page)}
        />
      ) : null}
    </RecordCard>
  );
}
