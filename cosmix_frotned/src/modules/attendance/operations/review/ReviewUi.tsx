import {
  EmployeeReference,
  EmployeePicker,
  useEmployeeCodes,
} from '@/components/employees/EmployeeReference';
import { useEffect, useId, useState, type ReactNode } from 'react';
import { employeeSearchTerm, formatEmployeeCode } from '@/utils/employee-code';
import { useParams } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  ArrowLeftRight,
  RefreshCw,
  Search,
  Users,
  User,
  Shield,
  ShieldCheck,
  Calendar,
  Clock,
  Scale,
  History,
  Layers,
  Info,
  CheckCircle2,
} from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Pagination } from '@/components/data-display/Pagination';
import { PrivateWorkspace, Command, Denied, QueryState } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { id, pageOf } from '../../schemas/common';
import { queryString } from '../../api/attendance-api';
import { canEmployee, scopeKey, scopeNames, supportedScopes, type Scope } from './access';
import { nextDate, today, validRange } from './helpers';
import { ApprovedAttendanceEmployee } from '../../components/AttendanceEmployeeSetup';
import {
  approvedAttendanceEmployees,
  approvedAttendanceFilter,
} from '../../schemas/employee-selection';
import '@/styles/attendance.css';
import '@/styles/attendance-register.css';
import '@/styles/attendance-corrections.css';

export function EvidenceField({
  label,
  value,
  onChange,
  max = 4000,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  max?: number;
}) {
  const fieldId = useId();
  return (
    <div className="field">
      <label htmlFor={fieldId}>{label}</label>
      <textarea
        id={fieldId}
        required
        rows={4}
        maxLength={max}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        style={{ width: '100%', boxSizing: 'border-box', resize: 'vertical' }}
      />
    </div>
  );
}
function label(key: string) {
  return key.replace(/([A-Z])/g, ' $1').replace(/^./, (c) => c.toUpperCase());
}

const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const HEX_HASH_REGEX = /^[0-9a-f]{32,}$/i;

const STATUS_FORMAT: Record<string, { label: string; cls: string }> = {
  employee_assignment_unavailable: { label: 'Assignment Unavailable', cls: 'att-pill-warning' },
  schedule_configuration_required: { label: 'Schedule Config Required', cls: 'att-pill-warning' },
  no_accepted_session_evidence: { label: 'No Punch Evidence', cls: 'att-pill-neutral' },
  closed_session_evidence: { label: 'Completed Session Evidence', cls: 'att-pill-success' },
  open_session_evidence: { label: 'Active Open Punch', cls: 'att-pill-info' },
  correction_evidence_unavailable: { label: 'Evidence Unavailable', cls: 'att-pill-warning' },
  present: { label: 'Present', cls: 'att-pill-success' },
  absent: { label: 'Absent', cls: 'att-pill-danger' },
  scheduled: { label: 'Scheduled', cls: 'att-pill-info' },
  configured: { label: 'Configured', cls: 'att-pill-info' },
  locked: { label: 'Locked', cls: 'att-pill-neutral' },
  open: { label: 'Open', cls: 'att-pill-info' },
};

