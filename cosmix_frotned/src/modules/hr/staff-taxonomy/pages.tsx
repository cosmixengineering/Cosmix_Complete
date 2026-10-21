import { useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { z } from 'zod';
import { Plus, RefreshCw, Search, X } from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { Denied } from '../components/Feedback';
import { useDirtyForm } from '../hooks/use-dirty-form';
import { canTaxonomy, taxonomyScope } from './access';
import {
  businessDate,
  categoryCode,
  historyPageSchema,
  policySchema,
  requestPageSchema,
  resolutionSchema,
  rulesSchema,
  taxonomyId,
  type Rules,
  type TaxonomyPolicy,
} from './contracts';
import { useTaxonomyMutation, useTaxonomyQuery } from './hooks';
import { TaxonomySession } from './session';
import { ProposalForm } from './ProposalForm';
import { RequestDetail } from './RequestDetail';
import { CapabilityBoundary, MappingView, ReadState } from './Views';
import { resolutionLabels } from './messages';
import './staff-taxonomy.css';

type View = 'Current' | 'Effective' | 'History' | 'Requests';
function TaxonomyPage({
  initialView = 'Current',
  requestId,
  newRequest = false,
}: {
  initialView?: View;
  requestId?: string;
  newRequest?: boolean;
}) {
  const { user, status } = useAuth();
  const epoch = useMemo(() => ({ user, status, key: crypto.randomUUID() }), [user, status]);
  const scope = JSON.stringify([epoch.key, taxonomyScope(user, status)]);
  const [generation, setGeneration] = useState(0);
  const [saved, setSaved] = useState<string | null>(null);
  if (requestId && !taxonomyId.safeParse(requestId).success)
    return <p role="alert">Invalid taxonomy request reference.</p>;
  if (!canTaxonomy(user, status, 'read') || (newRequest && !canTaxonomy(user, status, 'request')))
    return <Denied />;
  return (
    <section className="staff-taxonomy" aria-label="Staff taxonomy">
      <div className="hr-page-heading">
        <h1>Staff taxonomy</h1>
      </div>
      {saved === scope && (
        <p role="status" className="hr-message">
          The action was recorded.
        </p>
      )}
      <TaxonomySession
        key={JSON.stringify([scope, generation, initialView, requestId, newRequest])}
      >
        <TaxonomyWorkspace
          initialView={initialView}
          initialRequestId={requestId}
          newRequest={newRequest}
          onReset={(successful = false) => {
            setSaved(successful ? scope : null);
            setGeneration((value) => value + 1);
          }}
        />
      </TaxonomySession>
    </section>
  );
}
export function StaffTaxonomyPage() {
  return <TaxonomyPage />;
}
export function StaffTaxonomyRequestsPage() {
  return <TaxonomyPage initialView="Requests" />;
}
export function StaffTaxonomyNewRequestPage() {
  return <TaxonomyPage newRequest />;
}
export function StaffTaxonomyRequestPage() {
  const { requestId = '' } = useParams();
  if (!taxonomyId.safeParse(requestId).success)
    return <p role="alert">Invalid taxonomy request reference.</p>;
  return <TaxonomyPage initialView="Requests" requestId={requestId} />;
}
function TaxonomyWorkspace({
  initialView,
  initialRequestId,
  newRequest,
  onReset,
}: {
  initialView: View;
  initialRequestId?: string;
  newRequest: boolean;
  onReset: (successful?: boolean) => void;
}) {
  const { user, status } = useAuth();
  const [view, setView] = useState(initialView);
  const [page, setPage] = useState(1);
  const [dirty, setDirty] = useState(newRequest);
  const [proposal, setProposal] = useState<{ source?: Rules } | null>(newRequest ? {} : null);
  const [requestId, setRequestId] = useState<string | null>(initialRequestId ?? null);
  const mutation = useTaxonomyMutation();
  const policy = useTaxonomyQuery(
    '',
    policySchema.nullish().transform((value) => value ?? null),
  );
  const locked = dirty || mutation.locked;
  useDirtyForm(locked);
  function discard() {
    if (mutation.pending) return;
    if (
      locked &&
      !window.confirm(
        mutation.uncertain
          ? 'Discard the retained retry and reload to check whether the action completed?'
          : 'Discard unsaved changes and reload current taxonomy state?',
      )
    )
      return;
    onReset();
  }
  return (
    <>
      <div className="taxonomy-toolbar">
        <div className="hr-tabs" role="group" aria-label="Taxonomy view">
          {(['Current', 'Effective', 'History', 'Requests'] as const).map((tab) => (
            <button
              type="button"
              key={tab}
              disabled={locked}
              aria-pressed={view === tab}
              className={view === tab ? 'active' : ''}
              onClick={() => {
                setView(tab);
                setPage(1);
                setRequestId(null);
                setProposal(null);
              }}
            >
              {tab}
            </button>
          ))}
        </div>
        <Button
          variant="ghost"
          title="Reload taxonomy state"
          aria-label="Reload taxonomy state"
          disabled={mutation.pending}
          onClick={discard}
        >
          <RefreshCw size={16} />
        </Button>
      </div>
      <ReadState query={policy} locked={mutation.locked}>
        {policy.data !== undefined && (
          <>
            {!proposal && !requestId && canTaxonomy(user, status, 'request') && (
              <div className="taxonomy-toolbar">
                <Button
                  disabled={locked || policy.isFetching || policy.isError}
                  onClick={() => {
                    setProposal({});
                    setRequestId(null);
                    setDirty(true);
                  }}
                >
                  <Plus size={16} />
                  New proposal
                </Button>
              </div>
            )}
            {(proposal || requestId) && (
              <div className="taxonomy-toolbar">
                <Button
                  variant="ghost"
                  title="Close proposal"
                  aria-label="Close proposal"
                  disabled={mutation.locked}
                  onClick={() => {
                    if (dirty && !window.confirm('Discard unsaved proposal changes?')) return;
                    setProposal(null);
                    setRequestId(null);
                    setDirty(false);
                  }}
                >
                  <X size={16} />
                </Button>
              </div>
            )}
            {proposal ? (
              <ProposalForm
                policy={policy.data}
                source={proposal.source}
                mutation={mutation}
                onDirty={() => setDirty(true)}
                onSaved={() => onReset(true)}
              />
            ) : requestId ? (
              policy.data ? (
                <RequestDetail
                  key={requestId}
                  policy={policy.data}
                  requestId={requestId}
                  mutation={mutation}
                  onDirty={() => setDirty(true)}
                  onSaved={() => onReset(true)}
                />
              ) : (
                <p role="alert">
                  No taxonomy policy exists for this request. Reload current state.
                </p>
              )
            ) : view === 'Current' ? (
              <CurrentView policy={policy.data} />
            ) : view === 'Effective' ? (
              <EffectiveView />
            ) : view === 'History' ? (
              <HistoryView
                policy={policy.data}
                page={page}
                onPage={setPage}
                canRequest={canTaxonomy(user, status, 'request')}
                onProposal={(source) => {
                  setProposal({ source });
                  setDirty(true);
                }}
              />
            ) : (
              <RequestsView
                policy={policy.data}
                page={page}
                onPage={setPage}
                onOpen={setRequestId}
              />
            )}
          </>
        )}
      </ReadState>
    </>
  );
}
function CurrentView({ policy }: { policy: TaxonomyPolicy | null }) {
  return (
    <>
      <h2>Current policy record</h2>
      {policy ? (
        <dl className="taxonomy-facts">
          <div>
            <dt>Code</dt>
            <dd>{policy.code}</dd>
          </div>
          <div>
            <dt>Policy</dt>
            <dd>{policy.id}</dd>
          </div>
          <div>
            <dt>Revision</dt>
            <dd>{policy.revision}</dd>
          </div>
          <div>
            <dt>Current rowversion</dt>
            <dd>{policy.version}</dd>
          </div>
        </dl>
      ) : (
        <p>No reserved taxonomy policy record exists.</p>
      )}
      <p className="taxonomy-notice">
        Current workflow revision. Effective mapping status is not supplied with this record.
      </p>
      <CapabilityBoundary />
    </>
  );
}
function EffectiveView() {
  const [code, setCode] = useState('');
  const [on, setOn] = useState(businessDate());
  const [filter, setFilter] = useState<{ code: string; on: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const path = filter
    ? '/effective?' + new URLSearchParams({ categoryCode: filter.code, on: filter.on })
    : '/effective';
  const query = useTaxonomyQuery(
    path,
    resolutionSchema(filter?.code ?? '', filter?.on ?? ''),
    filter !== null,
  );
  return (
    <>
      <h2>Effective mapping</h2>
      <form
        className="taxonomy-toolbar"
        onSubmit={(e) => {
          e.preventDefault();
          const c = categoryCode.safeParse(code.trim().toUpperCase());
          const d = z.iso.date().safeParse(on);
          if (!c.success || !d.success || on === '0001-01-01' || on === '9999-12-31') {
            setError('Enter a valid category code and supported date.');
            return;
          }
          setError(null);
          setFilter({ code: c.data, on });
        }}
      >
        <TextInput
          label="Category code"
          value={code}
          maxLength={30}
          onChange={(e) => setCode(e.target.value)}
        />
        <TextInput label="On date" type="date" value={on} onChange={(e) => setOn(e.target.value)} />
        <Button type="submit" disabled={query.isFetching}>
          <Search size={16} />
          Resolve mapping
        </Button>
      </form>
      {error && <p role="alert">{error}</p>}
      {filter && (
        <ReadState query={query}>
          {query.data && (
            <>
              <h3>{resolutionLabels[query.data.state]}</h3>
              <dl className="taxonomy-facts">
                <div>
                  <dt>Category / date</dt>
                  <dd>
                    {filter.code} / {filter.on}
                  </dd>
                </div>
                <div>
                  <dt>Policy version</dt>
                  <dd>{query.data.policyVersionId ?? 'None'}</dd>
                </div>
                <div>
                  <dt>Mapping fingerprint</dt>
                  <dd>{query.data.fingerprint}</dd>
                </div>
                <div>
                  <dt>Effective period</dt>
                  <dd>
                    {query.data.effectiveFrom ?? 'Baseline'} /{' '}
                    {query.data.effectiveTo ?? 'Open ended'}
                  </dd>
                </div>
              </dl>
              {query.data.mapping && <MappingView mapping={query.data.mapping} />}
              <CapabilityBoundary />
            </>
          )}
        </ReadState>
      )}
    </>
  );
}
function HistoryView({
  policy,
  page,
  onPage,
  canRequest,
  onProposal,
}: {
  policy: TaxonomyPolicy | null;
  page: number;
  onPage: (page: number) => void;
  canRequest: boolean;
  onProposal: (rules: Rules) => void;
}) {
  const query = useTaxonomyQuery(
    `/history?page=${page}&pageSize=25`,
    historyPageSchema(policy?.id ?? null, page),
  );
  return (
    <>
      <h2>Publication history</h2>
      <ReadState query={query}>
        {query.data && (
          <>
            {!query.data.items.length && <p>No retained publications on this page.</p>}
            {query.data.items.map((v) => (
              <article key={v.id} className="taxonomy-publication">
                <h3>Version {v.number}</h3>
                <dl className="taxonomy-facts">
                  <div>
                    <dt>Publication</dt>
                    <dd>{v.id}</dd>
                  </div>
                  <div>
                    <dt>Period</dt>
                    <dd>
                      {v.effectiveFrom} / {v.effectiveTo ?? 'Open ended'}
                    </dd>
                  </div>
                  <div>
                    <dt>Evidence</dt>
                    <dd>{v.evidenceState === 'verified' ? 'Verified' : 'Invalid evidence'}</dd>
                  </div>
                  <div>
                    <dt>Published by / at</dt>
                    <dd>
                      {v.publisherId} / {v.publishedAt}
                    </dd>
                  </div>
                  <div>
                    <dt>Configuration fingerprint</dt>
                    <dd>{v.configurationSha256}</dd>
                  </div>
                </dl>
                {v.evidenceState === 'verified' ? (
                  <>
                    {rulesSchema.parse(JSON.parse(v.rulesJson)).categories.map((m) => (
                      <MappingView key={m.code} mapping={m} />
                    ))}
                    {canRequest && (
                      <Button
                        variant="secondary"
                        onClick={() => onProposal(rulesSchema.parse(JSON.parse(v.rulesJson)))}
                      >
                        <Plus size={16} />
                        Use as proposal
                      </Button>
                    )}
                  </>
                ) : (
                  <p role="alert">
                    These rules are unavailable because publication evidence is invalid.
                  </p>
                )}
              </article>
            ))}
            <Pagination
              page={page}
              pageSize={25}
              total={query.data.totalCount}
              disabled={query.isFetching}
              onChange={onPage}
            />
          </>
        )}
      </ReadState>
    </>
  );
}
function RequestsView({
  policy,
  page,
  onPage,
  onOpen,
}: {
  policy: TaxonomyPolicy | null;
  page: number;
  onPage: (page: number) => void;
  onOpen: (id: string) => void;
}) {
  const query = useTaxonomyQuery(
    `/requests?page=${page}&pageSize=25`,
    requestPageSchema(policy?.id ?? null, page),
  );
  return (
    <>
      <h2>Taxonomy requests</h2>
      <ReadState query={query}>
        {query.data && (
          <>
            {!query.data.items.length && <p>No retained requests on this page.</p>}
            {query.data.items.map((r) => (
              <article className="taxonomy-request-row" key={r.id}>
                <div>
                  <strong>{r.state}</strong>
                  <p>{r.requestedAt}</p>
                  <p>{r.id}</p>
                  <p>Requester: {r.requesterId}</p>
                  {r.closedAt && <p>Closed: {r.closedAt}</p>}
                </div>
                <Button
                  variant="secondary"
                  disabled={query.isFetching}
                  onClick={() => onOpen(r.id)}
                >
                  Open request
                </Button>
              </article>
            ))}
            <Pagination
              page={page}
              pageSize={25}
              total={query.data.totalCount}
              disabled={query.isFetching}
              onChange={onPage}
            />
          </>
        )}
      </ReadState>
    </>
  );
}
