import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  Plus,
  Pencil,
  History,
  Power,
  Search,
  RotateCw,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { Dialog } from '@/components/ui/Dialog';
import { SelectInput } from '@/components/forms/SelectInput';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf } from '../schemas/contracts';
import { masterSchema, type Master } from '../schemas/organization';
import { queryString } from '../api/hr-api';
import { Denied, QueryState } from '../components/Feedback';
import { MasterEditor, getMasterMeta } from '../components/organization/MasterEditor';
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
  const [adding, setAdding] = useState(false);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  if (!selected) return <Denied />;

  const kind = selected[0];
  const meta = getMasterMeta(kind);
  const manage = kind !== 'sites' && access.can('hr.organization.manage');

  return (
    <div className="org-page-container">
      {/* Top Breadcrumb */}
      <div className="org-breadcrumb">
        <span className="org-breadcrumb-parent">Human Resources</span>
        <span className="org-breadcrumb-sep">/</span>
        <span className="org-breadcrumb-current">Organization</span>
      </div>

      {/* Sub Header */}
      <div className="org-page-header">
        <div>
          <h1 className="org-page-title">Organization</h1>
          <p className="org-page-subtitle">Manage your organization records</p>
        </div>
        {kind !== 'holiday-calendars' && manage && (
          <button type="button" className="org-add-btn" onClick={() => setAdding(true)}>
            <Plus size={12} strokeWidth={2.5} />
            Add {meta.singular}
          </button>
        )}
      </div>

      {/* Tabs */}
      <nav className="org-tabs-bar" aria-label="Organization references">
        {available.map(([key, label]) => (
          <Link
            key={key}
            className={`org-tab-item ${key === selected[0] ? 'active' : ''}`}
            to={'/hr/organization?group=' + key}
          >
            {label}
          </Link>
        ))}
      </nav>

      {/* Master Content */}
      {selected[0] === 'holiday-calendars' ? (
        <HolidayCalendars />
      ) : (
        <MasterList
          key={`${selected[0]}-${refreshTrigger}`}
          kind={selected[0]}
          title={selected[1]}
          externalAdd={adding}
          onExternalAddClose={() => setAdding(false)}
          onRefreshNeeded={() => setRefreshTrigger((k) => k + 1)}
        />
      )}
    </div>
  );
}

