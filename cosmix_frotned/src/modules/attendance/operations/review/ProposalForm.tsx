import { useState } from 'react';
import { RefreshCw, ArrowRight, X, CalendarCheck2 } from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Command, MutationForm, QueryState } from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { id, date } from '../../schemas/common';
import {
  carryInput,
  carrySource,
  correctionInput,
  correctionSource,
  overtimeInput,
  overtimeSource,
} from './contracts';
import { EvidenceField, SourceFacts } from './ReviewUi';
import { overtimeReady, today } from './helpers';
import { workflowPaths, workflows, type Workflow } from './workflows';
import { reviewedOvertime } from './intents';
import '@/styles/attendance.css';
import '@/styles/attendance-register.css';
import '@/styles/attendance-corrections.css';

type Source =
  z.infer<typeof correctionSource> | z.infer<typeof carrySource> | z.infer<typeof overtimeSource>;
export function ProposalForm({
  workflow,
  employeeId,
  self,
  allowed,
  resubmission,
  onClose,
  onSaved,
}: {
  workflow: Workflow;
  employeeId: string;
  self: boolean;
  allowed: boolean;
  resubmission?: { id: string; date: string };
  onClose: () => void;
  onSaved: (value: unknown) => void;
}) {
  const [businessDate, setDate] = useState(resubmission?.date ?? today());
  const [rawPunchId, setRaw] = useState('');
  const [preview, setPreview] = useState<{ businessDate: string; rawPunchId?: string }>();
  const [source, setSource] = useState<Source>();
  const paths = workflowPaths(workflow, employeeId, self);
  const query = useAttendanceQuery(
    paths.source + queryString(preview ?? {}),
    workflows[workflow].source,
    allowed && Boolean(preview) && !source,
  );
  const valid =
    date.safeParse(businessDate).success && (!rawPunchId || id.safeParse(rawPunchId).success);
  if (source)
    return (
      <PinnedProposal
        workflow={workflow}
        source={source}
        path={paths.requests}
        resubmissionId={resubmission?.id}
        allowed={allowed}
        onClose={onClose}
        onSaved={onSaved}
      />
    );
  return (
    <div className="att-proposal-source-card">
      <div className="att-proposal-header">
        <div className="att-proposal-icon-box">
          <CalendarCheck2 size={18} />
        </div>
        <div>
          <h2 className="att-proposal-title">
            {resubmission ? 'Fresh resubmission source' : 'New request source'}
          </h2>
          <p className="att-proposal-sub">
            Query the attendance engine for historical roster, punches, and biometric logs.
          </p>
        </div>
      </div>

      <form
        className="att-source-form-grid"
        onSubmit={(e) => {
          e.preventDefault();
          if (valid)
            setPreview({
              businessDate,
              rawPunchId: workflow === 'overtime' ? undefined : rawPunchId || undefined,
            });
        }}
      >
        <TextInput
          label="Shift-start business date"
          type="date"
          value={businessDate}
          required
          onChange={(e) => {
            setPreview(undefined);
            setDate(e.target.value);
          }}
        />
        {workflow !== 'overtime' && (
          <TextInput
            label="Retained raw punch ID (optional)"
            value={rawPunchId}
            onChange={(e) => {
              setPreview(undefined);
              setRaw(e.target.value.trim());
            }}
          />
        )}
        <div className="att-source-form-actions">
          <Command icon={RefreshCw} type="submit" disabled={!allowed || !valid || query.isFetching}>
            Load source
          </Command>
          {preview && (
            <Command
              icon={RefreshCw}
              variant="secondary"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              Reload source
            </Command>
          )}
        </div>
      </form>

      {preview && (
        <div className="att-source-preview-area">
          <QueryState query={query} />
          {!query.isError && query.data && !query.isFetching && (
            <>
              <SourceFacts value={query.data} />
              <div className="att-source-bottom-actions">
                <button
                  type="button"
                  className="att-btn-primary"
                  disabled={!allowed}
                  onClick={() => setSource(structuredClone(query.data))}
                >
                  <ArrowRight size={14} />
                  <span>Review proposal against this source</span>
                </button>
                <button type="button" className="att-btn-secondary" onClick={onClose}>
                  <X size={14} />
                  <span>Close source selection</span>
                </button>
              </div>
            </>
          )}
        </div>
      )}

      {!preview && (
        <div className="att-source-bottom-actions" style={{ marginTop: '12px' }}>
          <button type="button" className="att-btn-secondary" onClick={onClose}>
            <X size={14} />
            <span>Close source selection</span>
          </button>
        </div>
      )}
    </div>
  );
}
function PinnedProposal({
  workflow,
  source,
  path,
  resubmissionId,
  allowed,
  onClose,
  onSaved,
}: {
  workflow: Workflow;
  source: Source;
  path: string;
  resubmissionId?: string;
  allowed: boolean;
  onClose: () => void;
  onSaved: (value: unknown) => void;
}) {
  const [kind, setKind] = useState(2);
  const [selectedSession, setSession] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState('');
  const [job, setJob] = useState('');
  const correctionValue =
    'sessionRevisions' in source || 'periodLocked' in source
      ? correctionSource.parse(source)
      : undefined;
  const carry = 'sourceLockEventId' in source ? carrySource.parse(source) : undefined;
  const ot = 'staffCategory' in source ? overtimeSource.parse(source) : undefined;
  const supported = ot
    ? overtimeReady(ot)
    : carry
      ? carry.historicalRoster.status === 'scheduled' && Boolean(carry.historicalRoster.siteId)
      : Boolean(
          correctionValue?.sourceFingerprint &&
          !correctionValue.periodLocked &&
          correctionValue.historicalRoster?.status === 'scheduled' &&
          correctionValue.historicalRoster.siteId,
        );
  const sessions = carry
    ? carry.sessions.map((s) => ({
        value: s.sessionKey,
        label: `${s.startsAtUtc} to ${s.endsAtUtc} / ${s.sessionKey}`,
      }))
    : ot
      ? ot.sessions
          .filter((s) => s.endsAtUtc && s.validDutyEvidence)
          .map((s) => ({
            value: s.id,
            label: `${s.startsAtUtc} to ${s.endsAtUtc} / revision ${s.revision}`,
          }))
      : (correctionValue?.sessionRevisions ?? []).map((s) => ({
          value: s.id,
          label: `${s.startsAtUtc} to ${s.endsAtUtc ?? 'Open'} / revision ${s.revision}`,
        }));
  function build() {
    if (!allowed || !supported) throw new Error('Source authority or readiness is unavailable.');
    const common = {
      reason,
      writtenEvidence: evidence,
      expectedSourceFingerprint: source.sourceFingerprint,
    };
    let body: unknown;
    if (ot) {
      const selected = ot.sessions.find(
        (s) => s.id === selectedSession && s.endsAtUtc && s.validDutyEvidence,
      );
      if (!selected) throw new Error('Select a retained closed duty session.');
      body = reviewedOvertime(
        ot,
        overtimeInput.parse({
          ...common,
          businessDate: ot.businessDate,
          workSessionId: selected.id,
          startsAtUtc: start,
          endsAtUtc: end,
          activity: 1,
          ...(job.trim() ? { jobReference: job } : {}),
          ...(resubmissionId ? { resubmissionOfRequestId: resubmissionId } : {}),
        }),
      );
    } else if (carry) {
      if (kind !== 4 && !carry.sessions.some((s) => s.sessionKey === selectedSession))
        throw new Error('Select an exact retained session key.');
      body = carryInput.parse({
        ...common,
        sourceBusinessDate: carry.sourceBusinessDate,
        kind,
        targetSessionKey: kind === 4 ? null : selectedSession,
        rawPunchId: carry.rawPunchId,
        requestedTimeUtc: start,
        requestedEndUtc: kind === 4 ? end : null,
        expectedSourceLockEventId: carry.sourceLockEventId,
        expectedSourceSummaryId: carry.sourceSummaryId,
        expectedPriorAdjustmentId: carry.priorAdjustmentId,
        expectedTargetMonthStart: carry.targetMonthStart,
        ...(resubmissionId ? { resubmissionOfRequestId: resubmissionId } : {}),
      });
    } else if (correctionValue) {
      if (
        kind !== 4 &&
        selectedSession &&
        !(correctionValue.sessionRevisions ?? []).some((s) => s.id === selectedSession)
      )
        throw new Error('Select a retained session revision.');
      body = correctionInput.parse({
        ...common,
        businessDate: correctionValue.businessDate,
        kind,
        rawPunchId: correctionValue.rawPunchId,
        expectedAssessmentId: correctionValue.latestAssessmentId,
        expectedSummaryId: correctionValue.latestSummaryId,
        requestedTimeUtc: start,
        requestedEndUtc: kind === 4 ? end : null,
        requestedStatus: null,
        ...(kind !== 4 && selectedSession ? { targetWorkSessionId: selectedSession } : {}),
        ...(resubmissionId ? { resubmissionOfCorrectionId: resubmissionId } : {}),
      });
    } else throw new Error('Source contract unavailable.');
    return { path, body, method: 'POST' as const, schema: workflows[workflow].response };
  }
  return (
    <MutationForm
      title={`Submit ${workflows[workflow].title.toLowerCase()}`}
      build={build}
      disabled={!allowed || !supported}
      onCancel={onClose}
      onSaved={onSaved}
      submitLabel="Submit for HR review"
    >
      <SourceFacts value={source} />
      {!supported && (
        <p role="status">
          {ot?.staffCategory === 'OFFICE'
            ? 'Office staff are ineligible for overtime.'
            : correctionValue?.periodLocked
              ? 'Source period is locked. Use the separate carry-forward request flow.'
              : 'Source roster, eligibility, fingerprint or duration-policy readiness is unavailable.'}
        </p>
      )}
      {!ot && (
        <SelectInput
          label="Correction kind"
          value={kind}
          options={[
            { value: '1', label: 'Check In time' },
            { value: '2', label: 'Check Out time / missing checkout' },
            { value: '4', label: 'Documented manual duty session' },
          ]}
          onChange={(e) => {
            setKind(Number(e.target.value));
            setSession('');
            setStart('');
            setEnd('');
          }}
        />
      )}
      {(ot || kind !== 4) && (
        <SelectInput
          label={carry ? 'Retained session key' : 'Exact session revision'}
          value={selectedSession}
          required={Boolean(ot || carry || !correctionValue?.rawPunchId)}
          options={[
            {
              value: '',
              label: correctionValue?.rawPunchId
                ? 'Use retained raw punch'
                : 'Select retained session',
            },
            ...sessions,
          ]}
          onChange={(e) => setSession(e.target.value)}
        />
      )}
      <TextInput
        label={
          kind === 4 || ot
            ? 'Proposed start (UTC, ISO timestamp)'
            : 'Proposed event time (UTC, ISO timestamp)'
        }
        placeholder="YYYY-MM-DDTHH:mm:ssZ"
        value={start}
        required
        onChange={(e) => setStart(e.target.value)}
      />
      {(kind === 4 || ot) && (
        <TextInput
          label="Proposed end (UTC, ISO timestamp)"
          placeholder="YYYY-MM-DDTHH:mm:ssZ"
          value={end}
          required
          onChange={(e) => setEnd(e.target.value)}
        />
      )}
      {ot && (
        <>
          <TextInput
            label="Job reference (optional)"
            value={job}
            maxLength={200}
            onChange={(e) => setJob(e.target.value)}
          />
          <p>
            Work activity only. Company-travel approval policy is unavailable. Eligibility and
            duration remain server decisions.
          </p>
        </>
      )}
      <EvidenceField label="Reason" max={1000} value={reason} onChange={setReason} />
      <EvidenceField label="Written evidence" value={evidence} onChange={setEvidence} />
      <p>
        Submission records a proposal for a different authorized HR reviewer. Raw punches and face
        evidence remain immutable. DayStatus, file uploads and payroll settlement are unavailable in
        this contract.
      </p>
    </MutationForm>
  );
}
