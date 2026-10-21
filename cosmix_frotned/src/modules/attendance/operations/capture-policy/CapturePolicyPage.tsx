import { useState } from 'react';
import { ChevronLeft, ChevronRight, RefreshCw, Search, ShieldCheck } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { queryString } from '../../api/attendance-api';
import {
  Command,
  Denied,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { captureValidationPolicyPermissions as permissions, hasCapturePolicyGrant } from './access';
import { historySchema, policySchema, previewSchema } from './contracts';
import {
  capturePolicyEndpoint,
  reviewSource,
  utcFromInput,
  type PublicationSource,
} from './inputs';
import { PolicyView } from './PolicyView';
import { PublishPolicyForm } from './PublishPolicyForm';
import './capture-policy.css';

export function CaptureValidationPolicyPage() {
  return (
    <PrivateWorkspace resource="capture-validation-policy">
      <CapturePolicyWorkspace />
    </PrivateWorkspace>
  );
}

function CapturePolicyWorkspace() {
  const access = useAttendanceAccess();
  const allowed = access.ready && hasCapturePolicyGrant(access.user, permissions.read);
  const mayManage = allowed && hasCapturePolicyGrant(access.user, permissions.manage);
  const [asOf, setAsOf] = useState(() => new Date().toISOString().slice(0, 19));
  const [requested, setRequested] = useState(() => utcFromInput(asOf));
  const [page, setPage] = useState(1);
  const [form, setForm] = useState<PublicationSource | null>(null);
  const [message, setMessage] = useState('');
  const [inputError, setInputError] = useState('');
  const preview = useAttendanceQuery(
    capturePolicyEndpoint + '/preview' + queryString({ asOfUtc: requested }),
    previewSchema(requested),
    allowed,
  );
  const head = useAttendanceQuery(
    capturePolicyEndpoint + '/history' + queryString({ page: 1, pageSize: 25 }),
    historySchema(1),
    allowed,
  );
  const history = useAttendanceQuery(
    capturePolicyEndpoint + '/history' + queryString({ page, pageSize: 25 }),
    historySchema(page),
    allowed,
  );
  const fetching = preview.isFetching || head.isFetching || history.isFetching;
  let source: PublicationSource | null = null;
  let sourceError = '';
  if (preview.isSuccess && head.isSuccess && history.isSuccess && !fetching) {
    try {
      source = reviewSource(preview.data, head.data, history.data);
    } catch (failure) {
      sourceError =
        failure instanceof Error
          ? failure.message
          : 'Reload and review the current policy timeline.';
    }
  }

  function reload() {
    setForm(null);
    setInputError('');
    void preview.refetch();
    void head.refetch();
    if (page !== 1) void history.refetch();
  }
  function saved(result: unknown) {
    const confirmed = policySchema.parse(result);
    setMessage(`Published revision ${confirmed.revision}.`);
    reload();
  }
  if (!allowed)
    return <Denied>Capture policy access requires the assigned All-scoped read permission.</Denied>;
  const locked = Boolean(form) || fetching;
  return (
    <div className="capture-policy">
      <div className="attendance-heading">
        <h1>Attendance capture policy</h1>
        <Command icon={RefreshCw} variant="secondary" disabled={locked} onClick={reload}>
          Reload
        </Command>
      </div>
      <SavedNotice message={message} />
      <form
        className="capture-policy-filter"
        onSubmit={(event) => {
          event.preventDefault();
          if (locked) return;
          try {
            const next = utcFromInput(asOf);
            setInputError('');
            setMessage('');
            if (next === requested) void preview.refetch();
            else setRequested(next);
          } catch {
            setInputError('Enter a valid date and time in UTC.');
          }
        }}
      >
        <TextInput
          label="As of (UTC)"
          type="datetime-local"
          step={1}
          required
          value={asOf}
          disabled={locked}
          onChange={(event) => setAsOf(event.target.value)}
        />
        <Command icon={Search} type="submit" variant="secondary" disabled={locked}>
          Read policy
        </Command>
      </form>
      {inputError && (
        <p role="alert" className="attendance-error">
          {inputError}
        </p>
      )}
      {mayManage ? (
        form ? (
          <PublishPolicyForm
            source={form}
            actorId={access.user!.account!.accountId}
            allowed={mayManage}
            onSaved={saved}
            onCancel={reload}
          />
        ) : (
          <div className="attendance-commands">
            <Command
              icon={ShieldCheck}
              disabled={!source || fetching || source.preview.latestRevision >= 512}
              onClick={() => {
                if (source && !fetching) {
                  setMessage('');
                  setForm(source);
                }
              }}
            >
              Publish future policy
            </Command>
          </div>
        )
      ) : (
        <Denied>
          Publication requires an assigned All-scoped manage permission and server-confirmed Admin
          authority.
        </Denied>
      )}
      {sourceError && (
        <p role="alert" className="attendance-error">
          {sourceError}
        </p>
      )}
      {source?.preview.latestRevision === 512 && (
        <p role="status">The server policy history limit has been reached.</p>
      )}
      <section className="attendance-section" aria-label="Effective capture policy">
        <h2>Effective configuration</h2>
        <QueryState query={preview} />
        {!preview.isError && preview.data && (
          <>
            <p>
              As of <time dateTime={preview.data.asOfUtc}>{preview.data.asOfUtc}</time>
            </p>
            {preview.data.state === 'not_configured' ? (
              <p className="attendance-empty">No effective policy at the selected UTC time.</p>
            ) : (
              preview.data.effective && <PolicyView policy={preview.data.effective} />
            )}
            <details>
              <summary>Timeline evidence</summary>
              <dl className="attendance-record">
                <dt>Latest published revision</dt>
                <dd>{preview.data.latestRevision}</dd>
                <dt>Timeline fingerprint</dt>
                <dd>{preview.data.timelineFingerprint}</dd>
              </dl>
            </details>
          </>
        )}
      </section>
      <section className="attendance-section" aria-label="Capture policy publication history">
        <h2>Publication history</h2>
        <QueryState
          query={history}
          empty={history.data?.items.length === 0}
          emptyText="No policy publications found."
        />
        {page !== 1 && <QueryState query={head} />}
        {!history.isError && history.data && (
          <>
            <p>{history.data.totalCount} published versions</p>
            {history.data.items.map((row) => (
              <details className="capture-policy-history" key={row.id}>
                <summary>
                  Revision {row.revision}: {row.effectiveFromUtc}
                </summary>
                <PolicyView policy={row} />
              </details>
            ))}
            <nav className="attendance-commands" aria-label="Policy history pagination">
              <Command
                icon={ChevronLeft}
                variant="secondary"
                disabled={locked || page === 1}
                onClick={() => setPage((value) => value - 1)}
              >
                Previous page
              </Command>
              <span>Page {page}</span>
              <Command
                icon={ChevronRight}
                variant="secondary"
                disabled={locked || page * 25 >= history.data.totalCount}
                onClick={() => setPage((value) => value + 1)}
              >
                Next page
              </Command>
            </nav>
          </>
        )}
      </section>
    </div>
  );
}
