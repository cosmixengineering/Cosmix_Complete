import { useEffect, useState } from 'react';
import { Copy, Plus } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
} from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { leaveType, leaveTypeInput, leaveTypes } from './contracts';
import { canAll, kindNames } from './inputs';
import { Choice, EvidenceRecord, Reload } from './Ui';
import { readableLeavePolicies, type PublishedLeavePolicy } from '../leave-benefits/api';
import { LeavePolicyPublisher } from './LeavePolicyPublisher';

export function LeaveSettingsPage() {
  return (
    <PrivateWorkspace resource="leave-settings">
      <LeaveSettings />
    </PrivateWorkspace>
  );
}
function LeaveSettings() {
  const access = useAttendanceAccess();
  const allowed = access.ready && canAll(access.user, 'attendance.leaves.configure');
  const types = useAttendanceQuery('/hr/leave-settings/types', leaveTypes, allowed);
  const policies = useAttendanceQuery(
    '/hr/leave-settings/policies',
    readableLeavePolicies,
    allowed,
  );
  const [form, setForm] = useState('');
  const [epoch, setEpoch] = useState(0);
  const [source, setSource] = useState<PublishedLeavePolicy | undefined>();
  useEffect(() => {
    if (policies.isError) {
      setSource(undefined);
      setForm((current) => (current === 'policy' ? '' : current));
    }
  }, [policies.isError]);
  const publicationBlock = policies.isError
    ? 'Publishing is disabled because the published settings could not be read. Reload the history before publishing.'
    : policies.isPending || !policies.data
      ? 'Publishing is disabled until the published settings have loaded.'
      : policies.isFetching
        ? 'Publishing is disabled while the published settings are being refreshed.'
        : '';
  function reload() {
    setForm('');
    setSource(undefined);
    setEpoch((e) => e + 1);
    void types.refetch();
    void policies.refetch();
  }
  if (!allowed)
    return <Denied>Leave settings require the explicit All-scoped configure permission.</Denied>;
  return (
    <>
      <div className="attendance-heading">
        <h1>Leave settings</h1>
        <Reload
          onClick={reload}
          disabled={Boolean(form) || types.isFetching || policies.isFetching}
        />
      </div>
      {publicationBlock && (
        <p id="leave-policy-publication-status" role="status">
          {publicationBlock}
        </p>
      )}
      {form === 'type' ? (
        <LeaveTypeForm key={epoch} onSaved={reload} onCancel={reload} />
      ) : form === 'policy' && !policies.isError ? (
        <LeavePolicyPublisher
          key={epoch}
          source={source}
          publicationBlock={publicationBlock}
          onSaved={reload}
          onCancel={reload}
        />
      ) : (
        <div className="attendance-commands">
          <Command icon={Plus} onClick={() => setForm('type')}>
            Create leave type
          </Command>
          <Command
            icon={Plus}
            disabled={Boolean(publicationBlock)}
            aria-describedby={publicationBlock ? 'leave-policy-publication-status' : undefined}
            onClick={() => {
              if (!publicationBlock) {
                setSource(undefined);
                setForm('policy');
              }
            }}
          >
            Publish v4 leave settings
          </Command>
        </div>
      )}
      <section className="attendance-section">
        <h2>Current types</h2>
        <QueryState query={types} empty={types.data?.length === 0} />
        {!types.isError &&
          types.data?.map((row) => (
            <details key={row.id}>
              <summary>
                {row.code}: {row.name}
                {row.isActive ? '' : ' (inactive)'}
              </summary>
              <EvidenceRecord value={{ ...row, kind: kindNames[row.kind] }} />
            </details>
          ))}
      </section>
      <section className="attendance-section">
        <h2>Immutable published settings</h2>
        <QueryState query={policies} empty={policies.data?.length === 0} />
        {!policies.isError &&
          policies.data?.map((row) => (
            <details key={row.versionId}>
              <summary>
                {row.code} (v{row.settings.settingsVersion ?? 1}): {row.effectiveFrom} to{' '}
                {row.effectiveTo ?? 'open'}
              </summary>
              <EvidenceRecord value={row} />
              <Command
                icon={Copy}
                variant="secondary"
                disabled={Boolean(form) || Boolean(publicationBlock)}
                onClick={() => {
                  if (!form && !publicationBlock) {
                    setSource(structuredClone(row));
                    setForm('policy');
                  }
                }}
              >
                Copy to new publication
              </Command>
            </details>
          ))}
      </section>
    </>
  );
}
function LeaveTypeForm({ onSaved, onCancel }: { onSaved: () => void; onCancel: () => void }) {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [kind, setKind] = useState('');
  const [evidence, setEvidence] = useState('');
  const [advance, setAdvance] = useState('');
  const [regular, setRegular] = useState('');
  const [inputReason, setReason] = useState('');
  const options = [
    { value: 'true', label: 'Yes' },
    { value: 'false', label: 'No' },
  ];
  function boolean(value: string) {
    if (!value) throw new Error('Review all leave-type flags explicitly.');
    return value === 'true';
  }
  return (
    <MutationForm
      title="Create explicit leave type"
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => ({
        path: '/hr/leave-settings/types',
        method: 'POST',
        schema: leaveType,
        body: leaveTypeInput.parse({
          code,
          name,
          kind: kind ? Number(kind) : null,
          requiresEvidence: boolean(evidence),
          requiresAdvanceApproval: boolean(advance),
          reducesRegularBalance: boolean(regular),
          reason: inputReason,
        }),
      })}
    >
      <div className="attendance-grid">
        <TextInput
          label="Code"
          required
          maxLength={40}
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
        <TextInput
          label="Name"
          required
          maxLength={200}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <Choice
        label="Kind"
        value={kind}
        options={kindNames.map((label, i) => ({ value: String(i), label }))}
        onChange={setKind}
      />
      <Choice label="Requires evidence" value={evidence} options={options} onChange={setEvidence} />
      <Choice
        label="Requires advance approval"
        value={advance}
        options={options}
        onChange={setAdvance}
      />
      <Choice
        label="Reduces regular balance"
        value={regular}
        options={options}
        onChange={setRegular}
      />
      <TextInput
        label="Reason"
        maxLength={1000}
        required
        value={inputReason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
