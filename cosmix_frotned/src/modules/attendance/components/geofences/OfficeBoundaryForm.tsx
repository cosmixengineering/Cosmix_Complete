import { useState } from 'react';
import type { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import {
  officeConfiguration,
  officeJurisdiction,
  officeReceiptFor,
  validateOfficePublication,
} from '../../schemas/office-boundaries';
import { businessToday } from '../../utils/messages';
import { nextDate } from '../../utils/shift-schedule';
import { MutationForm } from '../SchedulingUi';
import { CirclePreview } from './CirclePreview';

export function OfficeBoundaryForm({
  configuration,
  onSaved,
  onCancel,
}: {
  configuration: z.infer<typeof officeConfiguration>;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(configuration);
  const [effectiveFrom, setDate] = useState('');
  const [latitude, setLatitude] = useState(source.boundary?.latitude.toString() ?? '');
  const [longitude, setLongitude] = useState(source.boundary?.longitude.toString() ?? '');
  const [radius, setRadius] = useState(source.boundary?.radiusMeters.toString() ?? '');
  const [accuracy, setAccuracy] = useState(source.boundary?.maximumAccuracyMeters.toString() ?? '');
  const [jurisdiction, setJurisdiction] = useState(source.boundary?.jurisdictionCode ?? '');
  const [approved, setApproved] = useState(false);
  const [approvalReference, setReference] = useState('');
  const [reason, setReason] = useState('');
  const number = (value: string) => (value.trim() ? Number(value) : NaN);
  function build() {
    const parsed = validateOfficePublication(
      {
        effectiveFrom,
        latitude: number(latitude),
        longitude: number(longitude),
        radiusMeters: number(radius),
        maximumAccuracyMeters: number(accuracy),
        jurisdictionCode: jurisdiction,
        expectedBranchVersion: source.branchVersion,
        expectedLatestVersionNumber: source.latestVersionNumber,
        managementApproved: approved,
        approvalReference,
        reason,
      },
      businessToday(),
    );
    if (!parsed.success) throw parsed.error;
    return {
      path: `/company/offices/${source.branchId}/attendance-boundaries`,
      method: 'POST' as const,
      schema: officeReceiptFor(source.branchId, parsed.data),
      body: parsed.data,
    };
  }
  return (
    <MutationForm
      title="Publish Office Boundary"
      build={build}
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Publish version"
    >
      <div className="attendance-grid">
        <TextInput
          label="Effective from"
          type="date"
          required
          min={nextDate(businessToday())}
          value={effectiveFrom}
          onChange={(e) => setDate(e.target.value)}
        />
        <TextInput
          label="Latitude"
          type="number"
          required
          min={-90}
          max={90}
          step="0.0000001"
          value={latitude}
          onChange={(e) => setLatitude(e.target.value)}
        />
        <TextInput
          label="Longitude"
          type="number"
          required
          min={-180}
          max={180}
          step="0.0000001"
          value={longitude}
          onChange={(e) => setLongitude(e.target.value)}
        />
        <TextInput
          label="Radius (meters)"
          type="number"
          required
          min={0.01}
          max={99999999.99}
          step="0.01"
          value={radius}
          onChange={(e) => setRadius(e.target.value)}
        />
        <TextInput
          label="Maximum GPS accuracy (meters)"
          type="number"
          required
          min={0.01}
          max={99999999.99}
          step="0.01"
          value={accuracy}
          onChange={(e) => setAccuracy(e.target.value)}
        />
        <SelectInput
          label="Jurisdiction"
          required
          value={jurisdiction}
          options={[
            { value: '', label: 'Select approved jurisdiction' },
            ...officeJurisdiction.options.map((value) => ({ value, label: value })),
          ]}
          onChange={(e) => setJurisdiction(e.target.value as typeof jurisdiction)}
        />
      </div>
      <CirclePreview
        latitude={number(latitude)}
        longitude={number(longitude)}
        radiusMeters={number(radius)}
        maximumAccuracyMeters={number(accuracy)}
      />
      <label className="attendance-check">
        <input
          type="checkbox"
          required
          checked={approved}
          onChange={(e) => setApproved(e.target.checked)}
        />
        Management approval is documented.
      </label>
      <TextInput
        label="Approval reference"
        required
        maxLength={500}
        value={approvalReference}
        onChange={(e) => setReference(e.target.value)}
      />
      <TextInput
        label="Reason"
        required
        maxLength={500}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
