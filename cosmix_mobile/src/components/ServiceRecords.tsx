import React, { useCallback, useRef, useState } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { ActivityIndicator, Text, View } from 'react-native';
import { useServices, useSession } from '../app/AppProvider';
import { Button, Field, Notice, Screen } from './Controls';
import { DateField, InfoRow, ReasonSheet, RecordCard } from './ServiceControls';
import { ServiceSummary } from './ServiceSummary';
import { LeaveBalancePanel } from './LeaveBalancePanel';
import { RequestFeedback } from './RequestFeedback';
import { PaymentHistoryCard } from './PaymentHistoryCard';
import { useServiceResource } from '../hooks/useServiceResource';
import { useServiceMutation } from '../hooks/useServiceMutation';
import { allowanceTitle, serviceTitle } from '../constants/services';
import type { DataRecord, RequestKind, ServiceKey } from '../types/selfService';
import { AppError, describeError } from '../utils/errors';
import {
  amount,
  businessToday,
  isPending,
  numeric,
  reason,
  record,
  records,
  requireDate,
  status,
  text,
} from '../utils/serviceValues';
import { isGuid } from '../utils/validation';
import { fullLocalTime, localTime, outcomeLabel } from '../utils/presentation';
import { colors, layout } from '../theme/theme';

const actions: Partial<
  Record<ServiceKey, { kind: RequestKind; title: string }[]>
> = {
  leaves: [{ kind: 'leave', title: 'Apply for leave' }],
  loans: [
    { kind: 'advance', title: 'Salary advance' },
    { kind: 'loan', title: 'Loan request' },
  ],
  resignation: [{ kind: 'resignation', title: 'Request resignation' }],
  allowances: [{ kind: 'allowance', title: 'New allowance claim' }],
  vehicles: [
    { kind: 'trip', title: 'Record work trip' },
    { kind: 'fuel', title: 'Claim fuel' },
    { kind: 'maintenance', title: 'Maintenance claim' },
    { kind: 'incident', title: 'Report incident' },
  ],
  corrections: [{ kind: 'correction', title: 'Request attendance review' }],
  overtime: [{ kind: 'overtime', title: 'Request overtime' }],
  encashment: [{ kind: 'encashment', title: 'Request encashment' }],
};
const ranged = new Set<ServiceKey>([
  'leaves',
  'allowances',
  'corrections',
  'overtime',
  'punches',
]);
const paged = new Set<ServiceKey>([
  'leaves',
  'loans',
  'resignation',
  'allowances',
  'vehicles',
  'corrections',
  'overtime',
  'notifications',
  'punches',
  'payroll',
  'payments',
]);
type Cancellation = { row: DataRecord; route: string; versionName?: string };

