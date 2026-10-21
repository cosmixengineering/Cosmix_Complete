import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowRight, CalendarPlus, RefreshCw, Replace, StopCircle } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { id, referencePage } from '../schemas/common';
import { officeRosterHistory } from '../schemas/office-rosters';
import { OfficeRosterForm } from '../components/rosters/OfficeRosterForm';
import {
  Command,
  Denied,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../components/SchedulingUi';
import { businessToday } from '../utils/messages';

function OfficeSelection() {
  const access = useAttendanceAccess();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search);
  const allowed = access.can('hr.organization.read') && access.can('attendance.rosters.read');
  const query = useAttendanceQuery(
    '/hr/branches' + queryString({ page, pageSize: 20, search: term }),
    referencePage,
    allowed,
  );
  if (!allowed)
    return (
      <Denied>
        Office references require organization read and All-scoped roster read access.
      </Denied>
    );
  return (
    <>
      <div className="attendance-heading">
        <h1>Office Rosters</h1>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          Reload
        </Command>
      </div>
      <TextInput
        label="Find office"
        value={search}
        maxLength={100}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(1);
        }}
      />
      <QueryState
        query={query}
        empty={query.data?.items.length === 0}
        emptyText="No offices match this search."
      />
      {query.data && !query.isError && (
        <>
          <div className="hr-table-scroll">
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Office</th>
                  <th>Status</th>
                  <th>Roster</th>
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((office) => (
                  <tr key={office.id}>
                    <td>{office.code}</td>
                    <td>{office.name}</td>
                    <td>{office.isActive ? 'Active' : 'Inactive'}</td>
                    <td>
                      <Link
                        className="attendance-record-link"
                        to={`/hr/attendance/offices/${office.id}/rosters`}
                        title="Open office roster"
                      >
                        <ArrowRight size={16} aria-hidden="true" />
                        Open
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={page}
            pageSize={20}
            total={query.data.totalCount}
            onChange={setPage}
            disabled={query.isFetching}
          />
        </>
      )}
    </>
  );
}
function OfficeWorkspace({ branchId }: { branchId: string }) {
  const access = useAttendanceAccess();
  const canRead = access.can('attendance.rosters.read');
  const canManage = access.can('attendance.rosters.manage');
  const query = useAttendanceQuery(
    `/hr/branches/${branchId}/shift-assignments`,
    officeRosterHistory,
    canRead,
  );
  const [operation, setOperation] = useState<{
    mode: 'assign' | 'replace' | 'end';
    assignmentId?: string;
  } | null>(null);
  const [notice, setNotice] = useState('');
  function close() {
    setOperation(null);
    void query.refetch();
  }
  if (!canRead)
    return <Denied>Office roster history requires All-scoped roster read access.</Denied>;
  const history = query.data;
  const today = businessToday();
  return (
    <>
      <div className="attendance-heading">
        <h1>Office Roster</h1>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={Boolean(operation) || query.isFetching}
          onClick={() => void query.refetch()}
        >
          Reload
        </Command>
      </div>
      <p>Office: {branchId}</p>
      <Link to="/hr/attendance/offices">Offices</Link>
      <SavedNotice message={notice} />
      <QueryState query={query} />
      {history && !query.isError && (
        <>
          {operation && canManage && (
            <OfficeRosterForm
              key={`${operation.mode}:${operation.assignmentId ?? 'new'}`}
              history={history}
              mode={operation.mode}
              assignmentId={operation.assignmentId}
              onSaved={() => {
                setNotice('Office schedule saved. Reloading current history.');
                close();
              }}
              onCancel={close}
            />
          )}
          {canManage && (
            <div className="attendance-commands">
              <Command
                icon={CalendarPlus}
                disabled={Boolean(operation) || query.isFetching}
                onClick={() => setOperation({ mode: 'assign' })}
              >
                Assign shift
              </Command>
            </div>
          )}
          <section className="attendance-section">
            <h2>Assignment History</h2>
            {!history.assignments.length ? (
              <p>No office schedule assignments recorded.</p>
            ) : (
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Shift / assignment</th>
                      <th>Effective interval</th>
                      <th>Calendar revision</th>
                      <th>Recorded</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.assignments.map((row) => (
                      <tr key={row.id}>
                        <td>
                          {row.shiftId}
                          <details>
                            <summary>Assignment ID</summary>
                            {row.id}
                          </details>
                        </td>
                        <td>
                          {row.effectiveFrom} to {row.effectiveTo ?? 'open'} (exclusive)
                        </td>
                        <td>{row.holidayCalendarId ?? 'None pinned'}</td>
                        <td>
                          {row.recordedAtUtc}
                          <details>
                            <summary>Actor</summary>
                            {row.recordedBy}
                          </details>
                        </td>
                        <td>
                          {canManage &&
                            (!row.effectiveTo || row.effectiveTo > today) &&
                            !history.endings.some(
                              (end) => end.officeRosterAssignmentId === row.id,
                            ) && (
                              <div className="attendance-commands">
                                <Command
                                  icon={Replace}
                                  variant="ghost"
                                  disabled={Boolean(operation) || query.isFetching}
                                  onClick={() =>
                                    setOperation({ mode: 'replace', assignmentId: row.id })
                                  }
                                >
                                  Replace
                                </Command>
                                <Command
                                  icon={StopCircle}
                                  variant="ghost"
                                  disabled={Boolean(operation) || query.isFetching}
                                  onClick={() =>
                                    setOperation({ mode: 'end', assignmentId: row.id })
                                  }
                                >
                                  End
                                </Command>
                              </div>
                            )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
          <section className="attendance-section">
            <h2>Ending Evidence</h2>
            {!history.endings.length ? (
              <p>No ending records.</p>
            ) : (
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Assignment</th>
                      <th>Exclusive end</th>
                      <th>Reason</th>
                      <th>Actor / recorded</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.endings.map((end) => (
                      <tr key={end.id}>
                        <td>{end.officeRosterAssignmentId}</td>
                        <td>{end.effectiveTo}</td>
                        <td>{end.reason}</td>
                        <td>
                          {end.recordedBy}
                          <br />
                          {end.recordedAtUtc}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </>
  );
}
export function OfficeRostersPage() {
  const { branchId } = useParams<{ branchId: string }>();
  return (
    <PrivateWorkspace resource={branchId}>
      {branchId ? (
        id.safeParse(branchId).success ? (
          <OfficeWorkspace key={branchId} branchId={branchId} />
        ) : (
          <Denied>The route does not contain a valid office ID.</Denied>
        )
      ) : (
        <OfficeSelection />
      )}
    </PrivateWorkspace>
  );
}
