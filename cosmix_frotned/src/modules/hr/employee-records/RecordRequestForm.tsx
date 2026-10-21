import { useEffect, useState } from 'react';
import { Send, X } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { QueryState } from '../components/Feedback';
import { recordPermissions } from './access';
import {
  employeeVersionSchema,
  evidenceSchema,
  recordId,
  sameId,
  type RecordAction,
  type RecordKind,
  type OperationalRecord,
} from './contracts';
import { recordError, useRecordQuery, type RecordMutation } from './hooks';
import {
  actionLabels,
  availableEvidence,
  buildRecordCommand,
  initialRecordDraft,
  type RecordDraft,
} from './validation';

export function EvidenceField({
  instance,
  employeeId,
  metadata,
  value,
  onChange,
}: {
  instance: string;
  employeeId: string;
  metadata: boolean;
  value: string;
  onChange: (
    value: string,
    document?: { issuedOn: string | null; expiresOn: string | null },
  ) => void;
}) {
  const access = useHrAccess();
  const allowed = access.can('hr.employees.read', 'hr.employees.documents.read');
  const query = useRecordQuery(
    instance,
    `employees/${employeeId}/documents`,
    evidenceSchema,
    ['hr.employees.read', 'hr.employees.documents.read'],
    allowed && recordId.safeParse(employeeId).success,
  );
  if (!allowed)
    return (
      <TextInput
        label="Evidence document reference"
        required
        value={value}
        maxLength={36}
        onChange={(e) => onChange(e.target.value)}
        autoComplete="off"
      />
    );
  return (
    <QueryState query={query}>
      <SelectInput
        label="Verified evidence"
        required
        value={value}
        options={[
          { value: '', label: 'Select current verified evidence' },
          ...(query.data ?? [])
            .filter((document) => availableEvidence(document, metadata))
            .map((document) => ({ value: document.id, label: document.fileName })),
        ]}
        onChange={(e) => {
          const document = query.data?.find((row) => row.id === e.target.value);
          onChange(e.target.value, document);
        }}
      />
      {query.data && !query.data.some((document) => availableEvidence(document, metadata)) && (
        <p className="muted">No eligible verified evidence.</p>
      )}
    </QueryState>
  );
}
export function RecordRequestForm({
  instance,
  employeeId,
  kind,
  action,
  record,
  mutation,
  onDirty,
  onSaved,
  onClose,
}: {
  instance: string;
  employeeId: string;
  kind: RecordKind;
  action: RecordAction;
  record?: OperationalRecord;
  mutation: RecordMutation;
  onDirty: (dirty: boolean) => void;
  onSaved: () => void;
  onClose: () => void;
}) {
  const access = useHrAccess();
  const [draft, setDraft] = useState(() => initialRecordDraft(record));
  const [changed, setChanged] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [payload, setPayload] = useState<ReturnType<typeof buildRecordCommand> | null>(null);
  const allowed = access.can(...recordPermissions(kind, 'request'));
  const current = useRecordQuery(
    instance,
    `employees/${employeeId}/current/basic`,
    employeeVersionSchema(employeeId),
    recordPermissions(kind, 'request'),
    allowed,
  );
  const transfer = action === 3;
  const targetValid =
    transfer &&
    recordId.safeParse(draft.targetEmployeeId).success &&
    !sameId(draft.targetEmployeeId, employeeId);
  const target = useRecordQuery(
    instance,
    `employees/${draft.targetEmployeeId}/current/basic`,
    employeeVersionSchema(targetValid ? draft.targetEmployeeId : employeeId),
    recordPermissions(kind, 'request'),
    targetValid,
  );
  const issue = action === 1;
  const metadata = kind === 'Certification' && [1, 4, 7].includes(action);
  const replacement = action === 7;
  useEffect(() => {
    onDirty(changed || Boolean(payload));
    return () => onDirty(false);
  }, [changed, payload, onDirty]);
  function set<K extends keyof RecordDraft>(key: K, value: RecordDraft[K]) {
    setChanged(true);
    setPayload(null);
    setError(null);
    setDraft((previous) => ({
      ...previous,
      [key]: value,
      ...(key === 'targetEmployeeId' ? { targetEvidenceDocumentId: '' } : {}),
    }));
  }
  const ready =
    !current.isFetching &&
    !current.isError &&
    Boolean(current.data) &&
    (!transfer || (targetValid && Boolean(target.data) && !target.isFetching && !target.isError));
  if (!allowed) return null;
  return (
    <section className="hr-form-section record-request-form">
      <div className="hr-repeat-heading">
        <h3>{actionLabels[action]} request</h3>
        <Button
          variant="ghost"
          aria-label="Close request draft"
          title="Close request draft"
          disabled={mutation.locked}
          onClick={onClose}
        >
          <X size={16} />
        </Button>
      </div>
      <QueryState query={current}>
        <form
          onSubmit={(event) => {
            event.preventDefault();
            setError(null);
            if (mutation.pending || mutation.reloadRequired || (!payload && !ready)) return;
            try {
              const body =
                payload ??
                buildRecordCommand(
                  kind,
                  action,
                  employeeId,
                  current.data!.version,
                  draft,
                  record,
                  target.data?.version,
                );
              setPayload(body);
              void mutation
                .run(`employees/${employeeId}/records/${kind}/requests`, body)
                .then((result) => {
                  if (result) onSaved();
                  else if (result === null) setPayload(null);
                });
            } catch (failure) {
              setError(failure instanceof Error ? failure.message : recordError(failure));
            }
          }}
        >
          <fieldset disabled={mutation.locked}>
            <div className="hr-form-grid">
              <TextInput
                label="Effective time (UTC)"
                required
                value={draft.effectiveAt}
                maxLength={40}
                onChange={(e) => set('effectiveAt', e.target.value)}
                autoComplete="off"
              />
              {issue && kind !== 'Certification' && (
                <TextInput
                  label={kind === 'Asset' ? 'Asset reference' : 'SIM reference'}
                  required
                  maxLength={100}
                  value={draft.reference}
                  onChange={(e) => set('reference', e.target.value)}
                />
              )}
              {kind === 'Asset' && (
                <TextInput
                  label={issue ? 'Issue condition' : 'Return condition'}
                  required
                  maxLength={1000}
                  value={draft.condition}
                  onChange={(e) => set('condition', e.target.value)}
                />
              )}
              {kind === 'Sim' && issue && (
                <TextInput
                  label="International phone number"
                  required
                  type="tel"
                  pattern="\+[1-9][0-9]{1,14}"
                  maxLength={16}
                  value={draft.phoneNumber}
                  onChange={(e) => set('phoneNumber', e.target.value)}
                />
              )}
              {kind === 'Sim' && (issue || transfer) && (
                <TextInput
                  label="Approved credit tier code"
                  required
                  maxLength={40}
                  value={draft.creditTierCode}
                  onChange={(e) => set('creditTierCode', e.target.value)}
                />
              )}
              {!issue && kind !== 'Certification' && (
                <TextInput
                  label="Receiving account reference"
                  required
                  maxLength={36}
                  value={draft.receiverAccountId}
                  onChange={(e) => set('receiverAccountId', e.target.value)}
                  autoComplete="off"
                />
              )}
              <EvidenceField
                instance={instance}
                employeeId={employeeId}
                metadata={metadata}
                value={draft.evidenceDocumentId}
                onChange={(value, document) => {
                  set('evidenceDocumentId', value);
                  if (metadata && !replacement && document)
                    setDraft((previous) => ({
                      ...previous,
                      issuedOn: document.issuedOn ?? '',
                      expiresOn: document.expiresOn ?? '',
                    }));
                }}
              />
              {metadata && (
                <>
                  <TextInput
                    label="Certificate type"
                    required
                    maxLength={100}
                    readOnly={!issue}
                    value={draft.type}
                    onChange={(e) => set('type', e.target.value)}
                  />
                  <TextInput
                    label="Certificate number"
                    required
                    maxLength={100}
                    readOnly={replacement}
                    value={draft.number}
                    onChange={(e) => set('number', e.target.value)}
                  />
                  <TextInput
                    label="Issuer"
                    required
                    maxLength={200}
                    readOnly={!issue}
                    value={draft.issuer}
                    onChange={(e) => set('issuer', e.target.value)}
                  />
                  <TextInput
                    label="Issued on"
                    type="date"
                    required
                    readOnly={replacement}
                    value={draft.issuedOn}
                    onChange={(e) => set('issuedOn', e.target.value)}
                  />
                  <TextInput
                    label="Expires on"
                    type="date"
                    required={action === 4}
                    readOnly={replacement}
                    value={draft.expiresOn}
                    onChange={(e) => set('expiresOn', e.target.value)}
                  />
                </>
              )}
              {transfer && (
                <>
                  <TextInput
                    label="Receiving employee reference"
                    required
                    maxLength={36}
                    value={draft.targetEmployeeId}
                    onChange={(e) => set('targetEmployeeId', e.target.value)}
                    autoComplete="off"
                  />
                  {targetValid && (
                    <QueryState query={target}>
                      {target.data && (
                        <EvidenceField
                          key={draft.targetEmployeeId}
                          instance={instance}
                          employeeId={draft.targetEmployeeId}
                          metadata={false}
                          value={draft.targetEvidenceDocumentId}
                          onChange={(value) => set('targetEvidenceDocumentId', value)}
                        />
                      )}
                    </QueryState>
                  )}
                  {kind === 'Asset' && (
                    <TextInput
                      label="Receiving condition"
                      required
                      maxLength={1000}
                      value={draft.targetCondition}
                      onChange={(e) => set('targetCondition', e.target.value)}
                    />
                  )}
                </>
              )}
              <TextInput
                label="Request reason"
                required
                maxLength={1000}
                value={draft.reason}
                onChange={(e) => set('reason', e.target.value)}
                autoComplete="off"
              />
            </div>
          </fieldset>
          {error && (
            <p role="alert" className="hr-message hr-message--error">
              {error}
            </p>
          )}
          <div className="hr-actions">
            <Button
              type="submit"
              disabled={
                mutation.pending || mutation.reloadRequired || (!mutation.uncertain && !ready)
              }
            >
              <Send size={16} />
              {mutation.pending
                ? 'Submitting...'
                : mutation.uncertain
                  ? 'Retry unchanged request'
                  : 'Submit request'}
            </Button>
          </div>
        </form>
      </QueryState>
    </section>
  );
}

export function RecordStateFilter({
  value,
  onChange,
  disabled,
}: {
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
}) {
  return (
    <SelectInput
      label="Request state"
      value={value}
      disabled={disabled}
      options={[
        { value: '', label: 'All states' },
        ...['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'].map((label, i) => ({
          value: String(i),
          label,
        })),
      ]}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}