function MasterList({
  kind,
  title,
  externalAdd,
  onExternalAddClose,
  onRefreshNeeded,
}: {
  kind: string;
  title: string;
  externalAdd?: boolean;
  onExternalAddClose?: () => void;
  onRefreshNeeded?: () => void;
}) {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [search, setSearch] = useState('');
  const [active, setActive] = useState('');
  const term = useDebouncedValue(search);
  const [editor, setEditor] = useState<{ record?: Master; statusOnly?: boolean } | null>(null);
  const [detail, setDetail] = useState<Master | null>(null);
  const [history, setHistory] = useState<Master | null>(null);
  const permission = readPermission(kind);
  const manage = kind !== 'sites' && access.can('hr.organization.manage');
  const meta = getMasterMeta(kind);

  const query = useHrQuery(
    kind + queryString({ page, pageSize, search: term, isActive: active }),
    pageOf(masterSchema),
    [permission],
  );

  return (
    <>
      {/* Toolbar */}
      <div className="org-toolbar">
        <div className="org-search-box">
          <Search size={12} className="org-search-icon" />
          <input
            type="text"
            className="org-search-input"
            placeholder={`Search ${meta.singular} name or code...`}
            value={search}
            maxLength={100}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div style={{ width: '110px' }}>
          <SelectInput
            compact
            value={active}
            onChange={(e) => {
              setActive(e.target.value);
              setPage(1);
            }}
            options={[
              { value: '', label: 'All statuses' },
              { value: 'true', label: 'Active' },
              { value: 'false', label: 'Inactive' },
            ]}
          />
        </div>

        <button
          type="button"
          className="org-refresh-btn"
          title="Refresh records"
          onClick={() => void query.refetch()}
        >
          <RotateCw size={12} />
        </button>
      </div>

      {/* Table Data */}
      <QueryState query={query}>
        {query.data && (
          <div className="org-table-container">
            <table className="org-table">
              <thead>
                <tr>
                  <th>{`${meta.title.toUpperCase()} NAME`}</th>
                  <th>{`${meta.title.toUpperCase()} CODE`}</th>
                  {kind === 'cities' && <th>PROVINCE</th>}
                  {kind === 'branches' && <th>CITY</th>}
                  <th>STATUS</th>
                  <th style={{ textAlign: 'right' }}>ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {query.data.items.length === 0 ? (
                  <tr>
                    <td
                      colSpan={kind === 'cities' || kind === 'branches' ? 5 : 4}
                      style={{ textAlign: 'center', padding: '36px 18px', color: '#94a3b8' }}
                    >
                      No {title.toLowerCase()} found matching your filters.
                    </td>
                  </tr>
                ) : (
                  query.data.items.map((r) => (
                    <tr key={r.id}>
                      <td>
                        <button
                          type="button"
                          className="org-table-name-btn"
                          onClick={() => setDetail(r)}
                        >
                          {r.name}
                        </button>
                      </td>
                      <td>
                        <span className="org-table-code">{r.code}</span>
                      </td>
                      {kind === 'cities' && <td>{r.province ?? '—'}</td>}
                      {kind === 'branches' && <td>{r.cityName ?? '—'}</td>}
                      <td>
                        <span className={`org-badge-status ${r.isActive ? 'active' : 'inactive'}`}>
                          <span className="org-badge-dot"></span>
                          {r.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>
                        <div className="org-actions-cell" style={{ justifyContent: 'flex-end' }}>
                          {manage && (
                            <>
                              <button
                                type="button"
                                className="org-action-icon-btn edit"
                                title={`Edit ${r.name}`}
                                aria-label={`Edit ${r.name}`}
                                onClick={() => setEditor({ record: r })}
                              >
                                <Pencil size={11} />
                              </button>
                              <button
                                type="button"
                                className="org-action-icon-btn power"
                                title={`Change ${r.name} status`}
                                aria-label={`Change ${r.name} status`}
                                onClick={() => setEditor({ record: r, statusOnly: true })}
                              >
                                <Power size={11} />
                              </button>
                            </>
                          )}
                          {kind !== 'sites' && (
                            <button
                              type="button"
                              className="org-action-icon-btn history"
                              title={`${r.name} history`}
                              aria-label={`${r.name} history`}
                              onClick={() => setHistory(r)}
                            >
                              <History size={11} />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>

            {/* Pagination footer */}
            <div className="org-pagination-bar">
              <div className="org-pagination-left">
                <span>{query.data.items.length} rows shown</span>
              </div>
              <div className="org-pagination-right">
                <div className="org-page-size-selector">
                  <span>Rows per page</span>
                  <div style={{ width: '65px' }}>
                    <SelectInput
                      compact
                      value={String(pageSize)}
                      onChange={(e) => {
                        setPageSize(Number(e.target.value));
                        setPage(1);
                      }}
                      options={[
                        { value: '10', label: '10' },
                        { value: '20', label: '20' },
                        { value: '50', label: '50' },
                      ]}
                    />
                  </div>
                </div>
                <div className="org-page-controls">
                  <button
                    type="button"
                    className="org-page-nav-btn"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    <ChevronLeft size={12} />
                    Previous
                  </button>
                  <span className="org-page-indicator">
                    {page} / {Math.max(1, Math.ceil(query.data.totalCount / pageSize))}
                  </span>
                  <button
                    type="button"
                    className="org-page-nav-btn"
                    disabled={page >= Math.ceil(query.data.totalCount / pageSize)}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Next
                    <ChevronRight size={12} />
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}
      </QueryState>

      {/* Editor Modal for Row Edit or Status Change */}
      {editor && (
        <MasterEditor
          kind={kind}
          {...editor}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null);
            void query.refetch();
            onRefreshNeeded?.();
          }}
        />
      )}

      {/* Editor Modal for Top + Add Button */}
      {externalAdd && (
        <MasterEditor
          kind={kind}
          onClose={() => onExternalAddClose?.()}
          onSaved={() => {
            onExternalAddClose?.();
            void query.refetch();
            onRefreshNeeded?.();
          }}
        />
      )}

      {/* Detail Modal */}
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

      {/* History Modal */}
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
