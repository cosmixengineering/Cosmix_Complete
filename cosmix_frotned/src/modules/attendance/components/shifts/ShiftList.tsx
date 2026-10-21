import { useState } from 'react';
import { Eye, Plus, RefreshCw } from 'lucide-react';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { shiftPage } from '../../schemas/shifts';
import { queryString } from '../../api/attendance-api';
import { Command, QueryState, SavedNotice } from '../SchedulingUi';
import { ShiftMetadataForm } from './ShiftMetadataForm';

export function ShiftList({ onOpen }: { onOpen: (id: string) => void }) {
  const access = useAttendanceAccess();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [sort, setSort] = useState('code');
  const [descending, setDescending] = useState(false);
  const [page, setPage] = useState(1);
  const [create, setCreate] = useState(false);
  const [notice, setNotice] = useState('');
  const term = useDebouncedValue(search);
  const query = useAttendanceQuery(
    '/hr/shifts' +
      queryString({
        search: term,
        page,
        pageSize: 20,
        sortBy: sort,
        descending,
        isActive: status === '' ? undefined : status === 'true',
      }),
    shiftPage,
    access.can('attendance.shifts.read'),
  );
  function close() {
    setCreate(false);
    void query.refetch();
  }
  return (
    <>
      <div className="attendance-heading">
        <h1>Shifts</h1>
        <div className="attendance-commands">
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={create || query.isFetching}
            onClick={() => void query.refetch()}
          >
            Reload
          </Command>
          {access.can('attendance.shifts.manage') && (
            <Command icon={Plus} disabled={create} onClick={() => setCreate(true)}>
              Create shift
            </Command>
          )}
        </div>
      </div>
      <SavedNotice message={notice} />
      {create && (
        <ShiftMetadataForm
          mode="create"
          onCancel={close}
          onSaved={(shift) => {
            setNotice(`Created ${shift.code}.`);
            setCreate(false);
            onOpen(shift.id);
          }}
        />
      )}
      <fieldset disabled={create}>
        <div className="attendance-grid">
          <TextInput
            label="Search shifts"
            maxLength={100}
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
          />
          <SelectInput
            label="Status"
            value={status}
            options={[
              { value: '', label: 'All statuses' },
              { value: 'true', label: 'Active' },
              { value: 'false', label: 'Inactive' },
            ]}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
            }}
          />
          <SelectInput
            label="Sort by"
            value={sort}
            options={[
              { value: 'code', label: 'Code' },
              { value: 'name', label: 'Name' },
            ]}
            onChange={(event) => {
              setSort(event.target.value);
              setPage(1);
            }}
          />
          <label className="attendance-check">
            <input
              type="checkbox"
              checked={descending}
              onChange={(event) => {
                setDescending(event.target.checked);
                setPage(1);
              }}
            />
            Descending
          </label>
        </div>
      </fieldset>
      <QueryState
        query={query}
        empty={!query.data?.items.length}
        emptyText="No shifts match the current filters."
      />
      {query.data && !query.isError && (
        <>
          <div className="hr-table-scroll">
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Status</th>
                  <th>Detail</th>
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((row) => (
                  <tr key={row.id}>
                    <td>{row.code}</td>
                    <td>{row.name}</td>
                    <td>{row.isActive ? 'Active' : 'Inactive'}</td>
                    <td>
                      <Command
                        icon={Eye}
                        variant="ghost"
                        disabled={create}
                        onClick={() => onOpen(row.id)}
                      >
                        Open
                      </Command>
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
            disabled={create || query.isFetching}
            onChange={setPage}
          />
        </>
      )}
    </>
  );
}
