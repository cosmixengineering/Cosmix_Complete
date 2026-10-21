import { useMemo, useState } from 'react';
import { z } from 'zod';
import { Clock, Plus, RefreshCw } from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { TextInput } from '@/components/forms/TextInput';
import { Button } from '@/components/ui/Button';
import { Pagination } from '@/components/data-display/Pagination';
import { useDirtyForm } from '@/hooks/use-dirty-form';
import { useHrAccess } from '../hooks/use-hr-access';
import { Denied, QueryState } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import { queryString } from '../api/hr-api';
import { labelDate } from '../utils/messages';
import { familyOf, recordPermissions, recordScope } from './access';
import {
  assetEventsSchema,
  operationalPageSchema,
  recordId,
  requestPageSchema,
  type OperationalRecord,
  type RecordAction,
  type RecordKind,
} from './contracts';
import { useRecordCache, useRecordMutation, useRecordQuery } from './hooks';
import { RecordRequestForm, RecordStateFilter } from './RecordRequestForm';
import { RecordRequestDetailView } from './RecordRequestDetail';
import { actionLabels, requestStates, supportedActions } from './validation';
import './employee-records.css';

export function EmployeeRecordsWorkspace({
  employeeId,
  kind,
}: {
  employeeId: string;
  kind: RecordKind;
}) {
  const { user, status } = useAuth();
  const access = useHrAccess();
  const authenticationLifetime = useMemo(
    () => ({ user, status, key: crypto.randomUUID() }),
    [user, status],
  );
  const [revision, setRevision] = useState(0);
  if (!recordId.safeParse(employeeId).success)
    return <p role="alert">Invalid employee reference.</p>;
  if (!access.can(...recordPermissions(kind))) return <Denied />;
  return (
    <RecordsWorkspace
      key={JSON.stringify([
        authenticationLifetime.key,
        recordScope(user, status),
        employeeId.toLowerCase(),
        kind,
        revision,
      ])}
      employeeId={employeeId.toLowerCase()}
      kind={kind}
      onReset={() => setRevision((value) => value + 1)}
    />
  );
}
function RecordsWorkspace({
  employeeId,
  kind,
  onReset,
}: {
  employeeId: string;
  kind: RecordKind;
  onReset: () => void;
}) {
  const instance = useRecordCache();
  const access = useHrAccess();
  const mutation = useRecordMutation(employeeId);
  const [view, setView] = useState<'current' | 'history' | 'requests'>('current');
  const [page, setPage] = useState(1);
  const [state, setState] = useState('');
  const [asOfDraft, setAsOfDraft] = useState('');
  const [asOf, setAsOf] = useState('');
  const [filterError, setFilterError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  const [form, setForm] = useState<{ action: RecordAction; record?: OperationalRecord } | null>(
    null,
  );
  const [requestId, setRequestId] = useState<string | null>(null);
  const [custodyId, setCustodyId] = useState<string | null>(null);
  const family = familyOf(kind);
  const path = `employees/${employeeId}/${family.segment}${view === 'history' ? '/history' : ''}${queryString({ page, pageSize: 25, asOf: view === 'current' && asOf ? asOf : undefined })}`;
  const records = useRecordQuery(
    instance,
    path,
    operationalPageSchema(kind, employeeId, page, view === 'history', asOf),
    recordPermissions(kind),
    view !== 'requests',
  );
  const requests = useRecordQuery(
    instance,
    `employees/${employeeId}/records/${kind}/requests${queryString({ page, pageSize: 25, state: state === '' ? undefined : Number(state) })}`,
    requestPageSchema(employeeId, page, state === '' ? undefined : Number(state)),
    recordPermissions(kind),
    view === 'requests',
  );
  const locked = dirty || mutation.locked;
  useDirtyForm(locked);
  function closeDraft() {
    if (!dirty || window.confirm('Discard this unsaved record draft?')) {
      setForm(null);
      setRequestId(null);
      setDirty(false);
    }
  }
  function savedAction() {
    setForm(null);
    setRequestId(null);
    setDirty(false);
    setSaved(true);
    if (view === 'requests') void requests.refetch();
    else void records.refetch();
  }
  const query = view === 'requests' ? requests : records;
  return (
    <section className="employee-records" aria-label={family.label}>
      <div className="hr-repeat-heading">
        <h2>{family.label}</h2>
        <Button
          variant="ghost"
          title="Reload operational records"
          aria-label="Reload operational records"
          disabled={mutation.pending}
          onClick={() => {
            if (
              locked &&
              !window.confirm(
                mutation.uncertain
                  ? 'Discard the retained retry and reload to check whether the action completed?'
                  : 'Discard the draft and reload current records?',
              )
            )
              return;
            onReset();
          }}
        >
          <RefreshCw size={16} />
        </Button>
      </div>
      <div className="hr-tabs" role="group" aria-label="Record view">
        {(['current', 'history', 'requests'] as const).map((value) => (
          <button
            type="button"
            key={value}
            disabled={locked}
            className={view === value ? 'active' : ''}
            aria-pressed={view === value}
            onClick={() => {
              setView(value);
              setPage(1);
              setCustodyId(null);
              setRequestId(null);
              setForm(null);
            }}
          >
            {value === 'current' ? 'Current' : value === 'history' ? 'History' : 'Requests'}
          </button>
        ))}
      </div>
      {saved && (
        <p className="hr-message" role="status">
          The action was recorded.
        </p>
      )}
      {mutation.error && (
        <p role="alert" className="hr-message hr-message--error">
          {mutation.error}
        </p>
      )}
      {mutation.uncertain && (
        <p role="status" className="hr-message">
          The result is unconfirmed. Retry the unchanged action or reload and check its state.
        </p>
      )}
      {mutation.reloadRequired && (
        <p role="alert" className="hr-message">
          Reload and review current records before another action.
        </p>
      )}
      <div className="hr-toolbar">
        {view === 'requests' && (
          <RecordStateFilter
            value={state}
            disabled={locked}
            onChange={(value) => {
              setState(value);
              setPage(1);
              setRequestId(null);
            }}
          />
        )}
        {view === 'current' && (
          <form
            className="record-asof"
            onSubmit={(event) => {
              event.preventDefault();
              if (locked) return;
              const valid =
                asOfDraft === '' ||
                (kind === 'Asset'
                  ? z.iso.datetime({ offset: true }).safeParse(asOfDraft).success &&
                    (asOfDraft.endsWith('Z') || asOfDraft.endsWith('+00:00')) &&
                    Date.parse(asOfDraft) <= Date.now()
                  : z.iso.date().safeParse(asOfDraft).success);
              if (!valid) {
                setFilterError(
                  kind === 'Asset' ? 'Enter a past UTC instant.' : 'Enter a valid date.',
                );
                return;
              }
              setFilterError(null);
              setAsOf(asOfDraft);
              setPage(1);
              setForm(null);
              setCustodyId(null);
            }}
          >
            <TextInput
              label={kind === 'Asset' ? 'As of (UTC)' : 'As of date'}
              type={kind === 'Asset' ? 'text' : 'date'}
              disabled={locked}
              value={asOfDraft}
              onChange={(e) => setAsOfDraft(e.target.value)}
            />
            <Button type="submit" variant="secondary" disabled={locked}>
              <Clock size={16} />
              Apply
            </Button>
          </form>
        )}
        {view === 'current' &&
          !asOf &&
          !form &&
          access.can(...recordPermissions(kind, 'request')) && (
            <Button
              disabled={locked || query.isFetching || query.isError || !query.data}
              onClick={() => {
                setRequestId(null);
                setForm({ action: 1 });
              }}
            >
              <Plus size={16} />
              New issue request
            </Button>
          )}
      </div>
      {filterError && (
        <p role="alert" className="hr-message hr-message--error">
          {filterError}
        </p>
      )}
      {form && (
        <RecordRequestForm
          key={`${form.action}:${form.record?.id ?? 'issue'}`}
          instance={instance}
          employeeId={employeeId}
          kind={kind}
          action={form.action}
          record={form.record}
          mutation={mutation}
          onDirty={setDirty}
          onSaved={savedAction}
          onClose={closeDraft}
        />
      )}
      {requestId && (
        <RecordRequestDetailView
          key={requestId}
          instance={instance}
          employeeId={employeeId}
          kind={kind}
          requestId={requestId}
          mutation={mutation}
          onDirty={setDirty}
          onSaved={savedAction}
          onClose={closeDraft}
        />
      )}
      <QueryState query={query}>
        {view === 'requests' ? (
          <>
            {!requests.data?.items.length && (
              <p className="hr-notice">No requests match this filter.</p>
            )}
            {requests.data?.items.map((request) => (
              <article className="record-row" key={request.id}>
                <div>
                  <strong>{requestStates[request.state]}</strong>
                  <p>{labelDate(request.requestedAt)}</p>
                  <p className="muted">{request.id}</p>
                </div>
                <Button
                  variant="secondary"
                  disabled={locked}
                  onClick={() => {
                    setForm(null);
                    setRequestId(request.id);
                  }}
                >
                  Open request
                </Button>
              </article>
            ))}
          </>
        ) : (
          <>
            {!records.data?.items.length && (
              <p className="hr-notice">
                No {view === 'history' ? 'historical' : 'current'} records found.
              </p>
            )}
            {records.data?.items.map((record) => (
              <article className="record-row" key={record.id}>
                <div className="record-facts">
                  <h3>
                    {'assetReference' in record
                      ? record.assetReference
                      : 'simReference' in record
                        ? record.simReference
                        : `${record.type} ${record.number}`}
                  </h3>
                  <RecordView
                    value={
                      'returnedAt' in record
                        ? {
                            recordReference: record.id,
                            issuedAt: labelDate(record.issuedAt),
                            returnedAt: labelDate(record.returnedAt),
                            issueCondition: record.issueCondition,
                            returnCondition: record.returnCondition,
                            receivingAccount: record.returnedToAccountId,
                            actor: record.actorId,
                          }
                        : 'simReference' in record
                          ? {
                              recordReference: record.id,
                              phone: record.phoneNumber,
                              creditTier: record.creditTierCode,
                              state: ['Active', 'Suspended', 'Returned'][record.status - 1],
                              revision: record.revision,
                              issuedOn: record.issuedOn,
                              effectiveOn: record.effectiveOn,
                              returnedOn: record.returnedOn,
                              predecessor: record.supersedesId,
                              transferSource: record.transferredFromId,
                              issueEvidence: record.issueDocumentId,
                              returnEvidence: record.returnDocumentId,
                              receivingAccount: record.returnedToAccountId,
                              approvalRequest: record.approvalRequestId,
                              actor: record.actorId,
                              recordedAt: labelDate(record.recordedAt),
                              reason: record.reason,
                            }
                          : {
                              recordReference: record.id,
                              issuer: record.issuer,
                              state: ['Active', 'Suspended', 'Revoked'][record.status - 1],
                              expired: record.isExpired,
                              revision: record.revision,
                              issuedOn: record.issuedOn,
                              expiresOn: record.expiresOn,
                              effectiveOn: record.effectiveOn,
                              predecessor: record.supersedesId,
                              certificateEvidence: record.employeeDocumentId,
                              actionEvidence: record.actionDocumentId,
                              approvalRequest: record.approvalRequestId,
                              actor: record.actorId,
                              recordedAt: labelDate(record.recordedAt),
                              reason: record.reason,
                            }
                    }
                  />
                  {'returnedAt' in record && custodyId === record.id && (
                    <AssetEvents
                      key={record.id}
                      instance={instance}
                      employeeId={employeeId}
                      custodyId={record.id}
                    />
                  )}
                </div>
                <div className="hr-actions">
                  {'returnedAt' in record && (
                    <Button
                      variant="secondary"
                      disabled={locked}
                      onClick={() => setCustodyId(custodyId === record.id ? null : record.id)}
                    >
                      <Clock size={16} />
                      {custodyId === record.id ? 'Close events' : 'Custody events'}
                    </Button>
                  )}
                  {view === 'current' &&
                    !asOf &&
                    access.can(...recordPermissions(kind, 'request')) &&
                    supportedActions(kind, record).map((action) => (
                      <Button
                        variant="secondary"
                        key={action}
                        disabled={locked || records.isFetching || records.isError}
                        onClick={() => {
                          setRequestId(null);
                          setForm({ action, record });
                        }}
                      >
                        {actionLabels[action]}
                      </Button>
                    ))}
                </div>
              </article>
            ))}
          </>
        )}
        {query.data && (
          <Pagination
            page={page}
            pageSize={25}
            total={query.data.totalCount}
            disabled={locked || query.isFetching}
            onChange={(value) => {
              setPage(value);
              setForm(null);
              setRequestId(null);
              setCustodyId(null);
            }}
          />
        )}
      </QueryState>
    </section>
  );
}
function AssetEvents({
  instance,
  employeeId,
  custodyId,
}: {
  instance: string;
  employeeId: string;
  custodyId: string;
}) {
  const query = useRecordQuery(
    instance,
    `employees/${employeeId}/assets/${custodyId}/history`,
    assetEventsSchema(custodyId),
    recordPermissions('Asset'),
  );
  return (
    <section>
      <h4>Custody events</h4>
      <QueryState query={query}>
        {!query.data?.length && <p className="muted">No operational events recorded.</p>}
        {query.data?.map((event) => (
          <RecordView
            key={event.id}
            value={{
              action: event.action === 1 ? 'Issued' : 'Returned',
              revision: event.revision,
              condition: event.condition,
              evidence: event.evidenceDocumentId,
              receiverAccount: event.receiverAccountId,
              transferSource: event.transferredFromCustodyId,
              approvalRequest: event.approvalRequestId,
              actor: event.actorId,
              effectiveAt: labelDate(event.effectiveAt),
              recordedAt: labelDate(event.recordedAt),
              reason: event.reason,
            }}
          />
        ))}
      </QueryState>
    </section>
  );
}
