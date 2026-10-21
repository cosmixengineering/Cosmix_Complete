import { useState } from 'react';
import { Link } from 'react-router-dom';
import { CalendarDays, Settings2 } from 'lucide-react';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { Denied, PrivateWorkspace } from '../components/SchedulingUi';
import { ShiftList } from '../components/shifts/ShiftList';
import { ShiftDetail } from '../components/shifts/ShiftDetail';

function ShiftsWorkspace() {
  const access = useAttendanceAccess();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const canReadRosters =
    access.can('attendance.rosters.read') ||
    access.user?.grants.some(
      (grant) => grant.targetId && access.can('attendance.rosters.read', grant.targetId),
    );
  const canReadPolicies = access.can('attendance.policies.configure');

  return (
    <>
      {(canReadRosters || canReadPolicies) && (
        <nav className="attendance-commands" aria-label="Scheduling">
          {canReadRosters && (
            <Link className="attendance-record-link" to="/hr/attendance/sites">
              <CalendarDays size={16} aria-hidden="true" />
              Site rosters
            </Link>
          )}
          {canReadPolicies && (
            <Link className="attendance-record-link" to="/hr/attendance/policies">
              <Settings2 size={16} aria-hidden="true" />
              Attendance policies
            </Link>
          )}
        </nav>
      )}
      {!access.can('attendance.shifts.read') ? (
        <Denied>Shift management requires All-scoped shift read access.</Denied>
      ) : selectedId ? (
        <ShiftDetail key={selectedId} id={selectedId} onBack={() => setSelectedId(null)} />
      ) : (
        <ShiftList onOpen={setSelectedId} />
      )}
    </>
  );
}

export function ShiftsPage() {
  return (
    <PrivateWorkspace resource="shifts">
      <ShiftsWorkspace />
    </PrivateWorkspace>
  );
}
