import { useState } from 'react';
import { X } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Command, Denied, MutationForm, QueryState } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { leaveTypes } from '../leave-policy/contracts';
import { canAll } from '../leave-policy/inputs';
import { EvidenceRecord, Reload } from '../leave-policy/Ui';
import {
  allocationSources,
  awardIntent,
  benefitImpact,
  benefitImpactPath,
  correctionIntent,
  hasPendingReservation,
  permissions,
  readableLeavePolicies,
  revokeIntent,
  usageNames,
  type Benefit,
  type BenefitImpact,
} from './api';

type FormCallbacks = { onSaved: (result: unknown) => void; onCancel: () => void };
function useCanReview(employeeId: string) {
  const access = useAttendanceAccess();
  return (
    access.ready &&
    canAll(access.user, permissions.benefits) &&
    Boolean(access.user?.account?.employeeId) &&
    access.user?.account?.employeeId !== employeeId
  );
}

export function AwardBenefitForm({
  employeeId,
  onSaved,
  onCancel,
}: FormCallbacks & { employeeId: string }) {
  const access = useAttendanceAccess();
  const canReview = useCanReview(employeeId);
  const canReadSettings = access.ready && canAll(access.user, permissions.configure);
  const types = useAttendanceQuery('/hr/leave-settings/types', leaveTypes, canReadSettings);
  const policies = useAttendanceQuery(
    '/hr/leave-settings/policies',
    readableLeavePolicies,
    canReadSettings,
  );
  const [leaveTypeId, setLeaveTypeId] = useState('');
  const [policyVersionId, setPolicyVersionId] = useState('');
  const [kpiYearFrom, setKpiYearFrom] = useState('');
  const [expectedKpiFinalizationId, setFinalization] = useState('');
  const [inputReason, setReason] = useState('');
  const configuredPolicies =
    policies.data?.filter(
      (row) => row.settings.settingsVersion === 4 && row.settings.benefits != null,
    ) ?? [];
  const selectedPolicy = configuredPolicies.find((row) => row.versionId === policyVersionId);
  const referencesUnavailable =
    canReadSettings &&
    (types.isError ||
      policies.isError ||
      !types.data ||
      !policies.data ||
      types.isFetching ||
      policies.isFetching);
  if (!canReview)
    return (
      <Denied>Independent benefit review requires the All-scoped benefit review grant.</Denied>
    );
  return (
    <MutationForm
      title="Award configured leave benefit"
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Award benefit"
      disabled={referencesUnavailable}
      build={() => {
        if (!canReview || referencesUnavailable)
          throw new Error('Reload authorized benefit references before awarding.');
        if (
          canReadSettings &&
          (!selectedPolicy ||
            !types.data?.some(
              (row) => row.id === leaveTypeId && row.isActive && row.reducesRegularBalance,
            ))
        )
          throw new Error('Select a published benefit policy and an active eligible leave type.');
        return awardIntent(employeeId, {
          leaveTypeId,
          policyVersionId,
          kpiYearFrom,
          expectedKpiFinalizationId,
          reason: inputReason,
        });
      }}
    >
      {canReadSettings ? (
        <>
          <QueryState
            query={types}
            empty={types.data?.length === 0}
            emptyText="No leave types returned."
          />
          <QueryState
            query={policies}
            empty={configuredPolicies.length === 0}
            emptyText="No published v4 benefit policies returned."
          />
          <Reload
            disabled={types.isFetching || policies.isFetching}
            onClick={() => {
              setLeaveTypeId('');
              setPolicyVersionId('');
              void types.refetch();
              void policies.refetch();
            }}
          />
          <SelectInput
            label="Published benefit policy revision"
            value={policyVersionId}
            required
            options={[
              { value: '', label: 'Select policy revision' },
              ...configuredPolicies.map((row) => ({
                value: row.versionId,
                label: `${row.code}: ${row.effectiveFrom} to ${row.effectiveTo ?? 'open'} (${row.versionId})`,
              })),
            ]}
            onChange={(event) => {
              setPolicyVersionId(event.target.value);
              setLeaveTypeId('');
            }}
          />
          <SelectInput
            label="Leave type"
            value={leaveTypeId}
            required
            options={[
              { value: '', label: 'Select leave type' },
              ...(types.data ?? [])
                .filter((row) => row.isActive && row.reducesRegularBalance)
                .map((row) => ({ value: row.id, label: `${row.code}: ${row.name}` })),
            ]}
            onChange={(event) => setLeaveTypeId(event.target.value)}
          />
          {selectedPolicy && (
            <details open>
              <summary>Published policy and benefit settings</summary>
              <EvidenceRecord value={selectedPolicy} />
            </details>
          )}
        </>
      ) : (
        <>
          <p role="status">
            Published reference lists require All-scoped leave configure access. Known references
            remain subject to server validation.
          </p>
          <TextInput
            label="Exact published v4 policy version ID"
            required
            value={policyVersionId}
            onChange={(event) => setPolicyVersionId(event.target.value.trim())}
          />
          <TextInput
            label="Exact leave type ID"
            required
            value={leaveTypeId}
            onChange={(event) => setLeaveTypeId(event.target.value.trim())}
          />
        </>
      )}
      <div className="attendance-grid">
        <TextInput
          label="Source KPI year starts on"
          type="date"
          min="1901-01-01"
          max="9991-12-31"
          required
          value={kpiYearFrom}
          onChange={(event) => setKpiYearFrom(event.target.value)}
        />
        <TextInput
          label="Expected KPI finalization ID"
          required
          value={expectedKpiFinalizationId}
          onChange={(event) => setFinalization(event.target.value.trim())}
        />
      </div>
      <TextInput
        label="Award reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}

export function BenefitChangeForm({
  mode,
  row,
  onSaved,
  onCancel,
}: FormCallbacks & { mode: 'revoke' | 'correct'; row: Benefit }) {
  const canReview = useCanReview(row.employeeId);
  const impact = useAttendanceQuery(
    benefitImpactPath(row),
    benefitImpact,
    canReview && mode === 'correct',
  );
  const [inputReason, setReason] = useState('');
  if (!canReview) return <Denied />;
  if (mode === 'revoke')
    return (
      <MutationForm
        title="Revoke unused benefit"
        onSaved={onSaved}
        onCancel={onCancel}
        submitLabel="Revoke unused award"
        build={() => revokeIntent(row, inputReason)}
      >
        <EvidenceRecord value={row} />
        <TextInput
          label="Revocation reason"
          required
          maxLength={1000}
          value={inputReason}
          onChange={(event) => setReason(event.target.value)}
        />
      </MutationForm>
    );
  return (
    <>
      <QueryState query={impact} />
      {impact.data && !impact.isError && !impact.isFetching ? (
        <CorrectionForm
          key={`${row.id}:${impact.data.usageFingerprint}`}
          row={row}
          impact={impact.data}
          onSaved={onSaved}
          onCancel={onCancel}
        />
      ) : (
        <div className="attendance-commands">
          <Reload disabled={impact.isFetching} onClick={() => void impact.refetch()} />
          <Command icon={X} variant="secondary" onClick={onCancel}>
            Cancel correction
          </Command>
        </div>
      )}
    </>
  );
}

function CorrectionForm({
  row,
  impact,
  onSaved,
  onCancel,
}: FormCallbacks & { row: Benefit; impact: BenefitImpact }) {
  const [source] = useState(() => structuredClone(impact));
  const [allocations, setAllocations] = useState<Record<string, string>>({});
  const [reviewed, setReviewed] = useState(false);
  const [inputReason, setReason] = useState('');
  const candidates = new Set(allocationSources(row, source).map((entry) => entry.id));
  const pendingReservation = hasPendingReservation(source);
  return (
    <MutationForm
      title="Correct benefit with exact usage allocation"
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Record correction"
      disabled={pendingReservation}
      build={() => {
        if (!reviewed)
          throw new Error(
            'Confirm the consumed-source allocation, including an explicit empty allocation.',
          );
        return correctionIntent(
          row,
          source,
          Object.entries(allocations).map(([ledgerEntryId, value]) => ({
            ledgerEntryId,
            minutes: value === '' ? NaN : Number(value),
          })),
          inputReason,
        );
      }}
    >
      <EvidenceRecord
        value={{
          awardId: row.id,
          awardMinutes: row.minutes,
          awardStandardDayMinutes: row.standardDayMinutes,
          evidenceFingerprint: row.evidenceFingerprint,
          usageFingerprint: source.usageFingerprint,
        }}
      />
      {pendingReservation && (
        <p role="alert">
          Pending reservations must complete their existing workflow before correction.
        </p>
      )}
      <fieldset disabled={pendingReservation}>
        <div className="hr-table-scroll">
          <table className="attendance-table">
            <thead>
              <tr>
                <th>Source entry</th>
                <th>Type / signed minutes</th>
                <th>Source minutes per day</th>
                <th>Consumed allocation</th>
              </tr>
            </thead>
            <tbody>
              {source.entries.map((entry) => (
                <tr key={entry.id}>
                  <td>
                    <details>
                      <summary>{entry.id}</summary>
                      <EvidenceRecord value={entry} />
                    </details>
                  </td>
                  <td>
                    {usageNames[entry.type]}
                    <br />
                    {entry.minutes}
                  </td>
                  <td>{entry.standardDayMinutes}</td>
                  <td>
                    {candidates.has(entry.id) ? (
                      <>
                        <label className="attendance-check">
                          <input
                            type="checkbox"
                            checked={Object.hasOwn(allocations, entry.id)}
                            onChange={(event) => {
                              setAllocations((current) => {
                                const next = { ...current };
                                if (event.target.checked) next[entry.id] = '';
                                else delete next[entry.id];
                                return next;
                              });
                              setReviewed(false);
                            }}
                          />
                          Allocate from this source
                        </label>
                        {Object.hasOwn(allocations, entry.id) && (
                          <TextInput
                            label={`Source minutes from ${entry.id}`}
                            type="number"
                            min={1}
                            max={-entry.minutes}
                            step={1}
                            required
                            value={allocations[entry.id] ?? ''}
                            onChange={(event) => {
                              setAllocations((current) => ({
                                ...current,
                                [entry.id]: event.target.value,
                              }));
                              setReviewed(false);
                            }}
                          />
                        )}
                      </>
                    ) : (
                      'Not selectable for this award'
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {source.entries.length === 0 && <p>No usage entries returned.</p>}
        <label className="attendance-check">
          <input
            type="checkbox"
            checked={reviewed}
            onChange={(event) => setReviewed(event.target.checked)}
          />
          I confirm the exact consumed-source allocations, including any empty allocation.
        </label>
        <TextInput
          label="Correction reason"
          required
          maxLength={1000}
          value={inputReason}
          onChange={(event) => setReason(event.target.value)}
        />
      </fieldset>
    </MutationForm>
  );
}
