import { useState } from 'react';
import {
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  Clock3,
  Layers3,
  LayoutGrid,
  ListFilter,
  PauseCircle,
  Plus,
  RefreshCw,
  Search,
  Table2,
  Timer,
} from 'lucide-react';
import { Pagination } from '@/components/data-display/Pagination';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import { shiftPage, shiftDetail, type Shift, type ShiftSummary } from '../../schemas/shifts';
import { businessToday } from '../../utils/messages';
import { weekdays, weekdayLabel } from '../../utils/shift-schedule';
import { Denied, QueryState } from '../SchedulingUi';
import { ShiftMetadataForm } from './ShiftMetadataForm';
import '@/styles/shift-management.css';
import '@/styles/shifts-workspace.css';

type ViewMode = 'cards' | 'table';
type ScheduleVersion = Shift['versions'][number];

function displayTime(time: string) {
  const [hours = '0', minutes = '00', seconds = '00'] = time.split(':');
  const hour = Number(hours);
  return `${hour % 12 || 12}:${minutes}${Number(seconds) ? `:${seconds}` : ''} ${hour >= 12 ? 'PM' : 'AM'}`;
}

function WeekdayChips({ mask }: { mask: number }) {
  return (
    <div className="sw-weekdays" aria-label={`Applicable weekdays: ${weekdayLabel(mask)}`}>
      {weekdays.map((day) => (
        <span
          key={day.value}
          className={(mask & day.value) !== 0 ? 'sw-day is-applicable' : 'sw-day'}
          aria-label={`${day.label}: ${(mask & day.value) !== 0 ? 'applicable' : 'not applicable'}`}
        >
          {day.label.slice(0, 3)}
        </span>
      ))}
    </div>
  );
}

function TimingPanel({ version }: { version: ScheduleVersion }) {
  return (
    <div className="sw-timing">
      <div>
        <span className="sw-label">Start time</span>
        <strong>{displayTime(version.startTime)}</strong>
      </div>
      <ArrowRight size={13} aria-hidden="true" />
      <div>
        <span className="sw-label">End time{version.endDayOffset === 1 ? ' · next day' : ''}</span>
        <strong>{displayTime(version.endTime)}</strong>
      </div>
    </div>
  );
}

