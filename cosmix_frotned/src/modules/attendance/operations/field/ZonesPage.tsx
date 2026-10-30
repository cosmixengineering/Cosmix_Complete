import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { z } from 'zod';
import { ChevronLeft, ChevronRight, Edit, Plus, Shield, X } from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { Command, Denied, QueryState } from '../../components/SchedulingUi';
import { fieldPermission } from './access';
import { assignment, configuration, zone, zonesPage, type Assignment } from './contracts';
import { BoundaryView } from './BoundaryView';
import { AssignmentForm, MonitoringForm, ZoneVersionForm } from './ZoneForms';
import { RecordFields, Reload, SiteWorkspace } from './FieldUi';

function ZoneDetail({
  siteId,
  zoneId,
  onClose,
}: {
  siteId: string;
  zoneId: string;
  onClose: () => void;
}) {
  const access = useAttendanceAccess();
  const canRead = fieldPermission(access.user, 'attendance.zones.read', siteId);
  const canManage = fieldPermission(access.user, 'attendance.zones.manage', siteId);
  const path = `/attendance/sites/${siteId}/zones/${zoneId}`;
  const detail = useAttendanceQuery(path, zone, canRead);
  const assignments = useAttendanceQuery(path + '/assignments', z.array(assignment), canRead);
  const [edit, setEdit] = useState<'version' | 'assign' | null>(null);
  const [replace, setReplace] = useState<Assignment>(),
    [end, setEnd] = useState<Assignment>();
  const busy = Boolean(edit || replace || end);
  const fresh = Boolean(
    detail.data &&
    assignments.data &&
    !detail.isError &&
    !assignments.isError &&
    !detail.isFetching &&
    !assignments.isFetching,
  );
  function reload() {
    void detail.refetch();
    void assignments.refetch();
  }
  function close() {
    setEdit(null);
    setReplace(undefined);
    setEnd(undefined);
    reload();
  }
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>Zone history and restrictions</h2>
        <div className="attendance-commands">
          <Reload disabled={busy || detail.isFetching || assignments.isFetching} onClick={reload} />
          <Command icon={X} variant="secondary" disabled={busy} onClick={onClose}>
            Close zone
          </Command>
        </div>
      </div>
      <QueryState query={detail} />
      {detail.data && !detail.isError && (
        <>
          <h3>
            {detail.data.code}: {detail.data.name}
          </h3>
          {canManage && detail.data.isActive && (
            <div className="attendance-commands">
              <Command
                icon={Edit}
                disabled={
                  busy ||
                  !fresh ||
                  !['OPERATIONAL', 'RED'].includes(detail.data.versions.at(-1)?.type ?? '')
                }
                onClick={() => setEdit('version')}
              >
                Publish version
              </Command>
              <Command icon={Shield} disabled={busy || !fresh} onClick={() => setEdit('assign')}>
                Assign / restrict
              </Command>
            </div>
          )}
          {detail.data.versions.map((v) => (
            <details key={v.id}>
              <summary>
                Version {v.version}: {v.type}, {v.effectiveFromUtc} - {v.effectiveToUtc}
              </summary>
              <RecordFields
                values={{
                  'Version ID': v.id,
                  Instruction: v.employeeInstruction,
                  'Reason code': v.reasonCode,
                }}
              />
              <BoundaryView value={v.boundary} />
            </details>
          ))}
          <QueryState
            query={assignments}
            empty={assignments.data?.length === 0}
            emptyText="No zone assignments."
          />
          {assignments.data &&
            !assignments.isError &&
            assignments.data.map((a) => (
              <details key={a.id}>
                <summary>
                  {['', 'Employee', 'Role', 'Team', 'Site'][a.populationKind]}:{' '}
                  {a.populationKind === 1 ? (
                    <EmployeeReference employeeId={a.targetId} />
                  ) : (
                    a.targetId
                  )}{' '}
                  ({a.endId ? 'Ended' : 'Assigned'})
                </summary>
                <RecordFields
                  values={{
                    'Assignment ID': a.id,
                    'Pinned version': a.zoneVersionId,
                    From: a.effectiveFromUtc,
                    To: a.effectiveToUtc,
                    'Legacy override': a.isEmployeeOverride,
                  }}
                />
                {canManage && !a.endId && !a.isEmployeeOverride && (
                  <div className="attendance-commands">
                    <Command icon={Edit} disabled={busy || !fresh} onClick={() => setReplace(a)}>
                      Replace
                    </Command>
                    <Command icon={X} disabled={busy || !fresh} onClick={() => setEnd(a)}>
                      End assignment
                    </Command>
                  </div>
                )}
              </details>
            ))}
          {edit === 'version' && (
            <ZoneVersionForm
              siteId={siteId}
              current={detail.data}
              onSaved={close}
              onCancel={close}
            />
          )}
          {(edit === 'assign' || replace || end) && (
            <AssignmentForm
              siteId={siteId}
              current={detail.data}
              replace={replace}
              end={end}
              onSaved={close}
              onCancel={close}
            />
          )}
        </>
      )}
    </section>
  );
}

