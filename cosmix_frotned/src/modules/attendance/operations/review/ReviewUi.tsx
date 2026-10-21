import { useId, useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { ArrowLeft, ArrowRight, RefreshCw, Search } from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Pagination } from '@/components/data-display/Pagination';
import { PrivateWorkspace, Command, Denied, QueryState } from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { id } from '../../schemas/common';
import { canEmployee, scopeKey, scopeNames, supportedScopes, type Scope } from './access';
import { nextDate, today, validRange } from './helpers';

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
export function RecordFacts({ value }: { value: unknown }) {
  if (value === null || value === undefined) return <span>Unavailable</span>;
  if (typeof value !== 'object')
    return (
      <span style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
        {typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value)}
      </span>
    );
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
      <span>No entries returned</span>
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
    <details className="attendance-section">
      <summary>{title}</summary>
      <RecordFacts value={value} />
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
  children,
}: {
  permission: string;
  title: string;
  self?: boolean;
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
        <EmployeeSelection permission={permission} title={title} routeEmployee={params.employeeId}>
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
  children,
}: {
  permission: string;
  title: string;
  routeEmployee?: string;
  children: (employeeId: string, scope?: Scope, onEditing?: (value: boolean) => void) => ReactNode;
}) {
  const access = useAttendanceAccess();
  const [employee, setEmployee] = useState(routeEmployee ?? '');
  const [scope, setScope] = useState<Scope>();
  const [selection, setSelection] = useState<{ employee: string; scope?: Scope } | null>(null);
  const [editing, setEditing] = useState(false);
  const valid = canEmployee(access.user, permission, employee, scope);
  return (
    <>
      <h1>{title}</h1>
      <form
        className="attendance-grid"
        onSubmit={(e) => {
          e.preventDefault();
          if (valid && !editing) setSelection({ employee, scope });
        }}
      >
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
        <Command icon={Search} type="submit" disabled={editing || !valid}>
          Open employee
        </Command>
      </form>
      {!id.safeParse(employee).success ? (
        <p>Select an employee resource.</p>
      ) : !valid ? (
        <Denied>
          Choose a matching grant for this employee or historical resource. Membership is checked by
          the server.
        </Denied>
      ) : null}
      {selection && (
        <PrivateWorkspace
          resource={`${selection.employee}:${selection.scope ? scopeKey(selection.scope) : ''}`}
        >
          {children(selection.employee, selection.scope, setEditing)}
        </PrivateWorkspace>
      )}
    </>
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
