import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Calendar, Plus, RefreshCw } from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { Pagination } from '@/components/data-display/Pagination';
import { useDirtyForm } from '@/hooks/use-dirty-form';
import { canFoundationPolicy, foundationPolicyScope } from './access';
import {
  currentSchema,
  historySchema,
  policyDate,
  policyId,
  requestStateLabels,
  requestsSchema,
  sameId,
  type FoundationPolicyKind,
} from './contracts';
import { foundationPolicyEndpoint } from './inputs';
import { usePolicyLifetime, usePolicyMutation, usePolicyQuery } from './hooks';
import { PolicyFacts, PolicyQueryState, PolicyVersionView } from './PolicyView';
import { ProposalForm } from './ProposalForm';
import { FoundationPolicyRequestDetail } from './RequestDetail';
import './foundation-policies.css';

export type FoundationPolicyView = 'current' | 'history' | 'requests';
export function FoundationPoliciesWorkspace({
  kind,
  requestId,
  initialView = 'current',
  newProposal = false,
}: {
  kind: FoundationPolicyKind;
  requestId?: string;
  initialView?: FoundationPolicyView;
  newProposal?: boolean;
}) {
  const { user, status } = useAuth();
  const authentication = useMemo(
    () => ({ user, status, epoch: crypto.randomUUID() }),
    [user, status],
  );
  const epoch = authentication.epoch;
  const [revision, setRevision] = useState({ number: 0, saved: false, epoch: '' });
  if (!canFoundationPolicy(user, status, 'read'))
    return (
      <section role="alert" className="hr-notice">
        <h2>Access restricted</h2>
        <p>Your current permissions do not allow HR foundation policy administration.</p>
      </section>
    );
  if (requestId && !policyId.safeParse(requestId).success)
    return <p role="alert">Invalid policy request reference.</p>;
  return (
    <PolicyWorkspace
      key={JSON.stringify([
        epoch,
        foundationPolicyScope(user, status),
        kind,
        requestId,
        initialView,
        newProposal,
        revision.number,
      ])}
      kind={kind}
      requestId={requestId}
      initialView={initialView}
      newProposal={newProposal}
      recorded={revision.saved && revision.epoch === epoch}
      onReset={(saved = false) =>
        setRevision((value) => ({ number: value.number + 1, saved, epoch }))
      }
    />
  );
}
function PolicyWorkspace({
  kind,
  requestId,
  initialView,
  newProposal,
  recorded,
  onReset,
}: {
  kind: FoundationPolicyKind;
  requestId?: string;
  initialView: FoundationPolicyView;
  newProposal: boolean;
  recorded: boolean;
  onReset: (saved?: boolean) => void;
}) {
  const { user, status } = useAuth();
  const navigate = useNavigate();
  const lifetime = usePolicyLifetime();
  const mutation = usePolicyMutation(lifetime);
  const [view, setView] = useState<FoundationPolicyView>(requestId ? 'requests' : initialView);
  const [page, setPage] = useState(1);
  const [state, setState] = useState('');
  const [dateDraft, setDateDraft] = useState('');
  const [date, setDate] = useState('');
  const [dateError, setDateError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const endpoint = foundationPolicyEndpoint(kind);
  const path = `/hr/foundation-policies/${kind.toLowerCase()}`;
  const canRequest = canFoundationPolicy(user, status, 'request');
  const current = usePolicyQuery(
    lifetime,
    `${endpoint}/current${date ? `?date=${date}` : ''}`,
    currentSchema(kind, date || undefined),
  );
  const history = usePolicyQuery(
    lifetime,
    `${endpoint}/history?page=${view === 'history' ? page : 1}&pageSize=25`,
    historySchema(kind, view === 'history' ? page : 1),
    view === 'history' || (canRequest && view === 'current'),
  );
  const pending = usePolicyQuery(
    lifetime,
    `${endpoint}/requests?page=1&pageSize=25&state=Pending`,
    requestsSchema(kind, 1, 0),
    canRequest && view === 'current',
  );
  const stateNumber =
    state === ''
      ? undefined
      : requestStateLabels.indexOf(state as (typeof requestStateLabels)[number]);
  const requests = usePolicyQuery(
    lifetime,
    `${endpoint}/requests?page=${page}&pageSize=25${state ? `&state=${state}` : ''}`,
    requestsSchema(kind, page, stateNumber),
    view === 'requests' && !requestId,
  );
  const locked = dirty || mutation.locked;
  useDirtyForm(locked);
  const historyMatches = Boolean(
    history.data &&
    (!history.data.items.length
      ? (current.data?.policy?.latestVersion ?? 0) === 0
      : current.data?.policy &&
        sameId(history.data.items[0]!.identity.policyId, current.data.policy.id) &&
        history.data.items[0]!.identity.number === current.data.policy.latestVersion &&
        history.data.items[0]!.state === 'published'),
  );
  const proposalReady =
    canRequest &&
    !date &&
    !current.isFetching &&
    !current.isError &&
    Boolean(current.data) &&
    !history.isFetching &&
    !history.isError &&
    historyMatches &&
    !pending.isFetching &&
    !pending.isError &&
    pending.data?.totalCount === 0;
  function onSaved() {
    if (lifetime.signal().aborted) return;
    setDirty(false);
    onReset(true);
  }
  return (
    <section className="foundation-policies" aria-label={`${kind} foundation policy`}>
      <div className="foundation-heading">
        <h2>{kind} policy</h2>
        <Button
          variant="ghost"
          title="Reload policy records"
          aria-label="Reload policy records"
          disabled={mutation.pending}
          onClick={() => {
            if (
              locked &&
              !window.confirm(
                mutation.uncertain
                  ? 'Discard the retained retry and reload to check whether the action completed?'
                  : 'Discard the draft and reload policy records?',
              )
            )
              return;
            onReset();
          }}
        >
          <RefreshCw size={16} />
        </Button>
      </div>
      <nav className="foundation-kinds" aria-label="Policy family">
        {(['Fields', 'Lifecycle', 'Records'] as const).map((value) => (
          <Link
            key={value}
            to={`/hr/foundation-policies/${value.toLowerCase()}`}
            aria-current={value === kind ? 'page' : undefined}
          >
            {value}
          </Link>
        ))}
      </nav>
      {!requestId && !newProposal && (
        <div className="hr-tabs" role="group" aria-label="Policy view">
          {(['current', 'history', 'requests'] as const).map((value) => (
            <button
              type="button"
              key={value}
              disabled={locked}
              className={value === view ? 'active' : ''}
              aria-pressed={value === view}
              onClick={() => {
                setView(value);
                setPage(1);
              }}
            >
              {value === 'current'
                ? 'Current / as of'
                : value === 'history'
                  ? 'Publication history'
                  : 'Proposals'}
            </button>
          ))}
        </div>
      )}
      {recorded && (
        <p role="status" className="hr-message">
          The policy action was recorded.
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
          Reload and review current policy records before another action.
        </p>
      )}
      {requestId ? (
        <PolicyQueryState query={current}>
          {current.data?.policy ? (
            <FoundationPolicyRequestDetail
              kind={kind}
              requestId={requestId}
              expectedPolicyId={current.data.policy.id}
              lifetime={lifetime}
              mutation={mutation}
              onDirty={setDirty}
              onSaved={onSaved}
              onClose={() => navigate(`${path}/requests`)}
            />
          ) : (
            <p role="alert">The policy definition for this request is unavailable.</p>
          )}
        </PolicyQueryState>
      ) : (
        <>
          {view === 'current' && (
            <>
              <div className="foundation-toolbar">
                <form
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (locked) return;
                    if (dateDraft && !policyDate.safeParse(dateDraft).success) {
                      setDateError('Select a valid policy business date.');
                      return;
                    }
                    setDateError(null);
                    setDate(dateDraft);
                  }}
                >
                  <TextInput
                    label="As of business date"
                    type="date"
                    value={dateDraft}
                    disabled={locked}
                    onChange={(event) => setDateDraft(event.target.value)}
                  />
                  <Button type="submit" variant="secondary" disabled={locked}>
                    <Calendar size={16} />
                    Apply date
                  </Button>
                  {date && (
                    <Button
                      variant="secondary"
                      disabled={locked}
                      onClick={() => {
                        setDate('');
                        setDateDraft('');
                        setDateError(null);
                      }}
                    >
                      Current date
                    </Button>
                  )}
                </form>
                {canRequest && !newProposal && (
                  <Button
                    disabled={!proposalReady || locked}
                    onClick={() => void navigate(`${path}/requests/new`)}
                  >
                    <Plus size={16} />
                    New proposal
                  </Button>
                )}
              </div>
              {dateError && <p role="alert">{dateError}</p>}
              <PolicyQueryState query={current}>
                {current.data && (
                  <>
                    <PolicyFacts
                      rows={[
                        ['Selected business date', current.data.selectedDate],
                        [
                          'Resolution',
                          current.data.state === 'effective'
                            ? 'Effective'
                            : current.data.state === 'none'
                              ? 'No published policy'
                              : 'Unavailable',
                        ],
                        ['Publication count', current.data.policy?.latestVersion ?? 0],
                      ]}
                    />
                    {current.data.state === 'effective' && current.data.effective ? (
                      <PolicyVersionView version={current.data.effective} />
                    ) : (
                      <p
                        className="hr-notice"
                        role={current.data.state === 'unavailable' ? 'alert' : 'status'}
                      >
                        {current.data.state === 'none'
                          ? 'No policy has been published for this family.'
                          : 'An effective policy is unavailable for this date. Review the publication history.'}
                      </p>
                    )}
                  </>
                )}
              </PolicyQueryState>
              {canRequest && (
                <>
                  {history.isError && (
                    <p role="alert">
                      Latest publication history is unavailable. New proposals are disabled.
                    </p>
                  )}
                  {pending.isError && (
                    <p role="alert">
                      Pending proposal state is unavailable. New proposals are disabled.
                    </p>
                  )}
                  {pending.data && pending.data.totalCount > 0 && (
                    <p className="hr-notice">
                      A proposal is pending. <Link to={`${path}/requests`}>Review proposals</Link>
                    </p>
                  )}
                </>
              )}
              {newProposal && !canRequest && (
                <p role="alert">Your current permissions do not allow policy proposals.</p>
              )}
              {newProposal && !proposalReady && !current.isPending && (
                <p className="hr-notice">
                  A new proposal is unavailable until current policy, publication history and
                  pending proposals can be reviewed.
                </p>
              )}
              {newProposal && proposalReady && (
                <ProposalForm
                  kind={kind}
                  current={current.data!}
                  latest={history.data!.items[0] ?? null}
                  lifetime={lifetime}
                  mutation={mutation}
                  onDirty={setDirty}
                  onSaved={onSaved}
                  onClose={() => void navigate(path)}
                />
              )}
            </>
          )}
          {view === 'history' && (
            <PolicyQueryState query={history}>
              {!history.data?.items.length && (
                <p className="hr-notice">No policy versions have been published.</p>
              )}
              {history.data?.items.map((version) => (
                <section className="foundation-history-row" key={version.identity.versionId}>
                  <h3>Publication {version.identity.number}</h3>
                  <PolicyVersionView version={version} />
                  {version.approvalRequestId && (
                    <Link
                      className="foundation-link"
                      to={`${path}/requests/${version.approvalRequestId}`}
                    >
                      Open publication proposal
                    </Link>
                  )}
                </section>
              ))}
              {history.data && (
                <Pagination
                  page={page}
                  pageSize={25}
                  total={history.data.totalCount}
                  disabled={locked || history.isFetching}
                  onChange={setPage}
                />
              )}
            </PolicyQueryState>
          )}
          {view === 'requests' && (
            <>
              <SelectInput
                label="Proposal state"
                value={state}
                disabled={locked}
                options={[
                  { value: '', label: 'All states' },
                  ...requestStateLabels.map((value) => ({ value, label: value })),
                ]}
                onChange={(event) => {
                  setState(event.target.value);
                  setPage(1);
                }}
              />
              <PolicyQueryState query={requests}>
                {!requests.data?.items.length && (
                  <p className="hr-notice">No proposals match this filter.</p>
                )}
                {requests.data?.items.map((item) => (
                  <article className="foundation-request-row" key={item.id}>
                    <div>
                      <strong>{requestStateLabels[item.state]}</strong>
                      <p>{new Date(item.requestedAt).toISOString()}</p>
                      <p className="muted">{item.id}</p>
                    </div>
                    <Link className="foundation-link" to={`${path}/requests/${item.id}`}>
                      Open proposal
                    </Link>
                  </article>
                ))}
                {requests.data && (
                  <Pagination
                    page={page}
                    pageSize={25}
                    total={requests.data.totalCount}
                    disabled={locked || requests.isFetching}
                    onChange={setPage}
                  />
                )}
              </PolicyQueryState>
            </>
          )}
        </>
      )}
    </section>
  );
}
