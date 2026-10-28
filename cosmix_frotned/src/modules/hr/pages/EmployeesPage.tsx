import { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import {
  Plus,
  Search,
  ArrowUpRight,
  Phone,
  Mail,
  Smartphone,
  Pencil,
  User,
  Users,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { DataTable } from '@/components/data-display/DataTable';
import { Pagination } from '@/components/data-display/Pagination';
import { SelectInput } from '@/components/forms/SelectInput';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../hooks/use-hr-queries';
import { EmployeeArchiveFilter } from '../components/employee/EmployeeArchiveFilter';
import {
  employeeArchivePageSchema,
  employeeArchiveQuery,
  type EmployeeArchiveFilterValue,
} from '../components/employee/employee-archive-directory';
import { onboardingLabels, employmentLabels } from '../types/contracts';
import { writeResult } from '../schemas/contracts';
import { queryString } from '../api/hr-api';
import { ActionMessage, Denied, QueryState } from '../components/Feedback';

function formatDate(isoDate?: string | null) {
  if (!isoDate) return '—';
  try {
    const d = new Date(isoDate);
    if (isNaN(d.getTime())) return isoDate;
    return d.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  } catch {
    return isoDate;
  }
}

export function EmployeesPage() {
  const access = useHrAccess();
  const navigate = useNavigate();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const createAction = useHrAction();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [archive, setArchive] = useState<EmployeeArchiveFilterValue>('current');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search, 300);
  const query = useHrQuery(
    'employees' +
      queryString({ page, pageSize: 25, search: term, status, ...employeeArchiveQuery(archive) }),
    employeeArchivePageSchema,
    ['hr.employees.read'],
  );

  const handleCreateNewEmployee = async () => {
    const r = await createAction.run('employees', undefined, 'POST', writeResult);
    if (r) {
      void navigate(`${basePath}/employees/${r.employeeId}/hiring`);
    }
  };

  if (!access.can('hr.employees.read')) return <Denied />;
  return (
    <>
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">HUMAN RESOURCES</p>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <h1>Employees Directory</h1>
            {query.data && (
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '5px',
                  padding: '3px 10px',
                  background: '#f1f5f9',
                  border: '1px solid #e2e8f0',
                  borderRadius: '999px',
                  fontSize: '12px',
                  fontWeight: 600,
                  color: '#475569',
                }}
              >
                <Users size={13} />
                <span>{query.data.totalCount} total</span>
              </span>
            )}
          </div>
        </div>
        {access.can('hr.employees.drafts.manage') && (
          <Button disabled={createAction.pending} onClick={() => void handleCreateNewEmployee()}>
            <Plus size={17} />
            {createAction.pending ? 'Opening form...' : 'New employee'}
          </Button>
        )}
      </div>
      <ActionMessage action={createAction} />
      <div className="hr-toolbar">
        <div className="hr-toolbar-search-field">
          <label htmlFor="employee-search-input" className="hr-toolbar-search-label">
            Search employee
          </label>
          <div className="hr-toolbar-search-wrap">
            <Search size={14} className="hr-toolbar-search-icon" aria-hidden="true" />
            <input
              id="employee-search-input"
              type="text"
              aria-label="Search employees"
              placeholder="Search employee name, number or phone..."
              maxLength={100}
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="hr-toolbar-search-input"
            />
            {search && (
              <button
                type="button"
                className="hr-toolbar-search-clear"
                onClick={() => {
                  setSearch('');
                  setPage(1);
                }}
                aria-label="Clear search"
                title="Clear search"
              >
                <X size={12} />
              </button>
            )}
          </div>
        </div>
        <SelectInput
          label="Hiring status"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
          options={[
            { value: '', label: 'All statuses' },
            ...onboardingLabels.map((label, i) => ({ value: String(i), label })),
          ]}
        />
        <EmployeeArchiveFilter
          value={archive}
          onChange={(value) => {
            setArchive(value);
            setPage(1);
          }}
        />
      </div>
      <QueryState query={query}>
        {query.data && (
          <>
            <DataTable
              caption="Employee directory"
              rows={query.data.items}
              rowKey={(r) => r.id}
              columns={[
                {
                  id: 'code',
                  header: 'EMP CODE',
                  cell: (r) => (
                    <span
                      style={{
                        fontFamily:
                          'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
                        fontSize: '11px',
                        fontWeight: 700,
                        color: '#334155',
                        background: '#f1f5f9',
                        border: '1px solid #e2e8f0',
                        padding: '2px 6px',
                        borderRadius: '4px',
                        display: 'inline-block',
                        letterSpacing: '0.04em',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      #{String(r.employeeNumber).padStart(4, '0')}
                    </span>
                  ),
                },
                {
                  id: 'employee',
                  header: 'EMPLOYEE',
                  cell: (r) => (
                    <Link
                      className="hr-employee-link"
                      to={`${basePath}/employees/${r.id}${r.onboardingStatus === 2 ? '' : '/hiring'}`}
                      style={{ textDecoration: 'none' }}
                    >
                      <div>
                        <strong
                          style={{
                            color: '#0f172a',
                            fontSize: '11.5px',
                            display: 'block',
                            lineHeight: 1.25,
                          }}
                        >
                          {r.name && r.name.trim().length > 0 ? r.name : 'Unnamed draft'}
                        </strong>
                        <span
                          style={{
                            fontSize: '10px',
                            color: '#94a3b8',
                            display: 'block',
                            marginTop: '1px',
                          }}
                        >
                          {r.identityNumber
                            ? `CNIC: ${r.identityNumber}`
                            : `Ref: ${r.id.slice(0, 8)}...`}
                        </span>
                      </div>
                    </Link>
                  ),
                },
                {
                  id: 'contact',
                  header: 'CONTACT INFO',
                  cell: (r) => (
                    <div
                      style={{
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '3px',
                        fontSize: '11.5px',
                      }}
                    >
                      {r.phone ? (
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '5px',
                            color: '#334155',
                            fontWeight: 500,
                          }}
                        >
                          <Phone size={12} style={{ color: '#64748b', flexShrink: 0 }} />
                          <span>{r.phone}</span>
                        </span>
                      ) : null}
                      {r.email ? (
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '5px',
                            color: '#64748b',
                          }}
                        >
                          <Mail size={12} style={{ color: '#94a3b8', flexShrink: 0 }} />
                          <span
                            style={{
                              maxWidth: '140px',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                            }}
                          >
                            {r.email}
                          </span>
                        </span>
                      ) : null}
                      {!r.phone && !r.email ? (
                        <span style={{ color: '#94a3b8', fontStyle: 'italic' }}>Not provided</span>
                      ) : null}
                    </div>
                  ),
                },
                {
                  id: 'joined',
                  header: 'JOINED / CREATED',
                  cell: (r) => (
                    <div
                      style={{
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '2px',
                        fontSize: '11.5px',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      <span style={{ fontWeight: 600, color: '#334155' }}>
                        {r.joinedOn
                          ? formatDate(r.joinedOn)
                          : r.createdAt
                            ? formatDate(r.createdAt)
                            : '—'}
                      </span>
                      <span style={{ fontSize: '10.5px', color: '#94a3b8' }}>
                        {r.joinedOn ? 'Joined' : r.createdAt ? 'Draft created' : 'Pending hire'}
                      </span>
                    </div>
                  ),
                },
                {
                  id: 'onboarding',
                  header: 'HIRING STATUS',
                  cell: (r) => (
                    <span
                      className={`hr-status hr-status--${
                        r.onboardingStatus === 2
                          ? 'positive'
                          : r.onboardingStatus === 4
                            ? 'negative'
                            : r.onboardingStatus === 3
                              ? 'warning'
                              : 'neutral'
                      }`}
                      style={{
                        fontSize: '11px',
                        fontWeight: 600,
                        padding: '3px 8px',
                        borderRadius: '6px',
                        whiteSpace: 'nowrap',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '5px',
                      }}
                    >
                      <span
                        style={{
                          width: '5px',
                          height: '5px',
                          borderRadius: '50%',
                          background:
                            r.onboardingStatus === 2
                              ? '#10b981'
                              : r.onboardingStatus === 4
                                ? '#ef4444'
                                : r.onboardingStatus === 3
                                  ? '#f59e0b'
                                  : '#94a3b8',
                        }}
                      />
                      {onboardingLabels[r.onboardingStatus] ?? 'Draft'}
                    </span>
                  ),
                },
                {
                  id: 'status',
                  header: 'EMPLOYMENT',
                  cell: (r) => (
                    <div
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      <span
                        style={{
                          width: '6px',
                          height: '6px',
                          borderRadius: '50%',
                          flexShrink: 0,
                          backgroundColor:
                            r.status === 2
                              ? '#10b981'
                              : r.status === 3
                                ? '#f59e0b'
                                : r.status === 5
                                  ? '#ef4444'
                                  : r.status === 4
                                    ? '#94a3b8'
                                    : '#64748b',
                        }}
                      />
                      <span style={{ fontSize: '12px', fontWeight: 500, color: '#1e293b' }}>
                        {employmentLabels[r.status] ?? 'Draft'}
                      </span>
                    </div>
                  ),
                },
                {
                  id: 'archive',
                  header: 'RECORD',
                  cell: (r) => (
                    <span
                      style={{
                        display: 'inline-block',
                        padding: '2px 8px',
                        borderRadius: '5px',
                        fontSize: '11px',
                        fontWeight: 600,
                        backgroundColor: r.isArchived ? '#fee2e2' : '#f0fdf4',
                        color: r.isArchived ? '#991b1b' : '#166534',
                        border: `1px solid ${r.isArchived ? '#fecaca' : '#bbf7d0'}`,
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {r.isArchived ? 'Archived' : 'Current'}
                    </span>
                  ),
                },
                {
                  id: 'mobile',
                  header: 'MOBILE APP',
                  cell: (r) => (
                    <div style={{ whiteSpace: 'nowrap' }}>
                      {r.mobileAccessRequested ? (
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '4px',
                            color: '#2563eb',
                            backgroundColor: '#eff6ff',
                            border: '1px solid #bfdbfe',
                            padding: '2px 7px',
                            borderRadius: '5px',
                            fontSize: '11px',
                            fontWeight: 500,
                          }}
                        >
                          <Smartphone size={12} />
                          <span>Requested</span>
                        </span>
                      ) : (
                        <span style={{ color: '#94a3b8', fontSize: '11.5px' }}>Standard</span>
                      )}
                    </div>
                  ),
                },
                {
                  id: 'actions',
                  header: '',
                  cell: (r) => (
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'flex-end',
                        gap: '6px',
                      }}
                    >
                      <Link
                        to={`${basePath}/employees/${r.id}${r.onboardingStatus === 2 ? '' : '/hiring'}`}
                        style={{
                          padding: '4.5px 9px',
                          fontSize: '11.5px',
                          fontWeight: 600,
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                          textDecoration: 'none',
                          borderRadius: '6px',
                          backgroundColor: '#f8fafc',
                          color: '#334155',
                          border: '1px solid #e2e8f0',
                          transition: 'all 0.15s ease',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {r.onboardingStatus === 2 ? (
                          <>
                            <User size={12} />
                            <span>Profile</span>
                          </>
                        ) : (
                          <>
                            <Pencil size={12} />
                            <span>Draft</span>
                          </>
                        )}
                      </Link>
                      <Link
                        className="icon-button"
                        aria-label={`Open ${r.name ?? 'draft'}`}
                        title="Open details"
                        to={`${basePath}/employees/${r.id}${r.onboardingStatus === 2 ? '' : '/hiring'}`}
                        style={{ width: '28px', height: '28px', borderRadius: '6px' }}
                      >
                        <ArrowUpRight size={14} />
                      </Link>
                    </div>
                  ),
                },
              ]}
            />
            {!query.data.items.length && (
              <p className="hr-notice">No employees match this search.</p>
            )}
            <div className="hr-pagination">
              <span>{query.data.totalCount} records</span>
              <Pagination
                page={page}
                pageSize={25}
                total={query.data.totalCount}
                onChange={setPage}
              />
            </div>
          </>
        )}
      </QueryState>
    </>
  );
}
