import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowRight, Building2, Plus, RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { calendarPage, id, referencePage } from '../schemas/common';
import { shiftPage, shiftDetail, shiftSummary, shiftVersion } from '../schemas/shifts';
import { Command, Denied, QueryState } from './SchedulingUi';
import { SiteEditor } from '@/modules/hr/components/sites/SiteEditor';

export type ShiftReferenceValue = z.infer<typeof shiftSummary>;
export function ShiftReference({
  value,
  onChange,
}: {
  value: ShiftReferenceValue | null;
  onChange: (value: ShiftReferenceValue | null) => void;
}) {
  const access = useAttendanceAccess();
  const allowed = access.can('attendance.shifts.read');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search);
  const query = useAttendanceQuery(
    '/hr/shifts' + queryString({ page, pageSize: 20, search: term, isActive: true }),
    shiftPage,
    allowed,
  );
  const rows = query.data?.items ?? [];
  const options = [
    { value: '', label: 'Select active shift' },
    ...rows.map((row) => ({ value: row.id, label: `${row.code} - ${row.name}` })),
  ];
  if (value && !rows.some((row) => row.id === value.id))
    options.push({ value: value.id, label: `${value.code} - ${value.name}` });
  return (
    <div className="attendance-reference">
      <TextInput
        label="Find shift"
        maxLength={100}
        value={search}
        disabled={!allowed}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(1);
        }}
      />
      <SelectInput
        label="Shift"
        value={value?.id ?? ''}
        required
        disabled={!allowed || query.isFetching}
        options={options}
        onChange={(event) =>
          onChange(rows.find((row) => row.id === event.target.value && row.isActive) ?? null)
        }
      />
      {allowed ? (
        <>
          <QueryState
            query={query}
            empty={rows.length === 0}
            emptyText="No active shifts match this search."
          />
          {query.data && (
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
              disabled={query.isFetching}
            />
          )}
          {query.isError && (
            <Command icon={RefreshCw} variant="secondary" onClick={() => void query.refetch()}>
              Reload shifts
            </Command>
          )}
        </>
      ) : (
        <Denied>Global shift references require All-scoped shift read access.</Denied>
      )}
    </div>
  );
}
export function VersionReference({
  value,
  onChange,
}: {
  value: z.infer<typeof shiftVersion> | null;
  onChange: (value: z.infer<typeof shiftVersion> | null) => void;
}) {
  const access = useAttendanceAccess();
  const [shift, setShift] = useState<ShiftReferenceValue | null>(null);
  const query = useAttendanceQuery(
    '/hr/shifts/' + (shift?.id ?? ''),
    shiftDetail,
    Boolean(shift) && access.can('attendance.shifts.read'),
  );
  return (
    <div className="attendance-reference">
      <ShiftReference
        value={shift}
        onChange={(row) => {
          setShift(row);
          onChange(null);
        }}
      />
      {shift && (
        <>
          <QueryState query={query} />
          <SelectInput
            label="Immutable replacement schedule"
            value={value?.id ?? ''}
            required
            disabled={!query.data || query.isFetching}
            options={[
              { value: '', label: 'Select schedule version' },
              ...(query.data?.versions ?? []).map((row) => ({
                value: row.id,
                label: `v${row.versionNumber} from ${row.effectiveFrom}: ${row.startTime} - ${row.endTime}${row.endDayOffset ? ' (+1 day)' : ''}`,
              })),
            ]}
            onChange={(event) =>
              onChange(query.data?.versions.find((row) => row.id === event.target.value) ?? null)
            }
          />
          {query.isError && (
            <Command icon={RefreshCw} variant="secondary" onClick={() => void query.refetch()}>
              Reload versions
            </Command>
          )}
        </>
      )}
    </div>
  );
}
export function CalendarReference({
  value,
  onChange,
}: {
  value: z.infer<typeof calendarPage>['items'][number] | null;
  onChange: (value: z.infer<typeof calendarPage>['items'][number] | null) => void;
}) {
  const access = useAttendanceAccess();
  const allowed = access.can('hr.holidays.read');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search);
  const query = useAttendanceQuery(
    '/hr/holiday-calendars' +
      queryString({ page, pageSize: 20, search: term, includeHistory: true }),
    calendarPage,
    allowed,
  );
  const rows = query.data?.items ?? [];
  const options = [
    { value: '', label: 'No pinned calendar' },
    ...rows.map((row) => ({
      value: row.id,
      label: `${row.code} - ${row.name}, revision ${row.revision} from ${row.effectiveFrom}`,
    })),
  ];
  if (value && !rows.some((row) => row.id === value.id))
    options.push({ value: value.id, label: `${value.name}, revision ${value.revision}` });
  return (
    <div className="attendance-reference">
      <TextInput
        label="Find calendar"
        value={search}
        maxLength={100}
        disabled={!allowed}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(1);
        }}
      />
      <SelectInput
        label="Pinned holiday calendar revision (optional)"
        value={value?.id ?? ''}
        disabled={!allowed || query.isFetching}
        options={options}
        onChange={(event) => onChange(rows.find((row) => row.id === event.target.value) ?? null)}
      />
      {allowed ? (
        <>
          <QueryState
            query={query}
            empty={rows.length === 0}
            emptyText="No calendar revisions found."
          />
          {query.data && (
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
              disabled={query.isFetching}
            />
          )}
          {query.isError && (
            <Command icon={RefreshCw} variant="secondary" onClick={() => void query.refetch()}>
              Reload calendars
            </Command>
          )}
        </>
      ) : (
        <p>Calendar references require All-scoped holiday read access. No calendar is selected.</p>
      )}
    </div>
  );
}
export function SiteNavigation({
  siteId,
  mode,
}: {
  siteId?: string;
  mode: 'rosters' | 'geofences';
}) {
  const access = useAttendanceAccess();
  const navigate = useNavigate();
  const allowed = access.can('hr.sites.read');
  const [search, setSearch] = useState('');
  const [knownId, setKnownId] = useState('');
  const [page, setPage] = useState(1);
  const [addingSite, setAddingSite] = useState(false);
  const term = useDebouncedValue(search);
  const query = useAttendanceQuery(
    '/hr/sites' + queryString({ page, pageSize: 20, search: term }),
    referencePage,
    allowed,
  );
  const known = [
    ...new Set(
      access.user?.grants
        .filter(
          (grant) =>
            grant.scopeKind === 5 &&
            grant.targetId &&
            (grant.permission === 'attendance.rosters.read' ||
              grant.permission === 'attendance.rosters.manage'),
        )
        .map((grant) => grant.targetId!) ?? [],
    ),
  ];
  function open(value: string) {
    if (id.safeParse(value).success) void navigate(`/hr/attendance/sites/${value}/${mode}`);
  }
  const options = [
    { value: '', label: 'Select site...' },
    ...(allowed
      ? (query.data?.items ?? []).map((row) => ({
          value: row.id,
          label: `${row.code} - ${row.name}${row.isActive ? '' : ' (inactive)'}`,
        }))
      : known.map((value) => ({ value, label: value }))),
  ];
  if (siteId && !options.some((option) => option.value === siteId))
    options.push({ value: siteId, label: siteId });

  return (
    <div className="site-picker-card">
      <div className="site-picker-header">
        <div className="site-picker-title">
          <Building2 size={17} style={{ color: '#2563eb' }} />
          <span>Worksite Directory</span>
        </div>
        {siteId ? (
          <span className="badge badge--positive">Selected</span>
        ) : (
          <span className="badge badge--neutral">Choose Site</span>
        )}
      </div>

      {access.can('hr.sites.manage') && (
        <div style={{ margin: '8px 0 12px' }}>
          <Button
            variant="primary"
            style={{
              width: '100%',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '6px',
              padding: '9px 12px',
              fontSize: '13px',
              fontWeight: 600,
            }}
            onClick={() => setAddingSite(true)}
          >
            <Plus size={15} />
            Add New Worksite
          </Button>
        </div>
      )}

      {allowed && (
        <TextInput
          label="Filter by Name or Code"
          placeholder="e.g. Head Office, Site Alpha..."
          maxLength={100}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
          }}
        />
      )}

      <SelectInput
        label="Select Worksite"
        value={siteId ?? ''}
        options={options}
        onChange={(event) => open(event.target.value)}
      />

      {allowed ? (
        <>
          <QueryState
            query={query}
            empty={!query.data?.items.length}
            emptyText="No matching sites found."
          />
          {!query.data?.items.length && !query.isFetching && (
            <div
              style={{
                margin: '10px 0',
                padding: '12px',
                background: '#f8fafc',
                border: '1px dashed #cbd5e1',
                borderRadius: '8px',
                fontSize: '12px',
                color: '#475569',
              }}
            >
              <p style={{ margin: '0 0 6px 0', fontWeight: 600, color: '#0f172a' }}>
                No physical worksites found.
              </p>
              <p style={{ margin: '0 0 10px 0', color: '#64748b' }}>
                Create a site with code, name, city, and supervisor to configure geofences.
              </p>
              {access.can('hr.sites.manage') && (
                <Button
                  variant="secondary"
                  style={{ width: '100%', fontSize: '12px' }}
                  onClick={() => setAddingSite(true)}
                >
                  <Plus size={13} style={{ marginRight: '4px' }} />
                  Create Worksite Now
                </Button>
              )}
            </div>
          )}
          {query.data && query.data.totalCount > 20 && (
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
              disabled={query.isFetching}
            />
          )}
          {query.isError && (
            <Command icon={RefreshCw} variant="secondary" onClick={() => void query.refetch()}>
              Reload sites
            </Command>
          )}
        </>
      ) : (
        <p
          className="attendance-empty"
          style={{ fontSize: '12px', textAlign: 'left', padding: '8px 0' }}
        >
          Global site catalogue access is restricted. Known assigned site IDs remain available where
          scheduling grants apply.
        </p>
      )}

      <div className="site-picker-direct-jump">
        <label htmlFor="known-site-input">Direct Jump by Site ID</label>
        <div className="direct-jump-input-group">
          <input
            id="known-site-input"
            className="field"
            style={{
              flex: 1,
              minHeight: '38px',
              padding: '6px 10px',
              fontSize: '13px',
              border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius)',
            }}
            placeholder="Enter UUID..."
            value={knownId}
            onChange={(event) => setKnownId(event.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && id.safeParse(knownId).success) {
                open(knownId);
              }
            }}
          />
          <Command
            icon={ArrowRight}
            variant="secondary"
            disabled={!id.safeParse(knownId).success}
            onClick={() => open(knownId)}
          >
            Open
          </Command>
        </div>
      </div>

      {addingSite && (
        <SiteEditor
          onClose={() => setAddingSite(false)}
          onSaved={(newSite) => {
            setAddingSite(false);
            void query.refetch();
            open(newSite.id);
          }}
        />
      )}
    </div>
  );
}