export function RecordFacts({ value, field }: { value: unknown; field?: string }) {
  if (field && /^(?:employeeId|.*EmployeeId)$/.test(field))
    return <EmployeeReference employeeId={typeof value === 'string' ? value : null} />;
  if (field === 'employeeNumber')
    return (
      <EmployeeReference
        employeeNumber={typeof value === 'number' || typeof value === 'string' ? value : null}
      />
    );
  if (value === null || value === undefined) return <span className="att-fact-muted">—</span>;
  if (typeof value !== 'object') {
    if (typeof value === 'boolean') {
      return (
        <span className={`att-fact-bool ${value ? 'true' : 'false'}`}>{value ? 'Yes' : 'No'}</span>
      );
    }
    const str = String(value);
    if (str === 'Unavailable') {
      return <span className="att-fact-muted">—</span>;
    }
    const lower = str.toLowerCase();
    if (STATUS_FORMAT[lower]) {
      const badge = STATUS_FORMAT[lower];
      return <span className={`att-status-pill ${badge.cls}`}>{badge.label}</span>;
    }
    if (UUID_REGEX.test(str)) {
      return (
        <span className="att-guid-chip" title={str}>
          {str.slice(0, 8)}…{str.slice(-4)}
        </span>
      );
    }
    if (HEX_HASH_REGEX.test(str)) {
      return (
        <span className="att-hash-chip" title={str}>
          {str.slice(0, 10)}…{str.slice(-6)}
        </span>
      );
    }
    return (
      <span className="att-fact-value" style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
        {str}
      </span>
    );
  }
  if (Array.isArray(value))
    return value.length ? (
      <ol className="attendance-manifest">
        {value.map((item, i) => (
          <li key={i}>
            <RecordFacts
              value={item}
              field={
                field && /^(?:employeeIds|.*EmployeeIds)$/.test(field) ? 'employeeId' : undefined
              }
            />
          </li>
        ))}
      </ol>
    ) : (
      <span className="att-fact-muted">No entries returned</span>
    );
  return (
    <dl className="attendance-record">
      {Object.entries(value).map(([key, item]) => (
        <div key={key} style={{ display: 'contents' }}>
          <dt>{label(key)}</dt>
          <dd>
            <RecordFacts
              value={item}
              field={
                key === 'targetId' &&
                ((field === 'scope' && 'kind' in value && value.kind === 2) ||
                  ('target' in value && value.target === 'employee'))
                  ? 'employeeId'
                  : key
              }
            />
          </dd>
        </div>
      ))}
    </dl>
  );
}

function isCompositeProvenance(val: unknown): val is Record<string, unknown> {
  if (!val || typeof val !== 'object' || Array.isArray(val)) return false;
  const keys = Object.keys(val);
  return (
    keys.length > 1 &&
    keys.some((k) => ['roster', 'facts', 'policy', 'latestSavedRevision'].includes(k))
  );
}

