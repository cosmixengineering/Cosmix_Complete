import { useState } from 'react';
import { z } from 'zod';
import { Check, RefreshCw, Send } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Command, MutationForm, QueryState, SavedNotice } from '../../components/SchedulingUi';
import { reason, timeline } from '../../schemas/common';
import {
  activationStatusFor,
  type PayrollActivationStatus,
  type PayrollSourceRevision,
} from './payroll-activation-schemas';

type FormProps = {
  path: string;
  source: PayrollSourceRevision;
  status: PayrollActivationStatus;
  onSaved: (result: unknown) => void;
  onCancel: () => void;
};

function RequestActivation({ path, source, status, onSaved, onCancel }: FormProps) {
  const [snapshot] = useState(status);
  const [evidenceReference, setReference] = useState('');
  const [evidenceSha256, setHash] = useState('');
  const [statement, setStatement] = useState('');
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title="Request company operational activation"
      submitLabel="Submit activation request"
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => {
        if (
          !snapshot.sourceCurrent ||
          !['unconfigured', 'returned', 'rejected'].includes(snapshot.state)
        )
          throw new Error('Reload and review the current source and activation history.');
        return {
          path: path + '/requests',
          method: 'POST',
          schema: activationStatusFor(source),
          body: {
            expectedSourceRevision: snapshot.binding.sourceRevision,
            expectedSourceFingerprint: snapshot.binding.sourceFingerprint,
            expectedLatestRequestId: snapshot.latestRequestId,
            evidenceReference: reason(500).parse(evidenceReference),
            evidenceSha256: timeline.parse(evidenceSha256.trim()),
            statement: reason(4000).min(20).parse(statement),
            reason: reason().parse(inputReason),
          },
        };
      }}
    >
      <p>
        Declare the company authorization covering this exact source, policies and jurisdictions. An
        independent authorized reviewer must decide this request.
      </p>
      <TextInput
        label="Authorization evidence reference"
        required
        maxLength={500}
        value={evidenceReference}
        onChange={(event) => setReference(event.target.value)}
      />
      <TextInput
        label="Evidence SHA-256"
        required
        minLength={64}
        maxLength={64}
        value={evidenceSha256}
        onChange={(event) => setHash(event.target.value)}
      />
      <TextInput
        label="Authorization statement"
        required
        minLength={20}
        maxLength={4000}
        value={statement}
        onChange={(event) => setStatement(event.target.value)}
      />
      <TextInput
        label="Request reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}

