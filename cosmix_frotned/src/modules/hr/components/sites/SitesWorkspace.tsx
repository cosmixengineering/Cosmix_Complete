import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Pencil, History, RefreshCw, MapPin } from 'lucide-react';
import { Dialog } from '@/components/ui/Dialog';
import { SelectInput } from '@/components/forms/SelectInput';
import { DataTable } from '@/components/data-display/DataTable';
import { Pagination } from '@/components/data-display/Pagination';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { queryString } from '../../api/hr-api';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { pageOf } from '../../schemas/contracts';
import { siteSchema } from '../../schemas/sites';
import { QueryState, Denied } from '../Feedback';
import { RecordView } from '../RecordView';
import { OrganizationHistory } from '../organization/OrganizationHistory';
import { SiteEditor } from './SiteEditor';
import '@/styles/site-detail.css';

export function SitesWorkspace() {
  const access = useHrAccess();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [sort, setSort] = useState('code');
  const [descending, setDescending] = useState(false);
  const [editor, setEditor] = useState<{ siteId?: string; statusOnly?: boolean } | null>(null);
  const [detail, setDetail] = useState<{ id: string; history: boolean } | null>(null);
  const [saved, setSaved] = useState(false);
  const term = useDebouncedValue(search);

  const query = useHrQuery(
    'sites' +
      queryString({ page, pageSize: 20, search: term, isActive: status, sortBy: sort, descending }),
    pageOf(siteSchema),
    ['hr.sites.read'],
  );

  const manage = access.can('hr.sites.manage');
  if (!access.can('hr.sites.read')) return <Denied />;

  return (
    <>
      {/* Clean Flat Header */}
      <div className="sites-list-header">
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '8px',
          }}
        >
          <div>
            <div
              style={{
                fontSize: '9.5px',
                fontWeight: 700,
                color: '#64748b',
                textTransform: 'uppercase',
                letterSpacing: '0.04em',
              }}
            >
              HUMAN RESOURCES &bull; WORKING SITES
            </div>
            <h1 style={{ margin: '1px 0 0', fontSize: '15px', fontWeight: 750, color: '#0f172a' }}>
              Working Sites & Locations
            </h1>
          </div>

          <div className="hr-inline" style={{ gap: '6px' }}>
            <button
              type="button"
              className="sites-compact-btn-ghost"
              title="Reload sites"
              aria-label="Reload sites"
              disabled={Boolean(editor)}
              onClick={() => void query.refetch()}
            >
              <RefreshCw size={12} />
            </button>
            {manage && (
              <button
                type="button"
                className="sites-compact-btn-primary"
                onClick={() => setEditor({})}
              >
                <Plus size={12} />
                <span>Add site</span>
              </button>
            )}
          </div>
        </div>
      </div>

      {saved && (
        <p
          role="status"
          className="hr-message"
          style={{ marginBottom: '8px', padding: '4px 8px', fontSize: '11px' }}
        >
          Site saved successfully.
        </p>
      )}

      {/* Toolbar & Filters */}
      <div className="sites-filter-card">
        <div
          style={{ display: 'flex', alignItems: 'flex-end', gap: '8px', flexWrap: 'wrap', flex: 1 }}
        >
          <div style={{ width: '190px' }}>
            <label
              style={{
                display: 'block',
                fontSize: '9.5px',
                fontWeight: 650,
                color: '#64748b',
                marginBottom: '2px',
              }}
            >
              Search sites
            </label>
            <input
              type="text"
              className="sites-compact-input"
              value={search}
              maxLength={100}
              placeholder="Search sites..."
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div style={{ width: '84px' }}>
            <SelectInput
              label="Status"
              compact
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
              options={[
                { value: '', label: 'All' },
                { value: 'true', label: 'Active' },
                { value: 'false', label: 'Inactive' },
              ]}
            />
          </div>
          <div style={{ width: '84px' }}>
            <SelectInput
              label="Sort by"
              compact
              value={sort}
              onChange={(e) => {
                setSort(e.target.value);
                setPage(1);
              }}
              options={[
                { value: 'code', label: 'Code' },
                { value: 'name', label: 'Name' },
              ]}
            />
          </div>
          <label
            className="hr-inline"
            style={{
              fontSize: '10.5px',
              color: '#475569',
              cursor: 'pointer',
              height: '24px',
              alignItems: 'center',
              marginBottom: '1px',
            }}
          >
            <input
              type="checkbox"
              style={{ width: '12px', height: '12px' }}
              checked={descending}
              onChange={(e) => {
                setDescending(e.target.checked);
                setPage(1);
              }}
            />
            Descending
          </label>
        </div>
      </div>

      {/* Data Table */}
      <QueryState query={query}>
        {query.data && (
          <>
            <div className="sites-compact-table-wrap">
              <DataTable
                caption="Operational Sites & Geofences"
                rows={query.data.items}
                rowKey={(row) => row.id}
                columns={[
                  {
                    id: 'name',
                    header: 'NAME',
                    cell: (row) => (
                      <button
                        type="button"
                        className="site-cell-item"
                        title={`Open ${row.name} Detail`}
                        onClick={() => navigate(`/hr/sites/${row.id}`)}
                      >
                        <div className="site-cell-avatar">
                          <MapPin size={12} />
                        </div>
                        <div className="site-cell-content">
                          <span className="site-cell-name">{row.name}</span>
                          <span className="site-cell-subtitle">
                            {row.cityName ? `${row.cityName}` : 'Active Site'} &bull; {row.code}
                          </span>
                        </div>
                      </button>
                    ),
                  },
                  {
                    id: 'code',
                    header: 'CODE',
                    cell: (row) => <span className="site-code-badge">{row.code}</span>,
                  },
                  {
                    id: 'city',
                    header: 'CITY',
                    cell: (row) => (
                      <span style={{ color: '#334155', fontSize: '11px' }}>
                        {row.cityName ?? '-'}
                      </span>
                    ),
                  },
                  {
                    id: 'client',
                    header: 'CLIENT',
                    cell: (row) => (
                      <span style={{ color: '#334155', fontSize: '11px' }}>
                        {row.clientName ?? '-'}
                      </span>
                    ),
                  },
                  {
                    id: 'dates',
                    header: 'DATES',
                    cell: (row) => (
                      <span style={{ color: '#64748b', fontSize: '10.5px' }}>
                        {row.startsOn ?? '2026-01-01'} / {row.endsOn ?? 'Open'}
                      </span>
                    ),
                  },
                  {
                    id: 'status',
                    header: 'STATUS',
                    cell: (row) => (
                      <span
                        className={`hr-status hr-status--${row.isActive ? 'positive' : 'neutral'}`}
                        style={{ fontSize: '9.5px', padding: '1px 6px' }}
                      >
                        {row.isActive ? 'Active' : 'Inactive'}
                      </span>
                    ),
                  },
                  {
                    id: 'actions',
                    header: '',
                    cell: (row) => (
                      <div className="hr-inline" style={{ gap: '4px', justifyContent: 'flex-end' }}>
                        <button
                          className="site-btn-view"
                          title="View site detail, supervisor, workers & geofence map"
                          onClick={() => navigate(`/hr/sites/${row.id}`)}
                        >
                          <MapPin size={11} /> View
                        </button>
                        {manage && (
                          <>
                            <button
                              className="icon-button"
                              style={{ width: '22px', height: '22px', padding: 0 }}
                              title="Edit site"
                              aria-label={`Edit ${row.name}`}
                              onClick={() => setEditor({ siteId: row.id })}
                            >
                              <Pencil size={11} />
                            </button>
                            <button
                              className="icon-button"
                              style={{ width: '22px', height: '22px', padding: 0 }}
                              title="Site history"
                              aria-label={`${row.name} history`}
                              onClick={() => setDetail({ id: row.id, history: true })}
                            >
                              <History size={11} />
                            </button>
                          </>
                        )}
                      </div>
                    ),
                  },
                ]}
              />
            </div>
            {!query.data.items.length && (
              <p className="hr-notice" style={{ fontSize: '11px', padding: '8px' }}>
                No sites found.
              </p>
            )}
            <div style={{ marginTop: '6px' }}>
              <Pagination
                page={page}
                pageSize={20}
                total={query.data.totalCount}
                onChange={setPage}
              />
            </div>
          </>
        )}
      </QueryState>

      {editor && manage && (
        <SiteEditor
          {...editor}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null);
            setSaved(true);
            void query.refetch();
          }}
        />
      )}

      {detail && (!detail.history || manage) && (
        <SiteDetail key={detail.id + detail.history} {...detail} onClose={() => setDetail(null)} />
      )}
    </>
  );
}

function SiteDetail({
  id,
  history,
  onClose,
}: {
  id: string;
  history: boolean;
  onClose: () => void;
}) {
  const query = useHrQuery(`sites/${id}`, siteSchema, ['hr.sites.read']);
  return (
    <Dialog
      open
      title={history ? 'Site history' : (query.data?.name ?? 'Site details')}
      description={query.data?.code ?? id}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      {history ? (
        <OrganizationHistory path={`sites/${id}`} permission="hr.sites.manage" />
      ) : (
        <QueryState query={query}>
          {query.data && <RecordView value={query.data} omit={['id', 'version']} />}
        </QueryState>
      )}
    </Dialog>
  );
}
