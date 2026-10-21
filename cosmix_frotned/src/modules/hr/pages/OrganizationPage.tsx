import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Plus, Pencil, History, Power } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { DataTable } from '@/components/data-display/DataTable';
import { Pagination } from '@/components/data-display/Pagination';
import { Dialog } from '@/components/ui/Dialog';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf } from '../schemas/contracts';
import { masterSchema, type Master } from '../schemas/organization';
import { queryString } from '../api/hr-api';
import { Denied, QueryState } from '../components/Feedback';
import { MasterEditor } from '../components/organization/MasterEditor';
import { OrganizationHistory } from '../components/organization/OrganizationHistory';
import { HolidayCalendars } from '../components/organization/HolidayCalendars';
import { RecordView } from '../components/RecordView';
const groups = [
  ['departments', 'Departments'],
  ['designations', 'Designations'],
  ['cities', 'Cities'],
  ['branches', 'Offices'],
  ['staff-categories', 'Staff categories'],
  ['employment-types', 'Employment types'],
  ['holiday-calendars', 'Holidays'],
  ['sites', 'Sites'],
] as const;
const readPermission = (kind: string) =>
  kind === 'sites'
    ? 'hr.sites.read'
    : kind === 'holiday-calendars'
      ? 'hr.holidays.read'
      : 'hr.organization.read';
export function OrganizationPage() {
  const access = useHrAccess();
  const [params] = useSearchParams();
  const available = groups.filter(([key]) => access.can(readPermission(key)));
  const selected = available.find(([key]) => key === params.get('group')) ?? available[0];
  if (!selected) return <Denied />;
  return (
    <>
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">HUMAN RESOURCES</p>
          <h1>Organization</h1>
        </div>
      </div>
      <nav className="hr-tabs" aria-label="Organization references">
        {available.map(([key, label]) => (
          <Link
            key={key}
            className={key === selected[0] ? 'active' : ''}
            to={'/hr/organization?group=' + key}
          >
            {label}
          </Link>
        ))}
      </nav>
      {selected[0] === 'holiday-calendars' ? (
        <HolidayCalendars />
      ) : (
        <MasterList key={selected[0]} kind={selected[0]} title={selected[1]} />
      )}
    </>
  );
}
function MasterList({ kind, title }: { kind: string; title: string }) {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [active, setActive] = useState('');
  const term = useDebouncedValue(search);
  const [editor, setEditor] = useState<{ record?: Master; statusOnly?: boolean } | null>(null);
  const [detail, setDetail] = useState<Master | null>(null);
  const [history, setHistory] = useState<Master | null>(null);
  const permission = readPermission(kind);
  const manage = kind !== 'sites' && access.can('hr.organization.manage');
  const query = useHrQuery(
    kind + queryString({ page, pageSize: 20, search: term, isActive: active }),
    pageOf(masterSchema),
    [permission],
  );
  return (
    <>
      <div className="hr-page-heading">
        <h2>{title}</h2>
        {manage && (
          <Button onClick={() => setEditor({})}>
            <Plus size={16} />
            Add record
          </Button>
        )}
      </div>
      <div className="hr-toolbar">
        <TextInput
          label={`Search ${title.toLowerCase()}`}
          value={search}
          maxLength={100}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <SelectInput
          label="Status"
          value={active}
          onChange={(e) => {
            setActive(e.target.value);
            setPage(1);
          }}
          options={[
            { value: '', label: 'All' },
            { value: 'true', label: 'Active' },
            { value: 'false', label: 'Inactive' },
          ]}
        />
      </div>
      <QueryState query={query}>
        {query.data && (
          <>
            <DataTable
              caption={title}
              rows={query.data.items}
              rowKey={(r) => r.id}
              columns={[
                {
                  id: 'name',
                  header: 'NAME',
                  cell: (r) => (
                    <button className="hr-text-button" onClick={() => setDetail(r)}>
                      {r.name}
                    </button>
                  ),
                },
                { id: 'code', header: 'CODE', cell: (r) => r.code },
                {
                  id: 'city',
                  header: kind === 'cities' ? 'PROVINCE' : 'CITY',
                  cell: (r) => (kind === 'cities' ? (r.province ?? '-') : (r.cityName ?? '-')),
                },
                {
                  id: 'status',
                  header: 'STATUS',
                  cell: (r) => (
                    <span className={`hr-status hr-status--${r.isActive ? 'positive' : 'neutral'}`}>
                      {r.isActive ? 'Active' : 'Inactive'}
                    </span>
                  ),
                },
                {
                  id: 'actions',
                  header: '',
                  cell: (r) => (
                    <div className="hr-inline">
                      {manage && (
                        <>
                          <button
                            className="icon-button"
                            title="Edit record"
                            aria-label={`Edit ${r.name}`}
                            onClick={() => setEditor({ record: r })}
                          >
                            <Pencil size={16} />
                          </button>
                          <button
                            className="icon-button"
                            title="Change status"
                            aria-label={`Change ${r.name} status`}
                            onClick={() => setEditor({ record: r, statusOnly: true })}
                          >
                            <Power size={16} />
                          </button>
                        </>
                      )}
                      {kind !== 'sites' && (
                        <button
                          className="icon-button"
                          title="Change history"
                          aria-label={`${r.name} history`}
                          onClick={() => setHistory(r)}
                        >
                          <History size={16} />
                        </button>
                      )}
                    </div>
                  ),
                },
              ]}
            />
            {!query.data.items.length && <p className="hr-notice">No records found.</p>}
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
      {editor && (
        <MasterEditor
          kind={kind}
          {...editor}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null);
            void query.refetch();
          }}
        />
      )}
      {detail && (
        <Dialog
          open
          title={detail.name}
          description={detail.code}
          onOpenChange={() => setDetail(null)}
        >
          <RecordView
            value={detail}
            omit={['id', 'version', 'projectId', 'supervisorEmployeeId']}
          />
        </Dialog>
      )}
      {history && (
        <Dialog
          open
          title="Organization history"
          description={history.name}
          onOpenChange={() => setHistory(null)}
        >
          <OrganizationHistory path={`${kind}/${history.id}`} permission={permission} />
        </Dialog>
      )}
    </>
  );
}
