import { useId, useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { ArrowLeft, ArrowRight, RefreshCw, Search, Users, ShieldCheck } from 'lucide-react';
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

export function RecordFacts({ value }: { value: unknown }) {
  if (value === null || value === undefined) return <span className="att-fact-muted">—</span>;
  if (typeof value !== 'object') {
    if (typeof value === 'boolean') {
      return (
        <span className={`att-fact-bool ${value ? 'true' : 'false'}`}>
          {value ? 'Yes' : 'No'}
        </span>
      );
    }
    const str = String(value);
    if (str === 'Unavailable') {
      return <span className="att-fact-muted">—</span>;
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
            <RecordFacts value={item} />
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
            <RecordFacts value={item} />
          </dd>
        </div>
      ))}
    </dl>
  );
}
export function SourceFacts({
  title = 'Retained source evidence',
  value,
}: {
  title?: string;
  value: unknown;
}) {
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
  return (
    <SelectInput
      label="Authority scope"
      value={value ? scopeKey(value) : ''}
      disabled={disabled}
      options={[
        { value: '', label: 'Select granted scope' },
        ...scopes.map((s) => ({
          value: scopeKey(s),
          label: `${scopeNames[s.kind]}${s.targetId ? ': ' + s.targetId : ''}`,
        })),
      ]}
      onChange={(e) => onChange(scopes.find((s) => scopeKey(s) === e.target.value))}
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
            <summary>Find by employee ID / access options</summary>
            <form
              className="att-register-access"
              onSubmit={(event) => {
                event.preventDefault();
                if (valid && !editing) setSelection({ employee, scope: effectiveScope });
              }}
            >
              <TextInput
                label="Employee ID"
                required
                disabled={editing}
                value={employee}
                onChange={(event) => {
                  setSelection(null);
                  setEmployee(event.target.value.trim());
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
            <Users size={26} aria-hidden="true" />
            <h2>Select an employee to view attendance</h2>
            <p>
              Search by name or employee number above. Then review their dates, recorded hours and
              daily details.
            </p>
          </section>
        )}
      </>
    );
  return (
    <div className="att-employee-workspace-card">
      <div className="att-workspace-header">
        <h1 className="att-workspace-title">{title}</h1>
        <p className="att-workspace-subtitle">
          Find employee to review records, submit duty adjustments or request factual corrections.
        </p>
      </div>
      <div className="att-employee-lookup-box">
        <EmployeeLookup
          approvedOnly={approvedOnly}
          disabled={editing}
          selected={employee}
          onSelect={(value) => {
            setSelection(null);
            setEmployee(value);
          }}
        />
        <form
          className="att-employee-scope-form"
          onSubmit={(e) => {
            e.preventDefault();
            if (valid && !editing) setSelection({ employee, scope });
          }}
        >
          <div className="att-form-row">
            <TextInput
              label="Employee ID"
              required
              disabled={editing}
              value={employee}
              onChange={(e) => {
                setSelection(null);
                setEmployee(e.target.value.trim());
              }}
            />
            <ScopeSelect
              permission={permission}
              value={scope}
              disabled={editing}
              onChange={(value) => {
                setSelection(null);
                setScope(value);
              }}
            />
            <div className="att-form-submit-cell">
              <Command icon={Search} type="submit" disabled={editing || !valid}>
                Open employee
              </Command>
            </div>
          </div>
        </form>
      </div>
      {!id.safeParse(employee).success ? (
        <p className="att-workspace-hint">Select an employee resource from above or enter an ID.</p>
      ) : !valid ? (
        <Denied>
          Choose a matching grant for this employee or historical resource. Membership is checked by
          the server.
        </Denied>
      ) : null}
      {content}
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
  const [submitted, setSubmitted] = useState<string | null>(compact ? '' : null);
  const allowed = access.can('hr.employees.read');
  const query = useAttendanceQuery(
    '/hr/employees' +
      queryString({
        search: submitted ?? '',
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
      {approvedOnly && (
        <p className="att-register-help">
          Only approved employees are listed, including former and archived staff for historical
          review. Complete hiring and approval in the employee record if someone is missing.
        </p>
      )}
      {submitted !== null && (
        <QueryState
          query={query}
          empty={query.data?.items.length === 0}
          emptyText={approvedOnly ? 'No approved employees match this search.' : undefined}
        />
      )}
      {!query.isError && query.data && (
        <>
          <SelectInput
            label={compact ? 'Employee' : 'Employee search results'}
            value={query.data.items.some((employee) => employee.id === selected) ? selected : ''}
            disabled={disabled || query.isFetching}
            options={[
              { value: '', label: 'Choose an employee' },
              ...query.data.items.map((employee) => ({
                value: employee.id,
                label: `${employee.name ?? 'Employee'} · #${employee.employeeNumber}`,
              })),
            ]}
            onChange={(event) => {
              onSelect(event.target.value);
            }}
          />
          {query.data.totalCount > 25 && (
            <p className="att-register-help">
              Showing 25 matches. Search by name or number to narrow the list.
            </p>
          )}
        </>
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
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (valid && !disabled)
          onChange({ from, to, pageSize, state, kind: kind ? Number(kind) : undefined });
      }}
    >
      <fieldset className="attendance-grid" disabled={disabled}>
        <TextInput
          label="From (inclusive business date)"
          type="date"
          required
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <TextInput
          label="To (exclusive business date)"
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
        <Command icon={Search} type="submit" disabled={!valid}>
          Load history
        </Command>
        {!valid && <p role="alert">Choose an exclusive range of 1 to 366 days.</p>}
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
