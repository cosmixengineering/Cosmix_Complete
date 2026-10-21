import { ArrowLeft, Pencil, Power, CalendarPlus, RefreshCw } from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { shiftDetail } from '../../schemas/shifts';
import { weekdayLabel } from '../../utils/shift-schedule';
import { Command, QueryState, SavedNotice } from '../SchedulingUi';
import { useState } from 'react';
import { ShiftMetadataForm } from './ShiftMetadataForm';

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
  return (
    <>
      <div className="attendance-heading">
        <h2>{shift ? `${shift.code} - ${shift.name}` : 'Shift detail'}</h2>
        <div className="attendance-commands">
          <Command icon={ArrowLeft} variant="secondary" disabled={Boolean(mode)} onClick={onBack}>
            Back to shifts
          </Command>
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={Boolean(mode) || query.isFetching}
            onClick={() => void query.refetch()}
          >
            Reload
          </Command>
        </div>
      </div>
      <SavedNotice message={notice} />
      <QueryState query={query} />
      {shift && !query.isError && (
        <>
          <p>Status: {shift.isActive ? 'Active' : 'Inactive'}</p>
          {!mode && access.can('attendance.shifts.manage') && (
            <div className="attendance-commands">
              <Command icon={Pencil} onClick={() => setMode('rename')} disabled={query.isFetching}>
                Rename
              </Command>
              <Command
                icon={Power}
                variant="secondary"
                onClick={() => setMode('status')}
                disabled={query.isFetching}
              >
                {shift.isActive ? 'Deactivate' : 'Activate'}
              </Command>
              <Command
                icon={CalendarPlus}
                onClick={() => setMode('schedule')}
                disabled={!shift.isActive || query.isFetching}
              >
                Future schedule
              </Command>
            </div>
          )}
          {mode && (
            <ShiftMetadataForm
              mode={mode}
              shift={shift}
              onCancel={close}
              onSaved={() => {
                setNotice('Shift saved. Reloading current detail.');
                close();
              }}
            />
          )}
          <section className="attendance-section">
            <h3>Immutable Schedule Versions</h3>
            {shift.versions.length ? (
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Version / dates</th>
                      <th>Hours</th>
                      <th>Weekdays</th>
                      <th>Minutes</th>
                      <th>Reminder</th>
                    </tr>
                  </thead>
                  <tbody>
                    {shift.versions.map((version) => (
                      <tr key={version.id}>
                        <td>
                          v{version.versionNumber}
                          <br />
                          {version.effectiveFrom} to {version.effectiveTo ?? 'open'} (exclusive)
                          <details>
                            <summary>Version ID</summary>
                            {version.id}
                          </details>
                        </td>
                        <td>
                          {version.startTime} - {version.endTime}
                          {version.endDayOffset ? ' (+1 day)' : ''}
                        </td>
                        <td>{weekdayLabel(version.applicableWeekdays)}</td>
                        <td>
                          Scheduled: {version.scheduledMinutes}
                          <br />
                          Paid break: {version.paidBreakMinutes}
                          <br />
                          Unpaid break: {version.unpaidBreakMinutes}
                        </td>
                        <td>
                          {version.missingCheckoutReminderAfterMinutes === undefined
                            ? 'Not configured'
                            : `${version.missingCheckoutReminderAfterMinutes} minutes after end`}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p>No schedule versions returned.</p>
            )}
          </section>
        </>
      )}
    </>
  );
}