function CompositeSourceTabs({
  title,
  record,
}: {
  title: string;
  record: Record<string, unknown>;
}) {
  const [activeTab, setActiveTab] = useState<string>('roster');

  const knownKeys = ['roster', 'facts', 'policy', 'latestSavedRevision'];
  const otherKeys = Object.keys(record).filter((k) => !knownKeys.includes(k));
  const availableKeys = [...knownKeys.filter((k) => k in record), ...otherKeys];

  const effectiveTab =
    activeTab === 'all' || activeTab in record ? activeTab : availableKeys[0] || 'all';

  const tabDefs = [
    {
      key: 'roster',
      label: 'Shift & Roster',
      Icon: Calendar,
      desc: 'Scheduled shift timings, site assignment, break allocation and holiday rules',
      hasValue: Boolean(record.roster),
    },
    {
      key: 'facts',
      label: 'Punches & Facts',
      Icon: Clock,
      desc: 'Actual biometrics, mobile punch sessions, elapsed work time and raw evidence',
      hasValue: Boolean(record.facts),
    },
    {
      key: 'policy',
      label: 'Policy & Calculation',
      Icon: Scale,
      desc: 'Overtime rules, grace periods, lates deduction and payable time outcomes',
      hasValue: Boolean(record.policy),
    },
    {
      key: 'latestSavedRevision',
      label: 'Saved Revision',
      Icon: History,
      desc: 'Audited snapshot revision saved in attendance ledger',
      hasValue: Boolean(record.latestSavedRevision),
    },
    ...otherKeys.map((k) => ({
      key: k,
      label: label(k),
      Icon: Layers,
      desc: `Detailed ${label(k)} data`,
      hasValue: Boolean(record[k]),
    })),
    {
      key: 'all',
      label: 'All Details',
      Icon: Layers,
      desc: 'Full composite evidence record',
      hasValue: true,
    },
  ].filter((t) => t.key === 'all' || t.key in record);

  const fallbackTab = {
    key: 'all',
    label: 'All Details',
    Icon: Layers,
    desc: 'Full composite evidence record',
    hasValue: true,
  };
  const currentTab = tabDefs.find((t) => t.key === effectiveTab) ?? tabDefs[0] ?? fallbackTab;
  const currentValue = effectiveTab === 'all' ? record : record[effectiveTab];

  return (
    <div className="att-source-tabs-container">
      {/* Top Header */}
      <div className="att-source-tabs-topbar">
        <div className="att-source-title-group">
          <ShieldCheck size={16} className="att-source-shield-icon" />
          <span className="att-source-title-text">{title}</span>
        </div>
        <div className="att-source-badge-group">
          <span className="att-source-badge">Engine Audited</span>
        </div>
      </div>

      {/* Button Switcher / Tab Toolbar */}
      <div className="att-source-buttons-bar" role="tablist" aria-label="Evidence categories">
        {tabDefs.map((tab) => {
          const TabIcon = tab.Icon;
          const isSelected = tab.key === effectiveTab;
          return (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={isSelected}
              className={`att-source-action-btn ${isSelected ? 'is-active' : ''}`}
              onClick={() => setActiveTab(tab.key)}
            >
              <TabIcon size={13} className="att-source-btn-icon" />
              <span className="att-source-btn-text">{tab.label}</span>
              {tab.key !== 'all' && (
                <span
                  className={`att-source-dot ${tab.hasValue ? 'dot-active' : 'dot-empty'}`}
                  title={tab.hasValue ? 'Contains data' : 'Empty'}
                />
              )}
            </button>
          );
        })}
      </div>

      {/* Tab Content Panel */}
      <div className="att-source-active-panel">
        <div className="att-source-desc-row">
          <span className="att-source-tab-desc">{currentTab.desc}</span>
        </div>

        {effectiveTab === 'all' ? (
          <div className="att-source-all-grid">
            <RecordFacts value={record} />
          </div>
        ) : currentValue ? (
          <div className="att-source-tab-content">
            <RecordFacts value={currentValue} />
          </div>
        ) : (
          <div className="att-source-empty-state">
            <currentTab.Icon size={26} className="att-empty-icon" />
            <div className="att-empty-text">
              <span className="att-empty-heading">No {currentTab.label} Recorded</span>
              <p className="att-empty-sub">
                No {currentTab.label.toLowerCase()} entries have been filed for this date.
              </p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export function SourceFacts({
  title = 'Retained source evidence',
  value,
}: {
  title?: string;
  value: unknown;
}) {
  if (isCompositeProvenance(value)) {
    return <CompositeSourceTabs title={title} record={value} />;
  }

  return (
    <details className="att-source-facts-details" open>
      <summary className="att-source-facts-summary">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <ShieldCheck size={16} style={{ color: '#2563eb' }} />
          <span style={{ fontWeight: 650, color: '#0f172a' }}>{title}</span>
        </div>
        <span className="att-source-badge">Engine Audited</span>
      </summary>
      <div className="att-source-facts-content">
        <RecordFacts value={value} />
      </div>
    </details>
  );
}
export function ScopeSelect({
  permission,
  value,
  onChange,
  disabled = false,
}: {
  permission: string;
  value: Scope | undefined;
  onChange: (scope: Scope | undefined) => void;
  disabled?: boolean;
}) {
  const access = useAttendanceAccess();
  const scopes = supportedScopes(access.user, permission);
  const codes = useEmployeeCodes(
    scopes
      .filter((scope) => scope.kind === 2 && scope.targetId !== null)
      .map((scope) => scope.targetId!),
  );
  return (
    <SelectInput
      label="Authority scope"
      value={value ? scopeKey(value) : ''}
      disabled={disabled}
      options={[
        { value: '', label: 'Select granted scope' },
        ...scopes.map((scope) => ({
          value: scopeKey(scope),
          label: `${scopeNames[scope.kind]}${scope.targetId ? ': ' + (scope.kind === 2 ? (codes[scope.targetId] ?? 'Employee unavailable') : scope.targetId) : ''}`,
        })),
      ]}
      onChange={(event) => onChange(scopes.find((scope) => scopeKey(scope) === event.target.value))}
    />
  );
}

export function EmployeeWorkspace({
  permission,
  title,
  self = false,
  compact = false,
  approvedOnly = false,
  children,
}: {
  permission: string;
  title: string;
  self?: boolean;
  compact?: boolean;
  approvedOnly?: boolean;
  children: (employeeId: string, scope?: Scope, onEditing?: (value: boolean) => void) => ReactNode;
}) {
  const params = useParams<{ employeeId: string }>();
  return (
    <PrivateWorkspace resource={`${title}:${self ? 'self' : (params.employeeId ?? '')}`}>
      {self ? (
        <>
          <h1>{title}</h1>
          {children('')}
        </>
      ) : (
        <EmployeeSelection
          permission={permission}
          title={title}
          routeEmployee={params.employeeId}
          compact={compact}
          approvedOnly={approvedOnly}
        >
          {children}
        </EmployeeSelection>
      )}
    </PrivateWorkspace>
  );
}
function EmployeeSelection({
  permission,
  title,
  routeEmployee,
  compact,
  approvedOnly,
  children,
}: {
  permission: string;
  title: string;
  routeEmployee?: string;
  compact: boolean;
  approvedOnly: boolean;
  children: (employeeId: string, scope?: Scope, onEditing?: (value: boolean) => void) => ReactNode;
}) {
  const access = useAttendanceAccess();
  const scopes = supportedScopes(access.user, permission);
  const suggestedScope = (employeeId: string) =>
    scopes.find((grant) => grant.kind === 7) ??
    scopes.find((grant) => grant.kind === 2 && grant.targetId === employeeId) ??
    (scopes.length === 1 ? scopes[0] : undefined);
  const [employee, setEmployee] = useState(routeEmployee ?? '');
  const [scope, setScope] = useState<Scope | undefined>(() =>
    approvedOnly && routeEmployee ? suggestedScope(routeEmployee) : undefined,
  );
  const [selection, setSelection] = useState<{ employee: string; scope?: Scope } | null>(() =>
    approvedOnly && routeEmployee && canEmployee(access.user, permission, routeEmployee, scope)
      ? { employee: routeEmployee, scope }
      : null,
  );
  const [editing, setEditing] = useState(false);
  const effectiveScope = scope ?? (compact ? suggestedScope(employee) : undefined);
  const valid = canEmployee(access.user, permission, employee, effectiveScope);
  const content = selection && (
    <PrivateWorkspace
      resource={`${selection.employee}:${selection.scope ? scopeKey(selection.scope) : ''}`}
    >
      {approvedOnly ? (
        <ApprovedAttendanceEmployee employeeId={selection.employee}>
          {children(selection.employee, selection.scope, setEditing)}
        </ApprovedAttendanceEmployee>
      ) : (
        children(selection.employee, selection.scope, setEditing)
      )}
    </PrivateWorkspace>
  );
  if (compact)
    return (
      <>
        <section className="att-register-picker" aria-label="Select employee">
          <EmployeeLookup
            approvedOnly={approvedOnly}
            disabled={editing}
            selected={employee}
            compact
            onSelect={(value) => {
              const nextScope = scope ?? suggestedScope(value);
              setEmployee(value);
              setSelection(
                canEmployee(access.user, permission, value, nextScope)
                  ? { employee: value, scope: nextScope }
                  : null,
              );
            }}
          />
          <details
            className="att-register-advanced"
            open={!access.can('hr.employees.read') || (Boolean(employee) && !valid)}
          >
            <summary>Employee selection / access options</summary>
            <form
              className="att-register-access"
              onSubmit={(event) => {
                event.preventDefault();
                if (valid && !editing) setSelection({ employee, scope: effectiveScope });
              }}
            >
              <EmployeePicker
                label="Employee"
                required
                disabled={editing}
                value={employee}
                onChange={(uuid) => {
                  setSelection(null);
                  setEmployee(uuid);
                }}
              />
              {scopes.length > 1 && !scopes.some((grant) => grant.kind === 7) && (
                <ScopeSelect
                  permission={permission}
                  value={effectiveScope}
                  disabled={editing}
                  onChange={(value) => {
                    setSelection(null);
                    setScope(value);
                  }}
                />
              )}
              <Command icon={Search} type="submit" disabled={editing || !valid}>
                View attendance
              </Command>
            </form>
            {employee && !valid && (
              <Denied>Select an access area assigned to you for this employee.</Denied>
            )}
          </details>
        </section>
        {selection ? (
          content
        ) : (
          <section className="att-register-empty" aria-label="Attendance selection">
            <div className="att-empty-icon-wrap">
              <Users size={26} aria-hidden="true" />
            </div>
            <h2>Select an employee to view attendance</h2>
            <p>
              Search by employee name or code above to inspect daily working hours, biometric
              check-in logs, and monthly summary records.
            </p>
            <div className="att-empty-tags">
              <span className="att-empty-tag">
                <CheckCircle2 size={12} style={{ color: '#16a34a' }} />
                <span>Monthly Hours & Overtime</span>
              </span>
              <span className="att-empty-tag">
                <CheckCircle2 size={12} style={{ color: '#16a34a' }} />
                <span>Biometric Check-in/Out Logs</span>
              </span>
              <span className="att-empty-tag">
                <CheckCircle2 size={12} style={{ color: '#16a34a' }} />
                <span>Factual Exception Audits</span>
              </span>
            </div>
          </section>
        )}
      </>
    );
  return (
    <div className="att-employee-workspace-card">
      {selection ? (
        <div className="att-selected-employee-bar">
          <div className="att-selected-employee-info">
            <div className="att-selected-avatar">
              <User size={18} />
            </div>
            <div className="att-selected-details">
              <div className="att-selected-name-row">
                <span className="att-selected-name">
                  <EmployeeReference employeeId={selection.employee} showName />
                </span>
                <span className="att-selected-badge">Active Record</span>
              </div>
              <div className="att-selected-sub">
                <Shield size={12} />
                <span>
                  Authority Grant:{' '}
                  {selection.scope
                    ? `${scopeNames[selection.scope.kind]}`
                    : 'Standard Attendance Authority'}
                </span>
              </div>
            </div>
          </div>

          <div className="att-selected-actions">
            {scopes.length > 1 && !scopes.some((grant) => grant.kind === 7) && (
              <ScopeSelect
                permission={permission}
                value={selection.scope}
                disabled={editing}
                onChange={(value) => {
                  setScope(value);
                  setSelection({ employee: selection.employee, scope: value });
                }}
              />
            )}
            <button
              type="button"
              className="att-switch-employee-btn"
              disabled={editing}
              onClick={() => {
                setSelection(null);
                setEmployee('');
              }}
            >
              <ArrowLeftRight size={13} />
              <span>Change Employee</span>
            </button>
          </div>
        </div>
      ) : (
        <div className="att-employee-search-container">
          <div className="att-workspace-header">
            <div className="att-workspace-header-text">
              <h2 className="att-workspace-title">{title}</h2>
              <p className="att-workspace-subtitle">
                Search or select an employee to view history, review duty hours, or record overtime requests.
              </p>
            </div>
          </div>

          <div className="att-employee-lookup-box">
            <EmployeeLookup
              approvedOnly={approvedOnly}
              disabled={editing}
              selected={employee}
              onSelect={(value) => {
                const nextScope = scope ?? suggestedScope(value);
                setEmployee(value);
                if (value && canEmployee(access.user, permission, value, nextScope)) {
                  setScope(nextScope);
                  setSelection({ employee: value, scope: nextScope });
                } else {
                  setSelection(null);
                }
              }}
            />

            <details
              className="att-register-advanced"
              open={!access.can('hr.employees.read') || (Boolean(employee) && !valid)}
            >
              <summary>Advanced Employee ID / Scope Grants</summary>
              <form
                className="att-register-access"
                onSubmit={(event) => {
                  event.preventDefault();
                  if (valid && !editing) setSelection({ employee, scope: effectiveScope });
                }}
              >
                <EmployeePicker
                  label="Employee"
                  required
                  disabled={editing}
                  value={employee}
                  onChange={(uuid) => {
                    setSelection(null);
                    setEmployee(uuid);
                  }}
                />
                {scopes.length > 1 && !scopes.some((grant) => grant.kind === 7) && (
                  <ScopeSelect
                    permission={permission}
                    value={effectiveScope}
                    disabled={editing}
                    onChange={(value) => {
                      setSelection(null);
                      setScope(value);
                    }}
                  />
                )}
                <Command icon={Search} type="submit" disabled={editing || !valid}>
                  Open employee
                </Command>
              </form>
              {employee && !valid && (
                <Denied>Select an access area assigned to you for this employee.</Denied>
              )}
            </details>
          </div>

          <section className="att-register-empty" aria-label="Attendance selection">
            <div className="att-empty-icon-wrap">
              <Users size={26} aria-hidden="true" />
            </div>
            <h2>Select an employee to manage records</h2>
            <p>
              Search by name or EMP code above to view or submit requests for that employee.
            </p>
          </section>
        </div>
      )}

      {selection && content}
    </div>
  );
}
const employeeChoices = pageOf(
  z.object({
    id,
    employeeNumber: z.number().int(),
    name: z.string().nullable(),
  }),
);

function EmployeeLookup({
  approvedOnly,
  disabled,
  selected,
  onSelect,
  compact = false,
}: {
  approvedOnly: boolean;
  disabled: boolean;
  selected: string;
  onSelect: (value: string) => void;
  compact?: boolean;
}) {
  const access = useAttendanceAccess();
  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState<string | null>('');
  const allowed = access.can('hr.employees.read');
  const query = useAttendanceQuery(
    '/hr/employees' +
      queryString({
        search: employeeSearchTerm(submitted ?? ''),
        page: 1,
        pageSize: 25,
        ...(approvedOnly ? approvedAttendanceFilter : {}),
      }),
    approvedOnly ? approvedAttendanceEmployees : employeeChoices,
    allowed && submitted !== null,
  );
  if (!allowed) return null;
  return (
    <section
      className={compact ? 'att-register-lookup' : 'attendance-section'}
      aria-label="Find employee"
    >
      <form
        className={compact ? 'att-register-search' : 'attendance-grid'}
        onSubmit={(event) => {
          event.preventDefault();
          if (disabled || query.isFetching) return;
          if (submitted === search.trim()) void query.refetch();
          else setSubmitted(search.trim());
        }}
      >
        <TextInput
          label={compact ? 'Search employees' : 'Find by employee name or number'}
          placeholder={compact ? 'Name or employee number' : undefined}
          value={search}
          maxLength={100}
          disabled={disabled}
          onChange={(event) => setSearch(event.target.value)}
        />
        <Command
          icon={Search}
          type="submit"
          variant="secondary"
          disabled={disabled || query.isFetching}
        >
          Search
        </Command>
      </form>

      <div className="att-register-select-box">
        <SelectInput
          label={
            compact
              ? query.data
                ? `Select employee (${query.data.items.length} loaded)`
                : 'Select employee'
              : 'Employee search results'
          }
          value={query.data?.items.some((employee) => employee.id === selected) ? selected : ''}
          disabled={disabled || query.isFetching || !query.data}
          options={[
            {
              value: '',
              label: query.isFetching ? 'Loading employees…' : 'Choose an employee',
            },
            ...(query.data?.items.map((employee) => ({
              value: employee.id,
              label: `${employee.name ?? 'Employee'} · ${formatEmployeeCode(employee.employeeNumber)}`,
            })) ?? []),
          ]}
          onChange={(event) => {
            onSelect(event.target.value);
          }}
        />
      </div>

      {submitted !== null && (
        <div style={{ gridColumn: '1 / -1' }}>
          <QueryState
            query={query}
            empty={query.data?.items.length === 0}
            emptyText={approvedOnly ? 'No approved employees match this search.' : undefined}
          />
        </div>
      )}

      {approvedOnly && (
        <p className="att-register-help">
          <Info size={13} style={{ flexShrink: 0, color: '#94a3b8' }} />
          <span>
            Only approved employees are listed, including former and archived staff for historical
            review. Complete hiring and approval in the employee record if someone is missing.
          </span>
        </p>
      )}

      {!query.isError && query.data && query.data.totalCount > 25 && (
        <p className="att-register-help" style={{ marginTop: '-4px' }}>
          Showing 25 matches. Search by name or number to narrow the list.
        </p>
      )}
    </section>
  );
}
export type Range = { from: string; to: string; pageSize: number; state: number; kind?: number };
export function RangeFilter({
  onChange,
  states = false,
  exceptionKinds,
  disabled = false,
}: {
  onChange: (range: Range) => void;
  states?: boolean;
  exceptionKinds?: readonly { value: string; label: string }[];
  disabled?: boolean;
}) {
  const [from, setFrom] = useState(today().slice(0, 8) + '01');
  const [to, setTo] = useState(nextDate(today()));
  const [pageSize, setPageSize] = useState(25);
  const [state, setState] = useState(0);
  const [kind, setKind] = useState('');
  const valid = validRange(from, to);

  useEffect(() => {
    if (valid && !disabled) {
      onChange({ from, to, pageSize, state, kind: kind ? Number(kind) : undefined });
    }
  }, []);

  return (
    <form
      className="att-range-filter-form"
      onSubmit={(e) => {
        e.preventDefault();
        if (valid && !disabled)
          onChange({ from, to, pageSize, state, kind: kind ? Number(kind) : undefined });
      }}
    >
      <fieldset className="att-range-filter-fieldset" disabled={disabled}>
        <TextInput
          label="From date"
          type="date"
          required
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <TextInput
          label="To date"
          type="date"
          required
          value={to}
          onChange={(e) => setTo(e.target.value)}
        />
        <SelectInput
          label="Page size"
          value={pageSize}
          options={[10, 25, 50, 100].map((n) => ({ value: String(n), label: String(n) }))}
          onChange={(e) => setPageSize(Number(e.target.value))}
        />
        {states && (
          <SelectInput
            label="Request state"
            value={state}
            options={['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'].map((v, i) => ({
              value: String(i),
              label: v,
            }))}
            onChange={(e) => setState(Number(e.target.value))}
          />
        )}
        {exceptionKinds && (
          <SelectInput
            label="Exception kind"
            value={kind}
            options={[{ value: '', label: 'All kinds' }, ...exceptionKinds]}
            onChange={(e) => setKind(e.target.value)}
          />
        )}
        <div className="att-filter-submit-wrap">
          <Command icon={Search} type="submit" disabled={!valid}>
            Filter history
          </Command>
        </div>
        {!valid && <p role="alert" className="att-filter-error">Choose an exclusive range of 1 to 366 days.</p>}
      </fieldset>
    </form>
  );
}
export function CursorControls({
  cursor,
  next,
  back,
  onBack,
  onNext,
  disabled,
}: {
  cursor: string | undefined;
  next: string | null;
  back: boolean;
  onBack: () => void;
  onNext: (cursor: string) => void;
  disabled: boolean;
}) {
  return (
    <nav className="attendance-commands" aria-label="History continuation">
      <Command icon={ArrowLeft} variant="secondary" disabled={disabled || !back} onClick={onBack}>
        Previous
      </Command>
      <Command
        icon={ArrowRight}
        variant="secondary"
        disabled={disabled || !next || next === cursor}
        onClick={() => next && onNext(next)}
      >
        Next
      </Command>
      {next && next === cursor && (
        <p role="alert">Continuation did not advance. Reload this history.</p>
      )}
    </nav>
  );
}
export function NumberedControls({
  page,
  data,
  onChange,
  disabled,
}: {
  page: number;
  data: { totalCount: number; pageSize: number };
  onChange: (page: number) => void;
  disabled: boolean;
}) {
  return (
    <Pagination
      page={page}
      pageSize={data.pageSize}
      total={data.totalCount}
      onChange={onChange}
      disabled={disabled}
    />
  );
}
export function ReadResult<T extends z.ZodType>({
  path,
  schema,
  allowed,
  children,
}: {
  path: string;
  schema: T;
  allowed: boolean;
  children: (value: z.infer<T>) => ReactNode;
}) {
  const query = useAttendanceQuery(path, schema, allowed);
  if (!allowed) return <Denied>Matching read permission is required.</Denied>;
  return (
    <>
      <QueryState query={query} />
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={query.isFetching}
        onClick={() => void query.refetch()}
      >
        Reload
      </Command>
      {!query.isError && query.data !== undefined && !query.isFetching && children(query.data)}
    </>
  );
}
