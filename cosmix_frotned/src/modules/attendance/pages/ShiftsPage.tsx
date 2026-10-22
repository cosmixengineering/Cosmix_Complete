import { useState } from 'react';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { Denied, PrivateWorkspace } from '../components/SchedulingUi';
import { ShiftList } from '../components/shifts/ShiftList';
import { ShiftDetail } from '../components/shifts/ShiftDetail';

function ShiftsWorkspace() {
  const access = useAttendanceAccess();
  const [selectedId, setSelectedId] = useState<string | null>(null);

  return (
    <>
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
