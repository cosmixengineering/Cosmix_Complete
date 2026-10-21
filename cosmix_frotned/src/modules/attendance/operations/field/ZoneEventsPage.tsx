import { useState } from 'react';
import { z } from 'zod';
import { useParams } from 'react-router-dom';
import { Check, ChevronLeft, ChevronRight, MessageSquare, Search, ShieldCheck } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import { Command, Denied, MutationForm, QueryState } from '../../components/SchedulingUi';
import { id } from '../../schemas/common';
import { fieldPermission } from './access';
import {
  dutyWindow,
  eventResponseInput,
  eventsPage,
  myZones,
  zoneEvent,
  type ZoneEvent,
} from './contracts';
import { RecordFields, Reload, SiteWorkspace } from './FieldUi';
import { BoundaryView } from './BoundaryView';
import { FieldWorkspace } from './FieldWorkspace';

function EventForm({
  event,
  kind,
  path,
  close,
}: {
  event: ZoneEvent;
  kind: number;
  path: string;
  close: () => void;
}) {
  const [reason, setReason] = useState('');
  return (
    <MutationForm
      title={
        kind === 3
          ? 'Review zone evidence'
          : kind === 2
            ? 'Dispute location evidence'
            : 'Acknowledge zone evidence'
      }
      onSaved={close}
      onCancel={close}
      build={() => ({
        path,
        method: 'POST',
        schema: zoneEvent,
        body: eventResponseInput.parse({
          kind,
          reason,
          expectedLastResponseId: event.lastResponseId,
        }),
      })}
    >
      <RecordFields
        values={{
          Event: event.id,
          Instruction: event.instruction,
          'Previous response': event.lastResponseId,
        }}
      />
      <TextInput
        label="Reason"
        required
        maxLength={1000}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}

function EventsWorkspace({ siteId, own = false }: { siteId?: string; own?: boolean }) {
  const access = useAttendanceAccess();
  const allowed = fieldPermission(
    access.user,
    own ? 'attendance.zones.self' : 'attendance.zones.review',
    siteId,
  );
  const [employeeInput, setEmployeeInput] = useState(''),
    [employee, setEmployee] = useState('');
  const [cursors, setCursors] = useState<string[]>([]);
  const [edit, setEdit] = useState<{ event: ZoneEvent; kind: number }>();
  const base = own ? '/me/attendance/zones' : `/attendance/sites/${siteId}/zones`;
  const query = useAttendanceQuery(
    base +
      '/events' +
      queryString({ pageSize: 25, cursor: cursors.at(-1), employeeId: own ? undefined : employee }),
    eventsPage,
    allowed,
  );
  function close() {
    setEdit(undefined);
    void query.refetch();
  }
  if (!allowed)
    return <Denied>Zone event access requires the matching own or site review grant.</Denied>;
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>Zone Events and Warnings</h2>
        <Reload
          disabled={Boolean(edit) || query.isFetching}
          onClick={() => {
            if (cursors.length) setCursors([]);
            else void query.refetch();
          }}
        />
      </div>
      {!own && (
        <form
          className="attendance-commands"
          onSubmit={(e) => {
            e.preventDefault();
            if (employeeInput === '' || id.safeParse(employeeInput).success) {
              setEmployee(employeeInput);
              setCursors([]);
            }
          }}
        >
          <TextInput
            label="Employee ID (optional)"
            disabled={Boolean(edit)}
            value={employeeInput}
            onChange={(e) => setEmployeeInput(e.target.value)}
          />
          <Command
            icon={Search}
            type="submit"
            disabled={
              Boolean(edit) ||
              query.isFetching ||
              (employeeInput !== '' && !id.safeParse(employeeInput).success)
            }
          >
            Filter
          </Command>
        </form>
      )}
      <QueryState query={query} empty={query.data?.items.length === 0} />
      {query.data && !query.isError && (
        <>
          {query.data.items.map((event) => (
            <section className="attendance-section" key={event.id}>
              <h3>
                {
                  ['', 'Approach', 'Entry attempt', 'Entered', 'Exited', 'Warning shown'][
                    event.kind
                  ]
                }
                : {event.receivedAtUtc}
              </h3>
              <RecordFields
                values={{
                  Event: event.id,
                  Employee: event.employeeId,
                  'Pinned zone version': event.zoneVersionId,
                  'Duty window': event.windowId,
                  Source:
                    ['', 'Foreground location', 'Background location', 'Employee report'][
                      event.source
                    ] ?? '',
                  Instruction: event.instruction,
                  'Last response':
                    event.lastResponseKind === null
                      ? null
                      : (['', 'Acknowledged', 'Location disputed', 'Reviewed'][
                          event.lastResponseKind
                        ] ?? ''),
                  'Location expiry': event.locationExpiresAtUtc,
                }}
              />
              <div className="attendance-commands">
                {own ? (
                  <>
                    <Command
                      icon={Check}
                      disabled={Boolean(edit) || query.isFetching}
                      onClick={() => setEdit({ event, kind: 1 })}
                    >
                      Acknowledge
                    </Command>
                    <Command
                      icon={MessageSquare}
                      disabled={Boolean(edit) || query.isFetching}
                      onClick={() => setEdit({ event, kind: 2 })}
                    >
                      Dispute
                    </Command>
                  </>
                ) : (
                  <Command
                    icon={ShieldCheck}
                    disabled={
                      Boolean(edit) ||
                      query.isFetching ||
                      !access.user?.account ||
                      access.user.account.employeeId === event.employeeId
                    }
                    onClick={() => setEdit({ event, kind: 3 })}
                  >
                    Record factual review
                  </Command>
                )}
              </div>
            </section>
          ))}
          <div className="attendance-commands">
            <Command
              icon={ChevronLeft}
              disabled={Boolean(edit) || query.isFetching || cursors.length === 0}
              onClick={() => setCursors((v) => v.slice(0, -1))}
            >
              Previous
            </Command>
            <Command
              icon={ChevronRight}
              disabled={Boolean(edit) || query.isFetching || !query.data.nextCursor}
              onClick={() => {
                const next = query.data?.nextCursor;
                if (next) setCursors((v) => [...v, next]);
              }}
            >
              Next
            </Command>
          </div>
        </>
      )}
      {edit && (
        <EventForm
          event={edit.event}
          kind={edit.kind}
          path={`${base}/events/${edit.event.id}/${own ? 'responses' : 'review'}`}
          close={close}
        />
      )}
    </section>
  );
}