function ReviewActivation({ path, source, status, onSaved, onCancel }: FormProps) {
  const [snapshot] = useState(status);
  const [decision, setDecision] = useState('');
  const [inputReason, setReason] = useState('');
  const latest = snapshot.requests.find((row) => row.id === snapshot.latestRequestId);
  return (
    <MutationForm
      title="Review company operational activation"
      submitLabel="Retain activation decision"
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => {
        if (!snapshot.sourceCurrent || snapshot.state !== 'pending' || latest?.state !== 'pending')
          throw new Error('Reload and review the current pending request.');
        return {
          path: path + '/requests/' + latest.id + '/decision',
          method: 'POST',
          schema: activationStatusFor(source),
          body: {
            expectedVersion: latest.version,
            decision: Number(z.enum(['1', '2', '3']).parse(decision)),
            reason: reason().parse(inputReason),
          },
        };
      }}
    >
      <p>Review the retained source binding and documentary evidence before deciding.</p>
      <SelectInput
        label="Activation decision"
        required
        value={decision}
        onChange={(event) => setDecision(event.target.value)}
        options={[
          { value: '', label: 'Choose a decision' },
          { value: '1', label: 'Approve company operational activation' },
          { value: '2', label: 'Return for correction' },
          { value: '3', label: 'Reject activation' },
        ]}
      />
      <TextInput
        label="Decision reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}

export function PayrollActivation({
  path,
  source,
  onEditing,
}: {
  path: string;
  source: PayrollSourceRevision;
  onEditing: (value: boolean) => void;
}) {
  const access = useAttendanceAccess();
  const account = access.user?.account;
  const canRead = Boolean(
    access.ready && account && access.can('attendance.payroll-activation.read'),
  );
  const query = useAttendanceQuery(path, activationStatusFor(source), canRead);
  const [editing, setEditing] = useState<'request' | 'review' | null>(null);
  const [notice, setNotice] = useState('');
  const current = canRead && !query.isError ? query.data : undefined;
  const latest = current?.requests.find((row) => row.id === current.latestRequestId);
  const canRequest = Boolean(
    current?.sourceCurrent &&
    ['unconfigured', 'returned', 'rejected'].includes(current.state) &&
    access.can('attendance.payroll-activation.request'),
  );
  const canReview = Boolean(
    current?.sourceCurrent &&
    current.state === 'pending' &&
    latest?.state === 'pending' &&
    account &&
    latest.requesterAccountId.toLowerCase() !== account.accountId.toLowerCase() &&
    source.employeeId.toLowerCase() !== account.employeeId.toLowerCase() &&
    access.can('attendance.payroll-activation.approve'),
  );
  function close() {
    setEditing(null);
    onEditing(false);
    void query.refetch();
  }
  function saved(result: unknown) {
    const confirmed = activationStatusFor(source).parse(result);
    setNotice(`Activation record retained: ${confirmed.state}.`);
    close();
  }
  return (
    <section className="attendance-section">
      <h3>Company operational activation</h3>
      <p>
        Activation records declared company authorization for this saved source. Legal
        certification: No. Payment instruction: No.
      </p>
      {!canRead ? (
        <p>
          Activation evidence requires the separate All-scope payroll activation read permission.
        </p>
      ) : (
        <>
          <SavedNotice message={notice} />
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={Boolean(editing) || query.isFetching}
            onClick={() => void query.refetch()}
          >
            Reload activation
          </Command>
          <QueryState query={query} />
          {current && (
            <>
              <dl className="attendance-record">
                <dt>Activation state</dt>
                <dd>{current.state}</dd>
                <dt>Source current</dt>
                <dd>{current.sourceCurrent ? 'Yes' : 'No — review current payroll inputs'}</dd>
                <dt>Activation fingerprint</dt>
                <dd>{current.activationFingerprint ?? 'No current activation'}</dd>
                <dt>Source revision / fingerprint</dt>
                <dd>
                  {current.binding.sourceRevision} / {current.binding.sourceFingerprint}
                </dd>
                <dt>Retained source evidence SHA-256</dt>
                <dd>{current.binding.sourceEvidenceSha256}</dd>
                <dt>Coverage dates / fingerprint</dt>
                <dd>
                  {current.binding.sourceFrom} to {current.binding.sourceTo} /{' '}
                  {current.binding.coverageFingerprint}
                </dd>
                <dt>Bound policy versions</dt>
                <dd>{current.binding.policyVersionIds.join(', ') || 'None retained'}</dd>
              </dl>
              <details>
                <summary>Bound jurisdiction evidence</summary>
                {current.binding.jurisdictions.map((row) => (
                  <p key={row.businessDate}>
                    {row.businessDate}: {row.jurisdictionCode ?? 'Unconfigured'}; {row.state}; site{' '}
                    {row.siteId ?? 'Unavailable'}; configuration{' '}
                    {row.configurationVersionId ?? 'Unavailable'}
                  </p>
                ))}
              </details>
              {current.requests.length === 0 && <p>No activation requests retained.</p>}
              {current.requests.map((row) => (
                <section className="attendance-section" key={row.id}>
                  <h4>Request {row.id}</h4>
                  <dl className="attendance-record">
                    <dt>State</dt>
                    <dd>{row.state}</dd>
                    <dt>Requester / requested</dt>
                    <dd>
                      {row.requesterAccountId} / {row.requestedAtUtc}
                    </dd>
                    <dt>Authorization evidence reference</dt>
                    <dd>{row.evidenceReference}</dd>
                    <dt>Evidence SHA-256</dt>
                    <dd>{row.evidenceSha256}</dd>
                    <dt>Statement</dt>
                    <dd>{row.statement}</dd>
                    <dt>Request reason</dt>
                    <dd>{row.reason}</dd>
                    <dt>Reviewer / decided</dt>
                    <dd>
                      {row.reviewerAccountId ?? 'Pending'} / {row.decidedAtUtc ?? 'Pending'}
                    </dd>
                    <dt>Decision reason</dt>
                    <dd>{row.decisionReason ?? 'No decision retained'}</dd>
                  </dl>
                </section>
              ))}
              {!editing && canRequest && (
                <Command
                  icon={Send}
                  disabled={query.isFetching}
                  onClick={() => {
                    setNotice('');
                    setEditing('request');
                    onEditing(true);
                  }}
                >
                  Request activation
                </Command>
              )}
              {!editing && canReview && (
                <Command
                  icon={Check}
                  disabled={query.isFetching}
                  onClick={() => {
                    setNotice('');
                    setEditing('review');
                    onEditing(true);
                  }}
                >
                  Review activation
                </Command>
              )}
              {editing === 'request' && canRequest && (
                <RequestActivation
                  path={path}
                  source={source}
                  status={current}
                  onSaved={saved}
                  onCancel={close}
                />
              )}
              {editing === 'review' && canReview && (
                <ReviewActivation
                  path={path}
                  source={source}
                  status={current}
                  onSaved={saved}
                  onCancel={close}
                />
              )}
            </>
          )}
        </>
      )}
    </section>
  );
}
