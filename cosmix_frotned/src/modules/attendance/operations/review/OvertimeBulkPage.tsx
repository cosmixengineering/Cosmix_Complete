import { EmployeePicker } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { Plus, RefreshCw, X } from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useDirtyForm } from '@/hooks/use-dirty-form';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import {
  Command,
  MutationForm,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { date, id } from '../../schemas/common';
import { canEmployee, type Scope } from './access';
import {
  decisionInput,
  overtimeBulkResponse,
  overtimeDetail,
  overtimeInput,
  overtimeSource,
} from './contracts';
import { EvidenceField, RecordFacts, ScopeSelect, SourceFacts } from './ReviewUi';
import { overtimeBulkDecide, overtimeBulkSubmit, reviewedOvertime } from './intents';
import { overtimeReady, today } from './helpers';

type BatchItem = { employeeId: string; scope?: Scope } & (
  | { request: z.infer<typeof overtimeInput> }
  | { requestId: string; decision: z.infer<typeof decisionInput> }
);
export function AttendanceOvertimeBulkPage() {
  return (
    <PrivateWorkspace resource="overtime-named-batch">
      <BulkMode />
    </PrivateWorkspace>
  );
}
function BulkMode() {
  const [mode, setMode] = useState('requests');
  const [editing, setEditing] = useState(false);
  return (
    <>
      <h1>Named employee overtime batch</h1>
      <SelectInput
        label="Batch operation"
        value={mode}
        disabled={editing}
        options={[
          { value: 'requests', label: 'Submit explicit employee requests' },
          { value: 'decide', label: 'Decide explicit employee requests' },
        ]}
        onChange={(e) => {
          if (!editing) setMode(e.target.value);
        }}
      />
      <PrivateWorkspace resource={mode}>
        <BulkComposer decide={mode === 'decide'} onEditing={setEditing} />
      </PrivateWorkspace>
    </>
  );
}
function BulkComposer({
  decide,
  onEditing,
}: {
  decide: boolean;
  onEditing: (value: boolean) => void;
}) {
  const access = useAttendanceAccess();
  const [items, setItems] = useState<BatchItem[]>([]);
  const [adding, setAdding] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [reason, setReason] = useState('');
  const [notice, setNotice] = useState('');
  const permission = 'attendance.overtime.' + (decide ? 'decide' : 'submit');
  const allowed =
    items.length > 0 &&
    items.every(
      (item) =>
        canEmployee(access.user, permission, item.employeeId, item.scope) &&
        (!decide || item.employeeId !== access.user?.account?.employeeId),
    );
  function discard() {
    setItems([]);
    setReviewing(false);
    setAdding(false);
    setReason('');
    onEditing(false);
  }
  if (reviewing)
    return (
      <MutationForm
        title={decide ? 'Review atomic named decisions' : 'Review atomic named requests'}
        disabled={!allowed}
        build={() => ({
          path: '/attendance/overtime/bulk/' + (decide ? 'decide' : 'requests'),
          method: 'POST',
          schema: overtimeBulkResponse,
          body: decide
            ? overtimeBulkDecide.parse({
                employees: items.map(({ scope: _scope, ...item }) => item),
                reason,
              })
            : overtimeBulkSubmit.parse({
                employees: items.map(({ scope: _scope, ...item }) => item),
                reason,
              }),
        })}
        onCancel={discard}
        onSaved={(result) => {
          const batch = overtimeBulkResponse.parse(result);
          discard();
          setNotice(
            `Server batch ${batch.bulkId}: ${batch.employees.length} named records returned.`,
          );
        }}
        submitLabel={decide ? 'Submit named decisions' : 'Submit named requests'}
      >
        <RecordFacts value={items} />
        <EvidenceField label="Batch reason" max={1000} value={reason} onChange={setReason} />
        <p>
          Every employee, request, source and authority is checked before any item is applied. One
          stale, denied or self-review item rejects the entire batch.
        </p>
      </MutationForm>
    );
  return (
    <>
      <BatchDraftGuard dirty={items.length > 0 || adding} />
      <SavedNotice message={notice} />
      <p>{items.length} of 25 named entries staged in memory.</p>
      {items.map((item) => (
        <section className="attendance-section" key={item.employeeId}>
          <RecordFacts value={item} />
          <Command
            icon={X}
            variant="secondary"
            onClick={() => {
              setItems((old) => old.filter((row) => row.employeeId !== item.employeeId));
              onEditing(items.length > 1 || adding);
            }}
          >
            Remove entry
          </Command>
        </section>
      ))}
      {!adding && (
        <div className="attendance-commands">
          <Command
            icon={Plus}
            disabled={items.length >= 25}
            onClick={() => {
              setAdding(true);
              onEditing(true);
            }}
          >
            Add named employee
          </Command>
          <Command icon={RefreshCw} disabled={!allowed} onClick={() => setReviewing(true)}>
            Review batch
          </Command>
        </div>
      )}
      {adding && (
        <BulkCandidate
          decide={decide}
          onClose={() => {
            setAdding(false);
            onEditing(items.length > 0);
          }}
          onStage={(item) => {
            if (
              items.some(
                (row) =>
                  row.employeeId === item.employeeId ||
                  ('requestId' in row && 'requestId' in item && row.requestId === item.requestId),
              )
            )
              throw new Error('This employee or request is already staged.');
            setItems((old) => [...old, structuredClone(item)]);
            setAdding(false);
          }}
        />
      )}
    </>
  );
}
function BatchDraftGuard({ dirty }: { dirty: boolean }) {
  useDirtyForm(dirty);
  return null;
}
function BulkCandidate({
  decide,
  onClose,
  onStage,
}: {
  decide: boolean;
  onClose: () => void;
  onStage: (item: BatchItem) => void;
}) {
  const access = useAttendanceAccess();
  const [employeeId, setEmployee] = useState('');
  const [requestId, setRequest] = useState('');
  const [businessDate, setDate] = useState(today());
  const [scope, setScope] = useState<Scope>();
  const [loaded, setLoaded] = useState<{
    employeeId: string;
    requestId: string;
    businessDate: string;
    scope?: Scope;
  }>();
  const [sessionId, setSession] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState('');
  const [job, setJob] = useState('');
  const [decision, setDecision] = useState(1);
  const [error, setError] = useState('');
  const readPermission = 'attendance.overtime.' + (decide ? 'review' : 'submit');
  const allowed = Boolean(
    loaded && canEmployee(access.user, readPermission, loaded.employeeId, loaded.scope),
  );
  const path = decide
    ? `/attendance/overtime/employees/${loaded?.employeeId}/requests/${loaded?.requestId}`
    : `/attendance/employees/${loaded?.employeeId}/overtime/sources` +
      queryString({ businessDate: loaded?.businessDate });
  const query = useAttendanceQuery(path, decide ? overtimeDetail : overtimeSource, allowed);
  const value = !query.isError ? query.data : undefined;
  const source = value && ('source' in value ? value.source : value);
  const writeScope =
    source?.siteId &&
    canEmployee(
      access.user,
      'attendance.overtime.' + (decide ? 'decide' : 'submit'),
      loaded?.employeeId ?? '',
      { kind: 5, targetId: source.siteId },
    )
      ? { kind: 5, targetId: source.siteId }
      : loaded?.scope;
  const canWrite = Boolean(
    loaded &&
    source &&
    canEmployee(
      access.user,
      'attendance.overtime.' + (decide ? 'decide' : 'submit'),
      loaded.employeeId,
      writeScope,
    ) &&
    (!decide || loaded.employeeId !== access.user?.account?.employeeId),
  );
  function stage() {
    try {
      if (!loaded || !source || !canWrite || query.isFetching)
        throw new Error('Load and review an authorized source first.');
      if (decide) {
        if (!value || !('request' in value) || value.request.state !== 'Pending')
          throw new Error('Only pending requests can be decided.');
        if (decision === 1 && !overtimeReady(source))
          throw new Error('Approval readiness is unavailable.');
        onStage({
          employeeId: loaded.employeeId,
          scope: writeScope,
          requestId: loaded.requestId,
          decision: decisionInput.parse({
            expectedVersion: value.request.version,
            decision,
            reason,
          }),
        });
      } else
        onStage({
          employeeId: loaded.employeeId,
          scope: writeScope,
          request: reviewedOvertime(source, {
            businessDate: source.businessDate,
            workSessionId: sessionId,
            startsAtUtc: start,
            endsAtUtc: end,
            activity: 1,
            expectedSourceFingerprint: source.sourceFingerprint,
            reason,
            writtenEvidence: evidence,
            ...(job.trim() ? { jobReference: job } : {}),
          }),
        });
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Check named entry.');
    }
  }
  return (
    <section className="attendance-section">
      <h2>Named employee evidence</h2>
      {!loaded && (
        <form
          className="attendance-grid"
          onSubmit={(e) => {
            e.preventDefault();
            if (
              canEmployee(access.user, readPermission, employeeId, scope) &&
              (decide ? id.safeParse(requestId).success : date.safeParse(businessDate).success)
            )
              setLoaded({ employeeId, requestId, businessDate, scope });
          }}
        >
          <EmployeePicker label="Employee" value={employeeId} required onChange={setEmployee} />
          <ScopeSelect permission={readPermission} value={scope} onChange={setScope} />
          {decide ? (
            <TextInput
              label="Request ID"
              value={requestId}
              required
              onChange={(e) => setRequest(e.target.value.trim())}
            />
          ) : (
            <TextInput
              label="Business date"
              type="date"
              value={businessDate}
              onChange={(e) => setDate(e.target.value)}
            />
          )}
          <Command
            icon={RefreshCw}
            type="submit"
            disabled={!canEmployee(access.user, readPermission, employeeId, scope)}
          >
            Load named source
          </Command>
        </form>
      )}
      {loaded && (
        <>
          <QueryState query={query} />
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Reload named source
          </Command>
          {value && source && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                stage();
              }}
            >
              <fieldset disabled={query.isFetching || !canWrite}>
                <SourceFacts title="Exact retained named source" value={value} />
                {decide ? (
                  <SelectInput
                    label="Decision"
                    value={decision}
                    options={[
                      { value: '1', label: 'Approve' },
                      { value: '2', label: 'Return' },
                      { value: '3', label: 'Reject' },
                    ]}
                    onChange={(e) => setDecision(Number(e.target.value))}
                  />
                ) : (
                  <>
                    <SelectInput
                      label="Closed session"
                      value={sessionId}
                      required
                      options={[
                        { value: '', label: 'Select session' },
                        ...source.sessions
                          .filter((s) => s.endsAtUtc && s.validDutyEvidence)
                          .map((s) => ({
                            value: s.id,
                            label: `${s.startsAtUtc} to ${s.endsAtUtc}`,
                          })),
                      ]}
                      onChange={(e) => setSession(e.target.value)}
                    />
                    <TextInput
                      label="Start (UTC ISO timestamp)"
                      value={start}
                      required
                      onChange={(e) => setStart(e.target.value)}
                    />
                    <TextInput
                      label="End (UTC ISO timestamp)"
                      value={end}
                      required
                      onChange={(e) => setEnd(e.target.value)}
                    />
                    <TextInput
                      label="Job reference (optional)"
                      value={job}
                      maxLength={200}
                      onChange={(e) => setJob(e.target.value)}
                    />
                    <EvidenceField
                      label="Written evidence"
                      value={evidence}
                      onChange={setEvidence}
                    />
                  </>
                )}
                <EvidenceField label="Reason" max={1000} value={reason} onChange={setReason} />
                <Command
                  icon={Plus}
                  type="submit"
                  disabled={
                    !canWrite ||
                    (!decide && !overtimeReady(source)) ||
                    (decide && decision === 1 && !overtimeReady(source))
                  }
                >
                  Stage named entry
                </Command>
              </fieldset>
            </form>
          )}
        </>
      )}
      {error && (
        <p role="alert" className="attendance-error">
          {error}
        </p>
      )}
      <Command icon={X} variant="secondary" onClick={onClose}>
        Discard candidate
      </Command>
    </section>
  );
}
