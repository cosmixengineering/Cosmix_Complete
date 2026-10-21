import { useState } from 'react';
import { Plus, Send, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextAreaField } from '../finance/FinanceChrome';
import { Denied, QueryState } from '../Feedback';
import { RecordView } from '../RecordView';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import type { VehicleTrip } from '../../schemas/finance';
import {
  explicitNumber,
  inputError,
  requiredId,
  requiredText,
  vehicleManagementPermissions,
  vehicleManagementRoot,
  vehicleAttribution,
  vehicleAttributionContext,
  vehicleAttributionHistory,
  vehicleSegmentRequest,
} from '../../api/vehicle-management';
import {
  useVehicleCommand,
  VehicleCommandStatus,
  VehicleDecisionForm,
  VehicleRefresh,
} from './VehicleManagementShared';

const permission = vehicleManagementPermissions.attribution;
const blankSegment = () => ({
  businessDate: '',
  startedAt: '',
  endedAt: '',
  odometerStartKm: '',
  odometerEndKm: '',
  mapDistanceKm: '',
  policyVersionId: '',
  commuteChoice: '',
  commuteDistanceId: '',
  evidenceReference: '',
});
type SegmentForm = ReturnType<typeof blankSegment>;

export function VehicleTripAttribution({
  trip,
  onChanged,
}: {
  trip: VehicleTrip;
  onChanged: () => void;
}) {
  const access = useFinanceAccess();
  const allowed = access.can(permission);
  const root = vehicleManagementRoot + '/trips/' + trip.id;
  const context = useFinanceQuery(
    root + '/attribution-context',
    vehicleAttributionContext.refine(
      (value) => value.tripId === trip.id && value.employeeId === trip.employeeId,
    ),
    allowed,
  );
  const history = useFinanceQuery(
    root + '/attributions',
    vehicleAttributionHistory.refine((value) => value.every((item) => item.tripId === trip.id)),
    allowed,
  );
  const command = useVehicleCommand(permission);
  const [segments, setSegments] = useState<SegmentForm[]>([]);
  const [evidence, setEvidence] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const ready =
    !context.isPending &&
    !context.isError &&
    context.data &&
    !history.isPending &&
    !history.isError &&
    history.data;
  const canSubmit =
    ready &&
    context.data?.tripState === 'Pending' &&
    !history.data?.some((item) => item.state === 'Pending' || item.state === 'Approved');
  function refresh() {
    void context.refetch();
    void history.refetch();
    onChanged();
  }
  function update(index: number, key: keyof SegmentForm, value: string) {
    setSegments((previous) =>
      previous.map((row, i) => (i === index ? { ...row, [key]: value } : row)),
    );
  }
  async function submit() {
    if (!allowed || command.locked || !canSubmit || !context.data) return;
    setError(null);
    try {
      if (!segments.length) throw new Error('At least one evidenced daily segment is required.');
      function utc(value: string) {
        if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,3})?)?$/.test(value))
          throw new Error('Enter each segment start and end explicitly in UTC.');
        return new Date(value + 'Z').toISOString();
      }
      const sources = segments.map((row) => {
        if (!['none', 'record'].includes(row.commuteChoice))
          throw new Error('Explicitly select the commute evidence treatment for every segment.');
        return vehicleSegmentRequest.parse({
          businessDate: row.businessDate,
          startedAt: utc(row.startedAt),
          endedAt: utc(row.endedAt),
          odometerStartKm: explicitNumber(row.odometerStartKm, 'Start odometer', 3),
          odometerEndKm: explicitNumber(row.odometerEndKm, 'End odometer', 3),
          mapDistanceKm: explicitNumber(row.mapDistanceKm, 'Map distance', 3),
          policyVersionId: requiredId(row.policyVersionId, 'Policy version'),
          commuteDistanceId:
            row.commuteChoice === 'none'
              ? null
              : requiredId(row.commuteDistanceId, 'Commute revision'),
          evidenceReference: requiredText(row.evidenceReference, 'Segment evidence', 500),
        });
      });
      await command.run(
        root + '/attributions',
        {
          expectedTripVersion: context.data.tripVersion,
          expectedLatestRequestId: context.data.latestRequestId,
          segments: sources,
          evidenceReference: requiredText(evidence, 'Attribution evidence', 500),
          reason: requiredText(reason, 'Reason'),
        },
        vehicleAttribution.refine((value) => value.tripId === trip.id),
        refresh,
        refresh,
      );
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  if (!allowed) return <Denied />;
  return (
    <section className="hr-form-section">
      <div className="hr-repeat-heading">
        <h3>Dated attribution: {trip.id}</h3>
        <VehicleRefresh
          busy={context.isFetching || history.isFetching || command.locked}
          onClick={refresh}
        />
      </div>
      <RecordView value={trip} />
      <QueryState query={context}>{context.data && <RecordView value={context.data} />}</QueryState>
      <QueryState query={history}>
        {canSubmit && (
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void submit();
            }}
          >
            <fieldset disabled={command.locked} style={{ minWidth: 0 }}>
              <legend>Explicit daily segments</legend>
              {segments.map((row, index) => (
                <fieldset key={index} className="hr-form-grid" style={{ minWidth: 0 }}>
                  <legend>Segment {index + 1}</legend>
                  <TextInput
                    label="Business date (Pakistan)"
                    required
                    type="date"
                    value={row.businessDate}
                    onChange={(event) => update(index, 'businessDate', event.target.value)}
                  />
                  <TextInput
                    label="Start (UTC)"
                    required
                    type="datetime-local"
                    step="1"
                    value={row.startedAt}
                    onChange={(event) => update(index, 'startedAt', event.target.value)}
                  />
                  <TextInput
                    label="End (UTC)"
                    required
                    type="datetime-local"
                    step="1"
                    value={row.endedAt}
                    onChange={(event) => update(index, 'endedAt', event.target.value)}
                  />
                  {(['odometerStartKm', 'odometerEndKm', 'mapDistanceKm'] as const).map((key) => (
                    <TextInput
                      key={key}
                      label={
                        key === 'mapDistanceKm'
                          ? 'Map distance (km)'
                          : key === 'odometerStartKm'
                            ? 'Start odometer (km)'
                            : 'End odometer (km)'
                      }
                      required
                      type="number"
                      min={0}
                      step="0.001"
                      value={row[key]}
                      onChange={(event) => update(index, key, event.target.value)}
                    />
                  ))}
                  <TextInput
                    label="Exact policy version ID"
                    required
                    value={row.policyVersionId}
                    onChange={(event) => update(index, 'policyVersionId', event.target.value)}
                  />
                  <SelectInput
                    label="Retained commute evidence"
                    required
                    value={row.commuteChoice}
                    options={[
                      { value: '', label: 'Choose evidence treatment' },
                      { value: 'none', label: 'Explicitly none' },
                      { value: 'record', label: 'Exact retained revision' },
                    ]}
                    onChange={(event) => update(index, 'commuteChoice', event.target.value)}
                  />
                  {row.commuteChoice === 'record' && (
                    <TextInput
                      label="Exact commute revision ID"
                      required
                      value={row.commuteDistanceId}
                      onChange={(event) => update(index, 'commuteDistanceId', event.target.value)}
                    />
                  )}
                  <TextInput
                    label="Segment evidence reference"
                    required
                    maxLength={500}
                    value={row.evidenceReference}
                    onChange={(event) => update(index, 'evidenceReference', event.target.value)}
                  />
                  <Button
                    type="button"
                    variant="secondary"
                    title={'Remove segment ' + (index + 1)}
                    aria-label={'Remove segment ' + (index + 1)}
                    onClick={() =>
                      setSegments((previous) => previous.filter((_, i) => i !== index))
                    }
                  >
                    <Trash2 size={15} />
                  </Button>
                </fieldset>
              ))}
              <Button
                type="button"
                variant="secondary"
                disabled={segments.length >= 366}
                onClick={() => setSegments((previous) => [...previous, blankSegment()])}
              >
                <Plus size={15} /> Add evidenced segment
              </Button>
              <TextInput
                label="Attribution evidence reference"
                required
                maxLength={500}
                value={evidence}
                onChange={(event) => setEvidence(event.target.value)}
              />
              <TextAreaField label="Reason" required value={reason} onChange={setReason} />
              <Button type="submit" disabled={segments.length === 0}>
                <Send size={15} /> Submit attribution
              </Button>
            </fieldset>
          </form>
        )}
        {history.data?.length === 0 && <p>No dated attribution proposals returned.</p>}
        {history.data?.map((item) => (
          <details key={item.requestId} open={item.state === 'Pending'}>
            <summary>
              Attribution revision {item.revision}: {item.state}
            </summary>
            <RecordView value={item} />
            {item.state === 'Pending' && (
              <VehicleDecisionForm
                permission={permission}
                path={root + '/attributions/' + item.requestId + '/review'}
                schema={vehicleAttribution}
                version={item.version}
                requestedByAccountId={item.requestedByAccountId}
                employeeId={trip.employeeId}
                onSuccess={refresh}
                onRejected={refresh}
              />
            )}
          </details>
        ))}
      </QueryState>
      {error && <p role="alert">{error}</p>}
      <VehicleCommandStatus command={command} />
    </section>
  );
}
