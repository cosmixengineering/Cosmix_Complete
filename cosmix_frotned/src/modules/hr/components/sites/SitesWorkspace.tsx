import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Pencil, History, RefreshCw, MapPin } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { TextInput } from '@/components/forms/TextInput';
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
            gap: '16px',
          }}
        >
          <div>
            <div
              style={{
                fontSize: '11px',
                fontWeight: 600,
                color: '#64748b',
                textTransform: 'uppercase',
                letterSpacing: '0.04em',
              }}
            >
              HUMAN RESOURCES &bull; WORKING SITES
            </div>
            <h1 style={{ margin: '3px 0 0', fontSize: '20px', fontWeight: 700, color: '#0f172a' }}>
              Working Sites & Locations
            </h1>
          </div>

          <div className="hr-inline">
            <Button
              variant="ghost"
              title="Reload sites"
              aria-label="Reload sites"
              disabled={Boolean(editor)}
              onClick={() => void query.refetch()}
            >
              <RefreshCw size={15} />
            </Button>
            {manage && (
              <Button onClick={() => setEditor({})}>
                <Plus size={15} />
                Add site
              </Button>
            )}
          </div>
        </div>
      </div>

      {saved && (
        <p role="status" className="hr-message" style={{ marginBottom: '16px' }}>
          Site saved successfully.
        </p>
      )}

      {/* Toolbar & Filters */}
      <div className="sites-filter-card">
        <div
          style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap', flex: 1 }}
        >
          <div style={{ minWidth: '220px', flex: 1, maxWidth: '360px' }}>
            <TextInput
              label="Search sites"
              value={search}
              maxLength={100}
              placeholder="Filter by site name, code, client..."
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div style={{ width: '130px' }}>
            <SelectInput
              label="Status"
              value={status}
              options={[
                { value: '', label: 'All' },
                { value: 'true', label: 'Active' },
                { value: 'false', label: 'Inactive' },
              ]}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div style={{ width: '130px' }}>
            <SelectInput
              label="Sort by"
              value={sort}
              options={[
                { value: 'code', label: 'Code' },
                { value: 'name', label: 'Name' },
              ]}
              onChange={(e) => {
                setSort(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <label
            className="hr-inline"
            style={{ marginTop: '22px', fontSize: '13px', color: '#475569', cursor: 'pointer' }}
          >
            <input
              type="checkbox"
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
                        <MapPin size={16} />
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
                  cell: (row) => <span style={{ color: '#334155' }}>{row.cityName ?? '-'}</span>,
                },
                {
                  id: 'client',
                  header: 'CLIENT',
                  cell: (row) => <span style={{ color: '#334155' }}>{row.clientName ?? '-'}</span>,
                },
                {
                  id: 'dates',
                  header: 'DATES',
                  cell: (row) => (
                    <span style={{ color: '#64748b', fontSize: '12.5px' }}>
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
                    >
                      {row.isActive ? 'Active' : 'Inactive'}
                    </span>
                  ),
                },
                {
                  id: 'actions',
                  header: '',
                  cell: (row) => (
                    <div className="hr-inline" style={{ gap: '6px' }}>
                      <button
                        className="site-btn-view"
                        title="View site detail, supervisor, workers & geofence map"
                        onClick={() => navigate(`/hr/sites/${row.id}`)}
                      >
                        <MapPin size={13} /> View
                      </button>
                      {manage && (
                        <>
                          <button
                            className="icon-button"
                            title="Edit site"
                            aria-label={`Edit ${row.name}`}
                            onClick={() => setEditor({ siteId: row.id })}
                          >
                            <Pencil size={14} />
                          </button>
                          <button
                            className="icon-button"
                            title="Site history"
                            aria-label={`${row.name} history`}
                            onClick={() => setDetail({ id: row.id, history: true })}
                          >
                            <History size={14} />
                          </button>
                        </>
                      )}
                    </div>
                  ),
                },
              ]}
            />
            {!query.data.items.length && <p className="hr-notice">No sites found.</p>}
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
            />
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
