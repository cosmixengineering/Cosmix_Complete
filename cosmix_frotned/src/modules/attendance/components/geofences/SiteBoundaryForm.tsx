import { useState } from 'react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import {
  createGeofenceSchema,
  geofenceConfiguration,
  geofenceWrite,
} from '../../schemas/site-geofences';
import { businessToday } from '../../utils/messages';
import { nextDate } from '../../utils/shift-schedule';
import { validateGeofence } from '../../utils/geofence-input';
import { MutationForm } from '../SchedulingUi';
import { CirclePreview } from './CirclePreview';

export function SiteBoundaryForm({
  configuration,
  latestDate,
  onSaved,
  onCancel,
}: {
  configuration: z.infer<typeof geofenceConfiguration>;
  latestDate?: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(configuration);
  const [latest] = useState(latestDate);
  const [effectiveFrom, setDate] = useState('');
  const [latitude, setLatitude] = useState(source.boundary?.latitude.toString() ?? '');
  const [longitude, setLongitude] = useState(source.boundary?.longitude.toString() ?? '');
  const [radius, setRadius] = useState(source.boundary?.radiusMeters.toString() ?? '');
  const [accuracy, setAccuracy] = useState(source.boundary?.maximumAccuracyMeters.toString() ?? '');
  const [approved, setApproved] = useState(false);
  const [reference, setReference] = useState('');
  const [inputReason, setReason] = useState('');
  const today = businessToday();
  function numeric(value: string) {
    return value.trim() === '' ? NaN : Number(value);
  }
  function build() {
    const body = {
      effectiveFrom,
      latitude: numeric(latitude),
      longitude: numeric(longitude),
      radiusMeters: numeric(radius),
      maximumAccuracyMeters: numeric(accuracy),
      expectedSiteVersion: source.siteVersion,
      expectedLatestVersionNumber: source.latestVersionNumber,
      managementApproved: approved,
      approvalReference: reference,
      reason: inputReason,
    };
    const error = validateGeofence(body, businessToday(), latest);
    if (error) throw new Error(error);
    return {
      path: `/hr/sites/${source.siteId}/geofences/versions`,
      method: 'POST' as const,
      schema: geofenceWrite,
      body: createGeofenceSchema.parse(body),
    };
  }
  return (
    <MutationForm
      title="Publish future circular boundary"
      build={build}
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Publish version"
    >
      <p>
        Latest boundary version: {source.latestVersionNumber}. Publication requires an authorized
        site administrator and a documented management approval reference.
      </p>
      <div className="attendance-grid">
        <TextInput
          label="Effective from"
          type="date"
          required
          min={nextDate(latest && latest > today ? latest : today)}
          value={effectiveFrom}
          onChange={(event) => setDate(event.target.value)}
        />
        <TextInput
          label="Latitude"
          type="number"
          required
          min={-90}
          max={90}
          step="0.0000001"
          value={latitude}
          onChange={(event) => setLatitude(event.target.value)}
        />
        <TextInput
          label="Longitude"
          type="number"
          required
          min={-180}
          max={180}
          step="0.0000001"
          value={longitude}
          onChange={(event) => setLongitude(event.target.value)}
        />
        <TextInput
          label="Radius (meters)"
          type="number"
          required
          min={0.01}
          max={99999999.99}
          step="0.01"
          value={radius}
          onChange={(event) => setRadius(event.target.value)}
        />
        <TextInput
          label="Maximum accuracy (meters)"
          type="number"
          required
          min={0.01}
          max={99999999.99}
          step="0.01"
          value={accuracy}
          onChange={(event) => setAccuracy(event.target.value)}
        />
      </div>
      <CirclePreview
        latitude={numeric(latitude)}
        longitude={numeric(longitude)}
        radiusMeters={numeric(radius)}
        maximumAccuracyMeters={numeric(accuracy)}
      />
      <label className="attendance-check">
        <input
          type="checkbox"
          checked={approved}
          required
          onChange={(event) => setApproved(event.target.checked)}
        />
        Management approval is documented for this boundary.
      </label>
      <TextInput
        label="Management approval reference"
        required
        maxLength={500}
        value={reference}
        onChange={(event) => setReference(event.target.value)}
      />
      <TextInput
        label="Reason"
        required
        maxLength={500}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
