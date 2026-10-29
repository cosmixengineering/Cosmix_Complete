import React, { useCallback, useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { useServices, useSession } from '../app/AppProvider';
import { Button, Notice } from './Controls';
import { InfoRow } from './ServiceControls';
import { PayslipFiles } from '../services/documents/PayslipFiles';
import type { PayslipFormat } from '../utils/payslipFile';
import { AppError, describeError } from '../utils/errors';
import { amount, record, text } from '../utils/serviceValues';
import { payslipPeriod, payslipStatus } from '../utils/payslipPresentation';
import { isGuid } from '../utils/validation';
import { layout } from '../theme/theme';

export function PayslipHistory(): React.JSX.Element {
  const { session } = useServices();
  const auth = useSession();
  const openingGuard = useRef(false);
  const focusLifetime = useRef<AbortController | null>(null);
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [rows, setRows] = useState<Record<string, unknown>[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [busy, setBusy] = useState(false);
  const [opening, setOpening] = useState('');
  const [error, setError] = useState('');
  useFocusEffect(
    useCallback(() => {
      let active = true;
      const controller = new AbortController();
      focusLifetime.current = controller;
      setBusy(true);
      setError('');
      setRows([]);
      setHasMore(false);
      session
        .revalidate()
        .then(() =>
          session.request<unknown>(
            `/api/v1/self-service/finances/payslips?page=${page}&pageSize=20`,
          ),
        )
        .then(value => {
          const data = record(value);
          if (!Array.isArray(data.items) || typeof data.hasMore !== 'boolean')
            throw new AppError('invalid_server_response');
          const items = data.items.map(record);
          if (
            items.some(
              row =>
                !isGuid(row.batchId) ||
                !['grossAmount', 'deductionAmount', 'netAmount'].every(
                  key =>
                    typeof row[key] === 'number' && Number.isFinite(row[key]),
                ),
            )
          )
            throw new AppError('invalid_server_response');
          if (active) {
            setRows(items);
            setHasMore(data.hasMore);
          }
        })
        .catch(failure => {
          if (active) setError(describeError(failure));
        })
        .finally(() => {
          if (active) setBusy(false);
        });
      return () => {
        active = false;
        controller.abort();
      };
      // Session replacement cancels the old read; revision is the explicit refresh trigger.
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [session, auth.sessionId, page, revision]),
  );
  async function open(batchId: string, format: PayslipFormat) {
    const signal = focusLifetime.current?.signal;
    if (openingGuard.current || !signal || signal.aborted) return;
    openingGuard.current = true;
    setOpening(batchId);
    setError('');
    try {
      await PayslipFiles.open(session, batchId, format, signal);
    } catch (failure) {
      if (!signal.aborted) setError(describeError(failure));
    } finally {
      openingGuard.current = false;
      setOpening('');
    }
  }
  return (
    <View style={layout.stack8}>
      <Text style={layout.heading}>Approved payslips</Text>
      <Text style={layout.muted}>
        Salary status and payment status are separate. Open your approved
        breakdown as PDF or Excel.
      </Text>
      {busy ? <Text>Loading payslips…</Text> : null}
      {error ? <Notice tone="error" message={error} /> : null}
      <Button
        title="Refresh payslips"
        secondary
        disabled={busy || Boolean(opening)}
        onPress={() => setRevision(value => value + 1)}
      />
      {!busy && !error && !rows.length ? (
        <Text>No approved payslips for this page.</Text>
      ) : null}
      {rows.map(row => (
        <View style={layout.card} key={text(row.batchId)}>
          <Text style={layout.heading}>{text(row.batchReference)}</Text>
          <InfoRow
            label="Pay period"
            value={payslipPeriod(row.periodStart, row.periodEndExclusive)}
          />
          <InfoRow
            label="Gross earnings"
            value={amount(row.grossAmount, row.currency)}
          />
          <InfoRow
            label="Deductions"
            value={amount(row.deductionAmount, row.currency)}
          />
          <InfoRow
            label="Net salary"
            value={amount(row.netAmount, row.currency)}
          />
          <InfoRow
            label="Payroll status"
            value={payslipStatus(row.payrollState)}
          />
          <InfoRow
            label="Payment status"
            value={
              row.paymentState === 'NotReported'
                ? 'Not reported'
                : text(row.paymentState)
            }
          />
          <Button
            title="Open PDF payslip"
            disabled={Boolean(opening)}
            onPress={() => open(text(row.batchId), 'pdf')}
          />
          <Button
            title="Open Excel payslip"
            secondary
            disabled={Boolean(opening)}
            onPress={() => open(text(row.batchId), 'xlsx')}
          />
        </View>
      ))}
      <Button
        title="Previous payslip page"
        secondary
        disabled={page <= 1 || busy || Boolean(opening)}
        onPress={() => setPage(value => value - 1)}
      />
      <Button
        title="Next payslip page"
        secondary
        disabled={!hasMore || busy || Boolean(opening)}
        onPress={() => setPage(value => value + 1)}
      />
    </View>
  );
}