export function ZoneEventsPage() {
  return <SiteWorkspace>{(siteId) => <EventsWorkspace siteId={siteId} />}</SiteWorkspace>;
}

function OwnZonesWorkspace() {
  const access = useAttendanceAccess();
  const allowed = fieldPermission(access.user, 'attendance.zones.self');
  const query = useAttendanceQuery('/me/attendance/zones', myZones, allowed);
  if (!allowed) return <Denied>My zones requires your own zone grant.</Denied>;
  return (
    <>
      <div className="attendance-heading">
        <h1>My Duty Zones</h1>
        <Reload
          disabled={query.isFetching}
          onClick={() => {
            void query.refetch();
          }}
        />
      </div>
      <QueryState query={query} />
      {query.data && !query.isError && (
        <>
          <RecordFields
            values={{
              State: query.data.state,
              Site: query.data.siteId,
              'Monitoring available': query.data.monitoringAvailable,
              Notice: query.data.configuration?.visibleNotice ?? null,
            }}
          />
          {query.data.zones.map((item) => (
            <section className="attendance-section" key={item.assignmentId}>
              <h2>
                {item.name}: {item.version.type}
              </h2>
              <RecordFields
                values={{
                  Instruction: item.version.employeeInstruction,
                  From: item.version.effectiveFromUtc,
                  To: item.version.effectiveToUtc,
                }}
              />
              <BoundaryView value={item.version.boundary} />
            </section>
          ))}
        </>
      )}
      <EventsWorkspace own />
    </>
  );
}
export function MyZonesPage() {
  return (
    <FieldWorkspace>
      <OwnZonesWorkspace />
    </FieldWorkspace>
  );
}

function WindowsWorkspace({ siteId }: { siteId: string }) {
  const access = useAttendanceAccess(),
    params = useParams();
  const [input, setInput] = useState(''),
    [chosen, setChosen] = useState('');
  const employeeId = params.employeeId ?? chosen;
  const valid = id.safeParse(employeeId).success;
  const allowed =
    valid && fieldPermission(access.user, 'attendance.zones.location.read', siteId, employeeId);
  const query = useAttendanceQuery(
    `/attendance/sites/${siteId}/zones/employees/${employeeId}/monitoring-windows`,
    z.array(dutyWindow),
    allowed,
  );
  return (
    <>
      <h1>Authorized Duty Windows</h1>
      {!params.employeeId && (
        <form
          className="attendance-commands"
          onSubmit={(e) => {
            e.preventDefault();
            if (id.safeParse(input).success) setChosen(input);
          }}
        >
          <TextInput
            label="Employee ID"
            value={input}
            required
            onChange={(e) => setInput(e.target.value)}
          />
          <Command icon={Search} type="submit" disabled={!id.safeParse(input).success}>
            Read windows
          </Command>
        </form>
      )}
      {valid && !allowed ? (
        <Denied>
          Duty windows require an explicit location-read grant for this employee or site.
        </Denied>
      ) : (
        allowed && (
          <>
            <Reload
              disabled={query.isFetching}
              onClick={() => {
                void query.refetch();
              }}
            />
            <QueryState
              query={query}
              empty={query.data?.length === 0}
              emptyText="No currently authorized duty windows."
            />
            {query.data &&
              !query.isError &&
              query.data.map((v) => (
                <section className="attendance-section" key={v.id}>
                  <RecordFields
                    values={{
                      Window: v.id,
                      Assignment: v.assignmentId,
                      Configuration: v.configurationVersionId,
                      Starts: v.startsAtUtc,
                      Ends: v.endsAtUtc,
                    }}
                  />
                </section>
              ))}
          </>
        )
      )}
      {!valid && params.employeeId && <p role="alert">Invalid employee identifier.</p>}
    </>
  );
}
export function ZoneDutyWindowsPage() {
  return <SiteWorkspace>{(siteId) => <WindowsWorkspace siteId={siteId} />}</SiteWorkspace>;
}