function ShiftItem({
  shift,
  today,
  view,
  canManage,
  onOpen,
}: {
  shift: ShiftSummary;
  today: string;
  view: ViewMode;
  canManage: boolean;
  onOpen: (id: string) => void;
}) {
  const access = useAttendanceAccess();
  const query = useAttendanceQuery(
    '/hr/shifts/' + shift.id,
    shiftDetail,
    access.can('attendance.shifts.read'),
  );
  // EffectiveTo is exclusive. Never substitute a historical or future version.
  const effectiveVersions =
    !query.isError && !query.isFetching
      ? (query.data?.versions.filter(
          (version) =>
            version.effectiveFrom <= today &&
            (version.effectiveTo === null || today < version.effectiveTo),
        ) ?? [])
      : [];
  const version = effectiveVersions.length === 1 ? effectiveVersions[0] : undefined;
  const unavailable = query.isError ? (
    <div className="sw-schedule-state">
      <strong>Schedule unavailable</strong>
      <QueryState query={query} />
      <button
        type="button"
        className="sw-button sw-button-secondary"
        disabled={query.isFetching}
        onClick={() => void query.refetch()}
      >
        <RefreshCw size={14} aria-hidden="true" /> Retry schedule
      </button>
    </div>
  ) : query.isPending || query.isFetching ? (
    <p className="sw-schedule-state" role="status">
      Loading schedule…
    </p>
  ) : !version ? (
    <p className="sw-schedule-state">
      {effectiveVersions.length > 1
        ? 'Schedule unavailable: overlapping effective versions. Open details to review.'
        : `No schedule effective on ${today}. Open details for saved versions.`}
    </p>
  ) : null;
  const status = (
    <span className={`sw-status ${shift.isActive ? 'is-active' : 'is-inactive'}`}>
      <span aria-hidden="true" />
      {shift.isActive ? 'Active' : 'Inactive'}
    </span>
  );
  const manage = (
    <button
      type="button"
      className="sw-manage"
      aria-label={`${canManage ? 'Manage' : 'View'} ${shift.code}`}
      onClick={() => onOpen(shift.id)}
    >
      {canManage ? 'Manage' : 'View details'}
      <ArrowRight size={12} aria-hidden="true" />
    </button>
  );

  if (view === 'table') {
    return (
      <tr>
        <td>
          <span className="sw-code">{shift.code}</span>
          <strong className="sw-table-name">{shift.name}</strong>
        </td>
        <td>{status}</td>
        {version ? (
          <>
            <td>
              <span className="sw-table-time">
                {displayTime(version.startTime)} – {displayTime(version.endTime)}
              </span>
              {version.endDayOffset === 1 && <small>Ends next day</small>}
              <small>
                v{version.versionNumber} · from {version.effectiveFrom}
              </small>
              {version.effectiveTo && <small>Until {version.effectiveTo} (exclusive)</small>}
            </td>
            <td>
              <WeekdayChips mask={version.applicableWeekdays} />
            </td>
            <td>
              <strong>{version.scheduledMinutes} min scheduled</strong>
              <small>Paid break: {version.paidBreakMinutes} min</small>
              <small>Unpaid break: {version.unpaidBreakMinutes} min</small>
            </td>
          </>
        ) : (
          <td colSpan={3}>{unavailable}</td>
        )}
        <td>{manage}</td>
      </tr>
    );
  }
  return (
    <article className="sw-card" aria-label={`${shift.code} — ${shift.name}`}>
      <div className="sw-card-heading">
        <div className="sw-card-top">
          <span className="sw-code">{shift.code}</span>
          {status}
        </div>
        <h3>{shift.name}</h3>
      </div>
      <div className="sw-card-body">
        {version ? (
          <>
            <TimingPanel version={version} />
            <div className="sw-scheduled">
              <Timer size={12} aria-hidden="true" />
              <strong>{version.scheduledMinutes} min</strong>
              <span>scheduled</span>
            </div>
            <dl className="sw-breaks">
              <div>
                <dt>Paid break</dt>
                <dd>{version.paidBreakMinutes} min</dd>
              </div>
              <div>
                <dt>Unpaid break</dt>
                <dd>{version.unpaidBreakMinutes} min</dd>
              </div>
            </dl>
            <div className="sw-days-section">
              <span className="sw-label">Applicable weekdays</span>
              <WeekdayChips mask={version.applicableWeekdays} />
            </div>
          </>
        ) : (
          unavailable
        )}
      </div>
      <footer className="sw-card-footer">
        <span className="sw-version">
          {version ? (
            <>
              v{version.versionNumber} · From {version.effectiveFrom}
              {version.effectiveTo && <small>Until {version.effectiveTo} (exclusive)</small>}
            </>
          ) : (
            'Schedule details'
          )}{' '}
        </span>
        {manage}
      </footer>
    </article>
  );
}