export function ServiceRecords({
  service,
  header,
  readOnly = false,
  onRequest,
  onHistory,
}: {
  service: ServiceKey;
  header?: React.ReactNode;
  readOnly?: boolean;
  onRequest?(kind: RequestKind, previousRequestId?: string): void;
  onHistory?(service: ServiceKey): void;
}): React.JSX.Element {
  const { session, selfService } = useServices();
  const today = businessToday();
  const auth = useSession();
  const leaveScheduleAccess = auth.permissions.some(
    grant => grant.permission === 'attendance.rosters.self',
  );
  const [from, setFrom] = useState(today.slice(0, 8) + '01');
  const [to, setTo] = useState(today);
  const [date, setDate] = useState(today);
  const [year, setYear] = useState(today.slice(0, 4));
  const [month, setMonth] = useState(today.slice(5, 7));
  const [query, setQuery] = useState({
    from,
    to,
    date,
    year: Number(year),
    month: Number(month),
    page: 1,
    cursor: undefined as string | undefined,
  });
  const data = useServiceResource(service, query);
  const refresh = async () => {
    clearDetails();
    await data.reload();
  };
  const mutation = useServiceMutation(refresh);
  const [cancel, setCancel] = useState<Cancellation | null>(null);
  const [error, setError] = useState('');
  const [reading, setReading] = useState('');
  const [expanded, setExpanded] = useState<DataRecord | null>(null);
  const [detailBusy, setDetailBusy] = useState(false);
  const [showBalance, setShowBalance] = useState(false);
  const detailEpoch = useRef(0);
  const clearDetails = useCallback(() => {
    detailEpoch.current++;
    setExpanded(null);
    setDetailBusy(false);
  }, []);
  useFocusEffect(useCallback(() => clearDetails, [clearDetails]));
  const root = data.data?.root;
  const rows = data.data?.rows ?? [];
  const filter = () => {
    try {
      requireDate(from);
      requireDate(to);
      requireDate(date);
      if (
        from > to ||
        Date.parse(to) - Date.parse(from) > 365 * 86400000 ||
        !/^\d{4}$/.test(year) ||
        Number(year) < 1900 ||
        Number(year) > 9999 ||
        !/^\d{1,2}$/.test(month) ||
        Number(month) < 1 ||
        Number(month) > 12
      ) {
        throw new AppError('invalid_date_range');
      }
      setError('');
      clearDetails();
      setQuery({
        from,
        to,
        date,
        year: Number(year),
        month: Number(month),
        page: 1,
        cursor: undefined,
      });
    } catch (failure) {
      setError(describeError(failure));
    }
  };
  const cancellation = (row: DataRecord): Cancellation | null => {
    if (!isGuid(row.id)) {
      return null;
    }
    if (service === 'leaves' && (row.state === 1 || row.state === 'Approved')) {
      if (
        Array.isArray(row.cancellations) &&
        records(row.cancellations).some(item => isPending(item.state))
      ) {
        return null;
      }
      return {
        row,
        route: `/api/v1/leaves/mine/${row.id}/cancellations`,
        versionName: 'expectedApprovalVersion',
      };
    }
    if (!isPending(row.state)) {
      return null;
    }
    if (service === 'resignation' && row.requestedStatus !== 'Resigned') {
      return null;
    }
    if (service === 'loans') {
      return {
        row,
        route: `/api/v1/me/advances-loans/requests/${row.id}/cancel`,
      };
    }
    const base = {
      resignation: '/api/v1/me/resignation',
      allowances: '/api/v1/me/allowances/claims',
      corrections: '/api/v1/me/attendance/corrections',
      overtime: '/api/v1/me/overtime/requests',
    }[service as 'resignation' | 'allowances' | 'corrections' | 'overtime'];
    return base
      ? {
          row,
          route: `${base}/${row.id}/cancel`,
          versionName: 'expectedVersion',
        }
      : null;
  };
  const confirm = async (value: string) => {
    if (!cancel) {
      return;
    }
    const body: DataRecord = { reason: reason(value) };
    if (cancel.versionName) {
      const version = cancel.row.version ?? cancel.row.approvalVersion;
      if (typeof version !== 'string') {
        throw new AppError('invalid_server_response');
      }
      body[cancel.versionName] = version;
    }
    await mutation.submit(
      service,
      service === 'leaves'
        ? 'Leave cancellation request'
        : 'Cancel own request',
      cancel.route,
      body,
    );
  };
  const markRead = async (row: DataRecord) => {
    if (reading) {
      return;
    }
    setReading(text(row.id, ''));
    setError('');
    try {
      await selfService.readNotification(
        text(row.id, ''),
        text(row.version, ''),
      );
      await data.reload();
    } catch (failure) {
      setError(describeError(failure));
    } finally {
      setReading('');
    }
  };
  const agreement = async (id: unknown) => {
    if (!isGuid(id) || detailBusy) return;
    setDetailBusy(true);
    setError('');
    const current = ++detailEpoch.current;
    try {
      const value = record(
        await session.request(`/api/v1/me/advances-loans/agreements/${id}`),
      );
      if (current === detailEpoch.current) setExpanded(value);
    } catch (failure) {
      if (current === detailEpoch.current) setError(describeError(failure));
    } finally {
      if (current === detailEpoch.current) setDetailBusy(false);
    }
  };
  const detail = async (row: DataRecord) => {
    if (!isGuid(row.id) || detailBusy) return;
    const bases: Partial<Record<ServiceKey, string>> = {
      leaves: '/api/v1/leaves/mine',
      corrections: '/api/v1/me/attendance/corrections',
      allowances: '/api/v1/me/allowances/claims',
      overtime: '/api/v1/me/overtime/requests',
    };
    const base = bases[service];
    if (!base) return;
    setDetailBusy(true);
    setError('');
    const current = ++detailEpoch.current;
    try {
      const value = record(await session.request(`${base}/${row.id}`));
      if (current === detailEpoch.current) setExpanded(value);
    } catch (failure) {
      if (current === detailEpoch.current) setError(describeError(failure));
    } finally {
      if (current === detailEpoch.current) setDetailBusy(false);
    }
  };
  const more = () => {
    if (!root) {
      return false;
    }
    if (service === 'overtime') {
      return typeof root.nextCursor === 'string' && root.nextCursor.length > 0;
    }
    if (service === 'vehicles') {
      return ['trips', 'fuelClaims', 'maintenance', 'incidents'].some(
        key => root[key] && record(root[key]).hasMore === true,
      );
    }
    if (service === 'loans') {
      return ['requestHistory', 'agreementHistory'].some(
        key => root[key] && record(root[key]).hasMore === true,
      );
    }
    const page =
      service === 'resignation' && root.requests ? record(root.requests) : root;
    return (
      page.hasMore === true ||
      (numeric(page.totalCount) !== null &&
        query.page * 20 < Number(page.totalCount))
    );
  };
  const title = (row: DataRecord) =>
    service === 'leaves'
      ? text(row.typeName, 'Leave request')
      : service === 'vehicles'
      ? text(row.title)
      : service === 'notifications'
      ? text(row.title)
      : service === 'calendar'
      ? text(row.date ?? row.businessDate)
      : service === 'payroll'
      ? text(row.batchReference, 'Payroll summary')
      : service === 'punches'
      ? row.action === 'CheckIn'
        ? 'Check In'
        : row.action === 'CheckOut'
        ? 'Check Out'
        : 'Attendance receipt'
      : service === 'loans'
      ? row.recordKind === 'agreement'
        ? row.kind === 1
          ? 'Advance agreement'
          : 'Loan agreement'
        : row.kind === 1
        ? 'Salary advance'
        : 'Loan request'
      : service === 'resignation'
      ? `${status(row.requestedStatus)} employment request`
      : service === 'encashment'
      ? 'Leave encashment'
      : service === 'overtime'
      ? 'Overtime request'
      : service === 'corrections'
      ? status(row.kind)
      : allowanceTitle(row.kind);
  return (
    <Screen>
      {header}
      <Text style={header ? layout.heading : layout.title}>
        {serviceTitle(service)}
      </Text>
      <Text style={layout.muted}>
        {service === 'profile'
          ? 'Your current employment record, documents and assigned items.'
          : service === 'payments'
          ? 'Your salary payments by payroll period. Only confirmed payments count as paid. Turn the page for earlier periods.'
          : readOnly
          ? service === 'punches'
            ? 'Check In and Check Out times show when the server recorded each receipt, in Asia/Karachi. Change the dates to view older records.'
            : 'Your own company records. Select a date range or turn the page to see older records.'
          : 'Your company records. Only actions available to your account can be submitted.'}
      </Text>
      {ranged.has(service) || service === 'shift' || service === 'calendar' ? (
        <View style={layout.card}>
          {ranged.has(service) ? (
            <>
              <DateField label="From" value={from} onChange={setFrom} />
              <DateField label="To · inclusive" value={to} onChange={setTo} />
            </>
          ) : service === 'shift' ? (
            <DateField label="Business date" value={date} onChange={setDate} />
          ) : (
            <>
              <Field
                label="Year"
                value={year}
                onChangeText={setYear}
                keyboardType="number-pad"
                maxLength={4}
              />
              <Field
                label="Month · 1 to 12"
                value={month}
                onChangeText={setMonth}
                keyboardType="number-pad"
                maxLength={2}
              />
            </>
          )}
          <Button
            title="Apply filter"
            secondary
            disabled={mutation.busy || data.loading}
            onPress={filter}
          />
        </View>
      ) : null}
      {service === 'leaves' && !leaveScheduleAccess ? (
        <Notice
          tone="info"
          message="Your leave history is available. Ask HR to enable your own roster access before applying through the app."
        />
      ) : null}
      {!readOnly &&
        actions[service]?.map(action => (
          <Button
            key={action.kind}
            title={action.title}
            disabled={
              mutation.busy ||
              !!mutation.pending ||
              (action.kind === 'leave' && !leaveScheduleAccess)
            }
            onPress={() => onRequest?.(action.kind)}
          />
        ))}
      {readOnly ? (
        error ? (
          <Notice message={error} />
        ) : null
      ) : (
        <RequestFeedback
          error={mutation.error || error}
          receipt={mutation.receipt}
          pending={mutation.pending}
          busy={mutation.busy}
          onRetry={mutation.retry}
        />
      )}
      {data.error ? <Notice message={data.error} /> : null}
      {data.loading ? <ActivityIndicator color={colors.teal} /> : null}
      {root ? <ServiceSummary service={service} root={root} /> : null}
      {service === 'leaves' ? (
        <>
          <Button
            title={
              showBalance
                ? 'Hide detailed balance'
                : 'View detailed leave balance'
            }
            secondary
            onPress={() => setShowBalance(value => !value)}
          />
          {showBalance ? <LeaveBalancePanel /> : null}
        </>
      ) : null}
      {service === 'finances' && onHistory ? (
        <Button
          title="View full payroll history"
          secondary
          onPress={() => onHistory?.('payroll')}
        />
      ) : null}
      {service === 'payroll' ? (
        <Notice
          tone="info"
          message="These are your company's recorded payroll handoff summaries. A payable amount or prepared batch does not confirm payment. Contact Accounts for payment confirmation."
        />
      ) : null}
      {service === 'loans' && root && Array.isArray(root.activeAgreements) ? (
        <View style={layout.card}>
          <Text style={layout.heading}>Repayment agreements</Text>
          {records(root.activeAgreements).map((row, index) => (
            <View key={index} style={layout.stack8}>
              <InfoRow
                label="Agreement balance"
                value={amount(
                  row.outstandingBalance ?? row.remainingBalance,
                  row.currency,
                )}
              />
              <Button
                title="View repayment schedule & ledger"
                secondary
                busy={detailBusy}
                onPress={() => agreement(row.id ?? row.agreementId)}
              />
            </View>
          ))}
          {!records(root.activeAgreements).length ? (
            <Text style={layout.muted}>No active agreements.</Text>
          ) : null}
        </View>
      ) : null}
      {expanded ? (
        <DetailPanel
          service={service}
          detail={expanded}
          onClose={() => clearDetails()}
        />
      ) : null}
      {service === 'loans' && rows.length > 0 ? (
        <Text style={layout.heading}>Request & agreement history</Text>
      ) : null}
      {rows.map((row, index) => {
        if (service === 'payments') {
          return <PaymentHistoryCard key={text(row.batchId)} initial={row} />;
        }
        const action = readOnly ? null : cancellation(row);
        const displayRow =
          service === 'calendar'
            ? { ...row, status: row.factualStatus }
            : service === 'punches'
            ? { ...row, state: outcomeLabel(text(row.outcome, '')) }
            : service === 'notifications'
            ? { ...row, state: row.readAt ? 'Read' : 'Unread' }
            : row;
        return (
          <RecordCard
            key={text(row.id ?? row.date ?? row.businessDate, String(index))}
            row={displayRow}
            title={title(row)}
          >
            {service === 'notifications' ? (
              <>
                <Text style={layout.body}>{text(row.body)}</Text>
                <InfoRow label="Received" value={text(row.createdAt)} />
                {row.readAt ? (
                  <InfoRow label="Read" value={text(row.readAt)} />
                ) : (
                  <Button
                    title="Mark as read"
                    secondary
                    disabled={!!reading}
                    busy={reading === row.id}
                    onPress={() => markRead(row)}
                  />
                )}
              </>
            ) : (
              <>
                {service === 'leaves' ? (
                  <>
                    <InfoRow
                      label="Dates"
                      value={`${text(row.startDate)} – ${text(row.endDate)}`}
                    />
                    <InfoRow
                      label="Requested minutes"
                      value={String(
                        numeric(row.requestedMinutes) ?? 'Unavailable',
                      )}
                    />
                  </>
                ) : null}
                {service === 'payroll' ? (
                  <>
                    <InfoRow
                      label="Period"
                      value={`${text(row.periodStart)} – ${text(
                        row.periodEnd,
                      )}`}
                    />
                    <InfoRow
                      label="Recorded earnings"
                      value={amount(row.totalEarnings, row.currency)}
                    />
                    <InfoRow
                      label="Recorded deductions"
                      value={amount(row.totalDeductions, row.currency)}
                    />
                    <InfoRow
                      label="Net payable"
                      value={amount(row.netPayable, row.currency)}
                    />
                    <InfoRow
                      label="Prepared · Asia/Karachi"
                      value={localTime(
                        text(row.preparedAt, ''),
                        'Asia/Karachi',
                      )}
                    />
                  </>
                ) : null}
                {service === 'punches' ? (
                  <>
                    <InfoRow
                      label="Business date"
                      value={text(row.businessDate)}
                    />
                    <InfoRow
                      label="Recorded · Asia/Karachi"
                      value={fullLocalTime(
                        text(row.receivedAtUtc, ''),
                        'Asia/Karachi',
                      )}
                    />
                    <InfoRow
                      label="Server result"
                      value={outcomeLabel(text(row.outcome, ''))}
                    />
                    {row.outcome !== 'Accepted' ? (
                      <Text style={layout.muted}>
                        This receipt does not confirm ordinary accepted
                        attendance. Contact your supervisor if review is needed.
                      </Text>
                    ) : null}
                  </>
                ) : null}
                {service === 'loans' ? (
                  <>
                    <InfoRow
                      label={
                        row.recordKind === 'agreement'
                          ? 'Approved principal'
                          : 'Requested amount'
                      }
                      value={amount(
                        row.recordKind === 'agreement'
                          ? row.approvedPrincipal
                          : row.requestedAmount,
                        row.currency,
                      )}
                    />
                    <InfoRow
                      label={
                        row.recordKind === 'agreement'
                          ? 'Effective on'
                          : 'Requested on'
                      }
                      value={text(row.requestedOn ?? row.effectiveOn)}
                    />
                    {row.recordKind === 'agreement' ? (
                      <>
                        <InfoRow
                          label="Recorded repayments"
                          value={amount(row.totalRepaid, row.currency)}
                        />
                        <InfoRow
                          label="Outstanding balance"
                          value={amount(row.outstandingBalance, row.currency)}
                        />
                        <Button
                          title="View agreement schedule & ledger"
                          secondary
                          busy={detailBusy}
                          onPress={() => agreement(row.id)}
                        />
                      </>
                    ) : null}
                    {isGuid(row.agreementId) ? (
                      <Button
                        title="View repayment details"
                        secondary
                        busy={detailBusy}
                        onPress={() => agreement(row.agreementId)}
                      />
                    ) : null}
                  </>
                ) : null}
                {service === 'resignation' ? (
                  <InfoRow
                    label="Requested effective time"
                    value={text(row.requestedEffectiveAt)}
                  />
                ) : null}
                {['allowances', 'encashment'].includes(service) ? (
                  <>
                    {service === 'allowances' ? (
                      <InfoRow
                        label="Payable date"
                        value={text(row.payableDate)}
                      />
                    ) : (
                      <InfoRow
                        label="Requested minutes"
                        value={String(
                          numeric(row.requestedMinutes) ?? 'Unavailable',
                        )}
                      />
                    )}
                    <InfoRow
                      label={
                        service === 'encashment'
                          ? 'Proposed valuation'
                          : 'Claimed amount'
                      }
                      value={amount(
                        row.claimedAmount ?? row.proposedAmount,
                        row.currency,
                      )}
                    />
                    {row.settlementState ? (
                      <InfoRow
                        label="Settlement"
                        value={status(row.settlementState)}
                      />
                    ) : null}
                  </>
                ) : null}
                {service === 'vehicles' ? (
                  <>
                    <InfoRow label="Recorded" value={text(row.atUtc)} />
                    {row.registrationNumber ? (
                      <InfoRow
                        label="Vehicle"
                        value={text(row.registrationNumber)}
                      />
                    ) : null}
                    {row.amount !== null && row.amount !== undefined ? (
                      <InfoRow
                        label="Claimed"
                        value={amount(row.amount, row.currency)}
                      />
                    ) : null}
                  </>
                ) : null}
                {service === 'overtime' ? (
                  <>
                    <InfoRow label="Duty date" value={text(row.businessDate)} />
                    <InfoRow
                      label="Requested minutes"
                      value={String(
                        numeric(row.requestedMinutes) ?? 'Unavailable',
                      )}
                    />
                    <InfoRow
                      label="Approved minutes"
                      value={String(
                        numeric(row.approvedMinutes) ?? 'Awaiting decision',
                      )}
                    />
                    <InfoRow
                      label="Financial evaluation"
                      value={status(row.financialState)}
                    />
                  </>
                ) : null}
                {service === 'corrections' ? (
                  <>
                    <InfoRow label="Duty date" value={text(row.businessDate)} />
                    <InfoRow
                      label="Attendance effect"
                      value={
                        row.changesAttendance === true
                          ? 'Approved revision applied'
                          : 'No applied attendance change'
                      }
                    />
                  </>
                ) : null}
                {service === 'calendar' ? (
                  <>
                    <InfoRow
                      label="Factual status"
                      value={status(row.factualStatus)}
                    />
                    <InfoRow
                      label="Worked minutes"
                      value={String(
                        numeric(
                          row.facts ? record(row.facts).workedMinutes : null,
                        ) ?? 'Unavailable',
                      )}
                    />
                    <InfoRow
                      label="Company assessment"
                      value={
                        row.policy
                          ? attendanceDay(record(row.policy).dayStatus)
                          : 'Unavailable'
                      }
                    />
                  </>
                ) : null}
                {typeof row.reason === 'string' ? (
                  <Text style={layout.body}>{row.reason}</Text>
                ) : null}
                {['leaves', 'corrections', 'allowances', 'overtime'].includes(
                  service,
                ) ? (
                  <Button
                    title="View request details"
                    secondary
                    busy={detailBusy}
                    onPress={() => detail(row)}
                  />
                ) : null}
                {!readOnly &&
                service === 'leaves' &&
                (row.state === 2 || row.state === 'Returned') &&
                !row.supersededByRequestId ? (
                  <Button
                    title="Revise & resubmit"
                    secondary
                    disabled={
                      mutation.busy ||
                      !!mutation.pending ||
                      !leaveScheduleAccess
                    }
                    onPress={() => onRequest?.('leave', text(row.id, ''))}
                  />
                ) : null}
                {action ? (
                  <Button
                    title={
                      service === 'leaves'
                        ? 'Request cancellation'
                        : 'Cancel pending request'
                    }
                    secondary
                    disabled={mutation.busy || !!mutation.pending}
                    onPress={() => setCancel(action)}
                  />
                ) : null}
              </>
            )}
          </RecordCard>
        );
      })}
      {data.data &&
      rows.length === 0 &&
      !['profile', 'finances', 'requests', 'shift'].includes(service) ? (
        <View style={layout.card}>
          <Text style={layout.muted}>No records in this view.</Text>
        </View>
      ) : null}
      {paged.has(service) && root ? (
        <View style={layout.row}>
          <Button
            title={service === 'overtime' ? 'First page' : 'Previous'}
            secondary
            disabled={query.page === 1 || data.loading || mutation.busy}
            onPress={() => {
              clearDetails();
              setQuery(current => ({
                ...current,
                page: service === 'overtime' ? 1 : current.page - 1,
                cursor: undefined,
              }));
            }}
          />
          <Text style={layout.muted}>{query.page}</Text>
          <Button
            title="Next"
            secondary
            disabled={!more() || data.loading || mutation.busy}
            onPress={() => {
              clearDetails();
              setQuery(current => ({
                ...current,
                page: current.page + 1,
                cursor:
                  service === 'overtime'
                    ? text(root.nextCursor, '')
                    : undefined,
              }));
            }}
          />
        </View>
      ) : null}
      <Button
        title="Refresh records"
        secondary
        busy={data.loading}
        disabled={mutation.busy}
        onPress={refresh}
      />
      <ReasonSheet
        title={
          service === 'leaves'
            ? 'Request leave cancellation'
            : 'Cancel your pending request'
        }
        visible={!!cancel}
        busy={mutation.busy}
        onClose={() => setCancel(null)}
        onConfirm={confirm}
      />
    </Screen>
  );
}

