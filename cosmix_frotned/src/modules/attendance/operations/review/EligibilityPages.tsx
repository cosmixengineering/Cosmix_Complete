import { useState } from 'react';
import { RefreshCw, Settings } from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { date } from '../../schemas/common';
import { canEmployee, canScope, type Scope } from './access';
import { eligibilityEntry, eligibilityInput, eligibilityPreview, pageOf } from './contracts';
import {
  EmployeeWorkspace,
  EvidenceField,
  NumberedControls,
  RecordFacts,
  SourceFacts,
} from './ReviewUi';
import { today } from './helpers';

export function AttendanceOvertimeEligibilityPage() {
  return (
    <PrivateWorkspace resource="overtime-global-eligibility">
      <h1>Common outside-staff overtime eligibility</h1>
      <EligibilityView />
    </PrivateWorkspace>
  );
}
export function EmployeeOvertimeEligibilityPage() {
  return (
    <EmployeeWorkspace
      title="Employee overtime eligibility"
      permission="attendance.overtime.eligibility.read"
    >
      {(employeeId, scope) => <EligibilityView employeeId={employeeId} scope={scope} />}
    </EmployeeWorkspace>
  );
}
function EligibilityView({ employeeId, scope }: { employeeId?: string; scope?: Scope }) {
  const access = useAttendanceAccess();
  const [asOf, setAsOf] = useState(today());
  const [page, setPage] = useState(1);
  const [snapshot, setSnapshot] = useState<z.infer<typeof eligibilityPreview>>();
  const [notice, setNotice] = useState('');
  const allowed = employeeId
    ? canEmployee(access.user, 'attendance.overtime.eligibility.read', employeeId, scope)
    : canScope(access.user, 'attendance.overtime.eligibility.read', { kind: 7, targetId: null });
  const manage = employeeId
    ? canEmployee(access.user, 'attendance.overtime.eligibility.manage', employeeId, scope)
    : canScope(access.user, 'attendance.overtime.eligibility.manage', { kind: 7, targetId: null });
  const base = employeeId
    ? `/attendance/employees/${employeeId}/overtime/eligibility`
    : '/attendance/overtime/eligibility';
  const query = useAttendanceQuery(
    base + '/preview' + queryString({ asOf }),
    eligibilityPreview,
    allowed && date.safeParse(asOf).success,
  );
  const history = useAttendanceQuery(
    base + '/history' + queryString({ page, pageSize: 25 }),
    pageOf(eligibilityEntry),
    allowed,
  );
  const value = !query.isError ? query.data : undefined;
  async function reload() {
    setSnapshot(undefined);
    await query.refetch();
    await history.refetch();
  }
  if (!allowed)
    return (
      <Denied>
        Common settings require an All-scoped eligibility read grant. Employee settings require
        their own matching scope.
      </Denied>
    );
  return (
    <>
      <SavedNotice message={notice} />
      {!snapshot && (
        <>
          <TextInput
            label="Preview as of business date"
            type="date"
            required
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
          />
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={query.isFetching || history.isFetching || !date.safeParse(asOf).success}
            onClick={() => void reload()}
          >
            Reload eligibility and history
          </Command>
        </>
      )}
      <QueryState query={query} />
      {value && <RecordFacts value={value} />}
      <p>
        Office staff are ineligible. Employee OFF restricts common ON; restoring inheritance cannot
        override common OFF. Earlier decisions and locked periods retain their evidence.
      </p>
      {!snapshot && value && manage && (
        <Command
          icon={Settings}
          disabled={
            query.isFetching ||
            history.isFetching ||
            value.eligibility?.status === 'office_ineligible' ||
            value.eligibility?.status === 'category_unavailable'
          }
          onClick={() => {
            setNotice('');
            setSnapshot(structuredClone(value));
          }}
        >
          Review eligibility change
        </Command>
      )}
      {snapshot && (
        <EligibilityForm
          preview={snapshot}
          base={base}
          manage={manage}
          onClose={() => void reload()}
          onSaved={(result) => {
            const row = eligibilityEntry.parse(result);
            setNotice(
              `Eligibility revision ${row.revision}: ${row.effect}, effective ${row.effectiveFrom}.`,
            );
            void reload();
          }}
        />
      )}
      <section className="attendance-section">
        <h2>Immutable eligibility history</h2>
        <QueryState query={history} empty={history.data?.items.length === 0} />
        {!history.isError && history.data && (
          <>
            {history.data.items.map((row) => (
              <SourceFacts
                key={row.id}
                title={`Revision ${row.revision}: ${row.effect}, ${row.effectiveFrom}`}
                value={row}
              />
            ))}
            <NumberedControls
              page={page}
              data={history.data}
              disabled={history.isFetching || Boolean(snapshot)}
              onChange={setPage}
            />
          </>
        )}
      </section>
    </>
  );
}
function EligibilityForm({
  preview,
  base,
  manage,
  onClose,
  onSaved,
}: {
  preview: z.infer<typeof eligibilityPreview>;
  base: string;
  manage: boolean;
  onClose: () => void;
  onSaved: (value: unknown) => void;
}) {
  const [effectiveFrom, setDate] = useState(preview.asOf);
  const [isEnabled, setEnabled] = useState(preview.effective?.isEnabled ?? false);
  const [reason, setReason] = useState('');
  return (
    <MutationForm
      title="Append dated eligibility setting"
      disabled={
        !manage ||
        preview.eligibility?.status === 'office_ineligible' ||
        preview.eligibility?.status === 'category_unavailable'
      }
      build={() => ({
        path: base,
        method: 'POST',
        schema: eligibilityEntry,
        body: eligibilityInput.parse({
          effectiveFrom,
          isEnabled,
          expectedRevision: preview.latestRevision,
          expectedTimeline: preview.timelineFingerprint,
          reason,
        }),
      })}
      onCancel={onClose}
      onSaved={onSaved}
    >
      <SourceFacts title="Expected timeline" value={preview} />
      <TextInput
        label="Effective from"
        type="date"
        required
        value={effectiveFrom}
        onChange={(e) => setDate(e.target.value)}
      />
      <label className="attendance-check">
        <input type="checkbox" checked={isEnabled} onChange={(e) => setEnabled(e.target.checked)} />
        {preview.employeeId
          ? 'Restore common-rule inheritance'
          : 'Allow overtime for outside staff'}
      </label>
      <EvidenceField label="Reason" max={1000} value={reason} onChange={setReason} />
    </MutationForm>
  );
}
