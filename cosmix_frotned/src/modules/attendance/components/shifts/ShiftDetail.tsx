import { useState } from 'react';
import {
  ArrowLeft,
  Pencil,
  Power,
  CalendarPlus,
  RefreshCw,
  Clock3,
  Timer,
  CalendarDays,
  CheckCircle2,
  PauseCircle,
  History,
  X,
  Bell,
  ChevronRight,
} from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { shiftDetail } from '../../schemas/shifts';
import { weekdays, weekdayLabel } from '../../utils/shift-schedule';
import { QueryState, SavedNotice } from '../SchedulingUi';
import { ShiftMetadataForm } from './ShiftMetadataForm';
import '@/styles/shifts-workspace.css';

function displayTime(time?: string) {
  if (!time) return '--:--';
  const [hours = '0', minutes = '00', seconds = '00'] = time.split(':');
  const hour = Number(hours);
  return `${hour % 12 || 12}:${minutes}${Number(seconds) ? `:${seconds}` : ''} ${hour >= 12 ? 'PM' : 'AM'}`;
}

function WeekdayChips({ mask }: { mask: number }) {
  return (
    <div className="sd-weekdays-row" aria-label={`Applicable weekdays: ${weekdayLabel(mask)}`}>
      {weekdays.map((day) => {
        const isActive = (mask & day.value) !== 0;
        return (
          <span
            key={day.value}
            className={`sd-day-chip ${isActive ? 'is-active' : 'is-inactive'}`}
            title={`${day.label}: ${isActive ? 'Working day' : 'Off day'}`}
          >
            {day.label.slice(0, 3)}
          </span>
        );
      })}
    </div>
  );
}

