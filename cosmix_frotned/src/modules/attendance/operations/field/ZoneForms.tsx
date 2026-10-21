import { useState } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { MutationForm } from '../../components/SchedulingUi';
import {
  assignInput,
  assignment,
  configuration,
  configureInput,
  createZoneInput,
  effectiveWindow,
  endAssignmentInput,
  publishZoneInput,
  zone,
  zoneVersion,
  type Assignment,
  type Zone,
} from './contracts';

type Completion = { onSaved: (result: unknown) => void; onCancel: () => void };
export function ZoneVersionForm({
  siteId,
  current,
  ...done
}: Completion & { siteId: string; current?: Zone }) {
  const [code, setCode] = useState(''),
    [name, setName] = useState('');
  const latest = current?.versions.reduce((a, b) => (a.version > b.version ? a : b));
  const [type, setType] = useState(latest?.type ?? '');
  const [kind, setKind] = useState('');
  const [lat, setLat] = useState(''),
    [lon, setLon] = useState(''),
    [radius, setRadius] = useState(''),
    [json, setJson] = useState('');
  const [from, setFrom] = useState(''),
    [to, setTo] = useState(''),
    [instruction, setInstruction] = useState(''),
    [reasonCode, setReasonCode] = useState(''),
    [reason, setReason] = useState('');
  const path = `/attendance/sites/${siteId}/zones`;
  return (
    <MutationForm
      title={current ? 'Publish zone boundary' : 'Create zone'}
      {...done}
      build={() => {
        if (!['1', '2'].includes(kind)) throw new Error('Choose a boundary type.');
        if (kind === '1' && [lat, lon, radius].some((s) => s.trim() === ''))
          throw new Error('Complete the circle coordinates and radius.');
        const boundary =
          kind === '1'
            ? {
                kind: 1,
                latitude: Number(lat),
                longitude: Number(lon),
                radiusMeters: Number(radius),
                polygonGeoJson: null,
              }
            : {
                kind: 2,
                latitude: null,
                longitude: null,
                radiusMeters: null,
                polygonGeoJson: json,
              };
        if (
          kind === '1' &&
          (Math.abs(Number(lat)) > 90 ||
            Math.abs(Number(lon)) > 180 ||
            Number(radius) <= 0 ||
            Number(radius) > 100000)
        )
          throw new Error('Circle coordinates or radius are outside supported bounds.');
        if (kind === '2') {
          if (!json || json.length > 32000) throw new Error('Provide a bounded Polygon GeoJSON.');
          const value: unknown = JSON.parse(json);
          if (!value || typeof value !== 'object' || !('type' in value) || value.type !== 'Polygon')
            throw new Error('Provide a Polygon GeoJSON.');
        }
        const fields = {
          type,
          boundary,
          ...effectiveWindow(from, to),
          employeeInstruction: instruction,
          reasonCode,
          reason,
        };
        return current
          ? {
              path: `${path}/${current.id}/versions`,
              method: 'POST',
              body: publishZoneInput.parse({
                ...fields,
                expectedLatestVersion: latest?.version ?? 0,
              }),
              schema: zoneVersion,
            }
          : {
              path,
              method: 'POST',
              body: createZoneInput.parse({ ...fields, code, name }),
              schema: zone,
            };
      }}
    >
      {!current && (
        <>
          <TextInput
            label="Code"
            value={code}
            maxLength={50}
            required
            onChange={(e) => setCode(e.target.value)}
          />
          <TextInput
            label="Name"
            value={name}
            maxLength={150}
            required
            onChange={(e) => setName(e.target.value)}
          />
        </>
      )}
      <SelectInput
        label="Zone type"
        value={type}
        disabled={Boolean(current)}
        onChange={(e) => setType(e.target.value)}
        options={[
          { value: '', label: 'Select type' },
          { value: 'OPERATIONAL', label: 'Operational' },
          { value: 'RED', label: 'Red / restricted' },
        ]}
      />
      <SelectInput
        label="Boundary"
        value={kind}
        onChange={(e) => setKind(e.target.value)}
        options={[
          { value: '', label: 'Select boundary' },
          { value: '1', label: 'Circle' },
          { value: '2', label: 'Polygon' },
        ]}
      />
      {kind === '1' && (
        <div className="attendance-grid">
          <TextInput
            label="Latitude"
            type="number"
            step="any"
            value={lat}
            onChange={(e) => setLat(e.target.value)}
          />
          <TextInput
            label="Longitude"
            type="number"
            step="any"
            value={lon}
            onChange={(e) => setLon(e.target.value)}
          />
          <TextInput
            label="Radius (m)"
            type="number"
            step="any"
            value={radius}
            onChange={(e) => setRadius(e.target.value)}
          />
        </div>
      )}
      {kind === '2' && (
        <label className="field">
          Polygon GeoJSON
          <textarea
            rows={7}
            maxLength={32000}
            style={{ width: '100%' }}
            value={json}
            onChange={(e) => setJson(e.target.value)}
          />
        </label>
      )}
      <TextInput
        label="Effective from (ISO date/time with offset)"
        value={from}
        required
        onChange={(e) => setFrom(e.target.value)}
      />
      <TextInput
        label="Effective to (ISO date/time with offset)"
        value={to}
        required
        onChange={(e) => setTo(e.target.value)}
      />
      <TextInput
        label="Employee instruction"
        value={instruction}
        maxLength={2000}
        required
        onChange={(e) => setInstruction(e.target.value)}
      />
      <TextInput
        label="Reason code"
        value={reasonCode}
        maxLength={100}
        required
        onChange={(e) => setReasonCode(e.target.value)}
      />
      <TextInput
        label="Reason"
        value={reason}
        maxLength={1000}
        required
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}

export function MonitoringForm({
  siteId,
  latestVersion,
  ...done
}: Completion & { siteId: string; latestVersion: number }) {
  const [values, setValues] = useState<Record<string, string>>({});
  const fields = [
    ['maximumAccuracyMeters', 'Maximum accuracy (m)'],
    ['warningDistanceMeters', 'Warning distance (m)'],
    ['minimumIntervalSeconds', 'Minimum interval (seconds)'],
    ['retentionDays', 'Retention (days)'],
    ['maximumMonitoringSeconds', 'Maximum duty duration (seconds)'],
    ['liveVisibilitySeconds', 'Live visibility (seconds)'],
  ];
  function update(key: string, value: string) {
    setValues((v) => ({ ...v, [key]: value }));
  }
  return (
    <MutationForm
      title="Publish monitoring settings"
      {...done}
      build={() => {
        for (const [key] of fields)
          if (!key || !values[key]?.trim())
            throw new Error('All monitoring settings require explicit values.');
        return {
          path: `/attendance/sites/${siteId}/zones/monitoring-configurations`,
          method: 'POST',
          schema: configuration,
          body: configureInput.parse({
            expectedLatestVersion: latestVersion,
            ...effectiveWindow(values.effectiveFromUtc ?? '', values.effectiveToUtc ?? ''),
            ...Object.fromEntries(fields.map(([key]) => [key!, Number(values[key!])])),
            visibleNotice: values.visibleNotice,
            reason: values.reason,
          }),
        };
      }}
    >
      <div className="attendance-grid">
        {fields.map(([key, label]) => (
          <TextInput
            key={key}
            label={label!}
            type="number"
            step={key?.endsWith('Meters') ? 'any' : '1'}
            required
            value={values[key!] ?? ''}
            onChange={(e) => update(key!, e.target.value)}
          />
        ))}
      </div>
      {[
        ['effectiveFromUtc', 'Effective from (ISO date/time with offset)'],
        ['effectiveToUtc', 'Effective to (ISO date/time with offset)'],
        ['visibleNotice', 'Visible employee notice'],
        ['reason', 'Reason'],
      ].map(([key, label]) => (
        <TextInput
          key={key}
          label={label!}
          required
          value={values[key!] ?? ''}
          onChange={(e) => update(key!, e.target.value)}
        />
      ))}
    </MutationForm>
  );
}

export function AssignmentForm({
  siteId,
  current,
  replace,
  end,
  ...done
}: Completion & { siteId: string; current: Zone; replace?: Assignment; end?: Assignment }) {
  const [versionId, setVersionId] = useState(''),
    [population, setPopulation] = useState(String(replace?.populationKind ?? ''));
  const [target, setTarget] = useState(replace?.targetId ?? ''),
    [from, setFrom] = useState(''),
    [to, setTo] = useState(''),
    [reason, setReason] = useState('');
  const path = `/attendance/sites/${siteId}/zones/${current.id}/assignments`;
  return (
    <MutationForm
      title={
        end
          ? 'End zone assignment'
          : replace
            ? 'Replace zone assignment'
            : 'Assign zone / employee restriction'
      }
      {...done}
      build={() =>
        end
          ? {
              path: `${path}/${end.id}/end`,
              method: 'POST',
              schema: assignment,
              body: endAssignmentInput.parse({ effectiveToUtc: to, reason }),
            }
          : {
              path,
              method: 'POST',
              schema: assignment,
              body: assignInput.parse({
                zoneVersionId: versionId,
                populationKind: Number(population),
                targetId: population === '4' ? siteId : target,
                ...effectiveWindow(from, to),
                isEmployeeOverride: false,
                reason,
                replaceAssignmentId: replace?.id ?? null,
              }),
            }
      }
    >
      {!end && (
        <>
          <SelectInput
            label="Pinned zone version"
            value={versionId}
            onChange={(e) => setVersionId(e.target.value)}
            options={[
              { value: '', label: 'Select version' },
              ...current.versions
                .filter((v) => ['OPERATIONAL', 'RED'].includes(v.type))
                .map((v) => ({
                  value: v.id,
                  label: `Version ${v.version}: ${v.type} (${v.effectiveFromUtc} - ${v.effectiveToUtc})`,
                })),
            ]}
          />
          <SelectInput
            label="Population"
            value={population}
            disabled={Boolean(replace)}
            onChange={(e) => {
              setPopulation(e.target.value);
              setTarget('');
            }}
            options={[
              { value: '', label: 'Select population' },
              { value: '1', label: 'Employee' },
              { value: '2', label: 'Role' },
              { value: '3', label: 'Team' },
              { value: '4', label: 'Site' },
            ]}
          />
          {population !== '4' && (
            <TextInput
              label="Target ID"
              value={target}
              disabled={Boolean(replace)}
              required
              onChange={(e) => setTarget(e.target.value)}
            />
          )}
          <TextInput
            label="Effective from (ISO date/time with offset)"
            value={from}
            required
            onChange={(e) => setFrom(e.target.value)}
          />
        </>
      )}
      <TextInput
        label="Effective to (ISO date/time with offset)"
        value={to}
        required
        onChange={(e) => setTo(e.target.value)}
      />
      <TextInput
        label="Reason"
        value={reason}
        maxLength={1000}
        required
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