function DetailPanel({
  service,
  detail,
  onClose,
}: {
  service: ServiceKey;
  detail: DataRecord;
  onClose(): void;
}): React.JSX.Element {
  const row = detail.request
    ? record(detail.request)
    : detail.claim
    ? record(detail.claim)
    : detail;
  return (
    <View style={layout.card}>
      <Text style={layout.heading}>Record details</Text>
      {typeof row.reason === 'string' ? (
        <InfoRow label="Reason" value={row.reason} />
      ) : null}
      {typeof detail.writtenEvidence === 'string' ? (
        <InfoRow label="Written evidence" value={detail.writtenEvidence} />
      ) : null}
      {service === 'loans' ? (
        <>
          <InfoRow
            label="Approved principal"
            value={amount(detail.approvedPrincipal, detail.currency)}
          />
          <InfoRow
            label="Outstanding balance"
            value={amount(detail.outstandingBalance, detail.currency)}
          />
          <Text style={layout.label}>Repayment schedule</Text>
          {Array.isArray(detail.schedule)
            ? records(detail.schedule).map((entry, index) => (
                <InfoRow
                  key={index}
                  label={`${text(entry.dueDate)} · ${
                    entry.isSettled === true ? 'Settled' : 'Unsettled'
                  }`}
                  value={`${amount(
                    entry.totalDue,
                    entry.currency,
                  )} · recorded paid ${amount(
                    entry.paidAmount,
                    entry.currency,
                  )}`}
                />
              ))
            : null}
          <Text style={layout.label}>Ledger</Text>
          {Array.isArray(detail.ledger)
            ? records(detail.ledger).map((entry, index) => (
                <InfoRow
                  key={index}
                  label={text(entry.valueDate)}
                  value={amount(entry.amount, entry.currency)}
                />
              ))
            : null}
        </>
      ) : null}
      {service === 'leaves' ? (
        <>
          <InfoRow
            label="Leave treatment"
            value={
              row.treatment === 1
                ? 'Paid'
                : row.treatment === 2
                ? 'Unpaid'
                : row.treatment === 3
                ? 'Does not reduce regular leave'
                : 'Awaiting company decision'
            }
          />
          {Array.isArray(row.days)
            ? records(row.days).map((entry, index) => (
                <InfoRow
                  key={index}
                  label={text(entry.date)}
                  value={`${text(entry.startsAtUtc)} → ${text(
                    entry.endsAtUtc,
                  )}`}
                />
              ))
            : null}
          {Array.isArray(row.cancellations)
            ? records(row.cancellations).map((entry, index) => (
                <InfoRow
                  key={index}
                  label="Cancellation request"
                  value={`${status(entry.state)} · ${text(entry.reason)}`}
                />
              ))
            : null}
        </>
      ) : null}
      {service === 'overtime' ? (
        <InfoRow
          label="Requested interval"
          value={`${text(row.startsAtUtc)} → ${text(row.endsAtUtc)}`}
        />
      ) : null}
      {service === 'corrections' ? (
        <>
          <InfoRow label="Requested time" value={text(row.requestedTimeUtc)} />
          <InfoRow
            label="Requested end"
            value={text(row.requestedEndUtc, 'Not requested')}
          />
        </>
      ) : null}
      {Array.isArray(row.decisions)
        ? records(row.decisions).map((entry, index) => (
            <InfoRow
              key={index}
              label={status(entry.decision ?? entry.state)}
              value={text(entry.reason)}
            />
          ))
        : null}
      <Button title="Close details" secondary onPress={onClose} />
    </View>
  );
}

function attendanceDay(value: unknown): string {
  const names = [
    '',
    'Unverified',
    'Present',
    'Absent',
    'Half day',
    'Hourly',
    'Leave',
    'Holiday',
    'Rest day',
    'Incomplete',
  ];
  return typeof value === 'number' &&
    Number.isInteger(value) &&
    value > 0 &&
    value < names.length
    ? names[value]
    : 'Unavailable';
}