function ZonesWorkspace({ siteId }: { siteId: string }) {
  const access = useAttendanceAccess();
  const canRead = fieldPermission(access.user, 'attendance.zones.read', siteId),
    canManage = fieldPermission(access.user, 'attendance.zones.manage', siteId);
  const [page, setPage] = useState(1),
    [selected, setSelected] = useState(''),
    [editor, setEditor] = useState<'create' | 'config' | null>(null);
  const path = `/attendance/sites/${siteId}/zones`;
  const list = useAttendanceQuery(`${path}?page=${page}&pageSize=25`, zonesPage, canRead);
  const configs = useAttendanceQuery(
    path + '/monitoring-configurations',
    z.array(configuration),
    canRead,
  );
  function reload() {
    void list.refetch();
    void configs.refetch();
  }
  function close() {
    setEditor(null);
    reload();
  }
  if (!canRead)
    return <Denied>Zone configuration requires zone read permission for this site.</Denied>;
  return (
    <>
      <div className="attendance-heading">
        <h1>Operational Zones</h1>
        <Reload
          disabled={Boolean(editor || selected) || list.isFetching || configs.isFetching}
          onClick={reload}
        />
      </div>
      <p>Site: {siteId}</p>
      {canManage && (
        <div className="attendance-commands">
          <Command
            icon={Plus}
            disabled={Boolean(editor || selected)}
            onClick={() => setEditor('create')}
          >
            Create zone
          </Command>
          <Command
            icon={Edit}
            disabled={
              Boolean(editor || selected) || !configs.data || configs.isError || configs.isFetching
            }
            onClick={() => setEditor('config')}
          >
            Publish monitoring settings
          </Command>
        </div>
      )}
      {editor === 'create' && <ZoneVersionForm siteId={siteId} onSaved={close} onCancel={close} />}
      {editor === 'config' && configs.data && (
        <MonitoringForm
          siteId={siteId}
          latestVersion={Math.max(0, ...configs.data.map((v) => v.version))}
          onSaved={close}
          onCancel={close}
        />
      )}
      <QueryState query={list} empty={list.data?.items.length === 0} />
      {list.data && !list.isError && (
        <>
          <div className="attendance-commands">
            {list.data.items.map((item) => (
              <Command
                key={item.id}
                icon={Shield}
                variant="secondary"
                disabled={Boolean(editor || selected)}
                onClick={() => setSelected(item.id)}
              >
                {item.code}: {item.name}
              </Command>
            ))}
          </div>
          <div className="attendance-commands">
            <Command
              icon={ChevronLeft}
              disabled={Boolean(editor || selected) || list.isFetching || page <= 1}
              onClick={() => setPage(page - 1)}
            >
              Previous
            </Command>
            <span>Page {list.data.page}</span>
            <Command
              icon={ChevronRight}
              disabled={Boolean(editor || selected) || list.isFetching || !list.data.hasMore}
              onClick={() => setPage(page + 1)}
            >
              Next
            </Command>
          </div>
        </>
      )}
      {selected && !editor && (
        <ZoneDetail
          key={selected}
          siteId={siteId}
          zoneId={selected}
          onClose={() => {
            setSelected('');
            reload();
          }}
        />
      )}
      <section className="attendance-section">
        <h2>Monitoring configuration history</h2>
        <QueryState query={configs} empty={configs.data?.length === 0} />
        {configs.data &&
          !configs.isError &&
          configs.data.map((v) => (
            <details key={v.id}>
              <summary>
                Version {v.version}: {v.effectiveFromUtc} - {v.effectiveToUtc}
              </summary>
              <RecordFields
                values={{
                  'Configuration ID': v.id,
                  'Maximum accuracy (m)': v.maximumAccuracyMeters,
                  'Warning distance (m)': v.warningDistanceMeters,
                  'Minimum interval (s)': v.minimumIntervalSeconds,
                  'Retention (days)': v.retentionDays,
                  'Maximum duration (s)': v.maximumMonitoringSeconds,
                  'Live visibility (s)': v.liveVisibilitySeconds,
                  'Visible notice': v.visibleNotice,
                }}
              />
            </details>
          ))}
      </section>
    </>
  );
}

export function OperationalZonesPage() {
  return <SiteWorkspace>{(siteId) => <ZonesWorkspace siteId={siteId} />}</SiteWorkspace>;
}
