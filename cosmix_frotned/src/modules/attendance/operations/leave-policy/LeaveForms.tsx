import { EmployeePicker } from '@/components/employees/EmployeeReference';
import { useRef, useState } from 'react';
import { z } from 'zod';
import { Download, Eye, Plus, Trash2 } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { request } from '@/services/api/http-client';
import { Command, MutationForm, QueryState } from '../../components/SchedulingUi';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { useAttendanceLifetime } from '../../hooks/use-attendance-lifetime';
import { attendanceError } from '../../utils/messages';
import {
  cancellationDecision,
  cancellationInput,
  leaveDecision,
  leavePolicies,
  leavePreview,
  leaveRecord,
  leaveSubmit,
  leaveTypes,
  type LeaveRecord,
} from './contracts';
import { evidenceNames, leaveDays, localMinute, treatmentNames } from './inputs';
import { Choice, EvidenceRecord, Notes, Reload } from './Ui';
import { useServerPreview } from './preview';

type Attachment = z.infer<typeof leaveSubmit>['attachments'][number] & { size: number };
export function LeaveRequestForm({
  previous,
  onSaved,
  onCancel,
}: {
  previous?: LeaveRecord;
  onSaved: (record: LeaveRecord) => void;
  onCancel: () => void;
}) {
  const types = useAttendanceQuery('/leaves/mine/types', leaveTypes, true);
  const lifetime = useAttendanceLifetime();
  const [type, setType] = useState(previous?.leaveTypeId ?? '');
  const [days, setDays] = useState(
    () =>
      previous?.days.map((d) => ({
        key: crypto.randomUUID(),
        start: localMinute(d.startsAtUtc),
        end: localMinute(d.endsAtUtc),
      })) ?? [{ key: crypto.randomUUID(), start: '', end: '' }],
  );
  const [inputReason, setReason] = useState(previous?.reason ?? '');
  const [contact, setContact] = useState(previous?.contactDuringLeave ?? '');
  const [reliever, setReliever] = useState(previous?.relieverEmployeeId ?? '');
  const [handover, setHandover] = useState(previous?.handoverNotes ?? '');
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [kind, setKind] = useState('');
  const [fileError, setFileError] = useState('');
  const [reading, setReading] = useState(false);
  const readActive = useRef(false);
  const selectedType = types.data?.find((t) => t.id === type && t.isActive);
  async function attach(file: File | undefined) {
    if (!file || !kind || readActive.current) return;
    const signal = lifetime();
    if (signal.aborted) return;
    readActive.current = true;
    setReading(true);
    setFileError('');
    try {
      const mime = z.enum(['image/png', 'image/jpeg', 'application/pdf']).parse(file.type);
      if (
        attachments.length >= 4 ||
        file.size > 10 * 1024 * 1024 ||
        file.size === 0 ||
        attachments.reduce((sum, a) => sum + a.size, 0) + file.size > 20 * 1024 * 1024
      )
        throw new Error('Evidence limit: four files, 10 MiB each, 20 MiB combined.');
      if (attachments.some((a) => a.kind === Number(kind)))
        throw new Error('Use a distinct kind for each evidence file.');
      const bytes = new Uint8Array(await file.arrayBuffer());
      signal.throwIfAborted();
      let binary = '';
      for (let offset = 0; offset < bytes.length; offset += 32768)
        binary += String.fromCharCode(...bytes.subarray(offset, offset + 32768));
      setAttachments((rows) => [
        ...rows,
        {
          kind: Number(kind),
          fileName: file.name,
          contentType: mime,
          content: btoa(binary),
          size: file.size,
        },
      ]);
    } catch (error) {
      if (!signal.aborted)
        setFileError(error instanceof Error ? error.message : 'Evidence is unavailable.');
    } finally {
      readActive.current = false;
      if (!signal.aborted) setReading(false);
    }
  }
  function build() {
    if (!selectedType) throw new Error('Select a current active leave type.');
    const kinds = attachments.map((a) => a.kind);
    if (
      (selectedType.requiresEvidence && !kinds.length) ||
      (selectedType.kind === 1 && !kinds.includes(1)) ||
      (selectedType.kind === 3 && (!kinds.includes(2) || !kinds.includes(3))) ||
      (selectedType.kind === 5 && !kinds.includes(5))
    )
      throw new Error('Supply the required evidence for this leave type.');
    return {
      path: '/leaves/mine',
      method: 'POST' as const,
      schema: leaveRecord,
      body: leaveSubmit.parse({
        leaveTypeId: type,
        days: leaveDays(days),
        reason: inputReason,
        contactDuringLeave: contact || null,
        relieverEmployeeId: reliever || null,
        handoverNotes: handover || null,
        attachments: attachments.map(({ size: _size, ...a }) => a),
        previousRequestId: previous?.id ?? null,
      }),
    };
  }
  return (
    <MutationForm
      title={previous ? 'Correct returned leave' : 'Request leave'}
      build={build}
      onSaved={(result) => onSaved(leaveRecord.parse(result))}
      onCancel={onCancel}
      disabled={reading || !selectedType}
      submitLabel="Submit to Admin"
    >
      <QueryState query={types} />
      <Choice
        label="Leave type"
        value={type}
        options={(types.data ?? [])
          .filter((t) => t.isActive)
          .map((t) => ({ value: t.id, label: t.name }))}
        onChange={(value) => {
          setType(value);
          setAttachments([]);
          setFileError('');
        }}
        disabled={reading}
      />
      {selectedType && (
        <EvidenceRecord
          value={{
            requiresEvidence: selectedType.requiresEvidence,
            requiresAdvanceApproval: selectedType.requiresAdvanceApproval,
            reducesRegularBalance: selectedType.reducesRegularBalance,
          }}
        />
      )}
      {previous && (
        <p>New immutable revision of {previous.id}. Evidence must be supplied for this revision.</p>
      )}
      <h3>Exact intervals (Asia/Karachi)</h3>
      {days.map((day, index) => (
        <div className="attendance-grid" key={day.key}>
          <TextInput
            label={`Start ${index + 1}`}
            type="datetime-local"
            step={60}
            required
            value={day.start}
            onChange={(e) =>
              setDays((rows) =>
                rows.map((d) => (d.key === day.key ? { ...d, start: e.target.value } : d)),
              )
            }
          />
          <TextInput
            label={`End ${index + 1}`}
            type="datetime-local"
            step={60}
            required
            value={day.end}
            onChange={(e) =>
              setDays((rows) =>
                rows.map((d) => (d.key === day.key ? { ...d, end: e.target.value } : d)),
              )
            }
          />
          <Command
            icon={Trash2}
            variant="secondary"
            disabled={days.length === 1}
            onClick={() => setDays((rows) => rows.filter((d) => d.key !== day.key))}
          >
            Remove interval
          </Command>
        </div>
      ))}
      <Command
        icon={Plus}
        variant="secondary"
        disabled={days.length >= 366}
        onClick={() =>
          setDays((rows) => [...rows, { key: crypto.randomUUID(), start: '', end: '' }])
        }
      >
        Add interval
      </Command>
      <TextInput
        label="Reason"
        maxLength={1000}
        required
        value={inputReason}
        onChange={(e) => setReason(e.target.value)}
      />
      <div className="attendance-grid">
        <TextInput
          label="Contact during leave"
          maxLength={200}
          value={contact}
          onChange={(e) => setContact(e.target.value)}
        />
        <EmployeePicker
          label="Reliever employee (optional)"
          value={reliever}
          onChange={setReliever}
        />
      </div>
      <Notes label="Handover notes" value={handover} onChange={setHandover} />
      <Choice
        label="Evidence kind"
        value={kind}
        options={evidenceNames.map((label, i) => ({ value: String(i + 1), label }))}
        onChange={setKind}
        required={false}
        disabled={reading}
      />
      <TextInput
        label="Private evidence"
        type="file"
        accept=".png,.jpg,.jpeg,.pdf"
        disabled={reading || !kind}
        onChange={(e) => {
          void attach(e.target.files?.[0]);
          e.target.value = '';
        }}
      />
      {reading && <p role="status">Reading private evidence...</p>}
      {fileError && (
        <p className="attendance-error" role="alert">
          {fileError}
        </p>
      )}
      {attachments.map((a) => (
        <div className="attendance-commands" key={a.kind}>
          <span>
            {evidenceNames[a.kind - 1]}: {a.fileName}
          </span>
          <Command
            icon={Trash2}
            variant="secondary"
            disabled={reading}
            onClick={() => setAttachments((rows) => rows.filter((row) => row.kind !== a.kind))}
          >
            Remove evidence
          </Command>
        </div>
      ))}
    </MutationForm>
  );
}

