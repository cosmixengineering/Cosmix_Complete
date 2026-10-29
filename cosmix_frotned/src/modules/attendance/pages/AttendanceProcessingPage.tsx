import { lazy, Suspense, useState } from 'react';
import { Link } from 'react-router-dom';
import { LoadingState } from '@/components/feedback/LoadingState';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { supportedScopes } from '../operations/review/access';
import { Denied } from '../components/SchedulingUi';
import '@/styles/attendance.css';
import '@/styles/attendance-register.css';

const Register = lazy(() =>
  import('../operations/review/AttendancePages').then((m) => ({
    default: m.AttendanceRegisterPage,
  })),
);
const Daily = lazy(() =>
  import('../operations/review/AttendancePages').then((m) => ({ default: m.AttendanceDailyPage })),
);
const Exceptions = lazy(() =>
  import('../operations/review/ExceptionsPages').then((m) => ({
    default: m.AttendanceExceptionsPage,
  })),
);
const views = [
  {
    key: 'register',
    label: 'Monthly register',
    permission: 'attendance.summaries.read',
    Page: Register,
  },
  { key: 'daily', label: 'Daily details', permission: 'attendance.summaries.read', Page: Daily },
  {
    key: 'exceptions',
    label: 'Exceptions',
    permission: 'attendance.exceptions.read',
    Page: Exceptions,
  },
] as const;

export function AttendanceProcessingPage() {
  const access = useAttendanceAccess();
  const [selected, setSelected] = useState<string>('register');
  const available = views.filter(
    (view) => supportedScopes(access.user, view.permission).length > 0,
  );
  const active = available.find((view) => view.key === selected) ?? available[0];
  return (
    <div className="attendance att-register-page">
      <header className="att-register-heading">
        <div>
          <h1>Attendance</h1>
          <p>Employee attendance, working hours and daily records.</p>
        </div>
        <nav className="attendance-commands" aria-label="Attendance actions">
          {supportedScopes(access.user, 'attendance.corrections.review').length > 0 && (
            <Link className="button button--secondary" to="/hr/attendance/corrections/review">
              Correction review
            </Link>
          )}
          {supportedScopes(access.user, 'attendance.overtime.review').length > 0 && (
            <Link className="button button--secondary" to="/hr/overtime/review">
              Overtime review
            </Link>
          )}
        </nav>
      </header>
      <div className="att-register-tabs" role="group" aria-label="Attendance view">
        {available.map((view) => (
          <button
            key={view.key}
            type="button"
            className={view.key === active?.key ? 'att-register-tab is-active' : 'att-register-tab'}
            aria-pressed={view.key === active?.key}
            onClick={() => setSelected(view.key)}
          >
            {view.label}
          </button>
        ))}
      </div>
      {active ? (
        <Suspense fallback={<LoadingState message="Loading attendance..." />}>
          <active.Page key={active.key} />
        </Suspense>
      ) : (
        <Denied>
          Your assigned permissions do not include attendance records or exception review.
        </Denied>
      )}
    </div>
  );
}