export function ShiftDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const access = useAttendanceAccess();
  const query = useAttendanceQuery(
    '/hr/shifts/' + id,
    shiftDetail,
    access.can('attendance.shifts.read'),
  );
  const [mode, setMode] = useState<'rename' | 'status' | 'schedule' | null>(null);
  const [notice, setNotice] = useState('');
  const shift = query.data;

  function close() {
    setMode(null);
    void query.refetch();
  }

  const currentVersion = shift?.versions[0];
  const scheduledHours = currentVersion
    ? `${displayTime(currentVersion.startTime)} → ${displayTime(currentVersion.endTime)}`
    : 'Not scheduled';
  const breakInfo = currentVersion
    ? `${currentVersion.paidBreakMinutes}m paid • ${currentVersion.unpaidBreakMinutes}m unpaid`
    : '0m total';
  const reminderInfo =
    currentVersion?.missingCheckoutReminderAfterMinutes !== undefined
      ? `${currentVersion.missingCheckoutReminderAfterMinutes}m post-shift`
      : 'Not configured';

  return (
    <div className="shifts-workspace shift-detail-workspace">
      {/* Top Nav Strip */}
      <div className="sd-top-nav">
        <div className="sd-top-nav-left">
          <button
            type="button"
            className="sd-back-btn"
            disabled={Boolean(mode)}
            onClick={onBack}
            aria-label="Back to shift directory"
          >
            <ArrowLeft size={13} /> Back to shifts
          </button>
          <span className="sd-breadcrumb-separator" aria-hidden="true">
            <ChevronRight size={13} />
          </span>
          <span className="sd-breadcrumb-current">
            {shift ? `${shift.code} (${shift.name})` : 'Shift details'}
          </span>
        </div>

        <button
          type="button"
          className="sd-reload-btn"
          disabled={Boolean(mode) || query.isFetching}
          onClick={() => void query.refetch()}
          title="Reload shift data"
        >
          <RefreshCw size={12} className={query.isFetching ? 'animate-spin' : ''} />
          <span>Reload</span>
        </button>
      </div>

      <SavedNotice message={notice} />
      <QueryState query={query} />

      {shift && !query.isError && (
        <>
          {/* Main Hero Card */}
          <div className="sd-hero-card">
            <div className="sd-hero-header">
              <div className="sd-hero-titles">
                <div className="sd-hero-badges">
                  <span className="sd-code-chip">{shift.code}</span>
                  <span
                    className={`sd-status-pill ${shift.isActive ? 'is-active' : 'is-inactive'}`}
                  >
                    {shift.isActive ? (
                      <>
                        <CheckCircle2 size={11} /> Active Shift
                      </>
                    ) : (
                      <>
                        <PauseCircle size={11} /> Inactive
                      </>
                    )}
                  </span>
                </div>
                <h1 className="sd-hero-title">{shift.name}</h1>
                <p className="sd-hero-meta">
                  Shift ID: <code>{shift.id.slice(0, 8)}...</code> &bull; Registered organizational
                  working schedule
                </p>
              </div>

              {!mode && access.can('attendance.shifts.manage') && (
                <div className="sd-hero-actions">
                  <button
                    type="button"
                    className="sd-action-btn sd-action-btn--primary"
                    onClick={() => setMode('schedule')}
                    disabled={!shift.isActive || query.isFetching}
                  >
                    <CalendarPlus size={13} /> Future schedule
                  </button>
                  <button
                    type="button"
                    className="sd-action-btn sd-action-btn--secondary"
                    onClick={() => setMode('rename')}
                    disabled={query.isFetching}
                  >
                    <Pencil size={13} /> Rename
                  </button>
                  <button
                    type="button"
                    className={`sd-action-btn ${
                      shift.isActive
                        ? 'sd-action-btn--danger-outline'
                        : 'sd-action-btn--success-outline'
                    }`}
                    onClick={() => setMode('status')}
                    disabled={query.isFetching}
                  >
                    <Power size={13} /> {shift.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                </div>
              )}
            </div>
          </div>

          {/* Active Schedule 4 KPI Summary Cards */}
          <div className="sd-kpi-grid">
            <div className="sd-kpi-card">
              <div className="sd-kpi-info">
                <span className="sd-kpi-label">Daily Working Hours</span>
                <span className="sd-kpi-val">{scheduledHours}</span>
                <span className="sd-kpi-sub">
                  {currentVersion
                    ? `${currentVersion.scheduledMinutes} min total scheduled`
                    : 'Unassigned'}
                </span>
              </div>
              <div className="sd-kpi-icon">
                <Clock3 size={15} />
              </div>
            </div>

            <div className="sd-kpi-card">
              <div className="sd-kpi-info">
                <span className="sd-kpi-label">Break Allowances</span>
                <span className="sd-kpi-val">{breakInfo}</span>
                <span className="sd-kpi-sub">Calculated in net work hours</span>
              </div>
              <div className="sd-kpi-icon">
                <Timer size={15} />
              </div>
            </div>

            <div className="sd-kpi-card">
              <div className="sd-kpi-info">
                <span className="sd-kpi-label">Applicable Weekdays</span>
                {currentVersion ? (
                  <WeekdayChips mask={currentVersion.applicableWeekdays} />
                ) : (
                  <span className="sd-kpi-val">None</span>
                )}
                <span className="sd-kpi-sub">
                  {currentVersion
                    ? weekdayLabel(currentVersion.applicableWeekdays)
                    : 'No active schedule'}
                </span>
              </div>
              <div className="sd-kpi-icon">
                <CalendarDays size={15} />
              </div>
            </div>

            <div className="sd-kpi-card">
              <div className="sd-kpi-info">
                <span className="sd-kpi-label">Checkout Reminder</span>
                <span className="sd-kpi-val">{reminderInfo}</span>
                <span className="sd-kpi-sub">Automated mobile notification</span>
              </div>
              <div className="sd-kpi-icon">
                <Bell size={15} />
              </div>
            </div>
          </div>

          {/* Form Modal / Slot when in edit mode */}
          {mode && (
            <div className="sd-form-card">
              <div className="sd-form-header">
                <h3>
                  {mode === 'rename' && (
                    <>
                      <Pencil size={14} /> Rename Shift
                    </>
                  )}
                  {mode === 'status' && (
                    <>
                      <Power size={14} /> {shift.isActive ? 'Deactivate Shift' : 'Activate Shift'}
                    </>
                  )}
                  {mode === 'schedule' && (
                    <>
                      <CalendarPlus size={14} /> Schedule Future Version
                    </>
                  )}
                </h3>
                <button
                  type="button"
                  className="sd-form-close-btn"
                  onClick={close}
                  aria-label="Cancel editing"
                  title="Close form"
                >
                  <X size={14} />
                </button>
              </div>
              <ShiftMetadataForm
                mode={mode}
                shift={shift}
                onCancel={close}
                onSaved={() => {
                  setNotice('Shift updated successfully.');
                  close();
                }}
              />
            </div>
          )}

          {/* Immutable Schedule Versions Table */}
          <div className="sd-versions-section">
            <div className="sd-versions-header">
              <div className="sd-versions-title-group">
                <div className="sd-versions-icon">
                  <History size={14} />
                </div>
                <div>
                  <h3>Immutable Schedule Versions ({shift.versions.length})</h3>
                  <p className="sd-versions-subtitle">
                    Historical sequence of published operating hours and break rules for this shift.
                  </p>
                </div>
              </div>
            </div>

            {shift.versions.length ? (
              <div className="sd-table-scroll">
                <table className="sd-table">
                  <thead>
                    <tr>
                      <th style={{ width: '22%' }}>Version &amp; Timeline</th>
                      <th style={{ width: '22%' }}>Working Hours</th>
                      <th style={{ width: '22%' }}>Applicable Days</th>
                      <th style={{ width: '16%' }}>Breaks (Paid / Unpaid)</th>
                      <th style={{ width: '18%' }}>Checkout Reminder</th>
                    </tr>
                  </thead>
                  <tbody>
                    {shift.versions.map((version) => (
                      <tr key={version.id}>
                        <td>
                          <div
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              flexWrap: 'wrap',
                              gap: '4px',
                            }}
                          >
                            <span className="sd-version-pill">v{version.versionNumber}</span>
                            <span className="sd-date-range">
                              {version.effectiveFrom} &rarr; {version.effectiveTo ?? 'Present'}
                            </span>
                          </div>
                          <div style={{ marginTop: '4px' }}>
                            <span
                              className="sd-audit-chip"
                              title={`Version UUID: ${version.id} (Exclusive bound)`}
                            >
                              ID: {version.id.slice(0, 8)}...
                            </span>
                          </div>
                        </td>
                        <td>
                          <span className="sd-time-highlight">
                            {displayTime(version.startTime)} &ndash; {displayTime(version.endTime)}
                            {version.endDayOffset ? ' (+1 day overnight)' : ''}
                          </span>
                          <div className="sd-min-meta">
                            {version.scheduledMinutes} minutes scheduled
                          </div>
                        </td>
                        <td>
                          <WeekdayChips mask={version.applicableWeekdays} />
                          <div className="sd-min-meta">
                            {weekdayLabel(version.applicableWeekdays)}
                          </div>
                        </td>
                        <td>
                          <span className="sd-break-pill">
                            {version.paidBreakMinutes}m paid &bull; {version.unpaidBreakMinutes}m
                            unpaid
                          </span>
                        </td>
                        <td>
                          <span
                            className={`sd-reminder-pill ${
                              version.missingCheckoutReminderAfterMinutes !== undefined
                                ? 'is-configured'
                                : ''
                            }`}
                          >
                            {version.missingCheckoutReminderAfterMinutes === undefined
                              ? 'Not configured'
                              : `${version.missingCheckoutReminderAfterMinutes} min after shift end`}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p style={{ padding: '16px', color: '#64748b' }}>No schedule versions recorded.</p>
            )}
          </div>
        </>
      )}
    </div>
  );
}