export function ShiftList({ onOpen }: { onOpen: (id: string) => void }) {
  const access = useAttendanceAccess();
  const allowed = access.can('attendance.shifts.read');
  const canManage = access.can('attendance.shifts.manage');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [view, setView] = useState<ViewMode>('cards');
  const [isCreating, setIsCreating] = useState(false);
  const query = useAttendanceQuery(
    '/hr/shifts' +
      queryString({
        page,
        pageSize: 20,
        sortBy: 'code',
        search: search.trim(),
        isActive: status === '' ? undefined : status === 'active',
      }),
    shiftPage,
    allowed,
  );
  const today = businessToday();
  const data = !query.isError && !query.isFetching ? query.data : undefined;
  const unavailableMetric = query.isError ? 'Unavailable' : 'Loading…';
  const metrics = [
    {
      label: 'Matching shifts',
      value: data?.totalCount,
      note: 'All pages · current filters',
      icon: Layers3,
      tone: 'navy',
    },
    {
      label: 'Loaded shifts',
      value: data?.items.length,
      note: 'This page',
      icon: CalendarDays,
      tone: 'blue',
    },
    {
      label: 'Active shifts',
      value: data?.items.filter((shift) => shift.isActive).length,
      note: 'This page',
      icon: CheckCircle2,
      tone: 'green',
    },
    {
      label: 'Inactive shifts',
      value: data?.items.filter((shift) => !shift.isActive).length,
      note: 'This page',
      icon: PauseCircle,
      tone: 'gray',
    },
  ];
  if (!allowed) return <Denied>Shift management requires shift read access.</Denied>;
  if (isCreating && canManage) {
    return (
      <ShiftMetadataForm
        mode="create"
        onCancel={() => {
          setIsCreating(false);
          void query.refetch();
        }}
        onSaved={(createdShift) => {
          setIsCreating(false);
          onOpen(createdShift.id);
        }}
      />
    );
  }
  return (
    <section className="shifts-workspace" aria-label="Shift management">
      <header className="sw-heading">
        <div>
          <span className="sw-eyebrow">Attendance / Scheduling</span>
          <h2>Work shifts</h2>
          <p>Manage shift timings and weekly schedules.</p>
        </div>
        <span className="sw-date">
          <Clock3 size={13} aria-hidden="true" />
          {today}
          <span>Asia/Karachi</span>
        </span>
      </header>
      <div className="sw-summary" aria-label="Shift summary">
        {metrics.map(({ label, value, note, icon: Icon, tone }) => (
          <div className="sw-metric" key={label}>
            <div>
              <span className="sw-label">{label}</span>
              <strong className={value === undefined ? 'sw-metric-unavailable' : 'sw-metric-value'}>
                {value ?? unavailableMetric}
              </strong>
              <span className="sw-metric-note">{note}</span>
            </div>
            <span className={`sw-metric-icon sw-tone-${tone}`}>
              <Icon size={16} aria-hidden="true" />
            </span>
          </div>
        ))}
      </div>
      <div className="sw-toolbar">
        <label className="sw-search">
          <Search size={14} aria-hidden="true" />
          <span className="sw-sr-only">Search shifts by code or name</span>
          <input
            type="search"
            placeholder="Search by shift name or code…"
            maxLength={100}
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="sw-status-filter">
          <ListFilter size={13} aria-hidden="true" />
          <span className="sw-sr-only">Filter by status</span>
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
            }}
          >
            <option value="">All statuses</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </label>
        <div className="sw-view-toggle" role="group" aria-label="Display shifts as">
          <button type="button" aria-pressed={view === 'cards'} onClick={() => setView('cards')}>
            <LayoutGrid size={13} aria-hidden="true" />
            Cards
          </button>
          <button type="button" aria-pressed={view === 'table'} onClick={() => setView('table')}>
            <Table2 size={13} aria-hidden="true" />
            Table
          </button>
        </div>
        <button
          type="button"
          className="sw-button sw-button-secondary sw-reload"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          <RefreshCw size={13} aria-hidden="true" />
          Reload
        </button>
        {canManage && (
          <button
            type="button"
            className="sw-button sw-button-primary"
            onClick={() => setIsCreating(true)}
          >
            <Plus size={14} aria-hidden="true" />
            Add shift
          </button>
        )}
      </div>
      <div className="sw-section-heading">
        <h3>Shift directory</h3>
        <span>Schedules effective on {today}</span>
      </div>
      <div className="sw-results" aria-busy={query.isFetching}>
        <QueryState query={query} />
        {data && (
          <>
            {data.items.length === 0 ? (
              <div className="sw-empty">
                <Clock3 size={32} aria-hidden="true" />
                <h3>{search.trim() || status ? 'No matching shifts' : 'No shifts found'}</h3>
                <p>
                  {search.trim() || status
                    ? 'Try another name, code or status.'
                    : 'Saved shifts will appear here.'}
                </p>
              </div>
            ) : view === 'cards' ? (
              <div className="sw-card-grid">
                {data.items.map((shift) => (
                  <ShiftItem
                    key={shift.id}
                    shift={shift}
                    today={today}
                    view={view}
                    canManage={canManage}
                    onOpen={onOpen}
                  />
                ))}
              </div>
            ) : (
              <div
                className="sw-table-scroll"
                role="region"
                aria-label="Shift directory table"
                tabIndex={0}
              >
                <table className="sw-table">
                  <thead>
                    <tr>
                      <th scope="col">Shift</th>
                      <th scope="col">Status</th>
                      <th scope="col">Effective schedule</th>
                      <th scope="col">Weekdays</th>
                      <th scope="col">Minutes & breaks</th>
                      <th scope="col">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((shift) => (
                      <ShiftItem
                        key={shift.id}
                        shift={shift}
                        today={today}
                        view={view}
                        canManage={canManage}
                        onOpen={onOpen}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <footer className="sw-pagination">
              <span>
                {data.items.length} shown · {data.totalCount} matching shifts
              </span>
              <Pagination
                page={data.page}
                pageSize={data.pageSize}
                total={data.totalCount}
                onChange={setPage}
              />
            </footer>
          </>
        )}
      </div>
    </section>
  );
}
