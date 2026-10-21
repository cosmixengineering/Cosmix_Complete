import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowRight, History, Plus, RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { queryString } from '../api/attendance-api';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { date, id, referencePage } from '../schemas/common';
import { officeConfigurationFor } from '../schemas/office-boundaries';
import {
  Command,
  Denied,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../components/SchedulingUi';
import { OfficeBoundaryForm } from '../components/geofences/OfficeBoundaryForm';
import { CirclePreview } from '../components/geofences/CirclePreview';

function OfficeSelection() {
  const access = useAttendanceAccess();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search);
  const allowed = access.can('org.company.read') && access.can('hr.organization.read');
  const query = useAttendanceQuery(
    '/hr/branches' + queryString({ page, pageSize: 20, search: term }),
    referencePage,
    allowed,
  );
  if (!allowed)
    return (
      <Denied>
        Office boundaries require SuperAdmin company access and office catalogue read access.
      </Denied>
    );
  return (
    <>
      <div className="attendance-heading">
        <h1>Office Boundaries</h1>
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
        onChange={(e) => {
          setSearch(e.target.value);
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
                  <th>Boundary</th>
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
                        to={`/hr/attendance/offices/${office.id}/boundaries`}
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
function BoundaryWorkspace({ branchId }: { branchId: string }) {
  const access = useAttendanceAccess();
  const canRead = access.can('org.company.read');
  const canManage = access.can('org.company.manage');
  const [asOf, setAsOf] = useState('');
  const [selectedDate, setSelectedDate] = useState('');
  const [editing, setEditing] = useState(false);
  const [notice, setNotice] = useState('');
  const query = useAttendanceQuery(
    `/company/offices/${branchId}/attendance-boundaries` + queryString({ asOfDate: selectedDate }),
    officeConfigurationFor(branchId, selectedDate),
    canRead,
  );
  const configuration = !query.isError ? query.data : undefined;
  function close() {
    setEditing(false);
    void query.refetch();
  }
  if (!canRead)
    return (
      <Denied>Office boundary reads require SuperAdmin and All-scoped company read access.</Denied>
    );
  return (
    <>
      <div className="attendance-heading">
        <h1>Office Boundary</h1>
        <div className="attendance-commands">
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={editing || query.isFetching}
            onClick={() => void query.refetch()}
          >
            Reload
          </Command>
          {canManage && (
            <Command
              icon={Plus}
              disabled={editing || query.isFetching || !configuration}
              onClick={() => setEditing(true)}
            >
              Publish boundary
            </Command>
          )}
        </div>
      </div>
      <Link to="/hr/attendance/office-boundaries">Offices</Link>
      <p>Office: {branchId}</p>
      <SavedNotice message={notice} />
      <div className="attendance-commands">
        <TextInput
          label="As-of business date"
          type="date"
          value={asOf}
          disabled={editing}
          onChange={(e) => setAsOf(e.target.value)}
        />
        <Command
          icon={History}
          variant="secondary"
          disabled={editing || query.isFetching || (asOf !== '' && !date.safeParse(asOf).success)}
          onClick={() => {
            if (asOf === selectedDate) void query.refetch();
            else setSelectedDate(asOf);
          }}
        >
          Read date
        </Command>
      </div>
      <QueryState query={query} />
      {configuration && (
        <section className="attendance-section">
          <h2>Boundary On {configuration.businessDate}</h2>
          {configuration.boundary ? (
            <>
              <p>
                Version {configuration.boundary.versionNumber}, effective{' '}
                {configuration.boundary.effectiveFrom}
              </p>
              <CirclePreview {...configuration.boundary} />
              <dl>
                <dt>Jurisdiction</dt>
                <dd>{configuration.boundary.jurisdictionCode}</dd>
                <dt>Approval reference</dt>
                <dd>{configuration.boundary.approvalReference}</dd>
                <dt>Recorded by</dt>
                <dd>{configuration.boundary.recordedBy}</dd>
                <dt>Recorded at</dt>
                <dd>{configuration.boundary.recordedAtUtc}</dd>
              </dl>
            </>
          ) : (
            <p>No effective boundary configured for this date.</p>
          )}
          <p>Latest published or scheduled version: {configuration.latestVersionNumber}</p>
          {editing && canManage && (
            <OfficeBoundaryForm
              configuration={configuration}
              onCancel={close}
              onSaved={() => {
                setNotice('Office boundary version saved.');
                close();
              }}
            />
          )}
        </section>
      )}
    </>
  );
}
export function OfficeBoundariesPage() {
  const { branchId } = useParams<{ branchId: string }>();
  return (
    <PrivateWorkspace resource={branchId}>
      {branchId ? (
        id.safeParse(branchId).success ? (
          <BoundaryWorkspace key={branchId} branchId={branchId} />
        ) : (
          <Denied>Invalid office ID.</Denied>
        )
      ) : (
        <OfficeSelection />
      )}
    </PrivateWorkspace>
  );
}
