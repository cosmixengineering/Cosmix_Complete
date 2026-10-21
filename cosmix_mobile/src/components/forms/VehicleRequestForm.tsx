import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Button, Field, Notice } from '../Controls';
import { ChoiceField, DateField } from '../ServiceControls';
import { SubmissionFrame } from './SubmissionFrame';
import { useSource } from '../../hooks/useSource';
import type { DataRecord, RequestKind } from '../../types/selfService';
import {
  businessToday,
  companyInstant,
  moneyInput,
  reason,
  record,
  records,
  text,
} from '../../utils/serviceValues';
import { AppError } from '../../utils/errors';
import { layout } from '../../theme/theme';

type Kind = Extract<RequestKind, 'trip' | 'fuel' | 'maintenance' | 'incident'>;
const labels = {
  trip: 'Work trip',
  fuel: 'Fuel claim',
  maintenance: 'Maintenance claim',
  incident: 'Vehicle incident',
};
const routes = {
  trip: 'trips',
  fuel: 'fuel-claims',
  maintenance: 'maintenance',
  incident: 'incidents',
};
export function VehicleRequestForm({
  kind,
}: {
  kind: Kind;
}): React.JSX.Element {
  const [selected, setSelected] = useState('');
  const [trip, setTrip] = useState('');
  const [page, setPage] = useState(1);
  const custody = useSource<DataRecord[]>('/api/v1/me/vehicles/custody');
  const history = useSource<DataRecord>(
    kind === 'fuel'
      ? `/api/v1/me/vehicles/activity?page=${page}&pageSize=20`
      : null,
  );
  const [date, setDate] = useState(businessToday);
  const [endDate, setEndDate] = useState(businessToday);
  const [values, setValues] = useState<Record<string, string>>({
    purpose: '1',
    serviceKind: '1',
  });
  const rows = custody.value ? records(custody.value) : [];
  const assigned = rows.find(row => row.id === selected);
  const trips = history.value ? records(record(history.value.trips).items) : [];
  const chosen = trips.find(row => row.id === trip && row.state === 'Approved');
  const update = (name: string, value: string) =>
    setValues(current => ({ ...current, [name]: value }));
  const str = (name: string, max = 500) => reason(values[name] ?? '', max);
  const number = (name: string, max = 1000000) =>
    moneyInput(values[name] ?? '', 0, max);
  return (
    <SubmissionFrame
      service="vehicles"
      title={labels[kind]}
      description="Submit your own vehicle activity with supporting references. Your company reviews eligibility, mileage and claims."
      route={`/api/v1/me/vehicles/${routes[kind]}`}
      revision={JSON.stringify([
        kind,
        selected,
        trip,
        date,
        endDate,
        values,
        assigned,
        chosen,
      ])}
      ready={kind === 'fuel' ? !!chosen : !!assigned}
      build={async () => {
        const summary = [labels[kind]];
        let body: DataRecord;
        if (kind === 'fuel') {
          if (!chosen) {
            throw new AppError('source_unavailable');
          }
          body = {
            vehicleTripId: chosen.id,
            currency: 'PKR',
            receiptReference: values.receipt?.trim() || null,
          };
          summary.push(
            text(chosen.title),
            'Amount and fuel quantity are calculated by the server.',
          );
        } else {
          if (!assigned) {
            throw new AppError('source_unavailable');
          }
          summary.push(text(assigned.registrationNumber), date);
          if (kind === 'trip') {
            const startedAt = companyInstant(date, str('start', 5));
            const endedAt = companyInstant(endDate, str('end', 5));
            const odometerStartKm = number('odometerStart');
            const odometerEndKm = number('odometerEnd');
            if (
              Date.parse(endedAt) <= Date.parse(startedAt) ||
              odometerEndKm < odometerStartKm
            ) {
              throw new AppError('invalid_duty_interval');
            }
            body = {
              vehicleId: assigned.vehicleId,
              purpose: Number(values.purpose),
              startedAt,
              endedAt,
              originLabel: str('origin', 200),
              destinationLabel: str('destination', 200),
              odometerStartKm,
              odometerEndKm,
              mapDistanceKm: number('mapDistance', 50000),
              routeEvidenceReference: str('evidence'),
              workReference: str('work', 150),
            };
            summary.push(
              `${body.originLabel} → ${body.destinationLabel}`,
              `${date} ${values.start} → ${endDate} ${values.end} · Asia/Karachi`,
              `Odometer ${odometerStartKm} → ${odometerEndKm} km`,
              `Map distance ${body.mapDistanceKm} km`,
              str('evidence'),
              str('work', 150),
            );
          } else if (kind === 'maintenance') {
            body = {
              vehicleId: assigned.vehicleId,
              serviceKind: Number(values.serviceKind),
              serviceDate: date,
              odometerKm: number('odometer'),
              approvedOutstationKm: number('outstation', 50000),
              authorizedWorkshopReference: str('workshop', 200),
              evidenceReference: str('evidence'),
              claimedAmount: number('amount', 10000000),
              currency: 'PKR',
            };
            summary.push(
              `Requested service: ${
                values.serviceKind === '1'
                  ? 'Tuning'
                  : values.serviceKind === '2'
                  ? 'Engine overhaul'
                  : 'Other'
              }`,
              `Claim: ${body.claimedAmount} PKR`,
              `Odometer: ${body.odometerKm} km`,
              `Reported outstation distance: ${body.approvedOutstationKm} km`,
              str('workshop', 200),
              str('evidence'),
              'The server calculates verified approved mileage separately.',
            );
          } else {
            body = {
              vehicleCustodyId: assigned.id,
              occurredAt: companyInstant(date, str('start', 5)),
              incidentEvidenceReference: str('evidence'),
              thirdPartyDetailsReference: values.thirdParty?.trim() || null,
              insuranceClaimReference: values.insurance?.trim() || null,
              proposedEmployeeLiability: null,
              currency: null,
            };
            summary.push(
              `${date} ${values.start} · Asia/Karachi`,
              str('evidence'),
              'Financial liability requires a separate company decision.',
            );
          }
        }
        return { body, summary };
      }}
    >
      {disabled => {
        const input = (
          name: string,
          label: string,
          max = 500,
          decimal = false,
        ) => (
          <Field
            key={name}
            label={label}
            value={values[name] ?? ''}
            onChangeText={value => update(name, value)}
            editable={!disabled}
            maxLength={max}
            keyboardType={decimal ? 'decimal-pad' : 'default'}
          />
        );
        return (
          <View style={layout.card}>
            {custody.error || history.error ? (
              <Notice message={custody.error || history.error} />
            ) : null}
            {kind === 'fuel' ? (
              <>
                <ChoiceField
                  label="Your approved trip"
                  value={trip}
                  onChange={setTrip}
                  disabled={disabled}
                  choices={trips
                    .filter(row => row.state === 'Approved')
                    .map(row => ({
                      value: text(row.id, ''),
                      label: `${text(row.title)} · ${text(row.atUtc)}`,
                    }))}
                />
                <Button
                  title="Previous trips"
                  secondary
                  disabled={page === 1 || disabled}
                  onPress={() => {
                    setTrip('');
                    setPage(current => current - 1);
                  }}
                />
                <Button
                  title="More trips"
                  secondary
                  disabled={
                    disabled ||
                    !history.value ||
                    record(history.value.trips).hasMore !== true
                  }
                  onPress={() => {
                    setTrip('');
                    setPage(current => current + 1);
                  }}
                />
                {input('receipt', 'Receipt reference · optional')}
                <Text style={layout.muted}>
                  PKR · approved mileage and published fuel policy determine the
                  claim.
                </Text>
              </>
            ) : (
              <>
                <ChoiceField
                  label="Your assigned vehicle"
                  value={selected}
                  onChange={setSelected}
                  disabled={disabled}
                  choices={rows.map(row => ({
                    value: text(row.id, ''),
                    label: `${text(row.registrationNumber)} · ${
                      row.isActive === true ? 'Current' : 'Previous assignment'
                    }`,
                  }))}
                />
                <DateField
                  label={
                    kind === 'maintenance'
                      ? 'Service date'
                      : kind === 'incident'
                      ? 'Incident date'
                      : 'Trip starts on'
                  }
                  value={date}
                  onChange={value => {
                    setDate(value);
                    setEndDate(value);
                  }}
                  disabled={disabled}
                />
                {kind === 'trip' ? (
                  <>
                    <ChoiceField
                      label="Trip purpose"
                      value={values.purpose}
                      onChange={value => update('purpose', value)}
                      disabled={disabled}
                      choices={[
                        'Site to site',
                        'Complaint visit',
                        'Field visit',
                        'Outstation',
                        'Commute',
                      ].map((label, index) => ({
                        value: String(index + 1),
                        label,
                      }))}
                    />
                    {input('start', 'Start time · HH:mm, Asia/Karachi', 5)}
                    <DateField
                      label="Trip ends on"
                      value={endDate}
                      onChange={setEndDate}
                      disabled={disabled}
                    />
                    {input('end', 'End time · HH:mm, Asia/Karachi', 5)}
                    {input('origin', 'Origin', 200)}
                    {input('destination', 'Destination', 200)}
                    {input('odometerStart', 'Odometer at start · km', 12, true)}
                    {input('odometerEnd', 'Odometer at end · km', 12, true)}
                    {input('mapDistance', 'Map distance · km', 12, true)}
                    {input('work', 'Work / job reference', 150)}
                  </>
                ) : kind === 'maintenance' ? (
                  <>
                    <ChoiceField
                      label="Service"
                      value={values.serviceKind}
                      onChange={value => update('serviceKind', value)}
                      disabled={disabled}
                      choices={[
                        { value: '1', label: 'Tuning' },
                        { value: '2', label: 'Engine overhaul' },
                        { value: '3', label: 'Other' },
                      ]}
                    />
                    {input('odometer', 'Odometer · km', 12, true)}
                    {input(
                      'outstation',
                      'Reported approved outstation distance · km',
                      12,
                      true,
                    )}
                    {input('workshop', 'Authorized workshop reference', 200)}
                    {input('amount', 'Claimed amount · PKR', 12, true)}
                  </>
                ) : (
                  <>
                    {input('start', 'Time · HH:mm, Asia/Karachi', 5)}
                    {input(
                      'thirdParty',
                      'Third party details reference · optional',
                    )}
                    {input(
                      'insurance',
                      'Insurance claim reference · optional',
                      200,
                    )}
                  </>
                )}
                {input(
                  'evidence',
                  kind === 'trip'
                    ? 'Route / journey evidence reference'
                    : 'Supporting evidence reference',
                )}
                <Text style={layout.muted}>
                  Use a real company document or evidence reference. No
                  documents are created by this form.
                </Text>
              </>
            )}
          </View>
        );
      }}
    </SubmissionFrame>
  );
}