export function LeaveDecisionForm({
  record,
  cancellationId,
  onSaved,
  onCancel,
}: {
  record: LeaveRecord;
  cancellationId?: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const cancellation = record.cancellations.find((c) => c.id === cancellationId);
  const policies = useAttendanceQuery(
    `/leaves/admin/${record.id}/policies`,
    leavePolicies,
    !cancellationId,
  );
  const [decision, setDecision] = useState('');
  const [treatment, setTreatment] = useState('');
  const [policy, setPolicy] = useState('');
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title={cancellationId ? 'Review cancellation' : 'Review leave'}
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Record decision"
      build={() => ({
        path:
          `/leaves/admin/${record.id}` +
          (cancellationId ? `/cancellations/${cancellationId}/decisions` : '/decisions'),
        method: 'POST',
        schema: leaveRecord,
        body: cancellationId
          ? cancellationDecision.parse({
              decision: Number(decision),
              expectedApprovalVersion: cancellation?.approvalVersion,
              reason: inputReason,
            })
          : leaveDecision.parse({
              decision: Number(decision),
              expectedApprovalVersion: record.approvalVersion,
              reason: inputReason,
              treatment: decision === '1' ? Number(treatment) : 0,
              policyVersionId: decision === '1' ? policy || null : null,
            }),
      })}
    >
      <p>
        Approval request: {cancellation?.approvalRequestId ?? record.approvalRequestId}. Version:{' '}
        {cancellation?.approvalVersion ?? record.approvalVersion}
      </p>
      <Choice
        label="Decision"
        value={decision}
        options={[
          { value: '1', label: 'Approve' },
          { value: '2', label: 'Return' },
          { value: '3', label: 'Reject' },
        ]}
        onChange={(v) => {
          setDecision(v);
          setTreatment('');
          setPolicy('');
        }}
      />
      {!cancellationId && decision === '1' && (
        <>
          <QueryState query={policies} />
          <Choice
            label="Explicit treatment"
            value={treatment}
            options={treatmentNames.slice(1).map((label, i) => ({ value: String(i + 1), label }))}
            onChange={setTreatment}
          />
          <Choice
            label="Applicable published policy"
            value={policy}
            options={(policies.data ?? []).map((p) => ({
              value: p.versionId,
              label: `${p.code}: ${p.effectiveFrom} to ${p.effectiveTo ?? 'open'}`,
            }))}
            onChange={setPolicy}
          />
          {policies.data?.find((p) => p.versionId === policy) && (
            <EvidenceRecord value={policies.data.find((p) => p.versionId === policy)} />
          )}
          <Reload onClick={() => void policies.refetch()} disabled={policies.isFetching} />
        </>
      )}
      <TextInput
        label="Decision reason"
        maxLength={1000}
        required
        value={inputReason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
export function LeaveCancellationForm({
  record,
  onSaved,
  onCancel,
}: {
  record: LeaveRecord;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title="Request separate cancellation"
      onSaved={onSaved}
      onCancel={onCancel}
      submitLabel="Request cancellation"
      build={() => ({
        path: `/leaves/mine/${record.id}/cancellations`,
        method: 'POST',
        schema: leaveRecord,
        body: cancellationInput.parse({
          expectedApprovalVersion: record.approvalVersion,
          reason: inputReason,
        }),
      })}
    >
      <p>Approval version: {record.approvalVersion}</p>
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
export function PrivateLeaveEvidence({ record, admin }: { record: LeaveRecord; admin: boolean }) {
  const lifetime = useAttendanceLifetime();
  const [kind, setKind] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const active = useRef(false);
  async function download() {
    const signal = lifetime();
    if (!kind || active.current || signal.aborted) return;
    active.current = true;
    setPending(true);
    setError('');
    try {
      const result = await request(
        `/leaves/${admin ? 'admin' : 'mine'}/${record.id}/evidence/${kind}`,
        { signal, responseType: 'blob' },
      );
      signal.throwIfAborted();
      if (
        !(result instanceof Blob) ||
        !['application/pdf', 'image/png', 'image/jpeg'].includes(result.type)
      )
        throw new Error('Private evidence response is unavailable.');
      const url = URL.createObjectURL(result);
      const link = document.createElement('a');
      link.href = url;
      link.download = `leave-${record.id}-${kind}.${result.type === 'application/pdf' ? 'pdf' : result.type === 'image/png' ? 'png' : 'jpg'}`;
      try {
        link.click();
      } finally {
        URL.revokeObjectURL(url);
      }
    } catch (failure) {
      if (!signal.aborted) setError(attendanceError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return record.hasEvidence ? (
    <div className="attendance-section">
      <h3>Private evidence</h3>
      <Choice
        label="Evidence kind"
        value={kind}
        options={evidenceNames.map((label, i) => ({ value: String(i + 1), label }))}
        onChange={setKind}
      />
      <Command icon={Download} disabled={!kind || pending} onClick={() => void download()}>
        Download evidence
      </Command>
      {error && (
        <p role="alert" className="attendance-error">
          {error}
        </p>
      )}
    </div>
  ) : null;
}
export function LeaveIntervalPreview({
  type,
  policy,
  treatment,
  days,
}: {
  type: string;
  policy: string;
  treatment: string;
  days: readonly { start: string; end: string }[];
}) {
  const preview = useServerPreview('/leaves/mine/preview', leavePreview);
  const [result, setResult] = useState<{
    signature: string;
    value: z.infer<typeof leavePreview>;
  } | null>(null);
  const [error, setError] = useState('');
  const signature = JSON.stringify([type, policy, treatment, days]);
  async function run() {
    setResult(null);
    setError('');
    try {
      const response = await preview.run({
        leaveTypeId: z.uuid().parse(type),
        policyVersionId: z.uuid().parse(policy),
        treatment: z.number().int().min(1).max(3).parse(Number(treatment)),
        days: leaveDays(days),
      });
      if (response) setResult({ signature, value: response });
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Check the intervals.');
    }
  }
  return (
    <>
      <Command
        icon={Eye}
        disabled={preview.pending || !type || !policy || !treatment}
        onClick={() => void run()}
      >
        Preview exact leave
      </Command>
      {(error || preview.error) && (
        <p className="attendance-error" role="alert">
          {error || preview.error}
        </p>
      )}
      {result?.signature === signature && <EvidenceRecord value={result.value} />}
    </>
  );
}
